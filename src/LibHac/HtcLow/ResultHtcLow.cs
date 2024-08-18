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

namespace LibHac.HtcLow;

public static class ResultHtcLow
{
    public const int ModuleHtcLow = 29;

    /// <summary>Error code: 2029-0001; Inner value: 0x21d</summary>
    public static Result.Base ConnectionFailure => new Result.Base(ModuleHtcLow, 1);
    /// <summary>Error code: 2029-0003; Inner value: 0x61d</summary>
    public static Result.Base UnknownDriverType => new Result.Base(ModuleHtcLow, 3);
    /// <summary>Error code: 2029-0005; Inner value: 0xa1d</summary>
    public static Result.Base NonBlockingReceiveFailed => new Result.Base(ModuleHtcLow, 5);
    /// <summary>Error code: 2029-0008; Inner value: 0x101d</summary>
    public static Result.Base ChannelWaitCancelled => new Result.Base(ModuleHtcLow, 8);
    /// <summary>Error code: 2029-0009; Inner value: 0x121d</summary>
    public static Result.Base ChannelAlreadyExist => new Result.Base(ModuleHtcLow, 9);
    /// <summary>Error code: 2029-0010; Inner value: 0x141d</summary>
    public static Result.Base ChannelNotExist => new Result.Base(ModuleHtcLow, 10);
    /// <summary>Error code: 2029-0151; Inner value: 0x12e1d</summary>
    public static Result.Base OutOfChannel => new Result.Base(ModuleHtcLow, 151);
    /// <summary>Error code: 2029-0152; Inner value: 0x1301d</summary>
    public static Result.Base OutOfTask => new Result.Base(ModuleHtcLow, 152);
    /// <summary>Error code: 2029-0200; Inner value: 0x1901d</summary>
    public static Result.Base InvalidChannelState => new Result.Base(ModuleHtcLow, 200);
    /// <summary>Error code: 2029-0201; Inner value: 0x1921d</summary>
    public static Result.Base InvalidChannelStateDisconnected => new Result.Base(ModuleHtcLow, 201);

    /// <summary>Error code: 2029-1000; Range: 1000-2999; Inner value: 0x7d01d</summary>
    public static Result.Base InternalError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleHtcLow, 1000, 2999); }
        /// <summary>Error code: 2029-1001; Inner value: 0x7d21d</summary>
        public static Result.Base Overflow => new Result.Base(ModuleHtcLow, 1001);
        /// <summary>Error code: 2029-1002; Inner value: 0x7d41d</summary>
        public static Result.Base OutOfMemory => new Result.Base(ModuleHtcLow, 1002);
        /// <summary>Error code: 2029-1003; Inner value: 0x7d61d</summary>
        public static Result.Base InvalidArgument => new Result.Base(ModuleHtcLow, 1003);
        /// <summary>Error code: 2029-1004; Inner value: 0x7d81d</summary>
        public static Result.Base ProtocolError => new Result.Base(ModuleHtcLow, 1004);
        /// <summary>Error code: 2029-1005; Inner value: 0x7da1d</summary>
        public static Result.Base Cancelled => new Result.Base(ModuleHtcLow, 1005);

        /// <summary>Error code: 2029-1100; Range: 1100-1199; Inner value: 0x8981d</summary>
        public static Result.Base MuxError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleHtcLow, 1100, 1199); }
            /// <summary>Error code: 2029-1101; Inner value: 0x89a1d</summary>
            public static Result.Base ChannelBufferOverflow => new Result.Base(ModuleHtcLow, 1101);
            /// <summary>Error code: 2029-1102; Inner value: 0x89c1d</summary>
            public static Result.Base ChannelBufferHasNotEnoughData => new Result.Base(ModuleHtcLow, 1102);
            /// <summary>Error code: 2029-1103; Inner value: 0x89e1d</summary>
            public static Result.Base ChannelVersionNotMatched => new Result.Base(ModuleHtcLow, 1103);
            /// <summary>Error code: 2029-1104; Inner value: 0x8a01d</summary>
            public static Result.Base ChannelStateTransitionError => new Result.Base(ModuleHtcLow, 1104);
            /// <summary>Error code: 2029-1106; Inner value: 0x8a41d</summary>
            public static Result.Base ChannelReceiveBufferEmpty => new Result.Base(ModuleHtcLow, 1106);
            /// <summary>Error code: 2029-1107; Inner value: 0x8a61d</summary>
            public static Result.Base ChannelSequenceIdNotMatched => new Result.Base(ModuleHtcLow, 1107);
            /// <summary>Error code: 2029-1108; Inner value: 0x8a81d</summary>
            public static Result.Base ChannelCannotDiscard => new Result.Base(ModuleHtcLow, 1108);

        /// <summary>Error code: 2029-1200; Range: 1200-1999; Inner value: 0x9601d</summary>
        public static Result.Base DriverError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleHtcLow, 1200, 1999); }
            /// <summary>Error code: 2029-1201; Inner value: 0x9621d</summary>
            public static Result.Base DriverOpened => new Result.Base(ModuleHtcLow, 1201);

            /// <summary>Error code: 2029-1300; Range: 1300-1399; Inner value: 0xa281d</summary>
            public static Result.Base SocketDriverError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleHtcLow, 1300, 1399); }
                /// <summary>Error code: 2029-1301; Inner value: 0xa2a1d</summary>
                public static Result.Base SocketSocketExemptError => new Result.Base(ModuleHtcLow, 1301);
                /// <summary>Error code: 2029-1302; Inner value: 0xa2c1d</summary>
                public static Result.Base SocketBindError => new Result.Base(ModuleHtcLow, 1302);
                /// <summary>Error code: 2029-1304; Inner value: 0xa301d</summary>
                public static Result.Base SocketListenError => new Result.Base(ModuleHtcLow, 1304);
                /// <summary>Error code: 2029-1305; Inner value: 0xa321d</summary>
                public static Result.Base SocketAcceptError => new Result.Base(ModuleHtcLow, 1305);
                /// <summary>Error code: 2029-1306; Inner value: 0xa341d</summary>
                public static Result.Base SocketReceiveError => new Result.Base(ModuleHtcLow, 1306);
                /// <summary>Error code: 2029-1307; Inner value: 0xa361d</summary>
                public static Result.Base SocketSendError => new Result.Base(ModuleHtcLow, 1307);
                /// <summary>Error code: 2029-1308; Inner value: 0xa381d</summary>
                public static Result.Base SocketReceiveFromError => new Result.Base(ModuleHtcLow, 1308);
                /// <summary>Error code: 2029-1309; Inner value: 0xa3a1d</summary>
                public static Result.Base SocketSendToError => new Result.Base(ModuleHtcLow, 1309);
                /// <summary>Error code: 2029-1310; Inner value: 0xa3c1d</summary>
                public static Result.Base SocketSetSockOptError => new Result.Base(ModuleHtcLow, 1310);
                /// <summary>Error code: 2029-1311; Inner value: 0xa3e1d</summary>
                public static Result.Base SocketGetSockNameError => new Result.Base(ModuleHtcLow, 1311);

            /// <summary>Error code: 2029-1400; Range: 1400-1499; Inner value: 0xaf01d</summary>
            public static Result.Base UsbDriverError { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleHtcLow, 1400, 1499); }
                /// <summary>Error code: 2029-1401; Inner value: 0xaf21d</summary>
                public static Result.Base UsbDriverUnknownError => new Result.Base(ModuleHtcLow, 1401);
                /// <summary>Error code: 2029-1402; Inner value: 0xaf41d</summary>
                public static Result.Base UsbDriverBusyError => new Result.Base(ModuleHtcLow, 1402);
                /// <summary>Error code: 2029-1403; Inner value: 0xaf61d</summary>
                public static Result.Base UsbDriverReceiveError => new Result.Base(ModuleHtcLow, 1403);
                /// <summary>Error code: 2029-1404; Inner value: 0xaf81d</summary>
                public static Result.Base UsbDriverSendError => new Result.Base(ModuleHtcLow, 1404);

        /// <summary>Error code: 2029-2000; Inner value: 0xfa01d</summary>
        public static Result.Base HtcctrlError => new Result.Base(ModuleHtcLow, 2000);
        /// <summary>Error code: 2029-2001; Inner value: 0xfa21d</summary>
        public static Result.Base HtcctrlStateTransitionNotAllowed => new Result.Base(ModuleHtcLow, 2001);
        /// <summary>Error code: 2029-2002; Inner value: 0xfa41d</summary>
        public static Result.Base HtcctrlReceiveUnexpectedPacket => new Result.Base(ModuleHtcLow, 2002);
}