using System.IO;
using LibHac;

namespace hactoolnet
{
    internal static class ProcessPackage
    {
        public static void ProcessPk11(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var package1 = new Package1(ctx.Keyset, file);
                string outDir = ctx.Options.OutDir;

                if (outDir != null)
                {
                    Directory.CreateDirectory(outDir);

                    package1.Pk11.OpenWarmboot().WriteAllBytes(Path.Combine(outDir, "Warmboot.bin"), ctx.Logger);
                    package1.Pk11.OpenNxBootloader().WriteAllBytes(Path.Combine(outDir, "NX_Bootloader.bin"), ctx.Logger);
                    package1.Pk11.OpenSecureMonitor().WriteAllBytes(Path.Combine(outDir, "Secure_Monitor.bin"), ctx.Logger);

                    using (var decFile = new FileStream(Path.Combine(outDir, "Decrypted.bin"), FileMode.Create))
                    {
                        package1.OpenPackage1Ldr().CopyTo(decFile);
                        package1.Pk11.OpenDecryptedPk11().CopyTo(decFile);
                    }
                }
            }
        }
    }
}
