using System.IO;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.RomFs;

namespace hactoolnet
{
    internal static class ProcessRomfs
    {
        public static void Process(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                Process(ctx, file);
            }
        }

        public static void Process(Context ctx, IStorage romfsStorage)
        {
            var romfs = new RomFsFileSystem(romfsStorage);

            if (ctx.Options.ListRomFs)
            {
                foreach (DirectoryEntryEx entry in romfs.EnumerateEntries())
                {
                    ctx.Logger.LogMessage(entry.FullPath);
                }
            }

            if (ctx.Options.RomfsOut != null)
            {
                romfsStorage.GetSize(out long romFsSize).ThrowIfFailure();

                using (var outFile = new FileStream(ctx.Options.RomfsOut, FileMode.Create, FileAccess.ReadWrite))
                {
                    romfsStorage.CopyToStream(outFile, romFsSize, ctx.Logger);
                }
            }

            if (ctx.Options.RomfsOutDir != null)
            {
                romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
            }
        }
    }
}
