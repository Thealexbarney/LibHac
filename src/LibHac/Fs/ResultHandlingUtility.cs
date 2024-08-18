﻿using System.Runtime.CompilerServices;
using LibHac.Diag;
using LibHac.Htc;
using LibHac.HtcFs;
using LibHac.HtcLow;

namespace LibHac.Fs;

internal struct ResultHandlingUtilityGlobals
{
    public bool IsResultHandledByApplication;
}

public static class ResultHandlingUtility
{
    public static void SetResultHandledByApplication(this FileSystemClient fs, bool isHandledByApplication)
    {
        fs.Globals.ResultHandlingUtility.IsResultHandledByApplication = isHandledByApplication;
    }

    public static bool IsAbortNeeded(this FileSystemClientImpl fs, Result result)
    {
        if (result.IsSuccess())
            return false;

        switch (fs.Fs.GetCurrentThreadFsContext().HandleResult(result))
        {
            case AbortSpecifier.Default:
                if (fs.Globals.ResultHandlingUtility.IsResultHandledByApplication)
                {
                    return ResultFs.HandledByAllProcess.Includes(result);
                }
                else
                {
                    return !(ResultFs.HandledByAllProcess.Includes(result) ||
                             ResultFs.HandledBySystemProcess.Includes(result));
                }
            case AbortSpecifier.Abort:
                return true;
            case AbortSpecifier.Return:
                return false;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public static void LogErrorMessage(this FileSystemClientImpl fs, Result result, string message)
    {
        // Todo
    }

    public static void LogErrorMessage(this FileSystemClientImpl fs, Result result, string format, object arg0)
    {
        // Todo
    }

    public static void LogErrorMessage(this FileSystemClientImpl fs, Result result, string format,
        params object[] args)
    {
        // Todo
    }

    public static void LogResultErrorMessage(this FileSystemClientImpl fs, Result result,
        [CallerMemberName] string functionName = "")
    {
        // Todo
    }

    internal static void AbortIfNeeded(this FileSystemClientImpl fs, Result result,
        [CallerMemberName] string functionName = "")
    {
        if (!IsAbortNeeded(fs, result))
            return;

        fs.LogResultErrorMessage(result, functionName);

        if (!result.IsSuccess())
            Abort.DoAbort(result);
    }

    public static bool IsResultHtcAccessFailed(Result result)
    {
        if (ResultHtc.ConnectionFailure.Module == result.Module)
            return true;

        if (ResultHtcLow.ConnectionFailure.Module == result.Module)
            return true;

        if (ResultHtcFs.ConnectionFailure.Module == result.Module)
            return true;

        return false;
    }

    public static void SetErrorContextEnabled(this FileSystemClientImpl fs, bool isEnabled)
    {
        // Todo
    }
}