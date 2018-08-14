using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DiscUtils;
using DiscUtils.Ext;
using DiscUtils.Fat;
using DiscUtils.Ntfs;
using DiscUtils.Ntfs.Internals;
using DiscUtils.Partitions;
using DiscUtils.Streams;
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

        using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
        {
          VirtualDisk disk = GetVirtualDisk(target);

          GuidPartitionTable table = GuidPartitionTable.Initialize(disk);
          int idx = table.Create(table.FirstUsableSector, table.LastUsableSector, GuidPartitionTypes.WindowsBasicData, 0, "DATA");
          var part = table.Partitions[idx];
          using (var partStream = part.Open())
          {
            NtfsFileSystem targetFileSystem = NtfsFileSystem.Format(partStream, targetName, disk.Geometry, 0, part.SectorCount - 1);

            if (arg.IndexOf(@"\\.\", StringComparison.OrdinalIgnoreCase) == 0 ||
                arg.IndexOf(@"\\?\", StringComparison.OrdinalIgnoreCase) == 0)
            {
              using (var ds = new DriveStream(arg))
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
    }

    private static string GetTargetName(string arg)
    {
      if (arg.IndexOf(@"\\.\", StringComparison.OrdinalIgnoreCase) == 0 ||
          arg.IndexOf(@"\\?\", StringComparison.OrdinalIgnoreCase) == 0)
      {
        string chars = new string(Path.GetInvalidFileNameChars());
        string pattern = $"[{Regex.Escape(chars)}]";
        return Regex.Replace(arg.Substring(4), pattern, "_");
      }
      else
      {
        return Path.GetFileNameWithoutExtension(arg);
      }
    }

    private static VirtualDisk GetVirtualDisk(FileStream target)
    {
      VirtualDisk disk = null;
      if (config.TargetDiskType == TargetDiskType.Vhd)
        disk = DiscUtils.Vhd.Disk.InitializeDynamic(target, Ownership.None, config.TargetDiskSize);
      else if (config.TargetDiskType == TargetDiskType.Vdi)
        disk = DiscUtils.Vdi.Disk.InitializeDynamic(target, Ownership.None, config.TargetDiskSize);
      else if (config.TargetDiskType == TargetDiskType.Raw)
        disk = DiscUtils.Raw.Disk.Initialize(target, Ownership.None, config.TargetDiskSize);
      else
        throw new NotSupportedException();
      return disk;
    }

    private static string GetTargetPath(string targetName)
    {
      string targetPath = null;
      if (config.TargetDiskType == TargetDiskType.Vhd)
        targetPath = $".\\{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.vhd";
      else if (config.TargetDiskType == TargetDiskType.Vdi)
        targetPath = $".\\{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.vdi";
      else if (config.TargetDiskType == TargetDiskType.Raw)
        targetPath = $".\\{targetName}_{DateTimeOffset.Now.ToUnixTimeSeconds()}.raw";
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
          using (VirtualDisk disk = new DiscUtils.Raw.Disk(s, Ownership.None))
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
        catch
        { }
      }
    }

    private static void AnalyzeFileSystem(IFileSystem source, IFileSystem target, string pathOffset = null)
    {
      Regex search = new Regex(string.Join("|", config.SearchPatterns), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      Queue<DiscDirectoryInfo> queue = new Queue<DiscDirectoryInfo>();
      queue.Enqueue(source.Root);
      using (var md5 = MD5.Create())
      {
        while (queue.Count > 0)
        {
          var cur = queue.Dequeue();
          foreach (var file in cur.GetFiles())
          {
            if (search.IsMatch(file.FullName))
            {
              try
              {
                target.CreateDirectory($"{pathOffset}{file.DirectoryName}");
                using (var fs = target.OpenFile($"{pathOffset}{file.FullName}", FileMode.Create, FileAccess.ReadWrite))
                using (var ss = file.OpenRead())
                {
                  ss.CopyTo(fs);

                  ss.Seek(0, SeekOrigin.Begin);
                  fs.Seek(0, SeekOrigin.Begin);
                  string status = ss.SequenceEqual(fs) ? "ok" : "error";
                  
                  Console.WriteLine($"{file.FullName}: {status}");
                }
              }
              catch (Exception ex)
              {
                Console.Error.WriteLine(ex.ToString());
              }
            }
          }
          foreach (var dir in cur.GetDirectories())
          {
            queue.Enqueue(dir);
          }
        }
      }
    }
  }
}
