using System.IO;
using LibHac;
using LibHac.FsSystem;
using LibHac.Loader;

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
                    var uncompressed = new byte[kip.GetUncompressedSize()];

                    kip.ReadUncompressedKip(uncompressed).ThrowIfFailure();
                    
                    File.WriteAllBytes(ctx.Options.UncompressedOut, uncompressed);
                }
            }
        }

        public static void ProcessIni1(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var ini1 = new Ini1(file);

                string outDir = ctx.Options.OutDir;

                if (outDir != null)
                {
                    Directory.CreateDirectory(outDir);

                    foreach (KipReader kip in ini1.Kips)
                    {
                        var uncompressed = new byte[kip.GetUncompressedSize()];

                        kip.ReadUncompressedKip(uncompressed).ThrowIfFailure();

                        File.WriteAllBytes(Path.Combine(outDir, $"{kip.Name.ToString()}.kip1"), uncompressed);
                    }
                }
            }
        }
    }
}
