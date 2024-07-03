using System;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Os;
using Utility = LibHac.FsSrv.Impl.Utility;

namespace LibHac.FsSrv;

/// <summary>
/// Writes to the FS access log file on the SD card, filtering messages based on the current global log mode
/// and the sending program's ID.
/// </summary>
/// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
public class AccessLogServiceImpl : IDisposable
{
    private Configuration _config;
    private GlobalAccessLogMode _accessLogMode;
    private AccessLogSdCardWriter _sdCardWriter;
    private SdkMutexType _mutex;

    public FileSystemServer FsServer => _config.FsServer;

    public struct Configuration
    {
        public ulong ProgramIdWithoutPlatformIdMinForAccessLog;

        // LibHac additions
        public FileSystemServer FsServer;
    }

    public AccessLogServiceImpl(in Configuration configuration)
    {
        _config = configuration;
        _accessLogMode = GlobalAccessLogMode.None;
        _sdCardWriter = new AccessLogSdCardWriter(configuration.FsServer.Hos.Fs);
        _mutex = new SdkMutexType();
    }

    public void Dispose()
    {
        _sdCardWriter.Dispose();
    }

    public void SetAccessLogMode(GlobalAccessLogMode mode)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_accessLogMode.HasFlag(GlobalAccessLogMode.SdCard) && !mode.HasFlag(GlobalAccessLogMode.SdCard))
        {
            _sdCardWriter.Flush();
        }

        _accessLogMode = mode;
    }

    public GlobalAccessLogMode GetAccessLogMode()
    {
        return _accessLogMode;
    }

    public Result OutputAccessLogToSdCard(ReadOnlySpan<byte> text, ulong processId)
    {
        throw new NotImplementedException();
    }

    public Result OutputAccessLogToSdCard(ReadOnlySpan<byte> text, ulong programId, ulong processId)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (!_accessLogMode.HasFlag(GlobalAccessLogMode.SdCard))
            return Result.Success;

        Assert.SdkRequiresNotEqual(_config.ProgramIdWithoutPlatformIdMinForAccessLog, 0ul);

        if (Utility.ClearPlatformIdInProgramId(processId) >= _config.ProgramIdWithoutPlatformIdMinForAccessLog)
        {
            _sdCardWriter.AppendLog(text, programId);
        }

        return Result.Success;
    }

    public void FlushAccessLogSdCardWriter()
    {
        if (_accessLogMode.HasFlag(GlobalAccessLogMode.SdCard))
        {
            _sdCardWriter.Flush();
        }
    }

    public void FinalizeAccessLogSdCardWriter()
    {
        _sdCardWriter.FinalizeObject();
    }
}