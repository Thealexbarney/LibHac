using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;

#if DEBUG
using System.Diagnostics;
#endif

namespace LibHac.FsSrv.Sf
{
    [StructLayout(LayoutKind.Sequential, Size = PathTool.EntryNameLengthMax + 1)]
    public readonly struct Path
    {
#if DEBUG
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding000;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding100;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly Padding100 Padding200;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)] private readonly byte Padding300;
#endif

        public ReadOnlySpan<byte> Str => SpanHelpers.AsReadOnlyByteSpan(in this);
    }
}
