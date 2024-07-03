using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Sf;
using static LibHac.FsSrv.Anonymous;

namespace LibHac.FsSrv;

file static class Anonymous
{
    public static Result GetProgramInfo(FileSystemServer fsServer, out ProgramInfo programInfo, ulong processId)
    {
        var programRegistry = new ProgramRegistryImpl(fsServer);
        return programRegistry.GetProgramInfo(out programInfo, processId).Ret();
    }
}

/// <summary>
/// Handles access log calls for <see cref="FileSystemProxyImpl"/>.
/// </summary>
/// <remarks><para>This struct handles checking a process' permissions before forwarding
/// requests to the <see cref="AccessLogServiceImpl"/> object.</para>
/// <para>Based on nnSdk 18.3.0 (FS 18.0.0)</para></remarks>
internal readonly struct AccessLogService
{
    private readonly AccessLogServiceImpl _serviceImpl;
    private readonly ulong _processId;

    private FileSystemServer FsServer => _serviceImpl.FsServer;

    public AccessLogService(AccessLogServiceImpl serviceImpl, ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
    }

    public Result SetAccessLogMode(GlobalAccessLogMode mode)
    {
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.SetGlobalAccessLogMode))
            return ResultFs.PermissionDenied.Log();

        _serviceImpl.SetAccessLogMode(mode);
        return Result.Success;
    }

    public Result GetAccessLogMode(out GlobalAccessLogMode mode)
    {
        mode = _serviceImpl.GetAccessLogMode();
        return Result.Success;
    }

    public Result OutputAccessLogToSdCard(InBuffer textBuffer)
    {
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        return _serviceImpl.OutputAccessLogToSdCard(textBuffer.Buffer, programInfo.ProgramIdValue, _processId).Ret();
    }

    private static ReadOnlySpan<byte> OutputAccessLog => "FS_ACCESS: { multi_program_tag: true }\n"u8;
    private static ReadOnlySpan<byte> OutputLogTagUnknown => "FS_ACCESS: { application_info_tag: { launch_type: Unknown } }\n"u8;
    private static ReadOnlySpan<byte> OutputHead => "FS_ACCESS: { application_info_tag: { launch_type: "u8;
    private static ReadOnlySpan<byte> OutputApplicationId => "Application, application_id: 0x"u8;
    private static ReadOnlySpan<byte> OutputPatchId => "Patch, application_id: 0x"u8;
    private static ReadOnlySpan<byte> OutputVersion => ", release_version: 0x"u8;
    private static ReadOnlySpan<byte> OutputTail => " } }\n"u8;

    public Result OutputApplicationInfoAccessLog(in ApplicationInfo applicationInfo)
    {
        if (applicationInfo.IsMultiProgram)
        {
            _serviceImpl.OutputAccessLogToSdCard(OutputAccessLog, applicationInfo.ApplicationId.Value, _processId).IgnoreResult();
        }

        if (applicationInfo.LaunchType == 0)
        {
            _serviceImpl.OutputAccessLogToSdCard(OutputLogTagUnknown, applicationInfo.ApplicationId.Value, _processId).IgnoreResult();
        }
        else
        {
            Span<byte> buffer = stackalloc byte[0x80];
            ReadOnlySpan<byte> outputId = applicationInfo.LaunchType == 1 ? OutputApplicationId : OutputPatchId;

            var sb = new U8StringBuilder(buffer, autoExpand: true);
            sb.Append(OutputHead)
                .Append(outputId).AppendFormat(applicationInfo.ApplicationId.Value, 'X', 16)
                .Append(OutputVersion).AppendFormat(applicationInfo.Version >> 16, 'X', 4)
                .Append(OutputTail);

            _serviceImpl.OutputAccessLogToSdCard(sb.Buffer, applicationInfo.ApplicationId.Value, _processId);
        }

        return Result.Success;
    }

    public Result OutputMultiProgramTagAccessLog()
    {
        _serviceImpl.OutputAccessLogToSdCard(MultiProgramTag, _processId).IgnoreResult();
        return Result.Success;
    }

    public Result FlushAccessLogOnSdCard()
    {
        _serviceImpl.FlushAccessLogSdCardWriter();
        return Result.Success;
    }

    /// <summary>"<c>FS_ACCESS: { multi_program_tag: true }\n</c>"</summary>
    private static ReadOnlySpan<byte> MultiProgramTag => "FS_ACCESS: { multi_program_tag: true }\n"u8;
}