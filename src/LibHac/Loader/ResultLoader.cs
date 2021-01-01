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

namespace LibHac.Loader
{
    public static class ResultLoader
    {
        public const int ModuleLoader = 9;

        /// <summary>Error code: 2009-0001; Inner value: 0x209</summary>
        public static Result.Base TooLongArgument => new Result.Base(ModuleLoader, 1);
        /// <summary>Error code: 2009-0002; Inner value: 0x409</summary>
        public static Result.Base TooManyArguments => new Result.Base(ModuleLoader, 2);
        /// <summary>Error code: 2009-0003; Inner value: 0x609</summary>
        public static Result.Base TooLargeMeta => new Result.Base(ModuleLoader, 3);
        /// <summary>Error code: 2009-0004; Inner value: 0x809</summary>
        public static Result.Base InvalidMeta => new Result.Base(ModuleLoader, 4);
        /// <summary>Error code: 2009-0005; Inner value: 0xa09</summary>
        public static Result.Base InvalidNso => new Result.Base(ModuleLoader, 5);
        /// <summary>Error code: 2009-0006; Inner value: 0xc09</summary>
        public static Result.Base InvalidPath => new Result.Base(ModuleLoader, 6);
        /// <summary>Error code: 2009-0007; Inner value: 0xe09</summary>
        public static Result.Base TooManyProcesses => new Result.Base(ModuleLoader, 7);
        /// <summary>Error code: 2009-0008; Inner value: 0x1009</summary>
        public static Result.Base NotPinned => new Result.Base(ModuleLoader, 8);
        /// <summary>Error code: 2009-0009; Inner value: 0x1209</summary>
        public static Result.Base InvalidProgramId => new Result.Base(ModuleLoader, 9);
        /// <summary>Error code: 2009-0010; Inner value: 0x1409</summary>
        public static Result.Base InvalidVersion => new Result.Base(ModuleLoader, 10);
        /// <summary>Error code: 2009-0011; Inner value: 0x1609</summary>
        public static Result.Base InvalidAcidSignature => new Result.Base(ModuleLoader, 11);
        /// <summary>Error code: 2009-0012; Inner value: 0x1809</summary>
        public static Result.Base InvalidNcaSignature => new Result.Base(ModuleLoader, 12);
        /// <summary>Error code: 2009-0051; Inner value: 0x6609</summary>
        public static Result.Base InsufficientAddressSpace => new Result.Base(ModuleLoader, 51);
        /// <summary>Error code: 2009-0052; Inner value: 0x6809</summary>
        public static Result.Base InvalidNro => new Result.Base(ModuleLoader, 52);
        /// <summary>Error code: 2009-0053; Inner value: 0x6a09</summary>
        public static Result.Base InvalidNrr => new Result.Base(ModuleLoader, 53);
        /// <summary>Error code: 2009-0054; Inner value: 0x6c09</summary>
        public static Result.Base InvalidSignature => new Result.Base(ModuleLoader, 54);
        /// <summary>Error code: 2009-0055; Inner value: 0x6e09</summary>
        public static Result.Base InsufficientNroRegistrations => new Result.Base(ModuleLoader, 55);
        /// <summary>Error code: 2009-0056; Inner value: 0x7009</summary>
        public static Result.Base InsufficientNrrRegistrations => new Result.Base(ModuleLoader, 56);
        /// <summary>Error code: 2009-0057; Inner value: 0x7209</summary>
        public static Result.Base NroAlreadyLoaded => new Result.Base(ModuleLoader, 57);
        /// <summary>Error code: 2009-0081; Inner value: 0xa209</summary>
        public static Result.Base InvalidAddress => new Result.Base(ModuleLoader, 81);
        /// <summary>Error code: 2009-0082; Inner value: 0xa409</summary>
        public static Result.Base InvalidSize => new Result.Base(ModuleLoader, 82);
        /// <summary>Error code: 2009-0084; Inner value: 0xa809</summary>
        public static Result.Base NotLoaded => new Result.Base(ModuleLoader, 84);
        /// <summary>Error code: 2009-0085; Inner value: 0xaa09</summary>
        public static Result.Base NotRegistered => new Result.Base(ModuleLoader, 85);
        /// <summary>Error code: 2009-0086; Inner value: 0xac09</summary>
        public static Result.Base InvalidSession => new Result.Base(ModuleLoader, 86);
        /// <summary>Error code: 2009-0087; Inner value: 0xae09</summary>
        public static Result.Base InvalidProcess => new Result.Base(ModuleLoader, 87);
        /// <summary>Error code: 2009-0100; Inner value: 0xc809</summary>
        public static Result.Base UnknownCapability => new Result.Base(ModuleLoader, 100);
        /// <summary>Error code: 2009-0103; Inner value: 0xce09</summary>
        public static Result.Base InvalidCapabilityKernelFlags => new Result.Base(ModuleLoader, 103);
        /// <summary>Error code: 2009-0104; Inner value: 0xd009</summary>
        public static Result.Base InvalidCapabilitySyscallMask => new Result.Base(ModuleLoader, 104);
        /// <summary>Error code: 2009-0106; Inner value: 0xd409</summary>
        public static Result.Base InvalidCapabilityMapRange => new Result.Base(ModuleLoader, 106);
        /// <summary>Error code: 2009-0107; Inner value: 0xd609</summary>
        public static Result.Base InvalidCapabilityMapPage => new Result.Base(ModuleLoader, 107);
        /// <summary>Error code: 2009-0111; Inner value: 0xde09</summary>
        public static Result.Base InvalidCapabilityInterruptPair => new Result.Base(ModuleLoader, 111);
        /// <summary>Error code: 2009-0113; Inner value: 0xe209</summary>
        public static Result.Base InvalidCapabilityApplicationType => new Result.Base(ModuleLoader, 113);
        /// <summary>Error code: 2009-0114; Inner value: 0xe409</summary>
        public static Result.Base InvalidCapabilityKernelVersion => new Result.Base(ModuleLoader, 114);
        /// <summary>Error code: 2009-0115; Inner value: 0xe609</summary>
        public static Result.Base InvalidCapabilityHandleTable => new Result.Base(ModuleLoader, 115);
        /// <summary>Error code: 2009-0116; Inner value: 0xe809</summary>
        public static Result.Base InvalidCapabilityDebugFlags => new Result.Base(ModuleLoader, 116);
        /// <summary>Error code: 2009-0200; Inner value: 0x19009</summary>
        public static Result.Base InternalError => new Result.Base(ModuleLoader, 200);
    }
}
