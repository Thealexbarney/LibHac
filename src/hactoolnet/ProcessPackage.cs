using System.IO;
using System.Text;
using LibHac;
using LibHac.Boot;
using LibHac.Fs;
using LibHac.FsSystem;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessPackage
    {
        public static void ProcessPk11(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var package1 = new Package1(ctx.Keyset, file);
                string outDir = ctx.Options.OutDir;

                if (outDir != null)
                {
                    Directory.CreateDirectory(outDir);

                    package1.Pk11.OpenWarmboot().WriteAllBytes(Path.Combine(outDir, "Warmboot.bin"), ctx.Logger);
                    package1.Pk11.OpenNxBootloader().WriteAllBytes(Path.Combine(outDir, "NX_Bootloader.bin"), ctx.Logger);
                    package1.Pk11.OpenSecureMonitor().WriteAllBytes(Path.Combine(outDir, "Secure_Monitor.bin"), ctx.Logger);
                    package1.OpenDecryptedPackage().WriteAllBytes(Path.Combine(outDir, "Decrypted.bin"), ctx.Logger);
                }
            }
        }

        public static void ProcessPk21(Context ctx)
        {
            using (var file = new CachedStorage(new LocalStorage(ctx.Options.InFile, FileAccess.Read), 0x4000, 4, false))
            {
                var package2 = new Package2StorageReader();
                package2.Initialize(ctx.Keyset, file).ThrowIfFailure();

                ctx.Logger.LogMessage(package2.Print());

                string outDir = ctx.Options.OutDir;
                string iniDir = ctx.Options.Ini1OutDir;

                if (iniDir == null && ctx.Options.ExtractIni1)
                {
                    iniDir = Path.Combine(outDir, "INI1");
                }

                if (outDir != null)
                {
                    Directory.CreateDirectory(outDir);

                    package2.OpenPayload(out IStorage kernelStorage, 0).ThrowIfFailure();
                    kernelStorage.WriteAllBytes(Path.Combine(outDir, "Kernel.bin"), ctx.Logger);

                    package2.OpenIni(out IStorage ini1Storage).ThrowIfFailure();
                    ini1Storage.WriteAllBytes(Path.Combine(outDir, "INI1.bin"), ctx.Logger);

                    package2.OpenDecryptedPackage(out IStorage decPackageStorage).ThrowIfFailure();
                    decPackageStorage.WriteAllBytes(Path.Combine(outDir, "Decrypted.bin"), ctx.Logger);
                }

                if (iniDir != null)
                {
                    Directory.CreateDirectory(iniDir);

                    package2.OpenIni(out IStorage ini1Storage).ThrowIfFailure();

                    ProcessKip.ExtractIni1(ini1Storage, iniDir);
                }
            }
        }

        private static readonly string[] Package2SectionNames = { "Kernel", "INI1", "Empty" };

        private static string Print(this Package2StorageReader package2)
        {
            Result rc = package2.VerifySignature();

            Validity signatureValidity = rc.IsSuccess() ? Validity.Valid : Validity.Invalid;

            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("PK21:");
            PrintItem(sb, colLen, $"Signature{signatureValidity.GetValidityString()}:", package2.Header.Signature.ToArray());
            PrintItem(sb, colLen, "Header Version:", $"{package2.Header.Meta.KeyGeneration:x2}");

            for (int i = 0; i < 3; i++)
            {
                string name = package2.Header.Meta.PayloadSizes[i] != 0 ? Package2SectionNames[i] : "Empty";
                sb.AppendLine($"Section {i} ({name}):");

                PrintItem(sb, colLen, "    Hash:", package2.Header.Meta.PayloadHashes[i]);
                PrintItem(sb, colLen, "    CTR:", package2.Header.Meta.PayloadIvs[i]);
                PrintItem(sb, colLen, "    Load Address:", $"{package2.Header.Meta.PayloadOffsets[i] + 0x80000000:x8}");
                PrintItem(sb, colLen, "    Size:", $"{package2.Header.Meta.PayloadSizes[i]:x8}");
            }

            return sb.ToString();
        }
    }
}
