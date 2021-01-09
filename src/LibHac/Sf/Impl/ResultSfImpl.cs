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

namespace LibHac.Sf.Impl
{
    public static class ResultSfImpl
    {
        public const int ModuleSf = 10;

        /// <summary>Error code: 2010-0800; Range: 800-899</summary>
        public static Result.Base.Abstract RequestContextChanged { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base.Abstract(ModuleSf, 800, 899); }
            /// <summary>Error code: 2010-0801; Range: 801-809</summary>
            public static Result.Base.Abstract RequestInvalidated { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base.Abstract(ModuleSf, 801, 809); }
                /// <summary>Error code: 2010-0802; Inner value: 0x6440a</summary>
                public static Result.Base RequestInvalidatedByUser => new Result.Base(ModuleSf, 802);
    }
}
