using System.IO;
using LibHac.IO;
using LibHac.IO.RomFs;

namespace hactoolnet
{
    internal static class ProcessFsBuild
    {
        public static void ProcessRomFs(Context ctx)
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

        public static void ProcessPartitionFs(Context ctx)
        {
            if (ctx.Options.OutFile == null)
            {
                ctx.Logger.LogMessage("Output file must be specified.");
                return;
            }

            PartitionFileSystemType type = ctx.Options.BuildHfs
                ? PartitionFileSystemType.Hashed
                : PartitionFileSystemType.Standard;

            var localFs = new LocalFileSystem(ctx.Options.InFile);

            var builder = new PartitionFileSystemBuilder(localFs);
            IStorage partitionFs = builder.Build(type);

            ctx.Logger.LogMessage($"Building Partition FS as {ctx.Options.OutFile}");

            using (var outFile = new FileStream(ctx.Options.OutFile, FileMode.Create, FileAccess.ReadWrite))
            {
                partitionFs.CopyToStream(outFile, partitionFs.Length, ctx.Logger);
            }

            ctx.Logger.LogMessage($"Finished writing {ctx.Options.OutFile}");
        }
    }
}
