//-----------------------------------------------------------------------------
// This file was automatically generated.
// Changes to this file will be lost when the file is regenerated.
//
// To change this file, modify /build/CodeGen/results.csv at the root of this
// repo and run the build script.
//
// The script can be run with the "codegen" option to run only the
// code generation portion of the build.
//-----------------------------------------------------------------------------

namespace LibHac.Bcat
{
    public static class ResultBcat
    {
        public const int ModuleBcat = 122;

        /// <summary>Error code: 2122-0001; Inner value: 0x27a</summary>
        public static Result.Base InvalidArgument => new Result.Base(ModuleBcat, 1);
        /// <summary>Error code: 2122-0002; Inner value: 0x47a</summary>
        public static Result.Base NotFound => new Result.Base(ModuleBcat, 2);
        /// <summary>Error code: 2122-0007; Inner value: 0xe7a</summary>
        public static Result.Base NotOpen => new Result.Base(ModuleBcat, 7);
        /// <summary>Error code: 2122-0009; Inner value: 0x127a</summary>
        public static Result.Base ServiceOpenLimitReached => new Result.Base(ModuleBcat, 9);
        /// <summary>Error code: 2122-0010; Inner value: 0x147a</summary>
        public static Result.Base SaveDataNotFount => new Result.Base(ModuleBcat, 10);
        /// <summary>Error code: 2122-0031; Inner value: 0x3e7a</summary>
        public static Result.Base NetworkServiceAccountNotAvailable => new Result.Base(ModuleBcat, 31);
        /// <summary>Error code: 2122-0090; Inner value: 0xb47a</summary>
        public static Result.Base PermissionDenied => new Result.Base(ModuleBcat, 90);
        /// <summary>Error code: 2122-0204; Inner value: 0x1987a</summary>
        public static Result.Base InvalidStorageMetaVersion => new Result.Base(ModuleBcat, 204);
        /// <summary>Error code: 2122-0205; Inner value: 0x19a7a</summary>
        public static Result.Base StorageOpenLimitReached => new Result.Base(ModuleBcat, 205);
    }
}
