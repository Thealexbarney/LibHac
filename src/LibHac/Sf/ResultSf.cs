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

        /// <summary>Error code: 2010-0010; Range: 10-19; Inner value: 0x140a</summary>
        public static Result.Base MemoryAllocationFailed => new Result.Base(ModuleSf, 10, 19);

        /// <summary>Error code: 2010-0811; Range: 811-819</summary>
        public static Result.Base.Abstract RequestDeferred { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base.Abstract(ModuleSf, 811, 819); }
            /// <summary>Error code: 2010-0812; Inner value: 0x6580a</summary>
            public static Result.Base RequestDeferredByUser => new Result.Base(ModuleSf, 812);
    }
}
