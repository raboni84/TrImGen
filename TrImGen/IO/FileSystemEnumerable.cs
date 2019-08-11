using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DiscUtils;

namespace TrImGen.IO
{
  class FileSystemEnumerable : IEnumerable<DiscFileInfo>
  {
    IFileSystem fs;

    Regex pattern;

    public FileSystemEnumerable(IFileSystem fs, Regex pattern = null)
    {
      this.fs = fs;
      this.pattern = pattern;
    }

    public IEnumerator<DiscFileInfo> GetEnumerator() => new FileSystemEnumerator(fs, pattern);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  }
}