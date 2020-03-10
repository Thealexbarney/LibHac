using LibHac.Common;

namespace LibHac.Fs
{
    internal static class MountHelpers
    {
        public static Result CheckMountName(U8Span name)
        {
            if (name.IsNull()) return ResultFs.NullptrArgument.Log();

            if (name.Length > 0 && name[0] == '@') return ResultFs.InvalidMountName.Log();
            if (!CheckMountNameImpl(name)) return ResultFs.InvalidMountName.Log();

            return Result.Success;
        }

        public static Result CheckMountNameAcceptingReservedMountName(U8Span name)
        {
            if (name.IsNull()) return ResultFs.NullptrArgument.Log();

            if (!CheckMountNameImpl(name)) return ResultFs.InvalidMountName.Log();

            return Result.Success;
        }

        // ReSharper disable once UnusedParameter.Local
        private static bool CheckMountNameImpl(U8Span name)
        {
            // Todo
            return true;
        }
    }
}
