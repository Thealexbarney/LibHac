using System.IO;
using LibHac;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Loader;

namespace hactoolnet
{
    internal static class ProcessKip
    {
        public static void ProcessKip1(Context ctx)
        {
            using (var file = new LocalFile(ctx.Options.InFile, OpenMode.Read))
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

                    foreach (Kip kip in ini1.Kips)
                    {
                        kip.OpenRawFile().WriteAllBytes(Path.Combine(outDir, $"{kip.Header.Name}.kip1"));
                    }
                }
            }
        }
    }
}
