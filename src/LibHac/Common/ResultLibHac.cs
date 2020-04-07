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

        /// <summary>Error code: 2428-0051; Inner value: 0x67ac</summary>
        public static Result.Base ServiceNotInitialized => new Result.Base(ModuleLibHac, 51);

        /// <summary>Error code: 2428-1000; Range: 1000-1999; Inner value: 0x7d1ac</summary>
        public static Result.Base InvalidData { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleLibHac, 1000, 1999); }
            /// <summary>Error code: 2428-1001; Range: 1001-1019; Inner value: 0x7d3ac</summary>
            public static Result.Base InvalidKip { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleLibHac, 1001, 1019); }
                /// <summary>The size of the KIP file was smaller than expected.<br/>Error code: 2428-1002; Inner value: 0x7d5ac</summary>
                public static Result.Base InvalidKipFileSize => new Result.Base(ModuleLibHac, 1002);
                /// <summary>The magic value of the KIP file was not KIP1.<br/>Error code: 2428-1003; Inner value: 0x7d7ac</summary>
                public static Result.Base InvalidKipMagic => new Result.Base(ModuleLibHac, 1003);
                /// <summary>The size of the compressed KIP segment was smaller than expected.<br/>Error code: 2428-1004; Inner value: 0x7d9ac</summary>
                public static Result.Base InvalidKipSegmentSize => new Result.Base(ModuleLibHac, 1004);
                /// <summary>An error occurred while decompressing a KIP segment.<br/>Error code: 2428-1005; Inner value: 0x7dbac</summary>
                public static Result.Base KipSegmentDecompressionFailed => new Result.Base(ModuleLibHac, 1005);
    }
}
