using System.Collections.Generic;
using System.IO;
using System.Text;

namespace libhac.Savefile
{
    public class FileEntry
    {
        public int ParentDirIndex { get; }
        public string Name { get; }
        public int Field44 { get; }
        public int Offset { get; }
        public long Size { get; }
        public long Field54 { get; }
        public int NextIndex { get; }

        public string FullPath { get; private set; }
        public FileEntry ParentDir { get; internal set; }
        public FileEntry Next { get; internal set; }

        public FileEntry(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;
            ParentDirIndex = reader.ReadInt32();
            Name = reader.ReadUtf8Z(0x40);
            reader.BaseStream.Position = start + 0x44;

            Field44 = reader.ReadInt32();
            Offset = reader.ReadInt32();
            Size = reader.ReadInt64();
            Field54 = reader.ReadInt64();
            NextIndex = reader.ReadInt32();
        }

        public static void ResolveFilenames(FileEntry[] entries)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            var delimiter = "/";
            foreach (var file in entries)
            {
                list.Add(file.Name);
                var dir = file.ParentDir;
                while (dir != null)
                {
                    list.Add(delimiter);
                    list.Add(dir.Name);
                    dir = dir.ParentDir;
                }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    sb.Append(list[i]);
                }

                file.FullPath = sb.ToString();
                list.Clear();
                sb.Clear();
            }
        }
    }
}
