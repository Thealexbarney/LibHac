using System.IO;
using LibHac.IO;
using LibHac.IO.RomFs;

namespace hactoolnet
{
    internal static class ProcessRomFsBuild
    {
        public static void Process(Context ctx)
        {
            if (ctx.Options.OutFile == null)
            {
                ctx.Logger.LogMessage("Output file must be specified.");
                return;
            }

            var localFs = new LocalFileSystem(ctx.Options.InFile);

            var builder = new RomFsBuilder(localFs);
            IStorage romfs = builder.Build();

            ctx.Logger.LogMessage($"Building RomFS as {ctx.Options.OutFile}");

            using (var outFile = new FileStream(ctx.Options.OutFile, FileMode.Create, FileAccess.ReadWrite))
            {
                romfs.CopyToStream(outFile, romfs.Length, ctx.Logger);
            }

            ctx.Logger.LogMessage($"Finished writing {ctx.Options.OutFile}");
        }
    }
}
