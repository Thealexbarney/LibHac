using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace LibHac.IO.Save
{
    [DebuggerDisplay("{" + nameof(FullPath) + "}")]
    public abstract class SaveFsEntry
    {
        public int ParentDirIndex { get; protected set; }
        public string Name { get; protected set; }

        public string FullPath { get; private set; }
        public SaveDirectoryEntry ParentDir { get; internal set; }

        internal static void ResolveFilenames(IEnumerable<SaveFsEntry> entries)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            string delimiter = "/";
            foreach (SaveFsEntry file in entries)
            {
                list.Add(file.Name);
                SaveDirectoryEntry dir = file.ParentDir;
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

                file.FullPath = sb.Length == 0 ? delimiter : sb.ToString();
                list.Clear();
                sb.Clear();
            }
        }
    }

    public class SaveFileEntry : SaveFsEntry
    {
        public int NextSiblingIndex { get; }
        public int BlockIndex { get; }
        public long FileSize { get; }
        public long Field54 { get; }
        public int NextInChainIndex { get; }

        public SaveFileEntry NextSibling { get; internal set; }
        public SaveFileEntry NextInChain { get; internal set; }

        public SaveFileEntry(BinaryReader reader)
        {
            long start = reader.BaseStream.Position;
            ParentDirIndex = reader.ReadInt32();
            Name = reader.ReadUtf8Z(0x40);
            reader.BaseStream.Position = start + 0x44;

            NextSiblingIndex = reader.ReadInt32();
            BlockIndex = reader.ReadInt32();
            FileSize = reader.ReadInt64();
            Field54 = reader.ReadInt64();
            NextInChainIndex = reader.ReadInt32();
        }
    }

    public class SaveDirectoryEntry : SaveFsEntry
    {
        public int NextSiblingIndex { get; }
        public int FirstChildIndex { get; }
        public long FirstFileIndex { get; }
        public long Field54 { get; }
        public int NextInChainIndex { get; }

        public SaveDirectoryEntry NextSibling { get; internal set; }
        public SaveDirectoryEntry FirstChild { get; internal set; }
        public SaveFileEntry FirstFile { get; internal set; }
        public SaveDirectoryEntry NextInChain { get; internal set; }

        public SaveDirectoryEntry(BinaryReader reader)
        {
            long start = reader.BaseStream.Position;
            ParentDirIndex = reader.ReadInt32();
            Name = reader.ReadUtf8Z(0x40);
            reader.BaseStream.Position = start + 0x44;

            NextSiblingIndex = reader.ReadInt32();
            FirstChildIndex = reader.ReadInt32();
            FirstFileIndex = reader.ReadInt64();
            Field54 = reader.ReadInt64();
            NextInChainIndex = reader.ReadInt32();
        }
    }
}
