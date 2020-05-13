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
        /// <summary>Error code: 2122-0003; Inner value: 0x67a</summary>
        public static Result.Base TargetLocked => new Result.Base(ModuleBcat, 3);
        /// <summary>Error code: 2122-0004; Inner value: 0x87a</summary>
        public static Result.Base TargetAlreadyMounted => new Result.Base(ModuleBcat, 4);
        /// <summary>Error code: 2122-0005; Inner value: 0xa7a</summary>
        public static Result.Base TargetNotMounted => new Result.Base(ModuleBcat, 5);
        /// <summary>Error code: 2122-0006; Inner value: 0xc7a</summary>
        public static Result.Base AlreadyOpen => new Result.Base(ModuleBcat, 6);
        /// <summary>Error code: 2122-0007; Inner value: 0xe7a</summary>
        public static Result.Base NotOpen => new Result.Base(ModuleBcat, 7);
        /// <summary>Error code: 2122-0008; Inner value: 0x107a</summary>
        public static Result.Base InternetRequestDenied => new Result.Base(ModuleBcat, 8);
        /// <summary>Error code: 2122-0009; Inner value: 0x127a</summary>
        public static Result.Base ServiceOpenLimitReached => new Result.Base(ModuleBcat, 9);
        /// <summary>Error code: 2122-0010; Inner value: 0x147a</summary>
        public static Result.Base SaveDataNotFound => new Result.Base(ModuleBcat, 10);
        /// <summary>Error code: 2122-0031; Inner value: 0x3e7a</summary>
        public static Result.Base NetworkServiceAccountNotAvailable => new Result.Base(ModuleBcat, 31);
        /// <summary>Error code: 2122-0080; Inner value: 0xa07a</summary>
        public static Result.Base PassphrasePathNotFound => new Result.Base(ModuleBcat, 80);
        /// <summary>Error code: 2122-0081; Inner value: 0xa27a</summary>
        public static Result.Base DataVerificationFailed => new Result.Base(ModuleBcat, 81);
        /// <summary>Error code: 2122-0090; Inner value: 0xb47a</summary>
        public static Result.Base PermissionDenied => new Result.Base(ModuleBcat, 90);
        /// <summary>Error code: 2122-0091; Inner value: 0xb67a</summary>
        public static Result.Base AllocationFailed => new Result.Base(ModuleBcat, 91);
        /// <summary>Error code: 2122-0098; Inner value: 0xc47a</summary>
        public static Result.Base InvalidOperation => new Result.Base(ModuleBcat, 98);
        /// <summary>Error code: 2122-0204; Inner value: 0x1987a</summary>
        public static Result.Base InvalidDeliveryCacheStorageFile => new Result.Base(ModuleBcat, 204);
        /// <summary>Error code: 2122-0205; Inner value: 0x19a7a</summary>
        public static Result.Base StorageOpenLimitReached => new Result.Base(ModuleBcat, 205);
    }
}
