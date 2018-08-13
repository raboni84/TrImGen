using System.IO;
using System.Linq;

namespace TrImGen
{
  public static class StreamExtensions
  {
    public static bool SequenceEqual(this Stream src, Stream dst)
    {
      byte[] srcBuf = new byte[4096], dstBuf = new byte[4096];
      int srcBytesRead, dstBytesRead;
      while ((srcBytesRead = src.Read(srcBuf, 0, 4096)) > 0)
      {
        dstBytesRead = dst.Read(dstBuf, 0, 4096);
        if (srcBytesRead != dstBytesRead)
          return false;
        if (!srcBuf.Take(srcBytesRead).Zip(dstBuf.Take(dstBytesRead), (a, b) => a == b).All(_ => _))
          return false;
      }
      return true;
    }
  }
}