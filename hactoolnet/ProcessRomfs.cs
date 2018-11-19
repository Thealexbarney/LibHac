using System.IO;
using LibHac;
using LibHac.IO;

namespace hactoolnet
{
    internal static class ProcessRomfs
    {
        public static void Process(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var romfs = new Romfs(file.AsStorage());
                Process(ctx, romfs);
            }
        }

        public static void Process(Context ctx, Romfs romfs)
        {
            if (ctx.Options.ListRomFs)
            {
                foreach (RomfsFile romfsFile in romfs.Files)
                {
                    ctx.Logger.LogMessage(romfsFile.FullPath);
                }
            }

            if (ctx.Options.RomfsOut != null)
            {
                using (var outFile = new FileStream(ctx.Options.RomfsOut, FileMode.Create, FileAccess.ReadWrite))
                {
                    IStorage romfsStorage = romfs.OpenRawStream();
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
