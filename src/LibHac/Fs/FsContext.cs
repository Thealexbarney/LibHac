using System;

namespace LibHac.Fs
{
    public enum AbortSpecifier
    {
        Default,
        Abort,
        Return
    }

    internal struct FsContextHandlerGlobals
    {
        public bool IsAutoAbortEnabled;
        public FsContext DefaultContext;
        public FsContext AllReturnContext;

        public void Initialize(FileSystemClient fsClient)
        {
            DefaultContext = new FsContext(fsClient, FsContextHandler.DefaultResultHandler);
            AllReturnContext = new FsContext(fsClient, FsContextHandler.AllReturnResultHandler);
        }
    }

    public delegate AbortSpecifier ResultHandler(FileSystemClient fs, Result result);

    public static class FsContextHandler
    {
        [ThreadStatic]
        private static FsContext _currentThreadContext;

        internal static AbortSpecifier DefaultResultHandler(FileSystemClient fs, Result result)
        {
            if (fs.Globals.FsContextHandler.IsAutoAbortEnabled)
                return AbortSpecifier.Default;
            else
                return AbortSpecifier.Return;
        }

        internal static AbortSpecifier AllReturnResultHandler(FileSystemClient fs, Result result)
        {
            return AbortSpecifier.Return;
        }

        public static void SetEnabledAutoAbort(this FileSystemClient fs, bool isEnabled)
        {
            fs.Globals.FsContextHandler.IsAutoAbortEnabled = isEnabled;
        }

        public static void SetDefaultFsContextResultHandler(this FileSystemClient fs,
            ResultHandler resultHandler)
        {
            fs.Globals.FsContextHandler.DefaultContext.SetHandler(resultHandler ?? DefaultResultHandler);
        }

        public static ref FsContext GetCurrentThreadFsContext(this FileSystemClient fs)
        {
            ref FsContext context = ref _currentThreadContext;
            if (context.IsValid)
            {
                return ref context;
            }

            return ref fs.Globals.FsContextHandler.DefaultContext;
        }

        public static void SetCurrentThreadFsContext(this FileSystemClient fs, FsContext context)
        {
            _currentThreadContext = context;
        }

        public static bool IsResolubleAccessFailureResult(Result result)
        {
            return ResultFs.GameCardAccessFailed.Includes(result);
        }

        public static bool IsAutoAbortPolicyCustomized(this FileSystemClient fs)
        {
            ref FsContextHandlerGlobals g = ref fs.Globals.FsContextHandler;
            return !g.IsAutoAbortEnabled || GetCurrentThreadFsContext(fs).GetHandler() != DefaultResultHandler;
        }
    }

    public struct FsContext
    {
        private readonly FileSystemClient _fsClient;
        private ResultHandler _resultHandler;

        internal bool IsValid => _fsClient is not null;

        public FsContext(FileSystemClient fsClient, ResultHandler resultHandler)
        {
            _fsClient = fsClient;
            _resultHandler = resultHandler;
        }

        public AbortSpecifier HandleResult(Result result)
        {
            return _resultHandler(_fsClient, result);
        }

        public ResultHandler GetHandler()
        {
            return _resultHandler;
        }

        public void SetHandler(ResultHandler handler)
        {
            _resultHandler = handler;
        }
    }

    public struct ScopedAutoAbortDisabler
    {
        private FileSystemClient _fsClient;
        private FsContext _prevContext;

        public ScopedAutoAbortDisabler(FileSystemClient fs)
        {
            _fsClient = fs;
            _prevContext = fs.GetCurrentThreadFsContext();
            fs.SetCurrentThreadFsContext(fs.Globals.FsContextHandler.AllReturnContext);
        }

        public void Dispose()
        {
            _fsClient.SetCurrentThreadFsContext(_prevContext);
        }
    }
}
