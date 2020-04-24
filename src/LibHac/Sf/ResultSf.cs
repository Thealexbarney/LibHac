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

using System.Runtime.CompilerServices;

namespace LibHac.Sf
{
    public static class ResultSf
    {
        public const int ModuleSf = 10;

        /// <summary>Error code: 2010-0001; Inner value: 0x20a</summary>
        public static Result.Base NotSupported => new Result.Base(ModuleSf, 1);
        /// <summary>Error code: 2010-0003; Inner value: 0x60a</summary>
        public static Result.Base PreconditionViolation => new Result.Base(ModuleSf, 3);
        /// <summary>Error code: 2010-0202; Inner value: 0x1940a</summary>
        public static Result.Base InvalidHeaderSize => new Result.Base(ModuleSf, 202);
        /// <summary>Error code: 2010-0211; Inner value: 0x1a60a</summary>
        public static Result.Base InvalidInHeader => new Result.Base(ModuleSf, 211);
        /// <summary>Error code: 2010-0221; Inner value: 0x1ba0a</summary>
        public static Result.Base UnknownCommandId => new Result.Base(ModuleSf, 221);
        /// <summary>Error code: 2010-0232; Inner value: 0x1d00a</summary>
        public static Result.Base InvalidOutRawSize => new Result.Base(ModuleSf, 232);
        /// <summary>Error code: 2010-0235; Inner value: 0x1d60a</summary>
        public static Result.Base InvalidNumInObjects => new Result.Base(ModuleSf, 235);
        /// <summary>Error code: 2010-0236; Inner value: 0x1d80a</summary>
        public static Result.Base InvalidNumOutObjects => new Result.Base(ModuleSf, 236);
        /// <summary>Error code: 2010-0239; Inner value: 0x1de0a</summary>
        public static Result.Base InvalidInObject => new Result.Base(ModuleSf, 239);
        /// <summary>Error code: 2010-0261; Inner value: 0x20a0a</summary>
        public static Result.Base TargetNotFound => new Result.Base(ModuleSf, 261);
        /// <summary>Error code: 2010-0301; Inner value: 0x25a0a</summary>
        public static Result.Base OutOfDomainEntries => new Result.Base(ModuleSf, 301);

        /// <summary>Error code: 2010-0800; Range: 800-899; Inner value: 0x6400a</summary>
        public static Result.Base RequestContextChanged { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleSf, 800, 899); }
            /// <summary>Error code: 2010-0801; Range: 801-809; Inner value: 0x6420a</summary>
            public static Result.Base RequestInvalidated { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleSf, 801, 809); }
                /// <summary>Error code: 2010-0802; Inner value: 0x6440a</summary>
                public static Result.Base RequestInvalidatedByUser => new Result.Base(ModuleSf, 802);

            /// <summary>Error code: 2010-0811; Range: 811-819; Inner value: 0x6560a</summary>
            public static Result.Base RequestDeferred { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleSf, 811, 819); }
                /// <summary>Error code: 2010-0812; Inner value: 0x6580a</summary>
                public static Result.Base RequestDeferredByUser => new Result.Base(ModuleSf, 812);
    }
}
