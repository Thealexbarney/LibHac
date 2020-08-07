﻿using System;
using LibHac.Common;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;

namespace LibHac.Fs.Shim
{
    public static class SdCard
    {
        public static Result MountSdCard(this FileSystemClient fs, U8Span mountName)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = Run(fs, mountName);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, "");
            }
            else
            {
                rc = Run(fs, mountName);
            }

            if (rc.IsFailure()) return rc;

            if (fs.IsEnabledAccessLog(AccessLogTarget.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return Result.Success;

            static Result Run(FileSystemClient fs, U8Span mountName)
            {
                // ReSharper disable once VariableHidesOuterVariable
                Result rc = MountHelpers.CheckMountName(mountName);
                if (rc.IsFailure()) return rc;

                IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                rc = fsProxy.OpenSdCardFileSystem(out IFileSystem fileSystem);
                if (rc.IsFailure()) return rc;

                return fs.Register(mountName, fileSystem);
            }
        }

        public static bool IsSdCardInserted(this FileSystemClient fs)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.OpenDeviceOperator(out IDeviceOperator deviceOperator);
            if (rc.IsFailure()) throw new HorizonResultException(rc, "Abort");

            rc = deviceOperator.IsSdCardInserted(out bool isInserted);
            if (rc.IsFailure()) throw new HorizonResultException(rc, "Abort");

            return isInserted;
        }

        public static Result SetSdCardEncryptionSeed(this FileSystemClient fs, ref EncryptionSeed seed)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.SetSdCardEncryptionSeed(ref seed);
            if (rc.IsFailure()) throw new HorizonResultException(rc, "Abort");

            return Result.Success;
        }

        public static void SetSdCardAccessibility(this FileSystemClient fs, bool isAccessible)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.SetSdCardAccessibility(isAccessible);
            if (rc.IsFailure()) throw new HorizonResultException(rc, "Abort");
        }

        public static bool IsSdCardAccessible(this FileSystemClient fs)
        {
            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.IsSdCardAccessible(out bool isAccessible);
            if (rc.IsFailure()) throw new HorizonResultException(rc, "Abort");

            return isAccessible;
        }
    }
}
