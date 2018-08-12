using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace TrImGen
{
  public class DriveStream : Stream
  {
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(SafeFileHandle handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        EFileAttributes dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll")]
    public static extern bool ReadFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        int nNumberOfBytesToRead,
        ref int lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll")]
    public static extern bool SetFilePointerEx(
        SafeFileHandle hFile,
        long liDistanceToMove,
        ref long lpNewFilePointer,
        uint dwMoveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(
      SafeFileHandle hDevice,
      uint dwIoControlCode,
      byte[] InBuffer,
      int nInBufferSize,
      byte[] OutBuffer,
      int nOutBufferSize,
      ref int pBytesReturned,
      IntPtr lpOverlapped);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FlushFileBuffers(SafeFileHandle hDevice);

    [Flags]
    public enum EFileAttributes : uint
    {
      Readonly = 0x00000001,
      Hidden = 0x00000002,
      System = 0x00000004,
      Directory = 0x00000010,
      Archive = 0x00000020,
      Device = 0x00000040,
      Normal = 0x00000080,
      Temporary = 0x00000100,
      SparseFile = 0x00000200,
      ReparsePoint = 0x00000400,
      Compressed = 0x00000800,
      Offline = 0x00001000,
      NotContentIndexed = 0x00002000,
      Encrypted = 0x00004000,
      Write_Through = 0x80000000,
      Overlapped = 0x40000000,
      NoBuffering = 0x20000000,
      RandomAccess = 0x10000000,
      SequentialScan = 0x08000000,
      DeleteOnClose = 0x04000000,
      BackupSemantics = 0x02000000,
      PosixSemantics = 0x01000000,
      OpenReparsePoint = 0x00200000,
      OpenNoRecall = 0x00100000,
      FirstPipeInstance = 0x00080000
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    long length = -1;
    public override long Length => length;

    int geometry = -1;

    private static readonly uint GenericRead = 0x80000000;
    private static readonly uint ReadWrite = 0x00000001 | 0x00000002;
    private static readonly uint OpenExisting = 3;
    private static readonly uint DiskGetLength = 0x7405c;
    private static readonly uint DiskGetDriveGeometry = 0x70000;
    SafeFileHandle hDrive;

    long position = 0;
    public override long Position
    {
      get
      {
        return position;
      }
      set
      {
        Seek(value, SeekOrigin.Begin);
      }
    }

    public DriveStream(string drivePath)
    {
      hDrive = CreateFile(
        drivePath,
        GenericRead,
        ReadWrite,
        IntPtr.Zero,
        OpenExisting,
        EFileAttributes.Device | EFileAttributes.NoBuffering,
        IntPtr.Zero);

      byte[] buf = new byte[8];
      int bytesRet = -1;
      if (!DeviceIoControl(hDrive, DiskGetLength, null, 0, buf, buf.Length, ref bytesRet, IntPtr.Zero))
        throw new InvalidDataException();
      length = BitConverter.ToInt64(buf, 0);

      buf = new byte[24];
      if (!DeviceIoControl(hDrive, DiskGetDriveGeometry, null, 0, buf, buf.Length, ref bytesRet, IntPtr.Zero))
        throw new InvalidDataException();
      geometry = BitConverter.ToInt32(buf, 20);
    }

    public override void Flush()
    {
      if (!FlushFileBuffers(hDrive))
        throw new InvalidOperationException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      byte[] tmp = new byte[(int)(count / geometry) * geometry];
      int bytesRead = 0;
      if (!ReadFile(hDrive, tmp, tmp.Length, ref bytesRead, IntPtr.Zero))
        throw new IOException();
      Array.Copy(tmp, 0, buffer, offset, bytesRead);
      position += bytesRead;
      return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      if (origin == SeekOrigin.Begin)
      {
        position = (long)(offset / geometry) * geometry;
      }
      else if (origin == SeekOrigin.Current)
      {
        position = (long)((position + offset) / geometry) * geometry;
      }
      else
      {
        position = (long)((length - offset) / geometry) * geometry;
      }
      long newPos = 0;
      SetFilePointerEx(hDrive, position, ref newPos, 0);
      return position;
    }

    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
    }

    protected override void Dispose(bool disposing)
    {
      if (hDrive != null)
      {
        CloseHandle(hDrive);
        hDrive.SetHandleAsInvalid();
        hDrive = null;
      }
    }
  }
}