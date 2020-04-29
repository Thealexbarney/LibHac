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

namespace LibHac.Sm
{
    public static class ResultSm
    {
        public const int ModuleSm = 21;

        /// <summary>Error code: 2021-0001; Inner value: 0x215</summary>
        public static Result.Base OutOfProcesses => new Result.Base(ModuleSm, 1);
        /// <summary>Error code: 2021-0002; Inner value: 0x415</summary>
        public static Result.Base InvalidClient => new Result.Base(ModuleSm, 2);
        /// <summary>Error code: 2021-0003; Inner value: 0x615</summary>
        public static Result.Base OutOfSessions => new Result.Base(ModuleSm, 3);
        /// <summary>Error code: 2021-0004; Inner value: 0x815</summary>
        public static Result.Base AlreadyRegistered => new Result.Base(ModuleSm, 4);
        /// <summary>Error code: 2021-0005; Inner value: 0xa15</summary>
        public static Result.Base OutOfServices => new Result.Base(ModuleSm, 5);
        /// <summary>Error code: 2021-0006; Inner value: 0xc15</summary>
        public static Result.Base InvalidServiceName => new Result.Base(ModuleSm, 6);
        /// <summary>Error code: 2021-0007; Inner value: 0xe15</summary>
        public static Result.Base NotRegistered => new Result.Base(ModuleSm, 7);
        /// <summary>Error code: 2021-0008; Inner value: 0x1015</summary>
        public static Result.Base NotAllowed => new Result.Base(ModuleSm, 8);
        /// <summary>Error code: 2021-0009; Inner value: 0x1215</summary>
        public static Result.Base TooLargeAccessControl => new Result.Base(ModuleSm, 9);
    }
}
