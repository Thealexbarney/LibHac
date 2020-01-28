﻿using System;
using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void IDirectoryRead_AllEntriesAreReturned()
        {
            IFileSystem fs = CreateFileSystem();
            fs.CreateDirectory("/dir");
            fs.CreateDirectory("/dir/dir1");
            fs.CreateFile("/dir/dir1/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/dir/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/dir/file2", 0, CreateFileOptions.None);

            Result rc = fs.OpenDirectory(out IDirectory dir, "/dir", OpenDirectoryMode.All);
            Assert.True(rc.IsSuccess());

            var entry1 = new DirectoryEntry();
            var entry2 = new DirectoryEntry();
            var entry3 = new DirectoryEntry();
            var entry4 = new DirectoryEntry();

            Assert.True(dir.Read(out long entriesRead1, SpanHelpers.AsSpan(ref entry1)).IsSuccess());
            Assert.True(dir.Read(out long entriesRead2, SpanHelpers.AsSpan(ref entry2)).IsSuccess());
            Assert.True(dir.Read(out long entriesRead3, SpanHelpers.AsSpan(ref entry3)).IsSuccess());
            Assert.True(dir.Read(out long entriesRead4, SpanHelpers.AsSpan(ref entry4)).IsSuccess());

            Assert.Equal(1, entriesRead1);
            Assert.Equal(1, entriesRead2);
            Assert.Equal(1, entriesRead3);
            Assert.Equal(0, entriesRead4);

            bool dir1Read = false;
            bool file1Read = false;
            bool file2Read = false;

            // Entries are not guaranteed to be in any particular order
            CheckEntry(ref entry1);
            CheckEntry(ref entry2);
            CheckEntry(ref entry3);

            Assert.True(dir1Read);
            Assert.True(file1Read);
            Assert.True(file2Read);

            void CheckEntry(ref DirectoryEntry entry)
            {
                switch (StringUtils.Utf8ZToString(entry.Name))
                {
                    case "dir1":
                        Assert.False(dir1Read);
                        Assert.Equal(DirectoryEntryType.Directory, entry.Type);

                        dir1Read = true;
                        break;

                    case "file1":
                        Assert.False(file1Read);
                        Assert.Equal(DirectoryEntryType.File, entry.Type);

                        file1Read = true;
                        break;

                    case "file2":
                        Assert.False(file2Read);
                        Assert.Equal(DirectoryEntryType.File, entry.Type);

                        file2Read = true;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}