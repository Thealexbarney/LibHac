using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac
{
    internal partial class ResultNameResolver : Result.IResultNameResolver
    {
        private Lazy<Dictionary<Result, string>> ResultNames { get; } = new Lazy<Dictionary<Result, string>>(GetResultNames);

        public bool TryResolveName(Result result, out string name)
        {
            return ResultNames.Value.TryGetValue(result, out name);
        }

        private static Dictionary<Result, string> GetResultNames()
        {
            var archiveReader = new ResultArchiveReader(ArchiveData);
            return archiveReader.GetDictionary();
        }

        private readonly ref struct ResultArchiveReader
        {
            private readonly ReadOnlySpan<byte> _data;

            private ref HeaderStruct Header => ref Unsafe.As<byte, HeaderStruct>(ref MemoryMarshal.GetReference(_data));
            private ReadOnlySpan<byte> NameTable => _data.Slice(Header.NameTableOffset);
            private ReadOnlySpan<Element> Elements => MemoryMarshal.Cast<byte, Element>(
                _data.Slice(Unsafe.SizeOf<HeaderStruct>(), Header.ElementCount * Unsafe.SizeOf<Element>()));

            public ResultArchiveReader(ReadOnlySpan<byte> archive)
            {
                _data = archive;
            }

            public Dictionary<Result, string> GetDictionary()
            {
                var dict = new Dictionary<Result, string>();
                if (_data.Length < 8) return dict;

                ReadOnlySpan<Element> elements = Elements;

                foreach (ref readonly Element element in elements)
                {
                    var result = new Result(element.Module, element.DescriptionStart);
                    dict.Add(result, GetName(element.NameOffset).ToString());
                }

                return dict;
            }

            private U8Span GetName(int offset)
            {
                ReadOnlySpan<byte> untrimmed = NameTable.Slice(offset);
                int len = StringUtils.GetLength(untrimmed);

                return new U8Span(untrimmed.Slice(0, len));
            }

#pragma warning disable 649
            private struct HeaderStruct
            {
                public int ElementCount;
                public int NameTableOffset;
            }

            private struct Element
            {
                public int NameOffset;
                public short Module;
                public short DescriptionStart;
                public short DescriptionEnd;
            }
#pragma warning restore 649
        }
    }
}
