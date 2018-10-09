using System.IO;
using System.Linq;
using System.Text;
using LibHac;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessNca
    {
        public static void Process(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var nca = new Nca(ctx.Keyset, file, false);
                nca.ValidateMasterHashes();

                if (ctx.Options.BaseNca != null)
                {
                    var baseFile = new FileStream(ctx.Options.BaseNca, FileMode.Open, FileAccess.Read);
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

                    if (ctx.Options.Validate && nca.Sections[i] != null)
                    {
                        nca.VerifySection(i, ctx.Logger);
                    }
                }

                if (ctx.Options.ListRomFs && nca.Sections[1] != null)
                {
                    var romfs = new Romfs(nca.OpenSection(1, false, ctx.Options.IntegrityLevel));

                    foreach (RomfsFile romfsFile in romfs.Files)
                    {
                        ctx.Logger.LogMessage(romfsFile.FullPath);
                    }
                }

                if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
                {
                    NcaSection section = nca.Sections.FirstOrDefault(x => x?.Type == SectionType.Romfs || x?.Type == SectionType.Bktr);

                    if (section == null)
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    if (section.Type == SectionType.Bktr && ctx.Options.BaseNca == null)
                    {
                        ctx.Logger.LogMessage("Cannot save BKTR section without base RomFS");
                        return;
                    }

                    if (ctx.Options.RomfsOut != null)
                    {
                        nca.ExportSection(section.SectionNum, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.RomfsOutDir != null)
                    {
                        var romfs = new Romfs(nca.OpenSection(section.SectionNum, false, ctx.Options.IntegrityLevel));
                        romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                    }
                }

                if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
                {
                    NcaSection section = nca.Sections.FirstOrDefault(x => x?.IsExefs == true);

                    if (section == null)
                    {
                        ctx.Logger.LogMessage("Could not find an ExeFS section");
                        return;
                    }

                    if (ctx.Options.ExefsOut != null)
                    {
                        nca.ExportSection(section.SectionNum, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        nca.ExtractSection(section.SectionNum, ctx.Options.ExefsOutDir, ctx.Options.IntegrityLevel, ctx.Logger);
                    }
                }

                ctx.Logger.LogMessage(nca.Print());
            }
        }

        private static string Print(this Nca nca)
        {
            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("NCA:");
            PrintItem(sb, colLen, "Magic:", nca.Header.Magic);
            PrintItem(sb, colLen, "Fixed-Key Signature:", nca.Header.Signature1);
            PrintItem(sb, colLen, "NPDM Signature:", nca.Header.Signature2);
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

                    sb.AppendLine($"    Section {i}:");
                    PrintItem(sb, colLen, "        Offset:", $"0x{sect.Offset:x12}");
                    PrintItem(sb, colLen, "        Size:", $"0x{sect.Size:x12}");
                    PrintItem(sb, colLen, "        Partition Type:", sect.IsExefs ? "ExeFS" : sect.Type.ToString());
                    PrintItem(sb, colLen, "        Section CTR:", sect.Header.Ctr);

                    switch (sect.Type)
                    {
                        case SectionType.Pfs0:
                            PrintPfs0(sect);
                            break;
                        case SectionType.Romfs:
                            PrintRomfs(sect);
                            break;
                        case SectionType.Bktr:
                            break;
                        default:
                            sb.AppendLine("        Unknown/invalid superblock!");
                            break;
                    }
                }
            }

            void PrintPfs0(NcaSection sect)
            {
                Sha256Info hashInfo = sect.Header.Sha256Info;

                PrintItem(sb, colLen, $"        Superblock Hash{sect.MasterHashValidity.GetValidityString()}:", hashInfo.MasterHash);
                // todo sb.AppendLine($"        Hash Table{sect.Pfs0.Validity.GetValidityString()}:");
                sb.AppendLine($"        Hash Table:");

                PrintItem(sb, colLen, "            Offset:", $"0x{hashInfo.HashTableOffset:x12}");
                PrintItem(sb, colLen, "            Size:", $"0x{hashInfo.HashTableSize:x12}");
                PrintItem(sb, colLen, "            Block Size:", $"0x{hashInfo.BlockSize:x}");
                PrintItem(sb, colLen, "        PFS0 Offset:", $"0x{hashInfo.DataOffset:x12}");
                PrintItem(sb, colLen, "        PFS0 Size:", $"0x{hashInfo.DataSize:x12}");
            }

            void PrintRomfs(NcaSection sect)
            {
                IvfcHeader ivfcInfo = sect.Header.IvfcInfo;

                PrintItem(sb, colLen, $"        Superblock Hash{sect.MasterHashValidity.GetValidityString()}:", ivfcInfo.MasterHash);
                PrintItem(sb, colLen, "        Magic:", ivfcInfo.Magic);
                PrintItem(sb, colLen, "        Version:", $"{ivfcInfo.Version:x8}");

                for (int i = 0; i < Romfs.IvfcMaxLevel; i++)
                {
                    IvfcLevelHeader level = ivfcInfo.LevelHeaders[i];
                    long hashOffset = 0;

                    if (i != 0)
                    {
                        hashOffset = ivfcInfo.LevelHeaders[i - 1].LogicalOffset;
                    }

                    // todo  sb.AppendLine($"        Level {i}{level.HashValidity.GetValidityString()}:");
                    sb.AppendLine($"        Level {i}:");
                    PrintItem(sb, colLen, "            Data Offset:", $"0x{level.LogicalOffset:x12}");
                    PrintItem(sb, colLen, "            Data Size:", $"0x{level.HashDataSize:x12}");
                    PrintItem(sb, colLen, "            Hash Offset:", $"0x{hashOffset:x12}");
                    PrintItem(sb, colLen, "            Hash BlockSize:", $"0x{1 << level.BlockSizePower:x8}");
                }
            }
        }
    }
}
