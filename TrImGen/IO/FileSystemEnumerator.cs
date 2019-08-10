using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DiscUtils;

namespace TrImGen.IO
{
  class FileSystemEnumerator : IEnumerator<DiscFileInfo>
  {
    Queue<DiscDirectoryInfo> queue;

    IFileSystem fs;

    IEnumerator<DiscFileInfo> fileEnum;

    Regex pattern;

    DiscFileInfo current;

    public DiscFileInfo Current => current;

    object IEnumerator.Current => Current;

    public FileSystemEnumerator(IFileSystem fs, Regex pattern = null)
    {
      this.fs = fs;
      this.pattern = pattern;
      this.queue = new Queue<DiscDirectoryInfo>();
      Reset();
    }

    public void Dispose()
    {
      fileEnum?.Dispose();
      fileEnum = null;
    }

    public bool MoveNext()
    {
      while (fileEnum != null || queue.Count > 0)
      {
        if (fileEnum == null)
        {
          var dir = queue.Dequeue();
          fileEnum = ((IEnumerable<DiscFileInfo>)dir.GetFiles()).GetEnumerator();
          fileEnum.Reset();
          foreach (var elem in dir.GetDirectories())
          {
            if (elem.Name != "." && elem.Name != "..")
            {
              queue.Enqueue(elem);
            }
          }
        }
        if (!fileEnum.MoveNext())
        {
          fileEnum.Dispose();
          fileEnum = null;
          continue;
        }
        current = fileEnum.Current;
        if (pattern != null && !pattern.IsMatch(current.FullName))
        {
          continue;
        }
        return true;
      }
      return false;
    }

    public void Reset()
    {
      fileEnum?.Dispose();
      fileEnum = null;
      queue.Clear();
      queue.Enqueue(fs.Root);
    }
  }
}