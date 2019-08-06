using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using DiscUtils.Registry;
using ExFat;
using ExFat.DiscUtils;
using YamlDotNet.Serialization;

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

      using (var sr = new StreamReader(@".\config.yml"))
      {
        var yaml = new Deserializer();
        config = yaml.Deserialize<Config>(sr);
      }

      foreach (var arg in config.SourceDisks)
      {
        string targetName = GetTargetName(arg);
        string targetPath = GetTargetPath(targetName);

        using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
          VirtualDisk disk = GetVirtualDisk(target);

          GuidPartitionTable table = GuidPartitionTable.Initialize(disk);
          table.Create(table.FirstUsableSector, table.LastUsableSector, GuidPartitionTypes.WindowsBasicData, 0, "DATA");

          var vols = VolumeManager.GetPhysicalVolumes(disk);
          IFileSystem targetFileSystem = FormatTargetFileSystem(targetName, vols[0]);

          if (arg.IndexOf(@"\\.\", StringComparison.OrdinalIgnoreCase) == 0 ||
              arg.IndexOf(@"\\?\", StringComparison.OrdinalIgnoreCase) == 0)
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

    private static IFileSystem FormatTargetFileSystem(string targetName, PhysicalVolumeInfo vol)
    {
      IFileSystem targetFileSystem;
      if (config.TargetPartitionType == TargetPartitionType.ExFat)
        targetFileSystem = ExFatFileSystem.Format(vol, new ExFatFormatOptions()
        {
          SectorsPerCluster = vol.Length < 268435456L ? 8U : vol.Length < 34359738368L ? 64U : 256U,
          BytesPerSector = 512
        }, targetName);
      else if (config.TargetPartitionType == TargetPartitionType.Ntfs)
        targetFileSystem = NtfsFileSystem.Format(vol, targetName);
      else
        throw new NotSupportedException();
      return targetFileSystem;
    }

    private static string GetTargetName(string arg)
    {
      if (arg.IndexOf(@"\\.\", StringComparison.OrdinalIgnoreCase) == 0 ||
          arg.IndexOf(@"\\?\", StringComparison.OrdinalIgnoreCase) == 0)
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

    private static VirtualDisk GetVirtualDisk(FileStream target)
    {
      long size = config.TargetDiskSize;
      long miss = size % 1024;
      if (miss > 0)
      {
        size = size + 1024 - miss;
      }
      VirtualDisk disk = null;
      if (config.TargetDiskType == TargetDiskType.Vhd)
        disk = DiscUtils.Vhd.Disk.InitializeDynamic(target, Ownership.None, size);
      else if (config.TargetDiskType == TargetDiskType.Vhdx)
        disk = DiscUtils.Vhdx.Disk.InitializeDynamic(target, Ownership.None, size);
      else if (config.TargetDiskType == TargetDiskType.Vdi)
        disk = DiscUtils.Vdi.Disk.InitializeDynamic(target, Ownership.None, size);
      else if (config.TargetDiskType == TargetDiskType.Raw)
        disk = DiscUtils.Raw.Disk.Initialize(target, Ownership.None, size);
      else
        throw new NotSupportedException();
      return disk;
    }

    private static string GetTargetPath(string targetName)
    {
      string targetPath = null;
      if (config.TargetDiskType == TargetDiskType.Vhd)
        targetPath = $".\\{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.vhd";
      else if (config.TargetDiskType == TargetDiskType.Vhdx)
        targetPath = $".\\{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.vhdx";
      else if (config.TargetDiskType == TargetDiskType.Vdi)
        targetPath = $".\\{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.vdi";
      else if (config.TargetDiskType == TargetDiskType.Raw)
        targetPath = $".\\{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.img";
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
          Console.WriteLine(fs.Description);
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
          VirtualDisk disk = null;
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
          using (disk)
          {
            if (disk.IsPartitioned)
            {
              int partIdx = 0;
              foreach (var part in disk.Partitions.Partitions)
              {
                Console.WriteLine($"Partition {part.FirstSector}-{part.LastSector}");
                using (var sub = part.Open())
                {
                  DetectFileSystemAndAnalyze(sub, target, $"part{partIdx++}\\");
                }
              }
            }
          }
        }
        catch (Exception ex)
        {
          Console.Error.WriteLine(ex.ToString());
        }
      }
    }

    private static void AnalyzeFileSystem(IFileSystem source, IFileSystem target, string pathOffset = null)
    {
      EnableAllFilesNtfs(source);
      Regex search = new Regex(string.Join("|", config.SearchPatterns), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      Regex evtxHints = new Regex(string.Join("|", config.EventHints), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      Regex regHints = new Regex(string.Join("|", config.RegistryHints), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      Queue<DiscDirectoryInfo> queue = new Queue<DiscDirectoryInfo>();
      queue.Enqueue(source.Root);
      while (queue.Count > 0)
      {
        var cur = queue.Dequeue();
        foreach (var file in cur.GetFiles())
        {
          if (search.IsMatch(file.FullName))
          {
            string targetPath = null;

            Console.Write($"{file.FullName}: ");
            try
            {
              string answer = CopyFileToTarget(target, pathOffset, file, out targetPath);
              Console.WriteLine($"{answer}");
            }
            catch (Exception ex)
            {
              Console.Error.WriteLine(ex.ToString());
            }

            if (targetPath != null && evtxHints.IsMatch(file.FullName))
            {
              Console.Write($"{file.FullName} analyzing evtx file: ");
              try
              {
                string answer = AnalyzeEventFile(target, targetPath);
                Console.WriteLine($"{answer}");
              }
              catch (Exception ex)
              {
                Console.Error.WriteLine(ex.ToString());
              }
            }

            if (targetPath != null && regHints.IsMatch(file.FullName))
            {
              Console.Write($"{file.FullName} analyzing registry: ");
              try
              {
                string answer = AnalyzeRegistryFile(target, targetPath);
                Console.WriteLine($"{answer}");
              }
              catch (Exception ex)
              {
                Console.Error.WriteLine(ex.ToString());
              }
            }
          }
        }
        foreach (var dir in cur.GetDirectories())
        {
          if (dir.Name == "." || dir.Name == "..")
          {
            continue;
          }
          queue.Enqueue(dir);
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
      
      Directory.Delete("tmp", true);
      Directory.CreateDirectory("tmp");

      using (var ss = target.OpenFile(targetPath, FileMode.Create, FileAccess.Read))
      using (var fs = new FileStream(@"tmp\eventlog", FileMode.Create, FileAccess.Write, FileShare.None))
      {
        ss.CopyTo(fs);
      }
      
      using (var rd = new EventLogReader(@"tmp\eventlog", PathType.FilePath))
      using (var fs = target.OpenFile($"{targetPath}_EvtxLog.txt", FileMode.Create, FileAccess.Write))
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
              sw.WriteLine(evtstr);
            }
          }
          catch (Exception)
          {
            // silently ignore
          }
        }
      }

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
