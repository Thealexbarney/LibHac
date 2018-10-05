using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace LibHac
{
    public abstract class RomfsEntry
    {
        public int Offset { get; set; }
        public int ParentDirOffset { get; protected set; }
        public int NameLength { get; protected set; }
        public string Name { get; protected set; }

        public RomfsDir ParentDir { get; internal set; }
        public string FullPath { get; private set; }

        internal static void ResolveFilenames(IEnumerable<RomfsEntry> entries)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            const string delimiter = "/";
            foreach (RomfsEntry file in entries)
            {
                list.Add(file.Name);
                RomfsDir dir = file.ParentDir;
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

    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class RomfsDir : RomfsEntry
    {
        public int NextSiblingOffset { get; }
        public int FirstChildOffset { get; }
        public int FirstFileOffset { get; }
        public int NextDirHashOffset { get; }

        public RomfsDir NextSibling { get; internal set; }
        public RomfsDir FirstChild { get; internal set; }
        public RomfsFile FirstFile { get; internal set; }
        public RomfsDir NextDirHash { get; internal set; }

        public RomfsDir(BinaryReader reader)
        {
            ParentDirOffset = reader.ReadInt32();
            NextSiblingOffset = reader.ReadInt32();
            FirstChildOffset = reader.ReadInt32();
            FirstFileOffset = reader.ReadInt32();
            NextDirHashOffset = reader.ReadInt32();
            NameLength = reader.ReadInt32();
            Name = reader.ReadUtf8(NameLength);
            reader.BaseStream.Position = Util.GetNextMultiple(reader.BaseStream.Position, 4);
        }
    }

    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public class RomfsFile : RomfsEntry
    {
        public int NextSiblingOffset { get; }
        public long DataOffset { get; }
        public long DataLength { get; }
        public int NextFileHashOffset { get; }

        public RomfsFile NextSibling { get; internal set; }
        public RomfsFile NextFileHash { get; internal set; }

        public RomfsFile(BinaryReader reader)
        {
            ParentDirOffset = reader.ReadInt32();
            NextSiblingOffset = reader.ReadInt32();
            DataOffset = reader.ReadInt64();
            DataLength = reader.ReadInt64();
            NextFileHashOffset = reader.ReadInt32();
            NameLength = reader.ReadInt32();
            Name = reader.ReadUtf8(NameLength);
            reader.BaseStream.Position = Util.GetNextMultiple(reader.BaseStream.Position, 4);
        }
    }
}
