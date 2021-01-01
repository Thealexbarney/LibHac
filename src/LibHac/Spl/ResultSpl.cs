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

namespace LibHac.Spl
{
    public static class ResultSpl
    {
        public const int ModuleSpl = 26;

        /// <summary>Error code: 2026-0000; Range: 0-99; Inner value: 0x1a</summary>
        public static Result.Base SecureMonitorError => new Result.Base(ModuleSpl, 0, 99);
            /// <summary>Error code: 2026-0001; Inner value: 0x21a</summary>
            public static Result.Base SecureMonitorNotImplemented => new Result.Base(ModuleSpl, 1);
            /// <summary>Error code: 2026-0002; Inner value: 0x41a</summary>
            public static Result.Base SecureMonitorInvalidArgument => new Result.Base(ModuleSpl, 2);
            /// <summary>Error code: 2026-0003; Inner value: 0x61a</summary>
            public static Result.Base SecureMonitorBusy => new Result.Base(ModuleSpl, 3);
            /// <summary>Error code: 2026-0004; Inner value: 0x81a</summary>
            public static Result.Base SecureMonitorNoAsyncOperation => new Result.Base(ModuleSpl, 4);
            /// <summary>Error code: 2026-0005; Inner value: 0xa1a</summary>
            public static Result.Base SecureMonitorInvalidAsyncOperation => new Result.Base(ModuleSpl, 5);
            /// <summary>Error code: 2026-0006; Inner value: 0xc1a</summary>
            public static Result.Base SecureMonitorNotPermitted => new Result.Base(ModuleSpl, 6);
            /// <summary>Error code: 2026-0007; Inner value: 0xe1a</summary>
            public static Result.Base SecureMonitorNotInitialized => new Result.Base(ModuleSpl, 7);

        /// <summary>Error code: 2026-0100; Inner value: 0xc81a</summary>
        public static Result.Base InvalidSize => new Result.Base(ModuleSpl, 100);
        /// <summary>Error code: 2026-0101; Inner value: 0xca1a</summary>
        public static Result.Base UnknownSecureMonitorError => new Result.Base(ModuleSpl, 101);
        /// <summary>Error code: 2026-0102; Inner value: 0xcc1a</summary>
        public static Result.Base DecryptionFailed => new Result.Base(ModuleSpl, 102);
        /// <summary>Error code: 2026-0104; Inner value: 0xd01a</summary>
        public static Result.Base OutOfKeySlots => new Result.Base(ModuleSpl, 104);
        /// <summary>Error code: 2026-0105; Inner value: 0xd21a</summary>
        public static Result.Base InvalidKeySlot => new Result.Base(ModuleSpl, 105);
        /// <summary>Error code: 2026-0106; Inner value: 0xd41a</summary>
        public static Result.Base BootReasonAlreadySet => new Result.Base(ModuleSpl, 106);
        /// <summary>Error code: 2026-0107; Inner value: 0xd61a</summary>
        public static Result.Base BootReasonNotSet => new Result.Base(ModuleSpl, 107);
        /// <summary>Error code: 2026-0108; Inner value: 0xd81a</summary>
        public static Result.Base InvalidArgument => new Result.Base(ModuleSpl, 108);
    }
}
