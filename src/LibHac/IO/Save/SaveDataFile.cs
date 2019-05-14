using System;
using System.IO;

namespace LibHac.Fs.Save
{
    public class SaveDataFile : FileBase
    {
        private AllocationTableStorage BaseStorage { get; }
        private string Path { get; }
        private HierarchicalSaveFileTable FileTable { get; }
        private long Size { get; set; }

        public SaveDataFile(AllocationTableStorage baseStorage, string path, HierarchicalSaveFileTable fileTable, long size, OpenMode mode)
        {
            Mode = mode;
            BaseStorage = baseStorage;
            Path = path;
            FileTable = fileTable;
            Size = size;
        }

        public override int Read(Span<byte> destination, long offset)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            BaseStorage.Read(destination.Slice(0, toRead), offset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            ValidateWriteParams(source, offset);

            BaseStorage.Write(source, offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override long GetSize()
        {
            return Size;
        }

        public override void SetSize(long size)
        {
            if (size < 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (Size == size) return;

            BaseStorage.SetSize(size);

            if (!FileTable.TryOpenFile(Path, out SaveFileInfo fileInfo))
            {
                throw new FileNotFoundException();
            }

            fileInfo.StartBlock = BaseStorage.InitialBlock;
            fileInfo.Length = size;

            FileTable.AddFile(Path, ref fileInfo);

            Size = size;
        }
    }
}
