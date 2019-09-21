using System;
using LibHac.FsSystem.RomFs;
using Xunit;

namespace LibHac.Tests
{
    public class RomFsTests
    {
        [Fact]
        public void SimpleAddAndRead()
        {
            const string path = "/a/b";

            var table = new HierarchicalRomFileTable<RomFileInfo>();
            var item = new RomFileInfo { Length = 1, Offset = 1 };

            table.AddFile(path, ref item);
            bool success = table.TryOpenFile(path, out RomFileInfo readItem);

            Assert.True(success, "Table read failed");
            Assert.Equal(item, readItem);
        }

        [Fact]
        public void UpdateExistingFile()
        {
            const string path = "/a/b";

            var table = new HierarchicalRomFileTable<RomFileInfo>();
            var originalItem = new RomFileInfo { Length = 1, Offset = 1 };
            var newItem = new RomFileInfo { Length = 1, Offset = 1 };

            table.AddFile(path, ref originalItem);
            table.AddFile(path, ref newItem);

            bool success = table.TryOpenFile(path, out RomFileInfo readItem);

            Assert.True(success, "Table read failed");
            Assert.Equal(newItem, readItem);
        }

        [Fact]
        public void AddingDirectory()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();
            var expectedPosition = new FindPosition { NextDirectory = -1, NextFile = -1 };

            table.AddDirectory("/dir");
            bool success = table.TryOpenDirectory("/dir", out FindPosition position);

            Assert.True(success, "Opening directory failed");
            Assert.Equal(expectedPosition, position);
        }

        [Fact]
        public void AddingEmptyPathThrows()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();
            var item = new RomFileInfo();

            Assert.Throws<ArgumentException>(() => table.AddFile("", ref item));
        }

        [Fact]
        public void OpeningNonexistentFileFails()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();

            bool success = table.TryOpenFile("/foo", out _);
            Assert.False(success);
        }

        [Fact]
        public void OpeningNonexistentDirectoryFails()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();

            bool success = table.TryOpenDirectory("/foo", out _);
            Assert.False(success);
        }

        [Fact]
        public void OpeningFileAsDirectoryFails()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();
            var fileInfo = new RomFileInfo();
            table.AddFile("/file", ref fileInfo);

            bool success = table.TryOpenDirectory("/file", out _);
            Assert.False(success);
        }

        [Fact]
        public void OpeningDirectoryAsFileFails()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();
            table.AddDirectory("/dir");

            bool success = table.TryOpenFile("/dir", out _);
            Assert.False(success);
        }

        [Fact]
        public void ChildFileIteration()
        {
            const int fileCount = 10;
            var table = new HierarchicalRomFileTable<RomFileInfo>();

            for (int i = 0; i < fileCount; i++)
            {
                var item = new RomFileInfo { Length = i, Offset = i };
                table.AddFile($"/a/{i}", ref item);
            }

            bool openDirSuccess = table.TryOpenDirectory("/a", out FindPosition position);
            Assert.True(openDirSuccess, "Error opening directory");

            for (int i = 0; i < fileCount; i++)
            {
                var expectedItem = new RomFileInfo { Length = i, Offset = i };
                string expectedName = i.ToString();

                bool success = table.FindNextFile(ref position, out RomFileInfo actualItem, out string actualName);

                Assert.True(success, $"Failed reading file {i}");
                Assert.Equal(expectedItem, actualItem);
                Assert.Equal(expectedName, actualName);
            }

            bool endOfFilesSuccess = table.FindNextFile(ref position, out _, out _);
            Assert.False(endOfFilesSuccess, "Table returned more files than it should");
        }

        [Fact]
        public void ChildFileIterationPeek()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();

            var itemA = new RomFileInfo { Length = 1, Offset = 1 };
            var itemB = new RomFileInfo { Length = 2, Offset = 2 };

            table.AddFile("/a/a", ref itemA);
            table.AddFile("/a/b", ref itemB);

            table.TryOpenDirectory("/a", out FindPosition position);

            table.TryOpenFile(position.NextFile, out RomFileInfo peekItemA);
            Assert.Equal(itemA, peekItemA);

            table.FindNextFile(ref position, out RomFileInfo iterateItemA, out _);
            Assert.Equal(itemA, iterateItemA);

            table.TryOpenFile(position.NextFile, out RomFileInfo peekItemB);
            Assert.Equal(itemB, peekItemB);

            table.FindNextFile(ref position, out RomFileInfo iterateItemB, out _);
            Assert.Equal(itemB, iterateItemB);
        }

        [Fact]
        public void AddingCousinFiles()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();

            var itemB1 = new RomFileInfo { Length = 1, Offset = 1 };
            var itemB2 = new RomFileInfo { Length = 2, Offset = 2 };
            var itemB3 = new RomFileInfo { Length = 3, Offset = 3 };

            table.AddFile("/a/b1/c", ref itemB1);
            table.AddFile("/a/b2/c", ref itemB2);
            table.AddFile("/a/b3/c", ref itemB3);

            table.TryOpenFile("/a/b1/c", out RomFileInfo actualItemB1);
            table.TryOpenFile("/a/b2/c", out RomFileInfo actualItemB2);
            table.TryOpenFile("/a/b3/c", out RomFileInfo actualItemB3);

            Assert.Equal(itemB1, actualItemB1);
            Assert.Equal(itemB2, actualItemB2);
            Assert.Equal(itemB3, actualItemB3);
        }

        [Fact]
        public void AddingSiblingFiles()
        {
            var table = new HierarchicalRomFileTable<RomFileInfo>();

            var itemC1 = new RomFileInfo { Length = 1, Offset = 1 };
            var itemC2 = new RomFileInfo { Length = 2, Offset = 2 };
            var itemC3 = new RomFileInfo { Length = 3, Offset = 3 };

            table.AddFile("/a/b/c1", ref itemC1);
            table.AddFile("/a/b/c2", ref itemC2);
            table.AddFile("/a/b/c3", ref itemC3);

            table.TryOpenFile("/a/b/c1", out RomFileInfo actualItemC1);
            table.TryOpenFile("/a/b/c2", out RomFileInfo actualItemC2);
            table.TryOpenFile("/a/b/c3", out RomFileInfo actualItemC3);

            Assert.Equal(itemC1, actualItemC1);
            Assert.Equal(itemC2, actualItemC2);
            Assert.Equal(itemC3, actualItemC3);
        }
    }
}
