using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.Sf;

namespace LibHac.FsSrv
{
    internal readonly struct AccessLogService
    {
        private readonly AccessLogServiceImpl _serviceImpl;
        private readonly ulong _processId;

        public AccessLogService(AccessLogServiceImpl serviceImpl, ulong processId)
        {
            _serviceImpl = serviceImpl;
            _processId = processId;
        }

        public Result SetAccessLogMode(GlobalAccessLogMode mode)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

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
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            return _serviceImpl.OutputAccessLogToSdCard(textBuffer.Buffer, programInfo.ProgramIdValue, _processId);
        }

        public Result OutputApplicationInfoAccessLog(in ApplicationInfo applicationInfo)
        {
            throw new NotImplementedException();
        }

        public Result OutputMultiProgramTagAccessLog()
        {
            _serviceImpl.OutputAccessLogToSdCard(MultiProgramTag, _processId).IgnoreResult();
            return Result.Success;
        }

        public Result FlushAccessLogOnSdCard()
        {
            throw new NotImplementedException();
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return _serviceImpl.GetProgramInfo(out programInfo, _processId);
        }

        private static ReadOnlySpan<byte> MultiProgramTag => // FS_ACCESS: { multi_program_tag: true }\n
            new[]
            {
                (byte) 'F', (byte) 'S', (byte) '_', (byte) 'A', (byte) 'C', (byte) 'C', (byte) 'E', (byte) 'S',
                (byte) 'S', (byte) ':', (byte) ' ', (byte) '{', (byte) ' ', (byte) 'm', (byte) 'u', (byte) 'l',
                (byte) 't', (byte) 'i', (byte) '_', (byte) 'p', (byte) 'r', (byte) 'o', (byte) 'g', (byte) 'r',
                (byte) 'a', (byte) 'm', (byte) '_', (byte) 't', (byte) 'a', (byte) 'g', (byte) ':', (byte) ' ',
                (byte) 't', (byte) 'r', (byte) 'u', (byte) 'e', (byte) ' ', (byte) '}', (byte) '\n'
            };
    }
}
