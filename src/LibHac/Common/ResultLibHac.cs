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

namespace LibHac.Common
{
    public static class ResultLibHac
    {
        public const int ModuleLibHac = 428;

        /// <summary>Error code: 2428-0001; Range: 1-49; Inner value: 0x3ac</summary>
        public static Result.Base InvalidArgument => new Result.Base(ModuleLibHac, 1, 49);
            /// <summary>Error code: 2428-0002; Inner value: 0x5ac</summary>
            public static Result.Base NullArgument => new Result.Base(ModuleLibHac, 2);
            /// <summary>Error code: 2428-0003; Inner value: 0x7ac</summary>
            public static Result.Base ArgumentOutOfRange => new Result.Base(ModuleLibHac, 3);
            /// <summary>Error code: 2428-0004; Inner value: 0x9ac</summary>
            public static Result.Base BufferTooSmall => new Result.Base(ModuleLibHac, 4);
    }
}
