// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Environment.GetFolderPath calls Enum.IsDefined which currently doesn't work under CoreRT (2020-06-27)
// This code is copied from the .NET runtime with modifications to avoid that.
// The downside is that it won't work in Linux unless the HOME environmental variable is set.

#if CORERT_NO_REFLECTION
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace hactoolnet
{
    internal static class HomeFolder
    {
        public static string GetFolderPath(Environment.SpecialFolder folder) =>
            GetFolderPath(folder, Environment.SpecialFolderOption.None);

        public static string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetFolderPathCoreWin(folder, option);
            }
            else
            {
                return GetFolderPathCoreWithoutValidation(folder);
            }
        }

        /// <summary>
        /// (CSIDL_PROFILE) The root users profile folder "%USERPROFILE%"
        /// ("%SystemDrive%\Users\%USERNAME%")
        /// </summary>
        internal const string Profile = "{5E6C858F-0E22-4760-9AFE-EA3317B67173}";

        private static string GetFolderPathCoreWin(Environment.SpecialFolder folder,
            Environment.SpecialFolderOption option)
        {
            // We're using SHGetKnownFolderPath instead of SHGetFolderPath as SHGetFolderPath is
            // capped at MAX_PATH.
            //
            // Because we validate both of the input enums we shouldn't have to care about CSIDL and flag
            // definitions we haven't mapped. If we remove or loosen the checks we'd have to account
            // for mapping here (this includes tweaking as SHGetFolderPath would do).
            //
            // The only SpecialFolderOption defines we have are equivalent to KnownFolderFlags.

            string folderGuid;

            switch (folder)
            {
                case Environment.SpecialFolder.UserProfile:
                    folderGuid = Profile;
                    break;
                default:
                    throw new NotSupportedException();
            }

            return GetKnownFolderPath(folderGuid, option);
        }

        private static string GetKnownFolderPath(string folderGuid, Environment.SpecialFolderOption option)
        {
            var folderId = new Guid(folderGuid);

            int hr = Shell32.SHGetKnownFolderPath(folderId, (uint)option, IntPtr.Zero, out string path);
            if (hr != 0) // Not S_OK
            {
                return string.Empty;
            }

            return path;
        }

        private static string GetFolderPathCoreWithoutValidation(Environment.SpecialFolder folder)
        {
            // All other paths are based on the XDG Base Directory Specification:
            // https://specifications.freedesktop.org/basedir-spec/latest/
            string home = null;
            try
            {
                home = GetHomeDirectory();
            }
            catch (Exception exc)
            {
                Debug.Fail($"Unable to get home directory: {exc}");
            }

            // Fall back to '/' when we can't determine the home directory.
            // This location isn't writable by non-root users which provides some safeguard
            // that the application doesn't write data which is meant to be private.
            if (string.IsNullOrEmpty(home))
            {
                home = "/";
            }

            // TODO: Consider caching (or precomputing and caching) all subsequent results.
            // This would significantly improve performance for repeated access, at the expense
            // of not being responsive to changes in the underlying environment variables,
            // configuration files, etc.

            switch (folder)
            {
                case Environment.SpecialFolder.UserProfile:
                case Environment.SpecialFolder.MyDocuments: // same value as Personal
                    return home;
            }

            // No known path for the SpecialFolder
            return string.Empty;
        }

        /// <summary>Gets the current user's home directory.</summary>
        /// <returns>The path to the home directory, or null if it could not be determined.</returns>
        internal static string GetHomeDirectory()
        {
            // First try to get the user's home directory from the HOME environment variable.
            // This should work in most cases.
            string userHomeDirectory = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(userHomeDirectory))
                return userHomeDirectory;

            throw new NotSupportedException(
                "Unable to get your home directory. Please report this on the LibHac GitHub repository." +
                "You can use the netcore build in the GitHub repo's releases for now.");
        }
    }

    internal class Shell32
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, BestFitMapping = false)]
        internal static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags,
            IntPtr hToken,
            out string ppszPath);
    }
}
#endif