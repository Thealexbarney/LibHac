using System.IO;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Kernel;

namespace hactoolnet
{
    internal static class ProcessKip
    {
        public static void ProcessKip1(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var kip = new KipReader();
                kip.Initialize(file).ThrowIfFailure();

                if (!string.IsNullOrWhiteSpace(ctx.Options.UncompressedOut))
                {
                    byte[] uncompressed = new byte[kip.GetUncompressedSize()];

                    kip.ReadUncompressedKip(uncompressed).ThrowIfFailure();

                    File.WriteAllBytes(ctx.Options.UncompressedOut, uncompressed);
                }
            }
        }

        public static void ProcessIni1(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                string outDir = ctx.Options.OutDir;

                if (outDir != null)
                {
                    ExtractIni1(file, outDir);
                }
            }
        }

        public static void ExtractIni1(IStorage iniStorage, string outDir)
        {
            var ini1 = new InitialProcessBinaryReader();
            ini1.Initialize(iniStorage).ThrowIfFailure();

            Directory.CreateDirectory(outDir);
            var kipReader = new KipReader();

            for (int i = 0; i < ini1.ProcessCount; i++)
            {
                ini1.OpenKipStorage(out IStorage kipStorage, i).ThrowIfFailure();

                kipReader.Initialize(kipStorage).ThrowIfFailure();

                kipStorage.WriteAllBytes(Path.Combine(outDir, $"{kipReader.Name.ToString()}.kip1"));
            }
        }
    }
}
