using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace TrImGen.IO
{
  public class DriveStream : Stream
  {
    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    long length = -1;
    
    public override long Length => length;

    int sectorSize = -1;

    public int SectorSize => sectorSize;

    SafeFileHandle hDrive;

    public SafeFileHandle Handle => hDrive;

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

    private DriveStream()
    {
    }

    public static DriveStream OpenDrive(string drivePath)
    {
      DriveStream ds = new DriveStream();
      ds.hDrive = PInvoke.CreateDriveHandle(drivePath);
      ds.length = PInvoke.GetDriveLength(ds.hDrive);
      ds.sectorSize = PInvoke.GetSectorSize(ds.hDrive);
      return ds;
    }

    public override void Flush()
    {
    }

    public override unsafe int Read(byte[] buffer, int offset, int count)
    {
      int bytesRead;
      if (!PInvoke.ReadDrive(this, buffer, offset, count, ref position, out bytesRead))
        throw new IOException();
      return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      if (!PInvoke.SeekDrive(this, offset, origin, ref position))
        throw new IOException();
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
        PInvoke.ReleaseDriveHandle(hDrive);
        hDrive = null;
      }
    }
  }
}