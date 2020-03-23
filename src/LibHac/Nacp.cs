using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac
{
    [Obsolete("This class has been deprecated. LibHac.Ns.ApplicationControlProperty should be used instead.")]
    public class Nacp
    {
        public NacpDescription[] Descriptions { get; } = new NacpDescription[0x10];
        public string Isbn { get; }
        public byte StartupUserAccount { get; }
        public byte UserAccountSwitchLock { get; }
        public byte AocRegistrationType { get; }
        public int AttributeFlag { get; }
        public uint SupportedLanguageFlag { get; }
        public uint ParentalControlFlag { get; }
        public byte Screenshot { get; }
        public byte VideoCapture { get; }
        public byte DataLossConfirmation { get; }
        public byte PlayLogPolicy { get; }
        public ulong PresenceGroupId { get; }
        public sbyte[] RatingAge { get; } = new sbyte[32];
        public string DisplayVersion { get; }
        public ulong AddOnContentBaseId { get; }
        public ulong SaveDataOwnerId { get; }
        public long UserAccountSaveDataSize { get; }
        public long UserAccountSaveDataJournalSize { get; }
        public long DeviceSaveDataSize { get; }
        public long DeviceSaveDataJournalSize { get; }
        public long BcatDeliveryCacheStorageSize { get; }
        public string ApplicationErrorCodeCategory { get; }
        public ulong[] LocalCommunicationId { get; } = new ulong[8];
        public byte LogoType { get; }
        public byte LogoHandling { get; }
        public byte RuntimeAddOnContentInstall { get; }
        public byte[] Reserved00 { get; }
        public byte CrashReport { get; }
        public byte Hdcp { get; }
        public ulong SeedForPseudoDeviceId { get; }
        public string BcatPassphrase { get; }
        public byte Reserved01 { get; }
        public byte[] Reserved02 { get; }
        public long UserAccountSaveDataSizeMax { get; }
        public long UserAccountSaveDataJournalSizeMax { get; }
        public long DeviceSaveDataSizeMax { get; }
        public long DeviceSaveDataJournalSizeMax { get; }
        public long TemporaryStorageSize { get; }
        public long CacheStorageSize { get; }
        public long CacheStorageJournalSize { get; }
        public long CacheStorageDataAndJournalSizeMax { get; }
        public short CacheStorageIndex { get; }
        public byte[] Reserved03 { get; }
        public List<ulong> PlayLogQueryableApplicationId { get; } = new List<ulong>();
        public byte PlayLogQueryCapability { get; }
        public byte RepairFlag { get; }
        public byte ProgramIndex { get; }

        public long TotalSaveDataSize { get; }
        public long UserTotalSaveDataSize { get; }
        public long DeviceTotalSaveDataSize { get; }

        public Nacp() { }

        public Nacp(Stream file)
        {
            long start = file.Position;

            var reader = new BinaryReader(file);

            for (int i = 0; i < 16; i++)
            {
                Descriptions[i] = new NacpDescription(reader, i);
            }

            Isbn = reader.ReadUtf8Z(37);
            reader.BaseStream.Position = start + 0x3025;
            StartupUserAccount = reader.ReadByte();
            UserAccountSwitchLock = reader.ReadByte();
            AocRegistrationType = reader.ReadByte();
            AttributeFlag = reader.ReadInt32();
            SupportedLanguageFlag = reader.ReadUInt32();
            ParentalControlFlag = reader.ReadUInt32();
            Screenshot = reader.ReadByte();
            VideoCapture = reader.ReadByte();
            DataLossConfirmation = reader.ReadByte();
            PlayLogPolicy = reader.ReadByte();
            PresenceGroupId = reader.ReadUInt64();

            for (int i = 0; i < RatingAge.Length; i++)
            {
                RatingAge[i] = reader.ReadSByte();
            }

            DisplayVersion = reader.ReadUtf8Z(16);
            reader.BaseStream.Position = start + 0x3070;
            AddOnContentBaseId = reader.ReadUInt64();
            SaveDataOwnerId = reader.ReadUInt64();
            UserAccountSaveDataSize = reader.ReadInt64();
            UserAccountSaveDataJournalSize = reader.ReadInt64();
            DeviceSaveDataSize = reader.ReadInt64();
            DeviceSaveDataJournalSize = reader.ReadInt64();
            BcatDeliveryCacheStorageSize = reader.ReadInt64();
            ApplicationErrorCodeCategory = reader.ReadUtf8Z(8);
            reader.BaseStream.Position = start + 0x30B0;

            for (int i = 0; i < LocalCommunicationId.Length; i++)
            {
                LocalCommunicationId[i] = reader.ReadUInt64();
            }

            LogoType = reader.ReadByte();
            LogoHandling = reader.ReadByte();
            RuntimeAddOnContentInstall = reader.ReadByte();
            Reserved00 = reader.ReadBytes(3);
            CrashReport = reader.ReadByte();
            Hdcp = reader.ReadByte();
            SeedForPseudoDeviceId = reader.ReadUInt64();
            BcatPassphrase = reader.ReadUtf8Z(65);

            reader.BaseStream.Position = start + 0x3141;
            Reserved01 = reader.ReadByte();
            Reserved02 = reader.ReadBytes(6);

            UserAccountSaveDataSizeMax = reader.ReadInt64();
            UserAccountSaveDataJournalSizeMax = reader.ReadInt64();
            DeviceSaveDataSizeMax = reader.ReadInt64();
            DeviceSaveDataJournalSizeMax = reader.ReadInt64();
            TemporaryStorageSize = reader.ReadInt64();
            CacheStorageSize = reader.ReadInt64();
            CacheStorageJournalSize = reader.ReadInt64();
            CacheStorageDataAndJournalSizeMax = reader.ReadInt64();
            CacheStorageIndex = reader.ReadInt16();
            Reserved03 = reader.ReadBytes(6);

            for (int i = 0; i < 16; i++)
            {
                ulong value = reader.ReadUInt64();
                if (value != 0) PlayLogQueryableApplicationId.Add(value);
            }

            PlayLogQueryCapability = reader.ReadByte();
            RepairFlag = reader.ReadByte();
            ProgramIndex = reader.ReadByte();

            UserTotalSaveDataSize = UserAccountSaveDataSize + UserAccountSaveDataJournalSize;
            DeviceTotalSaveDataSize = DeviceSaveDataSize + DeviceSaveDataJournalSize;
            TotalSaveDataSize = UserTotalSaveDataSize + DeviceTotalSaveDataSize;
        }
    }

    public class NacpDescription
    {
        public string Title { get; }
        public string Developer { get; }

        public TitleLanguage Language;

        public NacpDescription() { }

        public NacpDescription(BinaryReader reader, int index)
        {
            Language = (TitleLanguage)index;
            long start = reader.BaseStream.Position;
            Title = reader.ReadUtf8Z();
            reader.BaseStream.Position = start + 0x200;
            Developer = reader.ReadUtf8Z();
            reader.BaseStream.Position = start + 0x300;
        }
    }

    public enum TitleLanguage
    {
        AmericanEnglish = 0,
        BritishEnglish,
        Japanese,
        French,
        German,
        LatinAmericanSpanish,
        Spanish,
        Italian,
        Dutch,
        CanadianFrench,
        Portuguese,
        Russian,
        Korean,
        Taiwanese,
        Chinese
    }
}
