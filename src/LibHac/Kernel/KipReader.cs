using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.Kernel
{
    public class KipReader
    {
        private IStorage KipStorage { get; set; }

        private KipHeader _header;

        public ReadOnlySpan<uint> Capabilities => _header.Capabilities;
        public U8Span Name => new U8Span(_header.Name);

        public ulong ProgramId => _header.ProgramId;
        public int Version => _header.Version;
        public byte Priority => _header.Priority;
        public byte IdealCoreId => _header.IdealCoreId;

        public bool IsTextCompressed => _header.Flags.HasFlag(KipHeader.Flag.TextCompress);
        public bool IsRoCompressed => _header.Flags.HasFlag(KipHeader.Flag.RoCompress);
        public bool IsDataCompressed => _header.Flags.HasFlag(KipHeader.Flag.DataCompress);
        public bool Is64Bit => _header.Flags.HasFlag(KipHeader.Flag.Is64BitInstruction);
        public bool Is64BitAddressSpace => _header.Flags.HasFlag(KipHeader.Flag.ProcessAddressSpace64Bit);
        public bool UsesSecureMemory => _header.Flags.HasFlag(KipHeader.Flag.UseSecureMemory);

        public ReadOnlySpan<KipHeader.SegmentHeader> Segments => _header.Segments;

        public int AffinityMask => _header.AffinityMask;
        public int StackSize => _header.StackSize;

        public Result Initialize(IStorage kipData)
        {
            if (kipData is null)
                return ResultLibHac.NullArgument.Log();

            // Verify there's enough data to read the header
            Result rc = kipData.GetSize(out long kipSize);
            if (rc.IsFailure()) return rc;

            if (kipSize < Unsafe.SizeOf<KipHeader>())
                return ResultLibHac.InvalidKipFileSize.Log();

            rc = kipData.Read(0, SpanHelpers.AsByteSpan(ref _header));
            if (rc.IsFailure()) return rc;

            if (!_header.IsValid)
                return ResultLibHac.InvalidKipMagic.Log();

            KipStorage = kipData;
            return Result.Success;
        }

        /// <summary>
        /// Gets the raw input KIP file.
        /// </summary>
        /// <param name="kipData">If the operation returns successfully, an <see cref="IStorage"/>
        /// containing the KIP data.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        public Result GetRawData(out IStorage kipData)
        {
            UnsafeHelpers.SkipParamInit(out kipData);

            int kipFileSize = GetFileSize();

            Result rc = KipStorage.GetSize(out long inputFileSize);
            if (rc.IsFailure()) return rc;

            // Verify the input KIP file isn't truncated
            if (inputFileSize < kipFileSize)
                return ResultLibHac.InvalidKipFileSize.Log();

            kipData = new SubStorage(KipStorage, 0, kipFileSize);
            return Result.Success;
        }

        public Result GetSegmentSize(SegmentType segment, out int size)
        {
            UnsafeHelpers.SkipParamInit(out size);

            switch (segment)
            {
                case SegmentType.Text:
                case SegmentType.Ro:
                case SegmentType.Data:
                case SegmentType.Bss:
                case SegmentType.Reserved1:
                case SegmentType.Reserved2:
                    size = _header.Segments[(int)segment].Size;
                    return Result.Success;
                default:
                    return ResultLibHac.ArgumentOutOfRange.Log();
            }
        }

        public int GetFileSize()
        {
            int size = Unsafe.SizeOf<KipHeader>();

            for (int i = 0; i < Segments.Length; i++)
            {
                size += Segments[i].FileSize;
            }

            return size;
        }

        public int GetUncompressedSize()
        {
            int size = Unsafe.SizeOf<KipHeader>();

            for (int i = 0; i < Segments.Length; i++)
            {
                if (Segments[i].FileSize != 0)
                {
                    size += Segments[i].Size;
                }
            }

            return size;
        }

        public Result ReadSegment(SegmentType segment, Span<byte> buffer)
        {
            Result rc = GetSegmentSize(segment, out int segmentSize);
            if (rc.IsFailure()) return rc;

            if (buffer.Length < segmentSize)
                return ResultLibHac.BufferTooSmall.Log();

            KipHeader.SegmentHeader segmentHeader = Segments[(int)segment];

            // Return early for empty segments.
            if (segmentHeader.Size == 0)
                return Result.Success;

            // The segment is all zeros if it has no data.
            if (segmentHeader.FileSize == 0)
            {
                buffer.Slice(0, segmentHeader.Size).Clear();
                return Result.Success;
            }

            int offset = CalculateSegmentOffset((int)segment);

            // Verify the segment offset is in-range
            rc = KipStorage.GetSize(out long kipSize);
            if (rc.IsFailure()) return rc;

            if (kipSize < offset + segmentHeader.FileSize)
                return ResultLibHac.InvalidKipFileSize.Log();

            // Read the segment data.
            rc = KipStorage.Read(offset, buffer.Slice(0, segmentHeader.FileSize));
            if (rc.IsFailure()) return rc;

            // Decompress if necessary.
            bool isCompressed = segment switch
            {
                SegmentType.Text => IsTextCompressed,
                SegmentType.Ro => IsRoCompressed,
                SegmentType.Data => IsDataCompressed,
                _ => false
            };

            if (isCompressed)
            {
                rc = DecompressBlz(buffer, segmentHeader.FileSize);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public Result ReadUncompressedKip(Span<byte> buffer)
        {
            if (buffer.Length < GetUncompressedSize())
                return ResultLibHac.BufferTooSmall.Log();

            Span<byte> segmentBuffer = buffer.Slice(Unsafe.SizeOf<KipHeader>());

            // Read each of the segments into the buffer.
            for (int i = 0; i < Segments.Length; i++)
            {
                if (Segments[i].FileSize != 0)
                {
                    Result rc = ReadSegment((SegmentType)i, segmentBuffer);
                    if (rc.IsFailure()) return rc;

                    segmentBuffer = segmentBuffer.Slice(Segments[i].Size);
                }
            }

            // Copy the header to the buffer and update the sizes and flags.
            ref KipHeader header = ref Unsafe.As<byte, KipHeader>(ref MemoryMarshal.GetReference(buffer));
            header = _header;

            // Remove any compression flags.
            const KipHeader.Flag compressFlagsMask =
                ~(KipHeader.Flag.TextCompress | KipHeader.Flag.RoCompress | KipHeader.Flag.DataCompress);

            header.Flags &= compressFlagsMask;

            // Update each segment's uncompressed size.
            foreach (ref KipHeader.SegmentHeader segment in header.Segments)
            {
                if (segment.FileSize != 0)
                {
                    segment.FileSize = segment.Size;
                }
            }

            return Result.Success;
        }

        private int CalculateSegmentOffset(int index)
        {
            Debug.Assert((uint)index <= (uint)SegmentType.Reserved2);

            int offset = Unsafe.SizeOf<KipHeader>();
            ReadOnlySpan<KipHeader.SegmentHeader> segments = Segments;

            for (int i = 0; i < index; i++)
            {
                offset += segments[i].FileSize;
            }

            return offset;
        }

        private static Result DecompressBlz(Span<byte> buffer, int compressedDataSize)
        {
            const int segmentFooterSize = 12;

            if (buffer.Length < segmentFooterSize)
                return ResultLibHac.InvalidKipSegmentSize.Log();

            // Parse the footer, endian agnostic.
            Span<byte> footer = buffer.Slice(compressedDataSize - segmentFooterSize);
            int totalCompSize = BinaryPrimitives.ReadInt32LittleEndian(footer);
            int footerSize = BinaryPrimitives.ReadInt32LittleEndian(footer.Slice(4));
            int additionalSize = BinaryPrimitives.ReadInt32LittleEndian(footer.Slice(8));

            if (buffer.Length < totalCompSize + additionalSize)
                return ResultLibHac.BufferTooSmall.Log();

            Span<byte> data = buffer.Slice(compressedDataSize - totalCompSize);

            int inOffset = totalCompSize - footerSize;
            int outOffset = totalCompSize + additionalSize;

            while (outOffset != 0)
            {
                byte control = data[--inOffset];

                // Each bit in the control byte is a flag indicating compressed or not compressed.
                for (int i = 0; i < 8; i++)
                {
                    if ((control & 0x80) != 0)
                    {
                        if (inOffset < 2)
                            return ResultLibHac.KipSegmentDecompressionFailed.Log();

                        inOffset -= 2;
                        ushort segmentValue = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(inOffset));
                        int segmentOffset = (segmentValue & 0x0FFF) + 3;
                        int segmentSize = Math.Min(((segmentValue >> 12) & 0xF) + 3, outOffset);

                        outOffset -= segmentSize;

                        for (int j = 0; j < segmentSize; j++)
                        {
                            data[outOffset + j] = data[outOffset + segmentOffset + j];
                        }
                    }
                    else
                    {
                        if (inOffset < 1)
                            return ResultLibHac.KipSegmentDecompressionFailed.Log();

                        // Copy directly.
                        data[--outOffset] = data[--inOffset];
                    }
                    control <<= 1;

                    if (outOffset == 0)
                        return Result.Success;
                }
            }

            return Result.Success;
        }

        public enum SegmentType
        {
            Text = 0,
            Ro = 1,
            Data = 2,
            Bss = 3,
            Reserved1 = 4,
            Reserved2 = 5
        }
    }
}
