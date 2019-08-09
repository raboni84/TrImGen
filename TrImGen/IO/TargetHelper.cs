using System;
using System.IO;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Streams;
using ExFat;
using ExFat.DiscUtils;

namespace TrImGen.IO
{
  static class TargetHelper
  {
    public static VirtualDisk InitializeVirtualDisk(FileStream target, long size, TargetDiskType disktype)
    {
      long miss = size % 1024;
      if (miss > 0)
      {
        size = size + 1024 - miss;
      }
      VirtualDisk disk = null;
      if (disktype == TargetDiskType.Vhd)
        disk = DiscUtils.Vhd.Disk.InitializeDynamic(target, Ownership.None, size);
      else if (disktype == TargetDiskType.Vhdx)
        disk = DiscUtils.Vhdx.Disk.InitializeDynamic(target, Ownership.None, size);
      else if (disktype == TargetDiskType.Vdi)
        disk = DiscUtils.Vdi.Disk.InitializeDynamic(target, Ownership.None, size);
      else if (disktype == TargetDiskType.Raw)
        disk = DiscUtils.Raw.Disk.Initialize(target, Ownership.None, size);
      else
        throw new NotSupportedException();
      return disk;
    }

    public static IFileSystem InitializeFirstPartition(VirtualDisk disk, TargetPartitionType partitiontype)
    {
      GuidPartitionTable table = GuidPartitionTable.Initialize(disk);
      table.Create(table.FirstUsableSector, table.LastUsableSector, GuidPartitionTypes.WindowsBasicData, 0, "DATA");
      var vol = VolumeManager.GetPhysicalVolumes(disk)[0];

      IFileSystem targetFileSystem;
      if (partitiontype == TargetPartitionType.ExFat)
        targetFileSystem = ExFatFileSystem.Format(vol, new ExFatFormatOptions()
        {
          SectorsPerCluster = vol.Length < 268435456L ? 8U : vol.Length < 34359738368L ? 64U : 256U,
          BytesPerSector = 512
        }, "DATA");
      else if (partitiontype == TargetPartitionType.Ntfs)
        targetFileSystem = NtfsFileSystem.Format(vol, "DATA");
      else
        throw new NotSupportedException();

      return targetFileSystem;
    }
  }
}