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

namespace LibHac.HtcFs;

public static class ResultHtcFs
{
    public const int ModuleHtcFs = 31;

    /// <summary>Error code: 2031-0003; Inner value: 0x61f</summary>
    public static Result.Base InvalidArgument => new Result.Base(ModuleHtcFs, 3);

    /// <summary>Error code: 2031-0100; Range: 100-199; Inner value: 0xc81f</summary>
    public static Result.Base ConnectionFailure => new Result.Base(ModuleHtcFs, 100, 199);
        /// <summary>Error code: 2031-0101; Inner value: 0xca1f</summary>
        public static Result.Base HtclowChannelClosed => new Result.Base(ModuleHtcFs, 101);

        /// <summary>Error code: 2031-0110; Range: 110-119; Inner value: 0xdc1f</summary>
        public static Result.Base UnexpectedResponse => new Result.Base(ModuleHtcFs, 110, 119);
            /// <summary>Error code: 2031-0111; Inner value: 0xde1f</summary>
            public static Result.Base UnexpectedResponseProtocolId => new Result.Base(ModuleHtcFs, 111);
            /// <summary>Error code: 2031-0112; Inner value: 0xe01f</summary>
            public static Result.Base UnexpectedResponseProtocolVersion => new Result.Base(ModuleHtcFs, 112);
            /// <summary>Error code: 2031-0113; Inner value: 0xe21f</summary>
            public static Result.Base UnexpectedResponsePacketCategory => new Result.Base(ModuleHtcFs, 113);
            /// <summary>Error code: 2031-0114; Inner value: 0xe41f</summary>
            public static Result.Base UnexpectedResponsePacketType => new Result.Base(ModuleHtcFs, 114);
            /// <summary>Error code: 2031-0115; Inner value: 0xe61f</summary>
            public static Result.Base UnexpectedResponseBodySize => new Result.Base(ModuleHtcFs, 115);
            /// <summary>Error code: 2031-0116; Inner value: 0xe81f</summary>
            public static Result.Base UnexpectedResponseBody => new Result.Base(ModuleHtcFs, 116);

    /// <summary>Error code: 2031-0200; Range: 200-299; Inner value: 0x1901f</summary>
    public static Result.Base InternalError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleHtcFs, 200, 299); }
        /// <summary>Error code: 2031-0201; Inner value: 0x1921f</summary>
        public static Result.Base InvalidSize => new Result.Base(ModuleHtcFs, 201);
        /// <summary>Error code: 2031-0211; Inner value: 0x1a61f</summary>
        public static Result.Base UnknownError => new Result.Base(ModuleHtcFs, 211);
        /// <summary>Error code: 2031-0212; Inner value: 0x1a81f</summary>
        public static Result.Base UnsupportedProtocolVersion => new Result.Base(ModuleHtcFs, 212);
        /// <summary>Error code: 2031-0213; Inner value: 0x1aa1f</summary>
        public static Result.Base InvalidRequest => new Result.Base(ModuleHtcFs, 213);
        /// <summary>Error code: 2031-0214; Inner value: 0x1ac1f</summary>
        public static Result.Base InvalidHandle => new Result.Base(ModuleHtcFs, 214);
        /// <summary>Error code: 2031-0215; Inner value: 0x1ae1f</summary>
        public static Result.Base OutOfHandle => new Result.Base(ModuleHtcFs, 215);
}