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

namespace LibHac.Kvdb
{
    public static class ResultKvdb
    {
        public const int ModuleKvdb = 20;

        /// <summary>There is no more space in the database or the key is too long.<br/>Error code: 2020-0001; Inner value: 0x214</summary>
        public static Result.Base OutOfKeyResource => new Result.Base(ModuleKvdb, 1);
        /// <summary>Error code: 2020-0002; Inner value: 0x414</summary>
        public static Result.Base KeyNotFound => new Result.Base(ModuleKvdb, 2);
        /// <summary>Error code: 2020-0004; Inner value: 0x814</summary>
        public static Result.Base AllocationFailed => new Result.Base(ModuleKvdb, 4);
        /// <summary>Error code: 2020-0005; Inner value: 0xa14</summary>
        public static Result.Base InvalidKeyValue => new Result.Base(ModuleKvdb, 5);
        /// <summary>Error code: 2020-0006; Inner value: 0xc14</summary>
        public static Result.Base BufferInsufficient => new Result.Base(ModuleKvdb, 6);
        /// <summary>Error code: 2020-0008; Inner value: 0x1014</summary>
        public static Result.Base InvalidFileSystemState => new Result.Base(ModuleKvdb, 8);
        /// <summary>Error code: 2020-0009; Inner value: 0x1214</summary>
        public static Result.Base NotCreated => new Result.Base(ModuleKvdb, 9);
    }
}
