﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Streams;
using DiscUtils.Registry;
using YamlDotNet.Serialization;
using TrImGen.IO;
using Serilog;
using Newtonsoft.Json;
using System.Xml.Linq;

namespace TrImGen
{
  class Program
  {
    static Config config;

    static void Main(string[] args)
    {
      DiscUtils.Complete.SetupHelper.SetupComplete();
      ExFat.DiscUtils.ExFatSetupHelper.SetupFileSystems();
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

      using (var sr = new StreamReader(@"config.yml"))
      {
        var yaml = new Deserializer();
        config = yaml.Deserialize<Config>(sr);
      }

#if DEBUG
      SelfTest.Run();
      config.SourceDisks = new string[] { "Test/Test.vhd" };
#endif

      foreach (var arg in config.SourceDisks)
      {
        string targetName = GetTargetName(arg);
        string targetPath = GetTargetPath(targetName);

        Log.Logger = new LoggerConfiguration()
          .WriteTo.Console()
          .WriteTo.File($"{targetName}-.log", rollingInterval: RollingInterval.Hour, encoding: Encoding.UTF8)
          .CreateLogger();

        using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
          VirtualDisk disk = TargetHelper.InitializeVirtualDisk(target, config.TargetDiskSize, config.TargetDiskType);

          IFileSystem targetFileSystem = TargetHelper.InitializeFirstPartition(disk, config.TargetPartitionType);

          if (arg.IndexOf(@"\\.\", StringComparison.OrdinalIgnoreCase) == 0
           || arg.IndexOf(@"\\?\", StringComparison.OrdinalIgnoreCase) == 0
           || arg.IndexOf(@"/dev/fd", StringComparison.OrdinalIgnoreCase) == 0
           || arg.IndexOf(@"/dev/hd", StringComparison.OrdinalIgnoreCase) == 0
           || arg.IndexOf(@"/dev/sd", StringComparison.OrdinalIgnoreCase) == 0
           || arg.IndexOf(@"/dev/sg", StringComparison.OrdinalIgnoreCase) == 0
           || arg.IndexOf(@"/dev/sr", StringComparison.OrdinalIgnoreCase) == 0)
          {
            using (var ds = DriveStream.OpenDrive(arg))
            {
              DetectFileSystemAndAnalyze(ds, targetFileSystem);
            }
          }
          else
          {
            using (var fs = new FileStream(arg, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
              DetectFileSystemAndAnalyze(fs, targetFileSystem);
            }
          }
        }
      }
    }

    private static string GetTargetName(string arg)
    {
      if (arg.IndexOf(@"\\.\", StringComparison.OrdinalIgnoreCase) == 0
       || arg.IndexOf(@"\\?\", StringComparison.OrdinalIgnoreCase) == 0
       || arg.IndexOf(@"/dev/fd", StringComparison.OrdinalIgnoreCase) == 0
       || arg.IndexOf(@"/dev/hd", StringComparison.OrdinalIgnoreCase) == 0
       || arg.IndexOf(@"/dev/sd", StringComparison.OrdinalIgnoreCase) == 0
       || arg.IndexOf(@"/dev/sg", StringComparison.OrdinalIgnoreCase) == 0
       || arg.IndexOf(@"/dev/sr", StringComparison.OrdinalIgnoreCase) == 0)
      {
        string chars = new string(Path.GetInvalidFileNameChars());
        string chars2 = new string(Path.GetInvalidPathChars());
        string pattern = $"[{Regex.Escape(chars)}{Regex.Escape(chars2)}]";
        return Regex.Replace(arg.Substring(4), pattern, "_");
      }
      else
      {
        return Path.GetFileNameWithoutExtension(arg);
      }
    }

    private static string GetTargetPath(string targetName)
    {
      string targetPath = null;
      if (config.TargetDiskType == TargetDiskType.Vhd)
        targetPath = $"{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.vhd";
      else if (config.TargetDiskType == TargetDiskType.Vhdx)
        targetPath = $"{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.vhdx";
      else if (config.TargetDiskType == TargetDiskType.Vdi)
        targetPath = $"{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.vdi";
      else if (config.TargetDiskType == TargetDiskType.Raw)
        targetPath = $"{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.img";
      else
        throw new NotSupportedException();
      return targetPath;
    }

    private static void DetectFileSystemAndAnalyze(Stream s, IFileSystem target, string pathOffset = null)
    {
      var fileSystems = FileSystemManager.DetectFileSystems(s);
      if (fileSystems.Any())
      {
        foreach (var fs in fileSystems)
        {
          Log.Information(fs.Description);
          using (var fsStream = fs.Open(s))
          {
            AnalyzeFileSystem(fsStream, target, pathOffset);
          }
        }
      }
      else
      {
        try
        {
          using (VirtualDisk disk = TryDifferentDiskTypes(s))
          {
            if (disk.IsPartitioned)
            {
              Log.Information($"Disk has {disk.Partitions.Count} partitions");
              int partIdx = 0;
              foreach (var part in disk.Partitions.Partitions)
              {
                Log.Information($"Partition {part.FirstSector}-{part.LastSector}");
                using (var sub = part.Open())
                {
                  DetectFileSystemAndAnalyze(sub, target, $"part{partIdx++}{Path.DirectorySeparatorChar}");
                }
              }
            }
          }
        }
        catch (Exception ex)
        {
          Log.Fatal(ex, ex.Message);
        }
      }
    }

    private static VirtualDisk TryDifferentDiskTypes(Stream s)
    {
      VirtualDisk disk;
      try
      {
        disk = new DiscUtils.Vhd.Disk(s, Ownership.None);
      }
      catch (Exception)
      {
        try
        {
          disk = new DiscUtils.Vhdx.Disk(s, Ownership.None);
        }
        catch (Exception)
        {
          try
          {
            disk = new DiscUtils.Vdi.Disk(s, Ownership.None);
          }
          catch (Exception)
          {
            try
            {
              disk = new DiscUtils.Raw.Disk(s, Ownership.None);
            }
            catch (Exception)
            {
              throw new NotSupportedException("File or disk type not supported.");
            }
          }
        }
      }
      return disk;
    }

    private static void AnalyzeFileSystem(IFileSystem source, IFileSystem target, string pathOffset = null)
    {
      EnableAllFilesNtfs(source);
      Regex search = new Regex(string.Join("|", config.SearchPatterns), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      Regex evtxHints = new Regex(string.Join("|", config.EventHints), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      Regex regHints = new Regex(string.Join("|", config.RegistryHints), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      foreach (var file in new FileSystemEnumerable(source, search))
      {
        string targetPath = null;

        try
        {
          string answer = CopyFileToTarget(target, pathOffset, file, out targetPath);
          if (answer == "ok")
            Log.Information($"{file.FullName}: {answer}");
          else
            Log.Warning($"{file.FullName}: {answer}");
        }
        catch (Exception ex)
        {
          Log.Error(ex, ex.Message);
        }

        CheckEvtxHint(target, evtxHints, file, targetPath);
        CheckRegHint(target, regHints, file, targetPath);
      }
    }

    private static void CheckRegHint(IFileSystem target, Regex regHints, DiscFileInfo file, string targetPath)
    {
      if (targetPath != null && regHints.IsMatch(file.FullName))
      {
        try
        {
          string answer = AnalyzeRegistryFile(target, targetPath);
          if (answer == "ok")
            Log.Information($"{file.FullName} analyzing registry: {answer}");
          else
            Log.Warning($"{file.FullName} analyzing registry: {answer}");
        }
        catch (Exception ex)
        {
          Log.Error(ex, ex.Message);
        }
      }
    }

    private static void CheckEvtxHint(IFileSystem target, Regex evtxHints, DiscFileInfo file, string targetPath)
    {
      if (targetPath != null && evtxHints.IsMatch(file.FullName))
      {
        try
        {
          string answer = AnalyzeEventFile(target, targetPath);
          if (answer == "ok")
            Log.Information($"{file.FullName} analyzing evtx file: {answer}");
          else
            Log.Warning($"{file.FullName} analyzing evtx file: {answer}");
        }
        catch (Exception ex)
        {
          Log.Error(ex, ex.Message);
        }
      }
    }

    private static void EnableAllFilesNtfs(IFileSystem source)
    {
      if (source is NtfsFileSystem)
      {
        var ntfs = (NtfsFileSystem)source;
        ntfs.NtfsOptions.HideHiddenFiles = false;
        ntfs.NtfsOptions.HideMetafiles = false;
        ntfs.NtfsOptions.HideSystemFiles = false;
      }
    }

    private static string CopyFileToTarget(IFileSystem target, string pathOffset, DiscFileInfo file, out string targetPath)
    {
      string sourcePath = $"{pathOffset}{file.DirectoryName}";
      targetPath = $"{pathOffset}{file.FullName}";
      if (target.FileExists(targetPath))
      {
        targetPath = $"{targetPath}_{DateTimeOffset.Now.ToUnixTimeSeconds()}";
      }

      target.CreateDirectory(sourcePath);
      bool done = false;
      int retry = 0;
      do
      {
        retry++;
        using (var fs = target.OpenFile(targetPath, FileMode.Create, FileAccess.ReadWrite))
        using (var ss = file.OpenRead())
        {
          ss.CopyTo(fs);
          ss.Seek(0, SeekOrigin.Begin);
          fs.Seek(0, SeekOrigin.Begin);
          done = ss.SequenceEqual(fs);
          target.SetAttributes(targetPath, file.FileSystem.GetAttributes(file.FullName));
        }
      }
      while (!done && retry <= config.CopyRetryCount);

      return retry <= config.CopyRetryCount ? "ok" : "error";
    }

    private static string AnalyzeEventFile(IFileSystem target, string targetPath)
    {
      Regex search = new Regex(string.Join("|", config.EventSearchPatterns), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

      if (Directory.Exists("tmp"))
        Directory.Delete("tmp", true);
      Directory.CreateDirectory("tmp");

      using (var ss = target.OpenFile(targetPath, FileMode.Create, FileAccess.Read))
      using (var fs = new FileStream($"tmp{Path.DirectorySeparatorChar}eventlog", FileMode.Create, FileAccess.Write, FileShare.None))
      {
        ss.CopyTo(fs);
      }

      using (var rd = new EventLogReader($"tmp{Path.DirectorySeparatorChar}eventlog", PathType.FilePath))
      using (var fs = target.OpenFile($"{targetPath}.ndjson", FileMode.Create, FileAccess.Write))
      using (var sw = new StreamWriter(fs))
      {
        EventRecord evt;
        while ((evt = rd.ReadEvent()) != null)
        {
          try
          {
            string evtstr = evt.ToXml();
            if (search.IsMatch(evtstr))
            {
              string json = JsonConvert.SerializeXNode(XElement.Parse(evtstr));
              sw.WriteLine(json);
            }
          }
          catch (Exception)
          {
            // silently ignore
          }
        }
      }

      if (Directory.Exists("tmp"))
        Directory.Delete("tmp", true);
      return "ok";
    }

    private static string AnalyzeRegistryFile(IFileSystem target, string targetPath)
    {
      Regex search = new Regex(string.Join("|", config.RegistrySearchPatterns), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

      using (var ss = target.OpenFile(targetPath, FileMode.Create, FileAccess.Read))
      using (var hive = new RegistryHive(ss))
      using (var fs = target.OpenFile($"{targetPath}_RegHive.txt", FileMode.Create, FileAccess.Write))
      using (var sw = new StreamWriter(fs))
      {
        Queue<RegistryKey> queue = new Queue<RegistryKey>();
        queue.Enqueue(hive.Root);
        while (queue.Count > 0)
        {
          var cur = queue.Dequeue();
          try
          {
            if (search.IsMatch(cur.Name))
            {
              sw.WriteLine(cur.Name);
              foreach (var valname in cur.GetValueNames())
              {
                object val = cur.GetValue(valname);
                if (val is byte[])
                {
                  val = Convert.ToBase64String((byte[])val, Base64FormattingOptions.None);
                }
                sw.WriteLine($"\t{valname} = {val} [{cur.GetValueType(valname)}]");
              }
            }
          }
          catch (Exception ex)
          {
            sw.WriteLine($"\tError reading values: {ex.Message}");
          }

          sw.Flush();

          try
          {
            foreach (var sub in cur.SubKeys)
            {
              queue.Enqueue(sub);
            }
          }
          catch (Exception)
          {
            // silent ignore
          }
        }
      }

      return "ok";
    }
  }
}
