﻿// This is ExFat, an exFAT accessor written in pure C#
// Released under MIT license
// https://github.com/picrap/ExFat

namespace ExFat.Partition
{
    using System;
    using System.IO;
    using System.Linq;
    using Buffers;
    using Buffer = Buffers.Buffer;

    /// <summary>
    /// exFAT boot sector
    /// </summary>
    public class ExFatBootSector
    {
        /// <summary>
        /// The default JMP boot
        /// </summary>
        public static readonly byte[] DefaultJmpBoot = new byte[] { 0xEB, 0x76, 0x90 };
        /// <summary>
        /// The OEM name
        /// </summary>
        public const string ExFatOemName = "EXFAT   ";

        private readonly byte[] _bytes;

        /// <summary>
        /// Gets or sets the JMP boot.
        /// </summary>
        /// <value>
        /// The JMP boot.
        /// </value>
        public BufferBytes JmpBoot { get; }

        /// <summary>
        /// Gets or sets the OEM name.
        /// </summary>
        /// <value>
        /// The name of the oem.
        /// </value>
        public IValueProvider<string> OemName { get; }

        /// <summary>
        /// Total number of Sectors.
        /// </summary>
        /// <value>
        /// The length of the volume.
        /// </value>
        public IValueProvider<UInt64> VolumeLengthSectors { get; }

        /// <summary>
        /// Sector address of 1st FAT.
        /// </summary>
        /// <value>
        /// The fat offset.
        /// </value>
        public IValueProvider<UInt32> FatOffsetSector { get; }

        /// <summary>
        /// Size of FAT in sectors.
        /// </summary>
        /// <value>
        /// The length of the fat.
        /// </value>
        public IValueProvider<UInt32> FatLengthSectors { get; }

        /// <summary>
        /// Starting sector of cluster heap
        /// </summary>
        /// <value>
        /// The cluster offset.
        /// </value>
        public IValueProvider<UInt32> ClusterOffsetSector { get; }

        /// <summary>
        /// Number of clusters.
        /// </summary>
        /// <value>
        /// The cluster count.
        /// </value>
        public IValueProvider<UInt32> ClusterCount { get; }

        /// <summary>
        /// First cluster of root directory.
        /// </summary>
        /// <value>
        /// The root directory.
        /// </value>
        public IValueProvider<UInt32> RootDirectoryCluster { get; }

        /// <summary>
        /// Gets or sets the volume serial number.
        /// </summary>
        /// <value>
        /// The volume serial number.
        /// </value>
        public IValueProvider<UInt32> VolumeSerialNumber { get; }

        /// <summary>
        /// Gets the file system revision (currently 256).
        /// </summary>
        /// <value>
        /// The file system revision.
        /// </value>
        public IValueProvider<UInt16> FileSystemRevision { get; }

        /// <summary>
        /// Gets the volume flags.
        /// Bit 0 – Active FAT
        ///         0 – 1st , 1 – 2nd
        /// Bit 1 – Volume Dirty
        ///         0 – Clean, 1- dirty
        /// Bits 2 + 3 – Media failure
        ///         0 – No failures, 1 – failures reported
        /// </summary>
        /// <value>
        /// The volume flags.
        /// </value>
        public IValueProvider<UInt16> VolumeFlags { get; }

        /// <summary>
        /// This is power of 2; Minimal value is 9; 2^9=512 bytes
        /// Bytes and maximum 2^12=4096 Bytes
        /// </summary>
        /// <value>
        /// The bytes per sector.
        /// </value>
        public IValueProvider<UInt32> BytesPerSector { get; }

        /// <summary>
        /// This is power of 2; Minimal value is 1; 2^0=1
        /// sector (512 Bytes) and maximum 32 MB cluster
        /// size in bytes
        /// </summary>
        /// <value>
        /// The sectors per cluster.
        /// </value>
        public IValueProvider<UInt32> SectorsPerCluster { get; }

        /// <summary>
        /// Either 1 or 2; if TexFAT is supported then it will be 2
        /// </summary>
        /// <value>
        /// The number of fats.
        /// </value>
        public IValueProvider<byte> NumberOfFats { get; }

        /// <summary>
        /// Extended INT 13h drive number; typically 0x80
        /// </summary>
        /// <value>
        /// The drive select.
        /// </value>
        public IValueProvider<byte> DriveSelect { get; }

        /// <summary>
        /// 0..100 – percentage of allocated clusters rounded down to the integer 0xFF – percentage is not available
        /// </summary>
        /// <value>
        /// The percent in use.
        /// </value>
        public IValueProvider<byte> PercentInUse { get; }

        /// <summary>
        /// Returns true if this boot sector is valid (better check this after reading it).
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </value>
        public bool IsExFat => OemName.Value == ExFatOemName;

        /// <summary>
        /// Returns true if the whole bootsector is valid.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is valid; otherwise, <c>false</c>.
        /// </value>
        public bool IsValid => IsExFat && _bytes[BytesPerSector.Value - 2] == 0x55 && _bytes[BytesPerSector.Value - 1] == 0xAA && IsChecksumValid();

        /// <summary>
        /// Initializes a new instance of the <see cref="ExFatBootSector"/> class.
        /// </summary>
        internal ExFatBootSector(byte[] bytes)
        {
            _bytes = bytes;
            var buffer = new Buffer(_bytes);
            JmpBoot = new BufferBytes(buffer, 0, 3);
            OemName = new BufferByteString(buffer, 3, 8);
            VolumeLengthSectors = new BufferUInt64(buffer, 72);
            FatOffsetSector = new BufferUInt32(buffer, 80);
            FatLengthSectors = new BufferUInt32(buffer, 84);
            ClusterOffsetSector = new BufferUInt32(buffer, 88);
            ClusterCount = new BufferUInt32(buffer, 92);
            RootDirectoryCluster = new BufferUInt32(buffer, 96);
            VolumeSerialNumber = new BufferUInt32(buffer, 100);
            FileSystemRevision = new BufferUInt16(buffer, 104);
            VolumeFlags = new BufferUInt16(buffer, 106);
            BytesPerSector = new CacheValueProvider<uint>(new ShiftValueProvider(new BufferUInt8(buffer, 108)));
            SectorsPerCluster = new ShiftValueProvider(new BufferUInt8(buffer, 109));
            NumberOfFats = new BufferUInt8(buffer, 110);
            DriveSelect = new BufferUInt8(buffer, 111);
            PercentInUse = new BufferUInt8(buffer, 112);
        }

        /// <summary>
        /// Computes the checksum.
        /// </summary>
        /// <returns></returns>
        public byte[] ComputeChecksum()
        {
            var checksum = _bytes.GetChecksum32(0, 106);
            checksum = _bytes.GetChecksum32(108, 4, checksum);
            checksum = _bytes.GetChecksum32(113, (int)(BytesPerSector.Value * 11 - 113), checksum);
            return LittleEndian.GetBytes(checksum);
        }

        private bool IsChecksumValid()
        {
            var checksum = ComputeChecksum();
            var startSectorOffset = 11 * BytesPerSector.Value;
            var endSectorOffset = 12 * BytesPerSector.Value;
            for (var lastSectorOffset = startSectorOffset; lastSectorOffset < endSectorOffset;)
            {
                if (checksum[0] != _bytes[lastSectorOffset++]
                    || checksum[1] != _bytes[lastSectorOffset++]
                    || checksum[2] != _bytes[lastSectorOffset++]
                    || checksum[3] != _bytes[lastSectorOffset++])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Reads boot sector from specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public void Read(Stream stream)
        {
            stream.Read(_bytes, 0, _bytes.Length);
        }
    }
}
