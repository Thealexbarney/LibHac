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

namespace LibHac.FsService
{
    public static class ResultSdmmc
    {
        public const int ModuleSdmmc = 24;

        /// <summary>Error code: 2024-0001; Inner value: 0x218</summary>
        public static Result.Base DeviceNotFound => new Result.Base(ModuleSdmmc, 1);
        /// <summary>Error code: 2024-0004; Inner value: 0x818</summary>
        public static Result.Base DeviceAsleep => new Result.Base(ModuleSdmmc, 4);
    }
}
