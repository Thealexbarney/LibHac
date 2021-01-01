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

namespace LibHac.Ncm
{
    public static class ResultNcm
    {
        public const int ModuleNcm = 5;

        /// <summary>Error code: 2005-0001; Inner value: 0x205</summary>
        public static Result.Base InvalidContentStorageBase => new Result.Base(ModuleNcm, 1);
        /// <summary>Error code: 2005-0002; Inner value: 0x405</summary>
        public static Result.Base PlaceHolderAlreadyExists => new Result.Base(ModuleNcm, 2);
        /// <summary>Error code: 2005-0003; Inner value: 0x605</summary>
        public static Result.Base PlaceHolderNotFound => new Result.Base(ModuleNcm, 3);
        /// <summary>Error code: 2005-0004; Inner value: 0x805</summary>
        public static Result.Base ContentAlreadyExists => new Result.Base(ModuleNcm, 4);
        /// <summary>Error code: 2005-0005; Inner value: 0xa05</summary>
        public static Result.Base ContentNotFound => new Result.Base(ModuleNcm, 5);
        /// <summary>Error code: 2005-0007; Inner value: 0xe05</summary>
        public static Result.Base ContentMetaNotFound => new Result.Base(ModuleNcm, 7);
        /// <summary>Error code: 2005-0008; Inner value: 0x1005</summary>
        public static Result.Base AllocationFailed => new Result.Base(ModuleNcm, 8);
        /// <summary>Error code: 2005-0012; Inner value: 0x1805</summary>
        public static Result.Base UnknownStorage => new Result.Base(ModuleNcm, 12);
        /// <summary>Error code: 2005-0100; Inner value: 0xc805</summary>
        public static Result.Base InvalidContentStorage => new Result.Base(ModuleNcm, 100);
        /// <summary>Error code: 2005-0110; Inner value: 0xdc05</summary>
        public static Result.Base InvalidContentMetaDatabase => new Result.Base(ModuleNcm, 110);
        /// <summary>Error code: 2005-0130; Inner value: 0x10405</summary>
        public static Result.Base InvalidPackageFormat => new Result.Base(ModuleNcm, 130);
        /// <summary>Error code: 2005-0140; Inner value: 0x11805</summary>
        public static Result.Base InvalidContentHash => new Result.Base(ModuleNcm, 140);
        /// <summary>Error code: 2005-0160; Inner value: 0x14005</summary>
        public static Result.Base InvalidInstallTaskState => new Result.Base(ModuleNcm, 160);
        /// <summary>Error code: 2005-0170; Inner value: 0x15405</summary>
        public static Result.Base InvalidPlaceHolderFile => new Result.Base(ModuleNcm, 170);
        /// <summary>Error code: 2005-0180; Inner value: 0x16805</summary>
        public static Result.Base BufferInsufficient => new Result.Base(ModuleNcm, 180);
        /// <summary>Error code: 2005-0190; Inner value: 0x17c05</summary>
        public static Result.Base WriteToReadOnlyContentStorage => new Result.Base(ModuleNcm, 190);
        /// <summary>Error code: 2005-0200; Inner value: 0x19005</summary>
        public static Result.Base NotEnoughInstallSpace => new Result.Base(ModuleNcm, 200);
        /// <summary>Error code: 2005-0210; Inner value: 0x1a405</summary>
        public static Result.Base SystemUpdateNotFoundInPackage => new Result.Base(ModuleNcm, 210);
        /// <summary>Error code: 2005-0220; Inner value: 0x1b805</summary>
        public static Result.Base ContentInfoNotFound => new Result.Base(ModuleNcm, 220);
        /// <summary>Error code: 2005-0237; Inner value: 0x1da05</summary>
        public static Result.Base DeltaNotFound => new Result.Base(ModuleNcm, 237);
        /// <summary>Error code: 2005-0240; Inner value: 0x1e005</summary>
        public static Result.Base InvalidContentMetaKey => new Result.Base(ModuleNcm, 240);

        /// <summary>Error code: 2005-0250; Range: 250-258; Inner value: 0x1f405</summary>
        public static Result.Base ContentStorageNotActive { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleNcm, 250, 258); }
            /// <summary>Error code: 2005-0251; Inner value: 0x1f605</summary>
            public static Result.Base GameCardContentStorageNotActive => new Result.Base(ModuleNcm, 251);
            /// <summary>Error code: 2005-0252; Inner value: 0x1f805</summary>
            public static Result.Base BuiltInSystemContentStorageNotActive => new Result.Base(ModuleNcm, 252);
            /// <summary>Error code: 2005-0253; Inner value: 0x1fa05</summary>
            public static Result.Base BuiltInUserContentStorageNotActive => new Result.Base(ModuleNcm, 253);
            /// <summary>Error code: 2005-0254; Inner value: 0x1fc05</summary>
            public static Result.Base SdCardContentStorageNotActive => new Result.Base(ModuleNcm, 254);
            /// <summary>Error code: 2005-0258; Inner value: 0x20405</summary>
            public static Result.Base UnknownContentStorageNotActive => new Result.Base(ModuleNcm, 258);

        /// <summary>Error code: 2005-0260; Range: 260-268; Inner value: 0x20805</summary>
        public static Result.Base ContentMetaDatabaseNotActive { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleNcm, 260, 268); }
            /// <summary>Error code: 2005-0261; Inner value: 0x20a05</summary>
            public static Result.Base GameCardContentMetaDatabaseNotActive => new Result.Base(ModuleNcm, 261);
            /// <summary>Error code: 2005-0262; Inner value: 0x20c05</summary>
            public static Result.Base BuiltInSystemContentMetaDatabaseNotActive => new Result.Base(ModuleNcm, 262);
            /// <summary>Error code: 2005-0263; Inner value: 0x20e05</summary>
            public static Result.Base BuiltInUserContentMetaDatabaseNotActive => new Result.Base(ModuleNcm, 263);
            /// <summary>Error code: 2005-0264; Inner value: 0x21005</summary>
            public static Result.Base SdCardContentMetaDatabaseNotActive => new Result.Base(ModuleNcm, 264);
            /// <summary>Error code: 2005-0268; Inner value: 0x21805</summary>
            public static Result.Base UnknownContentMetaDatabaseNotActive => new Result.Base(ModuleNcm, 268);

        /// <summary>Error code: 2005-0280; Inner value: 0x23005</summary>
        public static Result.Base IgnorableInstallTicketFailure => new Result.Base(ModuleNcm, 280);

        /// <summary>Error code: 2005-0290; Range: 290-299; Inner value: 0x24405</summary>
        public static Result.Base InstallTaskCancelled { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleNcm, 290, 299); }
            /// <summary>Error code: 2005-0291; Inner value: 0x24605</summary>
            public static Result.Base CreatePlaceHolderCancelled => new Result.Base(ModuleNcm, 291);
            /// <summary>Error code: 2005-0292; Inner value: 0x24805</summary>
            public static Result.Base WritePlaceHolderCancelled => new Result.Base(ModuleNcm, 292);

        /// <summary>Error code: 2005-0310; Inner value: 0x26c05</summary>
        public static Result.Base ContentStorageBaseNotFound => new Result.Base(ModuleNcm, 310);
        /// <summary>Error code: 2005-0330; Inner value: 0x29405</summary>
        public static Result.Base ListPartiallyNotCommitted => new Result.Base(ModuleNcm, 330);
        /// <summary>Error code: 2005-0360; Inner value: 0x2d005</summary>
        public static Result.Base UnexpectedContentMetaPrepared => new Result.Base(ModuleNcm, 360);
        /// <summary>Error code: 2005-0380; Inner value: 0x2f805</summary>
        public static Result.Base InvalidFirmwareVariation => new Result.Base(ModuleNcm, 380);

        /// <summary>Error code: 2005-8181; Range: 8181-8191; Inner value: 0x3fea05</summary>
        public static Result.Base InvalidArgument { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Result.Base(ModuleNcm, 8181, 8191); }
            /// <summary>Error code: 2005-8182; Inner value: 0x3fec05</summary>
            public static Result.Base InvalidOffset => new Result.Base(ModuleNcm, 8182);
    }
}
