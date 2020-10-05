using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
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
                deltaFile.Read(0, magic).ThrowIfFailure();

                if (MemoryMarshal.Read<uint>(magic) != Ndv0Magic)
                {
                    try
                    {
                        var nca = new Nca(ctx.KeySet, deltaStorage);
                        IFileSystem fs = nca.OpenFileSystem(0, IntegrityCheckLevel.ErrorOnInvalid);

                        if (!fs.FileExists(FragmentFileName))
                        {
                            throw new FileNotFoundException("Specified NCA does not contain a delta fragment");
                        }

                        fs.OpenFile(out IFile deltaFragmentFile, FragmentFileName.ToU8String(), OpenMode.Read).ThrowIfFailure();

                        deltaStorage = deltaFragmentFile.AsStorage();
                    }
                    catch (InvalidDataException) { } // Ignore non-NCA3 files
                }

                var delta = new Delta(deltaStorage);

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
                                patchedStorage.GetSize(out long patchedStorageSize).ThrowIfFailure();

                                patchedStorage.CopyToStream(outFile, patchedStorageSize, ctx.Logger);
                            }
                        }
                    }
                }

                ctx.Logger.LogMessage(delta.Print());
            }
        }

        private static string Print(this Delta delta)
        {
            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("Delta File:");
            PrintItem(sb, colLen, "Magic:", delta.Header.Magic);
            PrintItem(sb, colLen, "Base file size:", $"0x{delta.Header.OriginalSize:x12}");
            PrintItem(sb, colLen, "New file size:", $"0x{delta.Header.NewSize:x12}");
            PrintItem(sb, colLen, "Delta header size:", $"0x{delta.Header.HeaderSize:x12}");
            PrintItem(sb, colLen, "Delta body size:", $"0x{delta.Header.BodySize:x12}");

            return sb.ToString();
        }
    }
}
