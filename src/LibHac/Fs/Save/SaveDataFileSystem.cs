using System;
using System.Collections.Generic;
using System.IO;

namespace LibHac.Fs.Save
{
    public class SaveDataFileSystem : IFileSystem
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

        public void CreateDirectory(string path)
        {
            try
            {
                SaveDataFileSystemCore.CreateDirectory(path);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public void CreateFile(string path, long size, CreateFileOptions options)
        {
            try
            {
                SaveDataFileSystemCore.CreateFile(path, size, options);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public void DeleteDirectory(string path)
        {
            try
            {
                SaveDataFileSystemCore.DeleteDirectory(path);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public void DeleteDirectoryRecursively(string path)
        {
            try
            {
                SaveDataFileSystemCore.DeleteDirectoryRecursively(path);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public void CleanDirectoryRecursively(string path)
        {
            try
            {
                SaveDataFileSystemCore.CleanDirectoryRecursively(path);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public void DeleteFile(string path)
        {
            try
            {
                SaveDataFileSystemCore.DeleteFile(path);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public IDirectory OpenDirectory(string path, OpenDirectoryMode mode)
        {
            try
            {
                return SaveDataFileSystemCore.OpenDirectory(path, mode);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public IFile OpenFile(string path, OpenMode mode)
        {
            try
            {
                return SaveDataFileSystemCore.OpenFile(path, mode);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public void RenameDirectory(string srcPath, string dstPath)
        {
            try
            {
                SaveDataFileSystemCore.RenameDirectory(srcPath, dstPath);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public void RenameFile(string srcPath, string dstPath)
        {
            try
            {
                SaveDataFileSystemCore.RenameFile(srcPath, dstPath);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public DirectoryEntryType GetEntryType(string path)
        {
            try
            {
                return SaveDataFileSystemCore.GetEntryType(path);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public long GetFreeSpaceSize(string path)
        {
            try
            {
                return SaveDataFileSystemCore.GetFreeSpaceSize(path);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public long GetTotalSpaceSize(string path)
        {
            try
            {
                return SaveDataFileSystemCore.GetTotalSpaceSize(path);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public void Commit()
        {
            try
            {
                Commit(Keyset);
            }
            catch (HorizonResultException ex)
            {
                ConvertResultException(ex);
                throw;
            }
        }

        public FileTimeStampRaw GetFileTimeStampRaw(string path)
        {
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);
            return default;
        }

        public void QueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, string path, QueryId queryId) =>
            ThrowHelper.ThrowResult(ResultFs.NotImplemented);

        public bool Commit(Keyset keyset)
        {
            CoreDataIvfcStorage.Flush();
            FatIvfcStorage?.Flush();

            Stream headerStream = BaseStorage.AsStream();

            var hashData = new byte[0x3d00];

            headerStream.Position = 0x300;
            headerStream.Read(hashData, 0, hashData.Length);

            byte[] hash = Crypto.ComputeSha256(hashData, 0, hashData.Length);
            headerStream.Position = 0x108;
            headerStream.Write(hash, 0, hash.Length);

            if (keyset == null || keyset.SaveMacKey.IsEmpty()) return false;

            var cmacData = new byte[0x200];
            var cmac = new byte[0x10];

            headerStream.Position = 0x100;
            headerStream.Read(cmacData, 0, 0x200);

            Crypto.CalculateAesCmac(keyset.SaveMacKey, cmacData, 0, cmac, 0, 0x200);

            headerStream.Position = 0;
            headerStream.Write(cmac, 0, 0x10);
            headerStream.Flush();

            return true;
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

        private void ConvertResultException(HorizonResultException ex)
        {
            ex.ResultValue = SaveResults.ConvertToExternalResult(ex.ResultValue);
        }
    }
}
