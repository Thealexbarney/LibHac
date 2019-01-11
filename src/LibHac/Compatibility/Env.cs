using System;

namespace LibHac.Compatibility
{
    /// <summary>
    /// Contains variables describing runtime environment info
    /// needed for compatibility code.
    /// </summary>
    internal static class Env
    {
        public static bool IsMono { get; } = Type.GetType("Mono.Runtime") != null;
    }
}
