using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using LibHac;
using LibHac.Boot;
using LibHac.Common;
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
                var package1 = new LibHac.Boot.Package1();
                package1.Initialize(ctx.KeySet, file).ThrowIfFailure();

                ctx.Logger.LogMessage(package1.Print());

                string outDir = ctx.Options.OutDir;

                if (package1.IsDecrypted && outDir != null)
                {
                    Directory.CreateDirectory(outDir);

                    IStorage decryptedStorage = package1.OpenDecryptedPackage1Storage();

                    WriteFile(decryptedStorage, "Decrypted.bin");
                    WriteFile(package1.OpenWarmBootStorage(), "Warmboot.bin");
                    WriteFile(package1.OpenNxBootloaderStorage(), "NX_Bootloader.bin");
                    WriteFile(package1.OpenSecureMonitorStorage(), "Secure_Monitor.bin");

                    if (package1.IsMariko)
                    {
                        WriteFile(package1.OpenDecryptedWarmBootStorage(), "Warmboot_Decrypted.bin");

                        var marikoOemLoader = new SubStorage(decryptedStorage, Unsafe.SizeOf<Package1MarikoOemHeader>(),
                            package1.MarikoOemHeader.Size);

                        WriteFile(marikoOemLoader, "Mariko_OEM_Bootloader.bin");
                    }
                }

                void WriteFile(IStorage storage, string filename)
                {
                    string path = Path.Combine(outDir, filename);
                    ctx.Logger.LogMessage($"Writing {path}...");
                    storage.WriteAllBytes(path, ctx.Logger);
                }
            }
        }

        private static string Print(this LibHac.Boot.Package1 package1)
        {
            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            if (package1.IsMariko)
            {
                sb.AppendLine("Mariko OEM Header:");
                PrintItem(sb, colLen, "    Signature:", package1.MarikoOemHeader.RsaSig.ToArray());
                PrintItem(sb, colLen, "    Random Salt:", package1.MarikoOemHeader.Salt.ToArray());
                PrintItem(sb, colLen, "    OEM Bootloader Hash:", package1.MarikoOemHeader.Hash.ToArray());
                PrintItem(sb, colLen, "    OEM Bootloader Version:", $"{package1.MarikoOemHeader.Version:x2}");
                PrintItem(sb, colLen, "    OEM Bootloader Size:", $"{package1.MarikoOemHeader.Size:x8}");
                PrintItem(sb, colLen, "    OEM Bootloader Load Address:", $"{package1.MarikoOemHeader.LoadAddress:x8}");
                PrintItem(sb, colLen, "    OEM Bootloader Entrypoint:", $"{package1.MarikoOemHeader.EntryPoint:x8}");
            }

            sb.AppendLine("Package1 Metadata:");
            PrintItem(sb, colLen, "    Build Date:", package1.MetaData.BuildDate.ToString());
            PrintItem(sb, colLen, "    Package1ldr Hash:", SpanHelpers.AsReadOnlyByteSpan(in package1.MetaData.LoaderHash).ToArray());
            PrintItem(sb, colLen, "    Secure Monitor Hash:", SpanHelpers.AsReadOnlyByteSpan(in package1.MetaData.SecureMonitorHash).ToArray());
            PrintItem(sb, colLen, "    NX Bootloader Hash:", SpanHelpers.AsReadOnlyByteSpan(in package1.MetaData.BootloaderHash).ToArray());
            PrintItem(sb, colLen, "    Version:", $"{package1.MetaData.Version:x2}");

            if (!package1.IsMariko && package1.IsModern)
            {
                PrintItem(sb, colLen, "    PK11 MAC:", package1.Pk11Mac);
            }

            if (package1.IsDecrypted)
            {
                sb.AppendLine("PK11:");

                if (!package1.IsMariko)
                {
                    PrintItem(sb, colLen, "    Key Revision:", $"{package1.KeyRevision:x2} ({Utilities.GetKeyRevisionSummary(package1.KeyRevision)})");
                }

                PrintItem(sb, colLen, "    PK11 Size:", $"{package1.Pk11Size:x8}");
                PrintItem(sb, colLen, "    Warmboot.bin Size:", $"{package1.GetSectionSize(Package1Section.WarmBoot):x8}");
                PrintItem(sb, colLen, "    NX_Bootloader.bin Size:", $"{package1.GetSectionSize(Package1Section.Bootloader):x8}");
                PrintItem(sb, colLen, "    Secure_Monitor.bin Size:", $"{package1.GetSectionSize(Package1Section.SecureMonitor):x8}");
            }

            return sb.ToString();
        }

        public static void ProcessPk21(Context ctx)
        {
            using (var file = new CachedStorage(new LocalStorage(ctx.Options.InFile, FileAccess.Read), 0x4000, 4, false))
            {
                var package2 = new Package2StorageReader();
                package2.Initialize(ctx.KeySet, file).ThrowIfFailure();

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
