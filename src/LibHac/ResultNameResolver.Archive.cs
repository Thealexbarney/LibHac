using System;

namespace LibHac
{
    internal partial class ResultNameResolver
    {
        private static ReadOnlySpan<byte> ArchiveData => new byte[]
        {
            // This array will be populated when the build script is run.

            // The script can be run with the "codegen" option to run only the
            // code generation portion of the build.
        };
    }
}
