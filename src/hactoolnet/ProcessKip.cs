using System.IO;
using LibHac;
using LibHac.FsSystem;

namespace hactoolnet
{
    internal static class ProcessKip
    {
        public static void ProcessKip1(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var kip = new Kip(file);
                kip.OpenRawFile();
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
