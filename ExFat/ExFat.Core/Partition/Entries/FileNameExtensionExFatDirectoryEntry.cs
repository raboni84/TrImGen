﻿// This is ExFat, an exFAT accessor written in pure C#
// Released under MIT license
// https://github.com/picrap/ExFat

namespace ExFat.Partition.Entries
{
    using System;
    using System.Diagnostics;
    using Buffers;
    using Buffer = Buffers.Buffer;

    /// <inheritdoc />
    /// <summary>
    /// Secondary entry representing file name part (up to 15 characters).
    /// </summary>
    /// <seealso cref="T:ExFat.Partition.Entries.ExFatDirectoryEntry" />
    [DebuggerDisplay("File name part {FileName.Value}")]
    public class FileNameExtensionExFatDirectoryEntry : ExFatDirectoryEntry
    {
        /// <summary>
        /// Gets or sets the general secondary flags.
        /// </summary>
        /// <value>
        /// The general secondary flags.
        /// </value>
        public IValueProvider<ExFatGeneralSecondaryFlags> GeneralSecondaryFlags { get; }
        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
        /// <value>
        /// The name of the file.
        /// </value>
        public IValueProvider<string> FileName { get; }

        /// <inheritdoc />
        /// <summary>
        /// Initializes a new instance of the <see cref="T:ExFat.Partition.Entries.FileNameExtensionExFatDirectoryEntry" /> class.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        public FileNameExtensionExFatDirectoryEntry(Buffer buffer) : base(buffer)
        {
            GeneralSecondaryFlags = new EnumValueProvider<ExFatGeneralSecondaryFlags, Byte>(new BufferUInt8(buffer, 1));
            FileName = new BufferWideString(buffer, 2, 15);
        }
    }
}