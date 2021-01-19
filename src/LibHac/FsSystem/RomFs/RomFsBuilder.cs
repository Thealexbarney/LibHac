using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem.RomFs
{
    /// <summary>
    /// Builds a RomFS from a collection of files.
    /// </summary>
    /// <remarks>A <see cref="RomFsBuilder"/> produces a view of a RomFS archive.
    /// When doing so, it will create an <see cref="IStorage"/> instance that will
    /// provide the RomFS data when read. Random seek is supported.</remarks>
    public class RomFsBuilder
    {
        private const int FileAlignment = 0x10;
        private const int HeaderSize = 0x50;
        private const int HeaderWithPaddingSize = 0x200;

        private List<IStorage> Sources { get; } = new List<IStorage>();
        private HierarchicalRomFileTable<RomFileInfo> FileTable { get; } = new HierarchicalRomFileTable<RomFileInfo>();
        private long CurrentOffset { get; set; }

        /// <summary>
        /// Creates a new, empty <see cref="RomFsBuilder"/>
        /// </summary>
        public RomFsBuilder() { }

        /// <summary>
        /// Creates a new <see cref="RomFsBuilder"/> and populates it with all
        /// the files in the specified <see cref="IFileSystem"/>.
        /// </summary>
        public RomFsBuilder(IFileSystem input)
        {
            foreach (DirectoryEntryEx entry in input.EnumerateEntries().Where(x => x.Type == DirectoryEntryType.File)
                .OrderBy(x => x.FullPath, StringComparer.Ordinal))
            {
                input.OpenFile(out IFile file, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                AddFile(entry.FullPath, file);
            }
        }

        /// <summary>
        /// Adds a file to the RomFS.
        /// </summary>
        /// <param name="path">The full path in the RomFS</param>
        /// <param name="file">An <see cref="IFile"/> of the file data to add.</param>
        public void AddFile(string path, IFile file)
        {
            var fileInfo = new RomFileInfo();
            file.GetSize(out long fileSize).ThrowIfFailure();

            fileInfo.Offset = CurrentOffset;
            fileInfo.Length = fileSize;

            IStorage fileStorage = file.AsStorage();
            Sources.Add(fileStorage);

            long newOffset = CurrentOffset + fileSize;
            CurrentOffset = Alignment.AlignUp(newOffset, FileAlignment);

            var padding = new NullStorage(CurrentOffset - newOffset);
            Sources.Add(padding);

            FileTable.AddFile(path, ref fileInfo);
        }

        /// <summary>
        /// Returns a view of a RomFS containing all the currently added files.
        /// Additional files may be added and a new view produced without
        /// invalidating previously built RomFS views.
        /// </summary>
        /// <returns></returns>
        public IStorage Build()
        {
            FileTable.TrimExcess();

            byte[] header = new byte[HeaderWithPaddingSize];
            var headerWriter = new BinaryWriter(new MemoryStream(header));

            var sources = new List<IStorage>();
            sources.Add(new MemoryStorage(header));
            sources.AddRange(Sources);

            long fileLength = sources.Sum(x =>
            {
                x.GetSize(out long fileSize).ThrowIfFailure();
                return fileSize;
            });

            headerWriter.Write((long)HeaderSize);

            AddTable(FileTable.GetDirectoryBuckets());
            AddTable(FileTable.GetDirectoryEntries());
            AddTable(FileTable.GetFileBuckets());
            AddTable(FileTable.GetFileEntries());

            headerWriter.Write((long)HeaderWithPaddingSize);

            return new ConcatenationStorage(sources, true);

            void AddTable(byte[] table)
            {
                sources.Add(new MemoryStorage(table));
                headerWriter.Write(fileLength);
                headerWriter.Write((long)table.Length);
                fileLength += table.Length;
            }
        }
    }
}
