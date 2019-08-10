using System.IO;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Streams;

namespace TrImGen.IO
{
#if DEBUG
  static class SelfTest
  {
    public static bool Run()
    {
      Directory.CreateDirectory("Test");
      CreateTestBase();
      return true;
    }

    static void CreateTestBase()
    {
      using (var test = new FileStream("Test/Test.vhd", FileMode.Create, FileAccess.ReadWrite, FileShare.None))
      {
        VirtualDisk x = TargetHelper.InitializeVirtualDisk(test, 104857600L, TargetDiskType.Vhd);
        DiscUtils.Partitions.GuidPartitionTable table = DiscUtils.Partitions.GuidPartitionTable.Initialize(x);
        table.Create(table.FirstUsableSector, table.FirstUsableSector + (33554432L / x.SectorSize), DiscUtils.Partitions.GuidPartitionTypes.EfiSystem, 0, "EFI");
        table.Create(table.FirstUsableSector + (33554432L / x.SectorSize) + 1, table.LastUsableSector - (2097152L / x.SectorSize), DiscUtils.Partitions.GuidPartitionTypes.WindowsBasicData, 0, "DATA");
        var vol = VolumeManager.GetPhysicalVolumes(x);
        var fs1 = NtfsFileSystem.Format(vol[0], "EFI");
        fs1.CreateDirectory("EFI\\boot");
        fs1.CreateDirectory("EFI\\Microsoft\\boot");
        using (var bsd = fs1.OpenFile("EFI\\Microsoft\\boot\\BCD", FileMode.Create, FileAccess.ReadWrite))
        {
          DiscUtils.Registry.RegistryHive.Create(bsd, Ownership.None);
        }
        var fs2 = NtfsFileSystem.Format(vol[1], "DATA");
        fs2.CreateDirectory("Windows\\System32\\config");
        using (var bsd = fs2.OpenFile("Windows\\System32\\config\\SYSTEM", FileMode.Create, FileAccess.ReadWrite))
        {
          DiscUtils.Registry.RegistryHive.Create(bsd, Ownership.None);
        }
      }
    }
  }
#endif
}