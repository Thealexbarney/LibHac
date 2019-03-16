using System.IO;
using System.Text;
using LibHac;
using LibHac.IO;
using LibHac.IO.NcaUtils;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessNca
    {
        public static void Process(Context ctx)
        {
            using (IStorage file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var nca = new Nca(ctx.Keyset, file, false);

                if (ctx.Options.HeaderOut != null)
                {
                    using (var outHeader = new FileStream(ctx.Options.HeaderOut, FileMode.Create, FileAccess.ReadWrite))
                    {
                        nca.OpenHeaderStorage().Slice(0, 0xc00).CopyToStream(outHeader);
                    }
                }

                nca.ValidateMasterHashes();
                nca.ParseNpdm();

                if (ctx.Options.BaseNca != null)
                {
                    IStorage baseFile = new LocalStorage(ctx.Options.BaseNca, FileAccess.Read);
                    var baseNca = new Nca(ctx.Keyset, baseFile, false);
                    nca.SetBaseNca(baseNca);
                }

                for (int i = 0; i < 3; i++)
                {
                    if (ctx.Options.SectionOut[i] != null)
                    {
                        nca.ExportSection(i, ctx.Options.SectionOut[i], ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.SectionOutDir[i] != null)
                    {
                        nca.ExtractSection(i, ctx.Options.SectionOutDir[i], ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.Validate && nca.SectionExists(i))
                    {
                        nca.VerifySection(i, ctx.Logger);
                    }
                }

                if (ctx.Options.ListRomFs && nca.CanOpenSection(NcaSectionType.Data))
                {
                    IFileSystem romfs = nca.OpenFileSystem(NcaSectionType.Data, ctx.Options.IntegrityLevel);

                    foreach (DirectoryEntry entry in romfs.EnumerateEntries())
                    {
                        ctx.Logger.LogMessage(entry.FullPath);
                    }
                }

                if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null || ctx.Options.ReadBench)
                {
                    if (!nca.SectionExists(NcaSectionType.Data))
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    if (ctx.Options.RomfsOut != null)
                    {
                        nca.ExportSection(NcaSectionType.Data, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.RomfsOutDir != null)
                    {
                        nca.ExtractSection(NcaSectionType.Data, ctx.Options.RomfsOutDir, ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.ReadBench)
                    {
                        long bytesToRead = 1024L * 1024 * 1024 * 5;
                        IStorage storage = nca.OpenStorage(NcaSectionType.Data, ctx.Options.IntegrityLevel);
                        var dest = new NullStorage(storage.Length);

                        int iterations = (int)(bytesToRead / storage.Length) + 1;
                        ctx.Logger.LogMessage(iterations.ToString());

                        ctx.Logger.StartNewStopWatch();

                        for (int i = 0; i < iterations; i++)
                        {
                            storage.CopyTo(dest, ctx.Logger);
                            ctx.Logger.LogMessage(ctx.Logger.GetRateString());
                        }

                        ctx.Logger.PauseStopWatch();
                        ctx.Logger.LogMessage(ctx.Logger.GetRateString());
                    }
                }

                if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
                {
                    if (nca.Header.ContentType != ContentType.Program)
                    {
                        ctx.Logger.LogMessage("NCA's content type is not \"Program\"");
                        return;
                    }

                    if (!nca.SectionExists(NcaSectionType.Code))
                    {
                        ctx.Logger.LogMessage("Could not find an ExeFS section");
                        return;
                    }

                    if (ctx.Options.ExefsOut != null)
                    {
                        nca.ExportSection(NcaSectionType.Code, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        nca.ExtractSection(NcaSectionType.Code, ctx.Options.ExefsOutDir, ctx.Options.IntegrityLevel, ctx.Logger);
                    }
                }

                if (ctx.Options.PlaintextOut != null)
                {
                    nca.OpenDecryptedNca().WriteAllBytes(ctx.Options.PlaintextOut, ctx.Logger);
                }

                if (!ctx.Options.ReadBench) ctx.Logger.LogMessage(nca.Print());
            }
        }

        private static string Print(this Nca nca)
        {
            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("NCA:");
            PrintItem(sb, colLen, "Magic:", nca.Header.Magic);
            PrintItem(sb, colLen, $"Fixed-Key Signature{nca.Header.FixedSigValidity.GetValidityString()}:", nca.Header.Signature1);
            PrintItem(sb, colLen, $"NPDM Signature{nca.Header.NpdmSigValidity.GetValidityString()}:", nca.Header.Signature2);
            PrintItem(sb, colLen, "Content Size:", $"0x{nca.Header.NcaSize:x12}");
            PrintItem(sb, colLen, "TitleID:", $"{nca.Header.TitleId:X16}");
            PrintItem(sb, colLen, "SDK Version:", nca.Header.SdkVersion);
            PrintItem(sb, colLen, "Distribution type:", nca.Header.Distribution);
            PrintItem(sb, colLen, "Content Type:", nca.Header.ContentType);
            PrintItem(sb, colLen, "Master Key Revision:", $"{nca.CryptoType} ({Util.GetKeyRevisionSummary(nca.CryptoType)})");
            PrintItem(sb, colLen, "Encryption Type:", $"{(nca.HasRightsId ? "Titlekey crypto" : "Standard crypto")}");

            if (nca.HasRightsId)
            {
                PrintItem(sb, colLen, "Rights ID:", nca.Header.RightsId);
            }
            else
            {
                PrintItem(sb, colLen, "Key Area Encryption Key:", nca.Header.KaekInd);
                sb.AppendLine("Key Area (Encrypted):");
                for (int i = 0; i < 4; i++)
                {
                    PrintItem(sb, colLen, $"    Key {i} (Encrypted):", nca.Header.EncryptedKeys[i]);
                }

                sb.AppendLine("Key Area (Decrypted):");
                for (int i = 0; i < 4; i++)
                {
                    PrintItem(sb, colLen, $"    Key {i} (Decrypted):", nca.DecryptedKeys[i]);
                }
            }

            PrintSections();

            return sb.ToString();

            void PrintSections()
            {
                sb.AppendLine("Sections:");

                for (int i = 0; i < 4; i++)
                {
                    NcaSection sect = nca.Sections[i];
                    if (sect == null) continue;

                    bool isExefs = nca.Header.ContentType == ContentType.Program && i == 0;

                    sb.AppendLine($"    Section {i}:");
                    PrintItem(sb, colLen, "        Offset:", $"0x{sect.Offset:x12}");
                    PrintItem(sb, colLen, "        Size:", $"0x{sect.Size:x12}");
                    PrintItem(sb, colLen, "        Partition Type:", isExefs ? "ExeFS" : sect.Type.ToString());
                    PrintItem(sb, colLen, "        Section CTR:", sect.Header.Ctr);

                    switch (sect.Header.HashType)
                    {
                        case NcaHashType.Sha256:
                            PrintSha256Hash(sect);
                            break;
                        case NcaHashType.Ivfc:
                            PrintIvfcHash(sb, colLen, 8, sect.Header.IvfcInfo, IntegrityStorageType.RomFs);
                            break;
                        default:
                            sb.AppendLine("        Unknown/invalid superblock!");
                            break;
                    }
                }
            }

            void PrintSha256Hash(NcaSection sect)
            {
                Sha256Info hashInfo = sect.Header.Sha256Info;

                PrintItem(sb, colLen, $"        Master Hash{sect.MasterHashValidity.GetValidityString()}:", hashInfo.MasterHash);
                sb.AppendLine($"        Hash Table{sect.Header.Sha256Info.HashValidity.GetValidityString()}:");

                PrintItem(sb, colLen, "            Offset:", $"0x{hashInfo.HashTableOffset:x12}");
                PrintItem(sb, colLen, "            Size:", $"0x{hashInfo.HashTableSize:x12}");
                PrintItem(sb, colLen, "            Block Size:", $"0x{hashInfo.BlockSize:x}");
                PrintItem(sb, colLen, "        PFS0 Offset:", $"0x{hashInfo.DataOffset:x12}");
                PrintItem(sb, colLen, "        PFS0 Size:", $"0x{hashInfo.DataSize:x12}");
            }
        }
    }
}
