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

namespace LibHac.Svc
{
    public static class ResultSvc
    {
        public const int ModuleSvc = 1;

        /// <summary>Error code: 2001-0007; Inner value: 0xe01</summary>
        public static Result.Base OutOfSessions => new Result.Base(ModuleSvc, 7);
        /// <summary>Error code: 2001-0014; Inner value: 0x1c01</summary>
        public static Result.Base InvalidArgument => new Result.Base(ModuleSvc, 14);
        /// <summary>Error code: 2001-0033; Inner value: 0x4201</summary>
        public static Result.Base NotImplemented => new Result.Base(ModuleSvc, 33);
        /// <summary>Error code: 2001-0054; Inner value: 0x6c01</summary>
        public static Result.Base StopProcessingException => new Result.Base(ModuleSvc, 54);
        /// <summary>Error code: 2001-0057; Inner value: 0x7201</summary>
        public static Result.Base NoSynchronizationObject => new Result.Base(ModuleSvc, 57);
        /// <summary>Error code: 2001-0059; Inner value: 0x7601</summary>
        public static Result.Base TerminationRequested => new Result.Base(ModuleSvc, 59);
        /// <summary>Error code: 2001-0070; Inner value: 0x8c01</summary>
        public static Result.Base NoEvent => new Result.Base(ModuleSvc, 70);
        /// <summary>Error code: 2001-0101; Inner value: 0xca01</summary>
        public static Result.Base InvalidSize => new Result.Base(ModuleSvc, 101);
        /// <summary>Error code: 2001-0102; Inner value: 0xcc01</summary>
        public static Result.Base InvalidAddress => new Result.Base(ModuleSvc, 102);
        /// <summary>Error code: 2001-0103; Inner value: 0xce01</summary>
        public static Result.Base OutOfResource => new Result.Base(ModuleSvc, 103);
        /// <summary>Error code: 2001-0104; Inner value: 0xd001</summary>
        public static Result.Base OutOfMemory => new Result.Base(ModuleSvc, 104);
        /// <summary>Error code: 2001-0105; Inner value: 0xd201</summary>
        public static Result.Base OutOfHandles => new Result.Base(ModuleSvc, 105);
        /// <summary>Error code: 2001-0106; Inner value: 0xd401</summary>
        public static Result.Base InvalidCurrentMemory => new Result.Base(ModuleSvc, 106);
        /// <summary>Error code: 2001-0108; Inner value: 0xd801</summary>
        public static Result.Base InvalidNewMemoryPermission => new Result.Base(ModuleSvc, 108);
        /// <summary>Error code: 2001-0110; Inner value: 0xdc01</summary>
        public static Result.Base InvalidMemoryRegion => new Result.Base(ModuleSvc, 110);
        /// <summary>Error code: 2001-0112; Inner value: 0xe001</summary>
        public static Result.Base InvalidPriority => new Result.Base(ModuleSvc, 112);
        /// <summary>Error code: 2001-0113; Inner value: 0xe201</summary>
        public static Result.Base InvalidCoreId => new Result.Base(ModuleSvc, 113);
        /// <summary>Error code: 2001-0114; Inner value: 0xe401</summary>
        public static Result.Base InvalidHandle => new Result.Base(ModuleSvc, 114);
        /// <summary>Error code: 2001-0115; Inner value: 0xe601</summary>
        public static Result.Base InvalidPointer => new Result.Base(ModuleSvc, 115);
        /// <summary>Error code: 2001-0116; Inner value: 0xe801</summary>
        public static Result.Base InvalidCombination => new Result.Base(ModuleSvc, 116);
        /// <summary>Error code: 2001-0117; Inner value: 0xea01</summary>
        public static Result.Base TimedOut => new Result.Base(ModuleSvc, 117);
        /// <summary>Error code: 2001-0118; Inner value: 0xec01</summary>
        public static Result.Base Cancelled => new Result.Base(ModuleSvc, 118);
        /// <summary>Error code: 2001-0119; Inner value: 0xee01</summary>
        public static Result.Base OutOfRange => new Result.Base(ModuleSvc, 119);
        /// <summary>Error code: 2001-0120; Inner value: 0xf001</summary>
        public static Result.Base InvalidEnumValue => new Result.Base(ModuleSvc, 120);
        /// <summary>Error code: 2001-0121; Inner value: 0xf201</summary>
        public static Result.Base NotFound => new Result.Base(ModuleSvc, 121);
        /// <summary>Error code: 2001-0122; Inner value: 0xf401</summary>
        public static Result.Base Busy => new Result.Base(ModuleSvc, 122);
        /// <summary>Error code: 2001-0123; Inner value: 0xf601</summary>
        public static Result.Base SessionClosed => new Result.Base(ModuleSvc, 123);
        /// <summary>Error code: 2001-0124; Inner value: 0xf801</summary>
        public static Result.Base NotHandled => new Result.Base(ModuleSvc, 124);
        /// <summary>Error code: 2001-0125; Inner value: 0xfa01</summary>
        public static Result.Base InvalidState => new Result.Base(ModuleSvc, 125);
        /// <summary>Error code: 2001-0126; Inner value: 0xfc01</summary>
        public static Result.Base ReservedUsed => new Result.Base(ModuleSvc, 126);
        /// <summary>Error code: 2001-0127; Inner value: 0xfe01</summary>
        public static Result.Base NotSupported => new Result.Base(ModuleSvc, 127);
        /// <summary>Error code: 2001-0128; Inner value: 0x10001</summary>
        public static Result.Base Debug => new Result.Base(ModuleSvc, 128);
        /// <summary>Error code: 2001-0129; Inner value: 0x10201</summary>
        public static Result.Base NoThread => new Result.Base(ModuleSvc, 129);
        /// <summary>Error code: 2001-0130; Inner value: 0x10401</summary>
        public static Result.Base UnknownThread => new Result.Base(ModuleSvc, 130);
        /// <summary>Error code: 2001-0131; Inner value: 0x10601</summary>
        public static Result.Base PortClosed => new Result.Base(ModuleSvc, 131);
        /// <summary>Error code: 2001-0132; Inner value: 0x10801</summary>
        public static Result.Base LimitReached => new Result.Base(ModuleSvc, 132);
        /// <summary>Error code: 2001-0133; Inner value: 0x10a01</summary>
        public static Result.Base InvalidMemoryPool => new Result.Base(ModuleSvc, 133);
        /// <summary>Error code: 2001-0258; Inner value: 0x20401</summary>
        public static Result.Base ReceiveListBroken => new Result.Base(ModuleSvc, 258);
        /// <summary>Error code: 2001-0259; Inner value: 0x20601</summary>
        public static Result.Base OutOfAddressSpace => new Result.Base(ModuleSvc, 259);
        /// <summary>Error code: 2001-0260; Inner value: 0x20801</summary>
        public static Result.Base MessageTooLarge => new Result.Base(ModuleSvc, 260);
        /// <summary>Error code: 2001-0517; Inner value: 0x40a01</summary>
        public static Result.Base InvalidProcessId => new Result.Base(ModuleSvc, 517);
        /// <summary>Error code: 2001-0518; Inner value: 0x40c01</summary>
        public static Result.Base InvalidThreadId => new Result.Base(ModuleSvc, 518);
        /// <summary>Error code: 2001-0519; Inner value: 0x40e01</summary>
        public static Result.Base InvalidId => new Result.Base(ModuleSvc, 519);
        /// <summary>Error code: 2001-0520; Inner value: 0x41001</summary>
        public static Result.Base ProcessTerminated => new Result.Base(ModuleSvc, 520);
    }
}
