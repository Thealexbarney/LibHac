using System.Collections.Generic;
using System.IO;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Path = LibHac.Fs.Path;

namespace LibHac.FsSystem.Save;

public class SaveDataFileSystem : IFileSystem
{
    internal const byte TrimFillValue = 0;

    public Header Header { get; }
    private bool IsFirstHeaderInUse { get; }

    private SharedRef<IStorage> _baseStorageShared;
    public IStorage BaseStorage { get; private set; }
    public bool LeaveOpen { get; }

    public SaveDataFileSystemCore SaveDataFileSystemCore { get; }

    public RemapStorage DataRemapStorage { get; }
    public RemapStorage MetaRemapStorage { get; }

    public HierarchicalDuplexStorage DuplexStorage { get; }
    public JournalStorage JournalStorage { get; }

    public HierarchicalIntegrityVerificationStorage CoreDataIvfcStorage { get; }
    public HierarchicalIntegrityVerificationStorage FatIvfcStorage { get; }

    private KeySet KeySet { get; }

    public SaveDataFileSystem(KeySet keySet, ref SharedRef<IStorage> storage,
        IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
        : this(keySet, storage.Get, integrityCheckLevel, true)
    {
        _baseStorageShared = SharedRef<IStorage>.CreateMove(ref storage);
    }

    public SaveDataFileSystem(KeySet keySet, IStorage storage, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
    {
        BaseStorage = storage;
        LeaveOpen = leaveOpen;
        KeySet = keySet;

        var headerA = new Header(BaseStorage, keySet);
        var headerB = new Header(BaseStorage.Slice(0x4000), keySet);

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
            ThrowHelper.ThrowResult(ResultFs.JournalIntegritySaveDataControlAreaVerificationFailed.Value, "Savedata header is not valid.");
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
            FreeBlockBitmap = MetaRemapStorage.Slice(layout.JournalFreeBitmapOffset, layout.JournalFreeBitmapSize)
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

    protected override Result DoCreateDirectory(in Path path)
    {
        Result result = SaveDataFileSystemCore.CreateDirectory(in path);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        Result result = SaveDataFileSystemCore.CreateFile(in path, size, option);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        Result result = SaveDataFileSystemCore.DeleteDirectory(in path);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        Result result = SaveDataFileSystemCore.DeleteDirectoryRecursively(in path);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        Result result = SaveDataFileSystemCore.CleanDirectoryRecursively(in path);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoDeleteFile(in Path path)
    {
        Result result = SaveDataFileSystemCore.DeleteFile(in path);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        Result result = SaveDataFileSystemCore.OpenDirectory(ref outDirectory, in path, mode);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        Result result = SaveDataFileSystemCore.OpenFile(ref outFile, in path, mode);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        Result result = SaveDataFileSystemCore.RenameDirectory(in currentPath, in newPath);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        Result result = SaveDataFileSystemCore.RenameFile(in currentPath, in newPath);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        Result result = SaveDataFileSystemCore.GetEntryType(out entryType, in path);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        Result result = SaveDataFileSystemCore.GetFreeSpaceSize(out freeSpace, in path);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        Result result = SaveDataFileSystemCore.GetTotalSpaceSize(out totalSpace, in path);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    protected override Result DoCommit()
    {
        Result result = Commit(KeySet);

        return SaveResults.ConvertToExternalResult(result).LogConverted(result);
    }

    public Result Commit(KeySet keySet)
    {
        CoreDataIvfcStorage.Flush();
        FatIvfcStorage?.Flush();

        Stream headerStream = BaseStorage.AsStream();

        byte[] hashData = new byte[0x3d00];

        headerStream.Position = 0x300;
        headerStream.Read(hashData, 0, hashData.Length);

        byte[] hash = new byte[Sha256.DigestSize];
        Sha256.GenerateSha256Hash(hashData, hash);

        headerStream.Position = 0x108;
        headerStream.Write(hash, 0, hash.Length);

        if (keySet == null || keySet.DeviceUniqueSaveMacKeys[0].IsZeros()) return ResultFs.PreconditionViolation.Log();

        byte[] cmacData = new byte[0x200];
        byte[] cmac = new byte[0x10];

        headerStream.Position = 0x100;
        headerStream.Read(cmacData, 0, 0x200);

        Aes.CalculateCmac(cmac, cmacData, keySet.DeviceUniqueSaveMacKeys[0]);

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

    public override void Dispose()
    {
        if (!LeaveOpen)
        {
            BaseStorage?.Dispose();
            BaseStorage = null;
        }

        _baseStorageShared.Destroy();

        base.Dispose();
    }
}
