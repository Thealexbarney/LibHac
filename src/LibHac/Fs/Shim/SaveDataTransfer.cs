// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

file static class Anonymous
{
    public static Result QuerySaveDataExportSizeImpl(out long outExportSize, in SaveDataInfo saveDataInfo)
    {
        throw new NotImplementedException();
    }
}

public partial class SaveDataTransferManager
{
    private SharedRef<ISaveDataTransferManager> _baseInterface;

    // LibHac addition
    private FileSystemClient _fsClient;

    public SaveDataTransferManager()
    {
        throw new NotImplementedException();
    }

    public Result GetChallenge(Span<byte> outChallengeBuffer)
    {
        throw new NotImplementedException();
    }

    public Result SetToken(ReadOnlySpan<byte> token)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataExporter(ref UniqueRef<SaveDataExporter> outExporter, SaveDataSpaceId spaceId,
        ulong saveDataId)
    {
        throw new NotImplementedException();
    }

    public Result OpenSaveDataImporter(ref UniqueRef<SaveDataImporter> outImporter, out long outRequiredSize,
        ReadOnlySpan<byte> initialData, in UserId userId, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }
}

public static class SaveDataTransferSizeCalculator
{
    public static Result QuerySaveDataExportSize(out long outExportSize, in SaveDataInfo saveDataInfo)
    {
        throw new NotImplementedException();
    }

    public static long QuerySaveDataRequiredSizeForImport(in SaveDataInfo saveDataInfo)
    {
        throw new NotImplementedException();
    }

    public static Result QuerySaveDataExportSize(out long outExportSize, ReadOnlySpan<SaveDataInfo> saveDataInfo)
    {
        throw new NotImplementedException();
    }

    public static long QuerySaveDataRequiredSizeForImport(ReadOnlySpan<SaveDataInfo> saveDataInfo)
    {
        throw new NotImplementedException();
    }

    public static Result GetFreeSpaceSize(FileSystemClient fs, out long outSize, SaveDataSpaceId spaceId)
    {
        throw new NotImplementedException();
    }
}

public class SaveDataExporter : IDisposable
{
    private SharedRef<ISaveDataExporter> _baseInterface;
    private SaveDataInfo _saveDataInfo;

    internal SaveDataExporter(ref readonly SharedRef<ISaveDataExporter> exporter)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ref readonly SaveDataInfo GetSaveDataInfo()
    {
        throw new NotImplementedException();
    }

    public Result PullInitialData(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public Result Pull(out ulong outPulledSize, Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public long GetRestSize()
    {
        throw new NotImplementedException();
    }
}

public class SaveDataImporter : IDisposable
{
    private SharedRef<ISaveDataImporter> _baseInterface;
    private SaveDataInfo _saveDataInfo;

    internal SaveDataImporter(ref readonly SharedRef<ISaveDataImporter> importer)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public ref readonly SaveDataInfo GetSaveDataInfo()
    {
        throw new NotImplementedException();
    }

    public Result Push(ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public ulong GetRestSize()
    {
        throw new NotImplementedException();
    }
}