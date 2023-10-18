using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.Kernel;

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

        // .crt0 is located at the start of the kernel pre-17.0.0 
        // 17.0.0+ kernels start with a "b crt0" branch instruction followed by 0x7FC of zeros 
        // The kernel map in this case will contain offsets relative to itself rather than to the start of the kernel
        int crt0Offset = 0;
        bool isMapAddressRelativeToItself = false;
        
        // Check if the first 4 bytes of the kernel is a branch instruction, and get the target if it is
        ulong inst = 0;
        if (kernelStorage.Read(0, SpanHelpers.AsByteSpan(ref inst)).IsFailure())
            return false;
        
        if ((inst & 0xFFFFFFFFFF000000) == 0x0000000014000000)
        {
            crt0Offset = (int)((inst & 0x00FFFFFF) << 2);
            isMapAddressRelativeToItself = true;
        }
        
        using var array = new RentedArray<byte>(0x1000 + Unsafe.SizeOf<KernelMap>());
        if (kernelStorage.Read(crt0Offset, array.Span).IsFailure())
            return false;

        ref byte start = ref MemoryMarshal.GetReference(array.Span);
        
        // Search every 4 bytes for a valid kernel map
        for (int i = 0; i < 0x1000 - Unsafe.SizeOf<KernelMap>(); i += sizeof(int))
        {
            ref KernelMap map = ref Unsafe.As<byte, KernelMap>(ref Unsafe.Add(ref start, i));
            uint mapOffsetAdjustment = isMapAddressRelativeToItself ? (uint)(crt0Offset + i) : 0;

            if (IsValidKernelMap(in map, kernelSize, mapOffsetAdjustment))
            {
                // Verify the ini header at the offset in the found map
                var header = new InitialProcessBinaryReader.IniHeader();

                if (kernelStorage.Read(map.Ini1StartOffset + mapOffsetAdjustment, SpanHelpers.AsByteSpan(ref header)).IsFailure())
                    return false;

                if (header.Magic != InitialProcessBinaryReader.ExpectedMagic)
                    continue;

                offset = (int)(map.Ini1StartOffset + mapOffsetAdjustment);
                size = header.Size;
                return true;
            }
        }

        return false;
    }

    private static bool IsValidKernelMap(in KernelMap rawMap, uint maxSize, uint adj)
    {
        KernelMap adjustedMap = rawMap;
        adjustedMap.TextStartOffset += adj;
        adjustedMap.TextEndOffset += adj;
        adjustedMap.RodataStartOffset += adj;
        adjustedMap.RodataEndOffset += adj;
        adjustedMap.DataStartOffset += adj;
        adjustedMap.DataEndOffset += adj;
        adjustedMap.BssStartOffset += adj;
        adjustedMap.BssEndOffset += adj;
        adjustedMap.Ini1StartOffset += adj;
        adjustedMap.DynamicOffset += adj;
        adjustedMap.InitArrayStartOffset += adj;
        adjustedMap.InitArrayStartOffset += adj;

        ref KernelMap map = ref adjustedMap;

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