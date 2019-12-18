using System.Collections.Generic;
using System.IO;
using LibHac.Crypto;
using LibHac.Fs;

namespace LibHac.FsSystem.Save
{
    public class SaveDataFileSystem : FileSystemBase
    {
        internal const byte TrimFillValue = 0;

        public Header Header { get; }
        private bool IsFirstHeaderInUse { get; }

        public IStorage BaseStorage { get; }
        public bool LeaveOpen { get; }

        public SaveDataFileSystemCore SaveDataFileSystemCore { get; }

        public RemapStorage DataRemapStorage { get; }
        public RemapStorage MetaRemapStorage { get; }

        public HierarchicalDuplexStorage DuplexStorage { get; }
        public JournalStorage JournalStorage { get; }

        public HierarchicalIntegrityVerificationStorage CoreDataIvfcStorage { get; }
        public HierarchicalIntegrityVerificationStorage FatIvfcStorage { get; }

        private Keyset Keyset { get; }

        public SaveDataFileSystem(Keyset keyset, IStorage storage, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
        {
            BaseStorage = storage;
            LeaveOpen = leaveOpen;
            Keyset = keyset;

            var headerA = new Header(BaseStorage, keyset);
            var headerB = new Header(BaseStorage.Slice(0x4000), keyset);

            if (headerA.HeaderHashValidity == Validity.Valid)
            {
                IsFirstHeaderInUse = true;
            }
            else if (headerB.HeaderHashValidity == Validity.Valid)
            {
                IsFirstHeaderInUse = false;
            }
            else
            {
                ThrowHelper.ThrowResult(ResultFs.InvalidSaveDataHeader, "Savedata header is not valid.");
            }

            Header = IsFirstHeaderInUse ? headerA : headerB;

            FsLayout layout = Header.Layout;

            IStorage dataRemapBase = BaseStorage.Slice(layout.FileMapDataOffset, layout.FileMapDataSize);
            IStorage dataRemapEntries = BaseStorage.Slice(layout.FileMapEntryOffset, layout.FileMapEntrySize);
            IStorage metadataRemapEntries = BaseStorage.Slice(layout.MetaMapEntryOffset, layout.MetaMapEntrySize);

            DataRemapStorage = new RemapStorage(dataRemapBase, Header.MainRemapHeader, dataRemapEntries, leaveOpen);

            DuplexStorage = InitDuplexStorage(DataRemapStorage, Header);

            MetaRemapStorage = new RemapStorage(DuplexStorage, Header.MetaDataRemapHeader, metadataRemapEntries, leaveOpen);

            var journalMapInfo = new JournalMapParams
            {
                MapStorage = MetaRemapStorage.Slice(layout.JournalMapTableOffset, layout.JournalMapTableSize),
                PhysicalBlockBitmap = MetaRemapStorage.Slice(layout.JournalPhysicalBitmapOffset, layout.JournalPhysicalBitmapSize),
                VirtualBlockBitmap = MetaRemapStorage.Slice(layout.JournalVirtualBitmapOffset, layout.JournalVirtualBitmapSize),
                FreeBlockBitmap = MetaRemapStorage.Slice(layout.JournalFreeBitmapOffset, layout.JournalFreeBitmapSize),
            };

            IStorage journalData = DataRemapStorage.Slice(layout.JournalDataOffset,
                layout.JournalDataSizeB + layout.JournalSize);

            JournalStorage = new JournalStorage(journalData, Header.JournalHeader, journalMapInfo, leaveOpen);

            CoreDataIvfcStorage = InitJournalIvfcStorage(integrityCheckLevel);

            IStorage fatStorage = MetaRemapStorage.Slice(layout.FatOffset, layout.FatSize);

            if (Header.Layout.Version >= 0x50000)
            {
                FatIvfcStorage = InitFatIvfcStorage(integrityCheckLevel);
                fatStorage = FatIvfcStorage;
            }

            SaveDataFileSystemCore = new SaveDataFileSystemCore(CoreDataIvfcStorage, fatStorage, Header.SaveHeader);
        }

        private static HierarchicalDuplexStorage InitDuplexStorage(IStorage baseStorage, Header header)
        {
            FsLayout layout = header.Layout;
            var duplexLayers = new DuplexFsLayerInfo[3];

            duplexLayers[0] = new DuplexFsLayerInfo
            {
                DataA = header.DuplexMasterBitmapA,
                DataB = header.DuplexMasterBitmapB,
                Info = header.Duplex.Layers[0]
            };

            duplexLayers[1] = new DuplexFsLayerInfo
            {
                DataA = baseStorage.Slice(layout.DuplexL1OffsetA, layout.DuplexL1Size),
                DataB = baseStorage.Slice(layout.DuplexL1OffsetB, layout.DuplexL1Size),
                Info = header.Duplex.Layers[1]
            };

            duplexLayers[2] = new DuplexFsLayerInfo
            {
                DataA = baseStorage.Slice(layout.DuplexDataOffsetA, layout.DuplexDataSize),
                DataB = baseStorage.Slice(layout.DuplexDataOffsetB, layout.DuplexDataSize),
                Info = header.Duplex.Layers[2]
            };

            return new HierarchicalDuplexStorage(duplexLayers, layout.DuplexIndex == 1);
        }

        private HierarchicalIntegrityVerificationStorage InitJournalIvfcStorage(IntegrityCheckLevel integrityCheckLevel)
        {
            const int ivfcLevels = 5;
            IvfcHeader ivfc = Header.Ivfc;
            var levels = new List<IStorage> { Header.DataIvfcMaster };

            for (int i = 0; i < ivfcLevels - 2; i++)
            {
                IvfcLevelHeader level = ivfc.LevelHeaders[i];
                levels.Add(MetaRemapStorage.Slice(level.Offset, level.Size));
            }

            IvfcLevelHeader dataLevel = ivfc.LevelHeaders[ivfcLevels - 2];
            levels.Add(JournalStorage.Slice(dataLevel.Offset, dataLevel.Size));

            return new HierarchicalIntegrityVerificationStorage(ivfc, levels, IntegrityStorageType.Save, integrityCheckLevel, LeaveOpen);
        }

        private HierarchicalIntegrityVerificationStorage InitFatIvfcStorage(IntegrityCheckLevel integrityCheckLevel)
        {
            return new HierarchicalIntegrityVerificationStorage(Header.FatIvfc, Header.FatIvfcMaster, MetaRemapStorage,
                IntegrityStorageType.Save, integrityCheckLevel, LeaveOpen);
        }

        protected override Result CreateDirectoryImpl(string path)
        {
            Result result = SaveDataFileSystemCore.CreateDirectory(path);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result CreateFileImpl(string path, long size, CreateFileOptions options)
        {
            Result result = SaveDataFileSystemCore.CreateFile(path, size, options);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result DeleteDirectoryImpl(string path)
        {
            Result result = SaveDataFileSystemCore.DeleteDirectory(path);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result DeleteDirectoryRecursivelyImpl(string path)
        {
            Result result = SaveDataFileSystemCore.DeleteDirectoryRecursively(path);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result CleanDirectoryRecursivelyImpl(string path)
        {
            Result result = SaveDataFileSystemCore.CleanDirectoryRecursively(path);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result DeleteFileImpl(string path)
        {
            Result result = SaveDataFileSystemCore.DeleteFile(path);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result OpenDirectoryImpl(out IDirectory directory, string path, OpenDirectoryMode mode)
        {
            Result result = SaveDataFileSystemCore.OpenDirectory(out directory, path, mode);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result OpenFileImpl(out IFile file, string path, OpenMode mode)
        {
            Result result = SaveDataFileSystemCore.OpenFile(out file, path, mode);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result RenameDirectoryImpl(string oldPath, string newPath)
        {
            Result result = SaveDataFileSystemCore.RenameDirectory(oldPath, newPath);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result RenameFileImpl(string oldPath, string newPath)
        {
            Result result = SaveDataFileSystemCore.RenameFile(oldPath, newPath);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result GetEntryTypeImpl(out DirectoryEntryType entryType, string path)
        {
            Result result = SaveDataFileSystemCore.GetEntryType(out entryType, path);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result GetFreeSpaceSizeImpl(out long freeSpace, string path)
        {
            Result result = SaveDataFileSystemCore.GetFreeSpaceSize(out freeSpace, path);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result GetTotalSpaceSizeImpl(out long totalSpace, string path)
        {
            Result result = SaveDataFileSystemCore.GetTotalSpaceSize(out totalSpace, path);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        protected override Result CommitImpl()
        {
            Result result = Commit(Keyset);

            return SaveResults.ConvertToExternalResult(result).LogConverted(result);
        }

        public Result Commit(Keyset keyset)
        {
            CoreDataIvfcStorage.Flush();
            FatIvfcStorage?.Flush();

            Stream headerStream = BaseStorage.AsStream();

            var hashData = new byte[0x3d00];

            headerStream.Position = 0x300;
            headerStream.Read(hashData, 0, hashData.Length);

            var hash = new byte[Sha256.DigestSize];
            Sha256.GenerateSha256Hash(hashData, hash);

            headerStream.Position = 0x108;
            headerStream.Write(hash, 0, hash.Length);

            if (keyset == null || keyset.SaveMacKey.IsEmpty()) return ResultFs.PreconditionViolation.Log();

            var cmacData = new byte[0x200];
            var cmac = new byte[0x10];

            headerStream.Position = 0x100;
            headerStream.Read(cmacData, 0, 0x200);

            CryptoOld.CalculateAesCmac(keyset.SaveMacKey, cmacData, 0, cmac, 0, 0x200);

            headerStream.Position = 0;
            headerStream.Write(cmac, 0, 0x10);
            headerStream.Flush();

            return Result.Success;
        }

        public void FsTrim()
        {
            MetaRemapStorage.FsTrim();
            DataRemapStorage.FsTrim();
            DuplexStorage.FsTrim();
            JournalStorage.FsTrim();
            CoreDataIvfcStorage.FsTrim();
            FatIvfcStorage?.FsTrim();
            SaveDataFileSystemCore.FsTrim();

            int unusedHeaderOffset = IsFirstHeaderInUse ? 0x4000 : 0;
            BaseStorage.Slice(unusedHeaderOffset, 0x4000).Fill(TrimFillValue);
        }

        public Validity Verify(IProgressReport logger = null)
        {
            Validity journalValidity = CoreDataIvfcStorage.Validate(true, logger);
            CoreDataIvfcStorage.SetLevelValidities(Header.Ivfc);

            if (FatIvfcStorage == null) return journalValidity;

            Validity fatValidity = FatIvfcStorage.Validate(true, logger);
            FatIvfcStorage.SetLevelValidities(Header.Ivfc);

            if (journalValidity != Validity.Valid) return journalValidity;
            if (fatValidity != Validity.Valid) return fatValidity;

            return journalValidity;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!LeaveOpen)
                {
                    BaseStorage?.Dispose();
                }
            }
        }
    }
}
