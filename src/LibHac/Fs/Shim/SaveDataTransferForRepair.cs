// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

public class SaveDataTransferManagerForRepair
{
    private SharedRef<ISaveDataTransferManagerForRepair> _baseInterface;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataTransferManagerForRepair(FileSystemClient fs)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporter(ref UniqueRef<ISaveDataDivisionExporter> outExporter, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporter(ref UniqueRef<ISaveDataDivisionImporter> outImporter,
        in InitialDataVersion2 initialData, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }

    public Result GetSaveDataAttribute(out SaveDataAttribute outAttribute, in InitialDataVersion2 initialData)
    {
        throw new NotImplementedException();
    }
}