using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TrImGen
{
  public static class PInvoke
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
      uint dwFlagsAndAttributes,
      IntPtr hTemplateFile);
    
    [DllImport("kernel32.dll")]
    static extern unsafe bool ReadFile(
      SafeFileHandle hFile,
      byte* lpBuffer,
      int nNumberOfBytesToRead,
      ref int lpNumberOfBytesRead,
      IntPtr lpOverlapped);

    [DllImport("kernel32.dll")]
    static extern bool SetFilePointerEx(
      SafeFileHandle hFile,
      long liDistanceToMove,
      ref long lpNewFilePointer,
      uint dwMoveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern unsafe bool DeviceIoControl(
      SafeFileHandle hDevice,
      uint dwIoControlCode,
      byte[] InBuffer,
      int nInBufferSize,
      void* OutBuffer,
      int nOutBufferSize,
      int* pBytesReturned,
      IntPtr lpOverlapped);

    static readonly uint Win_DiskGetLength = 0x7405c;

    static readonly uint Win_DiskGetDriveGeometry = 0x70000;
    
    static readonly uint GenericRead = 0x80000000;
    
    static readonly uint ReadWrite = 0x00000001 | 0x00000002;
    
    static readonly uint OpenExisting = 3;
    
    static readonly uint DeviceNoBufferingRandomAccess = 0x00000040 | 0x20000000 | 0x10000000;

    public static bool IsLinux
    {
      get
      {
        int p = (int)Environment.OSVersion.Platform;
        return (p == 4) || (p == 6) || (p == 128);
      }
    }

    public static SafeFileHandle CreateDriveHandle(string drivePath)
    {
      SafeFileHandle hDrive = CreateFile(
        drivePath,
        GenericRead,
        ReadWrite,
        IntPtr.Zero,
        OpenExisting,
        DeviceNoBufferingRandomAccess,
        IntPtr.Zero);
      
      if (hDrive.IsInvalid)
        throw new InvalidOperationException(drivePath);
      
      return hDrive;
    }

    public static unsafe bool ReadDrive(DriveStream ds, byte[] buffer, int offset, int count, ref long position, out int bytesRead)
    {
      int sectorBoundary = (int)(count / ds.SectorSize) * ds.SectorSize;
      bytesRead = 0;
      bool success;
      fixed (byte* pinned = buffer)
      {
        success = ReadFile(ds.Handle, pinned, sectorBoundary, ref bytesRead, IntPtr.Zero);
      }
      position += bytesRead;
      return success;
    }

    public static bool SeekDrive(DriveStream ds, long offset, SeekOrigin origin, ref long position)
    {
      if (origin == SeekOrigin.Begin)
      {
        position = (long)(offset / ds.SectorSize) * ds.SectorSize;
      }
      else if (origin == SeekOrigin.Current)
      {
        position = (long)((position + offset) / ds.SectorSize) * ds.SectorSize;
      }
      else
      {
        position = (long)((ds.Length - offset) / ds.SectorSize) * ds.SectorSize;
      }
      return SetFilePointerEx(ds.Handle, position, ref position, 0);
    }

    public static void ReleaseDriveHandle(SafeFileHandle handle)
    {
      if (handle != null && !handle.IsInvalid)
      {
        handle.Dispose();
        handle.SetHandleAsInvalid();
      }
    }

    public static unsafe long GetDriveLength(SafeFileHandle hDrive)
    {
      if (IsLinux)
      {
        throw new NotImplementedException();
      }
      else
      {
        long length;
        int bytesRet;
        if (!DeviceIoControl(hDrive, Win_DiskGetLength, null, 0, &length, 8, &bytesRet, IntPtr.Zero))
          throw new InvalidDataException();
        return length;
      }
    }

    public static unsafe int GetSectorSize(SafeFileHandle hDrive)
    {
      if (IsLinux)
      {
        throw new NotImplementedException();
      }
      else
      {
        fixed (byte* ptr = new byte[24])
        {
          int bytesRet;
          if (!DeviceIoControl(hDrive, Win_DiskGetDriveGeometry, null, 0, ptr, 24, &bytesRet, IntPtr.Zero))
            throw new InvalidDataException();
          return *(int*)(ptr + 20);
        }
      }
    }
  }
}