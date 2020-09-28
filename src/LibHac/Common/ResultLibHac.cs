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
        /// <summary>Error code: 2428-0101; Inner value: 0xcbac</summary>
        public static Result.Base NotImplemented => new Result.Base(ModuleLibHac, 101);

        /// <summary>Error code: 2428-1000; Range: 1000-1999; Inner value: 0x7d1ac</summary>
        public static Result.Base InvalidData { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleLibHac, 1000, 1999); }
            /// <summary>Error code: 2428-1001; Range: 1001-1019; Inner value: 0x7d3ac</summary>
            public static Result.Base InvalidInitialProcessData { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleLibHac, 1001, 1019); }
                /// <summary>Error code: 2428-1002; Range: 1002-1009; Inner value: 0x7d5ac</summary>
                public static Result.Base InvalidKip { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleLibHac, 1002, 1009); }
                    /// <summary>The size of the KIP file was smaller than expected.<br/>Error code: 2428-1003; Inner value: 0x7d7ac</summary>
                    public static Result.Base InvalidKipFileSize => new Result.Base(ModuleLibHac, 1003);
                    /// <summary>The magic value of the KIP file was not KIP1.<br/>Error code: 2428-1004; Inner value: 0x7d9ac</summary>
                    public static Result.Base InvalidKipMagic => new Result.Base(ModuleLibHac, 1004);
                    /// <summary>The size of the compressed KIP segment was smaller than expected.<br/>Error code: 2428-1005; Inner value: 0x7dbac</summary>
                    public static Result.Base InvalidKipSegmentSize => new Result.Base(ModuleLibHac, 1005);
                    /// <summary>An error occurred while decompressing a KIP segment.<br/>Error code: 2428-1006; Inner value: 0x7ddac</summary>
                    public static Result.Base KipSegmentDecompressionFailed => new Result.Base(ModuleLibHac, 1006);

                /// <summary>Error code: 2428-1010; Range: 1010-1019; Inner value: 0x7e5ac</summary>
                public static Result.Base InvalidIni { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleLibHac, 1010, 1019); }
                    /// <summary>The size of the INI file was smaller than expected.<br/>Error code: 2428-1011; Inner value: 0x7e7ac</summary>
                    public static Result.Base InvalidIniFileSize => new Result.Base(ModuleLibHac, 1011);
                    /// <summary>The magic value of the INI file was not INI1.<br/>Error code: 2428-1012; Inner value: 0x7e9ac</summary>
                    public static Result.Base InvalidIniMagic => new Result.Base(ModuleLibHac, 1012);
                    /// <summary>The INI had an invalid process count.<br/>Error code: 2428-1013; Inner value: 0x7ebac</summary>
                    public static Result.Base InvalidIniProcessCount => new Result.Base(ModuleLibHac, 1013);

            /// <summary>Error code: 2428-1020; Range: 1020-1039; Inner value: 0x7f9ac</summary>
            public static Result.Base InvalidPackage2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleLibHac, 1020, 1039); }
                /// <summary>Error code: 2428-1021; Inner value: 0x7fbac</summary>
                public static Result.Base InvalidPackage2HeaderSignature => new Result.Base(ModuleLibHac, 1021);
                /// <summary>Error code: 2428-1022; Inner value: 0x7fdac</summary>
                public static Result.Base InvalidPackage2MetaSizeA => new Result.Base(ModuleLibHac, 1022);
                /// <summary>Error code: 2428-1023; Inner value: 0x7ffac</summary>
                public static Result.Base InvalidPackage2MetaSizeB => new Result.Base(ModuleLibHac, 1023);
                /// <summary>Error code: 2428-1024; Inner value: 0x801ac</summary>
                public static Result.Base InvalidPackage2MetaKeyGeneration => new Result.Base(ModuleLibHac, 1024);
                /// <summary>Error code: 2428-1025; Inner value: 0x803ac</summary>
                public static Result.Base InvalidPackage2MetaMagic => new Result.Base(ModuleLibHac, 1025);
                /// <summary>Error code: 2428-1026; Inner value: 0x805ac</summary>
                public static Result.Base InvalidPackage2MetaEntryPointAlignment => new Result.Base(ModuleLibHac, 1026);
                /// <summary>Error code: 2428-1027; Inner value: 0x807ac</summary>
                public static Result.Base InvalidPackage2MetaPayloadAlignment => new Result.Base(ModuleLibHac, 1027);
                /// <summary>Error code: 2428-1028; Inner value: 0x809ac</summary>
                public static Result.Base InvalidPackage2MetaPayloadSizeAlignment => new Result.Base(ModuleLibHac, 1028);
                /// <summary>Error code: 2428-1029; Inner value: 0x80bac</summary>
                public static Result.Base InvalidPackage2MetaTotalSize => new Result.Base(ModuleLibHac, 1029);
                /// <summary>Error code: 2428-1030; Inner value: 0x80dac</summary>
                public static Result.Base InvalidPackage2MetaPayloadSize => new Result.Base(ModuleLibHac, 1030);
                /// <summary>Error code: 2428-1031; Inner value: 0x80fac</summary>
                public static Result.Base InvalidPackage2MetaPayloadsOverlap => new Result.Base(ModuleLibHac, 1031);
                /// <summary>Error code: 2428-1032; Inner value: 0x811ac</summary>
                public static Result.Base InvalidPackage2MetaEntryPointNotFound => new Result.Base(ModuleLibHac, 1032);
                /// <summary>Error code: 2428-1033; Inner value: 0x813ac</summary>
                public static Result.Base InvalidPackage2PayloadCorrupted => new Result.Base(ModuleLibHac, 1033);

            /// <summary>Error code: 2428-1040; Range: 1040-1059; Inner value: 0x821ac</summary>
            public static Result.Base InvalidPackage1 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleLibHac, 1040, 1059); }
                /// <summary>Error code: 2428-1041; Inner value: 0x823ac</summary>
                public static Result.Base InvalidPackage1SectionSize => new Result.Base(ModuleLibHac, 1041);
                /// <summary>Error code: 2428-1042; Inner value: 0x825ac</summary>
                public static Result.Base InvalidPackage1MarikoBodySize => new Result.Base(ModuleLibHac, 1042);
                /// <summary>Error code: 2428-1043; Inner value: 0x827ac</summary>
                public static Result.Base InvalidPackage1Pk11Size => new Result.Base(ModuleLibHac, 1043);
    }
}
