using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;

using static LibHac.Fs.Impl.AccessLogStrings;
using static LibHac.Fs.SaveData;

namespace LibHac.Fs.Shim;

public static class DeviceSaveData
{
    private class DeviceSaveDataAttributeGetter : ISaveDataAttributeGetter
    {
        private ProgramId _programId;

        public DeviceSaveDataAttributeGetter(ProgramId programId)
        {
            _programId = programId;
        }

        public void Dispose() { }

        public Result GetSaveDataAttribute(out SaveDataAttribute attribute)
        {
            Result rc = SaveDataAttribute.Make(out attribute, _programId, SaveDataType.Device, InvalidUserId,
                InvalidSystemSaveDataId);
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }
    }

    private const SaveDataSpaceId DeviceSaveDataSpaceId = SaveDataSpaceId.User;

    private static Result MountDeviceSaveDataImpl(this FileSystemClientImpl fs, U8Span mountName,
        in SaveDataAttribute attribute)
    {
        Result rc = fs.CheckMountName(mountName);
        if (rc.IsFailure()) return rc;

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
        using var fileSystem = new SharedRef<IFileSystemSf>();

        rc = fileSystemProxy.Get.OpenSaveDataFileSystem(ref fileSystem.Ref(), DeviceSaveDataSpaceId, in attribute);
        if (rc.IsFailure()) return rc.Miss();

        var fileSystemAdapterRaw = new FileSystemServiceObjectAdapter(ref fileSystem.Ref());
        using var fileSystemAdapter = new UniqueRef<IFileSystem>(fileSystemAdapterRaw);

        if (!fileSystemAdapter.HasValue)
            return ResultFs.AllocationMemoryFailedInDeviceSaveDataA.Log();

        using var saveDataAttributeGetter =
            new UniqueRef<ISaveDataAttributeGetter>(new DeviceSaveDataAttributeGetter(attribute.ProgramId));

        using var mountNameGenerator = new UniqueRef<ICommonMountNameGenerator>();

        rc = fs.Fs.Register(mountName, fileSystemAdapterRaw, ref fileSystemAdapter.Ref(), ref mountNameGenerator.Ref(),
            ref saveDataAttributeGetter.Ref(), useDataCache: false, storageForPurgeFileDataCache: null,
            usePathCache: true);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result MountDeviceSaveData(this FileSystemClient fs, U8Span mountName)
    {
        Span<byte> logBuffer = stackalloc byte[0x30];

        Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, InvalidProgramId, SaveDataType.Device,
            InvalidUserId, InvalidSystemSaveDataId);

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountDeviceSaveDataImpl(fs.Impl, mountName, in attribute);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote);

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountDeviceSaveDataImpl(fs.Impl, mountName, in attribute);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountDeviceSaveData(this FileSystemClient fs, U8Span mountName,
        Ncm.ApplicationId applicationId)
    {
        Span<byte> logBuffer = stackalloc byte[0x50];

        Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Device,
            InvalidUserId, InvalidSystemSaveDataId);

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = MountDeviceSaveDataImpl(fs.Impl, mountName, in attribute);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogName).Append(mountName).Append(LogQuote)
                .Append(LogApplicationId).AppendFormat(applicationId.Value, 'X');

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = MountDeviceSaveDataImpl(fs.Impl, mountName, in attribute);
        }

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.Application))
            fs.Impl.EnableFileSystemAccessorAccessLog(mountName);

        return Result.Success;
    }

    public static Result MountDeviceSaveData(this FileSystemClient fs, U8Span mountName, ApplicationId applicationId)
    {
        return MountDeviceSaveData(fs, mountName, new Ncm.ApplicationId(applicationId.Value));
    }

    public static bool IsDeviceSaveDataExisting(this FileSystemClient fs, ApplicationId applicationId)
    {
        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x30];

        bool exists;
        var appId = new Ncm.ApplicationId(applicationId.Value);

        if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
        {
            Tick start = fs.Hos.Os.GetSystemTick();
            rc = fs.Impl.IsSaveDataExisting(out exists, appId, SaveDataType.Device, InvalidUserId);
            Tick end = fs.Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X');

            fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
        }
        else
        {
            rc = fs.Impl.IsSaveDataExisting(out exists, appId, SaveDataType.Device, InvalidUserId);
        }

        fs.Impl.LogResultErrorMessage(rc);
        Abort.DoAbortUnless(rc.IsSuccess());

        return exists;
    }
}