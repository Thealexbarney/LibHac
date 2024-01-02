// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.FsSrv.Sf;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

public partial class SaveDataTransferManagerForSaveDataRepair
{
    private SharedRef<ISaveDataTransferManagerForSaveDataRepair> _baseInterface;

    // LibHac addition
    private FileSystemClient _fsClient;

    public struct KeyPackageV0
    {
        public Array640<byte> Data;
    }

    public SaveDataTransferManagerForSaveDataRepair(FileSystemClient fs)
    {
        throw new NotImplementedException();
    }

    public Result GetChallenge(out SaveDataTransferManagerVersion2.Challenge outChallenge)
    {
        throw new NotImplementedException();
    }

    public Result SetKeyPackage(in KeyPackageV0 keyPackage)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporterAndGetEncryptedKey(ref UniqueRef<ISaveDataDivisionExporter> outExporter,
        out RsaEncryptedKey outEncryptedKey, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result PrepareOpenSaveDataImporter(out RsaEncryptedKey outEncryptedKey)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialDataBeforeRepair, in InitialDataVersion2 initialDataAfterRepair, in UserId userId,
        SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialData, in UserId userId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenDeviceSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialDataBeforeRepair, in InitialDataVersion2 initialDataAfterRepair,
        SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenDeviceSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialDataBeforeRepair, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporterWithKey(ref UniqueRef<ISaveDataDivisionExporter> outExporter, in AesKey key,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporterWithKey(ref UniqueRef<ISaveDataDivisionExporter> outExporter, in AesKey key,
        in InitialDataVersion2 initialData, in UserId userId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }
}