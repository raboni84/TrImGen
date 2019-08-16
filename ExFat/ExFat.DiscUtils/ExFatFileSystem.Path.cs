﻿// This is ExFat, an exFAT accessor written in pure C#
// Released under MIT license
// https://github.com/picrap/ExFat

namespace ExFat.DiscUtils
{
    partial class ExFatFileSystem
    {
        /// <summary>
        /// Gets or sets the path separators.
        /// First separator is also used to compose paths
        /// </summary>
        /// <value>
        /// The path separators.
        /// </value>
        public char[] PathSeparators
        {
            get { return _filesystem.PathSeparators; }
            set { _filesystem.PathSeparators = value; }
        }

        public static readonly char[] DefaultSeparators = new[] { '\\', '/' };

        private string GetFileName(string path)
        {
            var lastIndex = path.LastIndexOfAny(PathSeparators);
            if (lastIndex < 0)
                return path;
            return path.Substring(lastIndex + 1);
        }

        private string GetDirectoryName(string path)
        {
            if (path == "")
                return null;
            var lastIndex = path.LastIndexOfAny(PathSeparators);
            if (lastIndex < 0)
                return "";
            return path.Substring(0, lastIndex);
        }
    }
}
