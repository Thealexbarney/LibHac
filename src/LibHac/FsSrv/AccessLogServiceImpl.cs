using System;
using LibHac.Fs;
using LibHac.FsSrv.Impl;

namespace LibHac.FsSrv
{
    public class AccessLogServiceImpl : IDisposable
    {
        private Configuration _config;
        private GlobalAccessLogMode _accessLogMode;

        public AccessLogServiceImpl(in Configuration configuration)
        {
            _config = configuration;
        }

        public void Dispose()
        {

        }

        public struct Configuration
        {
            public ulong MinimumProgramIdForSdCardLog;

            // LibHac additions
            public FileSystemServer FsServer;
        }

        public void SetAccessLogMode(GlobalAccessLogMode mode)
        {
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
            throw new NotImplementedException();
        }

        public Result FlushAccessLogSdCardWriter()
        {
            throw new NotImplementedException();
        }

        public Result FinalizeAccessLogSdCardWriter()
        {
            throw new NotImplementedException();
        }

        internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
        {
            var registry = new ProgramRegistryImpl(_config.FsServer);
            return registry.GetProgramInfo(out programInfo, processId);
        }
    }
}
