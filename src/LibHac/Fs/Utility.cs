using System;
using LibHac.Account;
using LibHac.Os;

namespace LibHac.Fs;

/// <summary>
/// Contains various utility functions used by FS client code.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0</remarks>
public static class Utility
{
    public static UserId ConvertAccountUidToFsUserId(Uid uid)
    {
        return new UserId(uid.Id.High, uid.Id.Low);
    }

    public static Result CheckUid(HorizonClient hos, Uid uid)
    {
        throw new NotImplementedException();
    }

    public static Result DoContinuouslyUntilSaveDataListFetched(HorizonClient hos, Func<Result> listGetter)
    {
        const int maxTryCount = 5;
        const int initialSleepTimeMs = 5;
        const int sleepTimeMultiplier = 2;

        Result lastResult = Result.Success;
        long sleepTime = initialSleepTimeMs;

        for (int i = 0; i < maxTryCount; i++)
        {
            Result rc = listGetter();

            if (rc.IsSuccess())
                return rc;

            // Try again if any save data were added or removed while getting the list
            if (!ResultFs.InvalidHandle.Includes(rc))
                return rc.Miss();

            lastResult = rc;
            hos.Os.SleepThread(TimeSpan.FromMilliSeconds(sleepTime));
            sleepTime *= sleepTimeMultiplier;
        }

        return lastResult.Log();
    }
}