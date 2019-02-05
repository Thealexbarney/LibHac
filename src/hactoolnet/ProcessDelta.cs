using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LibHac;
using LibHac.IO;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessDelta
    {
        private const uint Ndv0Magic = 0x3056444E;
        private const string FragmentFileName = "fragment";

        public static void Process(Context ctx)
        {
            using (IStorage deltaFile = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                IStorage deltaStorage = deltaFile;
                Span<byte> magic = stackalloc byte[4];
                deltaFile.Read(magic, 0);

                if (MemoryMarshal.Read<uint>(magic) != Ndv0Magic)
                {
                    try
                    {
                        var nca = new Nca(ctx.Keyset, deltaStorage, true);
                        IFileSystem fs = nca.OpenSectionFileSystem(0, IntegrityCheckLevel.ErrorOnInvalid);

                        if (!fs.FileExists(FragmentFileName))
                        {
                            throw new FileNotFoundException("Specified NCA does not contain a delta fragment");
                        }

                        deltaStorage = fs.OpenFile(FragmentFileName, OpenMode.Read).AsStorage();
                    }
                    catch (InvalidDataException) { } // Ignore non-NCA3 files
                }

                var delta = new DeltaFragment(deltaStorage);

                if (ctx.Options.BaseFile != null)
                {
                    using (IStorage baseFile = new LocalStorage(ctx.Options.BaseFile, FileAccess.Read))
                    {
                        delta.SetBaseStorage(baseFile);

                        if (ctx.Options.OutFile != null)
                        {
                            using (var outFile = new FileStream(ctx.Options.OutFile, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                            {
                                IStorage patchedStorage = delta.GetPatchedStorage();
                                patchedStorage.CopyToStream(outFile, patchedStorage.Length, ctx.Logger);
                            }
                        }
                    }
                }

                ctx.Logger.LogMessage(delta.Print());
            }
        }

        private static string Print(this DeltaFragment delta)
        {
            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("Delta Fragment:");
            PrintItem(sb, colLen, "Magic:", delta.Header.Magic);
            PrintItem(sb, colLen, "Base file size:", $"0x{delta.Header.OriginalSize:x12}");
            PrintItem(sb, colLen, "New file size:", $"0x{delta.Header.NewSize:x12}");
            PrintItem(sb, colLen, "Fragment header size:", $"0x{delta.Header.FragmentHeaderSize:x12}");
            PrintItem(sb, colLen, "Fragment body size:", $"0x{delta.Header.FragmentBodySize:x12}");

            return sb.ToString();
        }
    }
}
