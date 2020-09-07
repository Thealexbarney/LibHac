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

namespace LibHac.Lr
{
    public static class ResultLr
    {
        public const int ModuleLr = 8;

        /// <summary>Error code: 2008-0002; Inner value: 0x408</summary>
        public static Result.Base ProgramNotFound => new Result.Base(ModuleLr, 2);
        /// <summary>Error code: 2008-0003; Inner value: 0x608</summary>
        public static Result.Base DataNotFound => new Result.Base(ModuleLr, 3);
        /// <summary>Error code: 2008-0004; Inner value: 0x808</summary>
        public static Result.Base UnknownStorageId => new Result.Base(ModuleLr, 4);
        /// <summary>Error code: 2008-0005; Inner value: 0xa08</summary>
        public static Result.Base LocationResolverNotFound => new Result.Base(ModuleLr, 5);
        /// <summary>Error code: 2008-0006; Inner value: 0xc08</summary>
        public static Result.Base HtmlDocumentNotFound => new Result.Base(ModuleLr, 6);
        /// <summary>Error code: 2008-0007; Inner value: 0xe08</summary>
        public static Result.Base AddOnContentNotFound => new Result.Base(ModuleLr, 7);
        /// <summary>Error code: 2008-0008; Inner value: 0x1008</summary>
        public static Result.Base ControlNotFound => new Result.Base(ModuleLr, 8);
        /// <summary>Error code: 2008-0009; Inner value: 0x1208</summary>
        public static Result.Base LegalInformationNotFound => new Result.Base(ModuleLr, 9);
        /// <summary>Error code: 2008-0010; Inner value: 0x1408</summary>
        public static Result.Base DebugProgramNotFound => new Result.Base(ModuleLr, 10);
        /// <summary>Error code: 2008-0090; Inner value: 0xb408</summary>
        public static Result.Base TooManyRegisteredPaths => new Result.Base(ModuleLr, 90);
    }
}
