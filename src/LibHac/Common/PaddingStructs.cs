using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LibHac.Common
{
    // In order for the Visual Studio debugger to accurately display a struct, every offset
    // in the struct that is used for the debugger display must be part of a field.
    // These padding structs make it easier to accomplish that.
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    internal struct Padding10
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong Padding00;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong Padding08;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    internal struct Padding20
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong Padding00;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong Padding08;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong Padding10;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly ulong Padding18;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x40)]
    internal struct Padding40
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding20 Padding00;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding20 Padding20;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x80)]
    internal struct Padding80
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding40 Padding00;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding40 Padding40;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x100)]
    internal struct Padding100
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding80 Padding00;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding80 Padding80;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x200)]
    internal struct Padding200
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding000;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding100;
    }
}
