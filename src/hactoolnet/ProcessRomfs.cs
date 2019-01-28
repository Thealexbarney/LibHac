using System.IO;
using LibHac.IO;

namespace hactoolnet
{
    internal static class ProcessRomfs
    {
        public static void Process(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var romfs = new RomFsFileSystem(file);
                Process(ctx, romfs);
            }
        }

        public static void Process(Context ctx, RomFsFileSystem romfs)
        {
            if (ctx.Options.ListRomFs)
            {
                foreach (DirectoryEntry entry in romfs.EnumerateEntries())
                {
                    ctx.Logger.LogMessage(entry.FullPath);
                }
            }

            if (ctx.Options.RomfsOut != null)
            {
                using (var outFile = new FileStream(ctx.Options.RomfsOut, FileMode.Create, FileAccess.ReadWrite))
                {
                    IStorage romfsStorage = romfs.GetBaseStorage();
                    romfsStorage.CopyToStream(outFile, romfsStorage.Length, ctx.Logger);
                }
            }

            if (ctx.Options.RomfsOutDir != null)
            {
                romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
            }
        }
    }
}
