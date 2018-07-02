using System.IO;

namespace libhac
{
    public class Nacp
    {
        public NacpLang[] Languages { get; } = new NacpLang[0x10];
        public string Version { get; }
        public ulong AddOnContentBaseId { get; }
        public ulong SaveDataOwnerId { get; }
        public long UserAccountSaveDataSize { get; }
        public long UserAccountSaveDataJournalSize { get; }
        public long DeviceSaveDataSize { get; }
        public long DeviceSaveDataJournalSize { get; }
        public long BcatSaveDataSize { get; }

        public long TotalSaveDataSize { get; }
        public long UserTotalSaveDataSize { get; }
        public long DeviceTotalSaveDataSize { get; }

        public Nacp(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;

            for (int i = 0; i < 16; i++)
            {
                Languages[i] = new NacpLang(reader);
            }

            reader.BaseStream.Position = start + 0x3060;
            Version = reader.ReadUtf8Z();
            reader.BaseStream.Position = start + 0x3070;
            AddOnContentBaseId = reader.ReadUInt64();
            SaveDataOwnerId = reader.ReadUInt64();
            UserAccountSaveDataSize = reader.ReadInt64();
            UserAccountSaveDataJournalSize = reader.ReadInt64();
            DeviceSaveDataSize = reader.ReadInt64();
            DeviceSaveDataJournalSize = reader.ReadInt64();
            BcatSaveDataSize = reader.ReadInt64();

            UserTotalSaveDataSize = UserAccountSaveDataSize + UserAccountSaveDataJournalSize;
            DeviceTotalSaveDataSize = DeviceSaveDataSize + DeviceSaveDataJournalSize;
            TotalSaveDataSize = UserTotalSaveDataSize + DeviceTotalSaveDataSize;
        }
    }

    public class NacpLang
    {
        public string Title { get; }
        public string Developer { get; }

        public NacpLang(BinaryReader reader)
        {
            var start = reader.BaseStream.Position;
            Title = reader.ReadUtf8Z();
            reader.BaseStream.Position = start + 0x200;
            Developer = reader.ReadUtf8Z();
            reader.BaseStream.Position = start + 0x300;
        }
    }
}
