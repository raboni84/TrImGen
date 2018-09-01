using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;
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

        using (var target = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
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
        catch (Exception ex)
        {
          Console.Error.WriteLine(ex.ToString());
        }
      }
    }

    private static void AnalyzeFileSystem(IFileSystem source, IFileSystem target, string pathOffset = null)
    {
      Regex search = new Regex(string.Join("|", config.SearchPatterns), RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
      Queue<DiscDirectoryInfo> queue = new Queue<DiscDirectoryInfo>();
      queue.Enqueue(source.Root);
      while (queue.Count > 0)
      {
        var cur = queue.Dequeue();
        foreach (var file in cur.GetFiles())
        {
          if (search.IsMatch(file.FullName))
          {
            try
            {
              string answer = CopyFileToTarget(target, pathOffset, file);
              Console.WriteLine($"{file.FullName}: {answer}");
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

    private static string CopyFileToTarget(IFileSystem target, string pathOffset, DiscFileInfo file)
    {
      string sourcePath = $"{pathOffset}{file.DirectoryName}";
      string targetPath = $"{pathOffset}{file.FullName}";

      target.CreateDirectory(sourcePath);
      bool done = false;
      int retry = 0;
      do
      {
        retry++;
        if (target.FileExists(targetPath))
          target.DeleteFile(targetPath);

        using (var fs = target.OpenFile(targetPath, FileMode.Create, FileAccess.ReadWrite))
        using (var ss = file.OpenRead())
        {
          ss.CopyTo(fs);
          ss.Seek(0, SeekOrigin.Begin);
          fs.Seek(0, SeekOrigin.Begin);
          done = ss.SequenceEqual(fs);
        }
      }
      while (!done && retry <= config.CopyRetryCount);

      return retry <= config.CopyRetryCount ? "ok" : "error";
    }
  }
}
