using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.Kernel
{
    public static class IniExtract
    {
        /// <summary>
        /// Locates and returns the offset and size of the initial process binary embedded in the kernel.
        /// The INI is only embedded in the kernel in system versions >= 8.0.0.
        /// </summary>
        /// <param name="offset">When this method returns, contains the offset of
        /// the INI inside the kernel if it was found.</param>
        /// <param name="size">When this method returns, contains the size of the INI if it was found.</param>
        /// <param name="kernelStorage">An <see cref="IStorage"/> containing the kernel to search.</param>
        /// <returns><see langword="true"/> if the embedded INI was found.</returns>
        public static bool TryGetIni1Offset(out int offset, out int size, IStorage kernelStorage)
        {
            offset = 0;
            size = 0;

            if (kernelStorage.GetSize(out long kernelSizeLong).IsFailure())
                return false;

            uint kernelSize = (uint)kernelSizeLong;

            using (var array = new RentedArray<byte>(0x1000 + Unsafe.SizeOf<KernelMap>()))
            {
                // The kernel map should be in the first 0x1000 bytes
                if (kernelStorage.Read(0, array.Span).IsFailure())
                    return false;

                ref byte start = ref array.Span[0];

                // Search every 4 bytes for a valid kernel map
                for (int i = 0; i < 0x1000; i += sizeof(int))
                {
                    ref KernelMap map = ref Unsafe.As<byte, KernelMap>(ref Unsafe.Add(ref start, i));

                    if (IsValidKernelMap(in map, kernelSize))
                    {
                        // Verify the ini header at the offset in the found map
                        var header = new InitialProcessBinaryReader.IniHeader();

                        if (kernelStorage.Read(map.Ini1StartOffset, SpanHelpers.AsByteSpan(ref header)).IsFailure())
                            return false;

                        if (header.Magic != InitialProcessBinaryReader.ExpectedMagic)
                            return false;

                        offset = (int)map.Ini1StartOffset;
                        size = header.Size;
                        return true;
                    }
                }

                return false;
            }
        }

        private static bool IsValidKernelMap(in KernelMap map, uint maxSize)
        {
            if (map.TextStartOffset != 0) return false;
            if (map.TextStartOffset >= map.TextEndOffset) return false;
            if ((map.TextEndOffset & 0xFFF) != 0) return false;
            if (map.TextEndOffset > map.RodataStartOffset) return false;
            if ((map.RodataStartOffset & 0xFFF) != 0) return false;
            if (map.RodataStartOffset >= map.RodataEndOffset) return false;
            if ((map.RodataEndOffset & 0xFFF) != 0) return false;
            if (map.RodataEndOffset > map.DataStartOffset) return false;
            if ((map.DataStartOffset & 0xFFF) != 0) return false;
            if (map.DataStartOffset >= map.DataEndOffset) return false;
            if (map.DataEndOffset > map.BssStartOffset) return false;
            if (map.BssStartOffset > map.BssEndOffset) return false;
            if (map.BssEndOffset > map.Ini1StartOffset) return false;
            if (map.Ini1StartOffset > maxSize - Unsafe.SizeOf<KernelMap>()) return false;

            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KernelMap
        {
            public uint TextStartOffset;
            public uint TextEndOffset;
            public uint RodataStartOffset;
            public uint RodataEndOffset;
            public uint DataStartOffset;
            public uint DataEndOffset;
            public uint BssStartOffset;
            public uint BssEndOffset;
            public uint Ini1StartOffset;
            public uint DynamicOffset;
            public uint InitArrayStartOffset;
            public uint InitArrayEndOffset;
        }
    }
}
