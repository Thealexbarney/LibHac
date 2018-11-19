using System.IO;
using System.Linq;
using System.Text;
using LibHac;
using LibHac.IO;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessXci
    {
        public static void Process(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.Read))
            {
                var xci = new Xci(ctx.Keyset, file.AsStorage());

                ctx.Logger.LogMessage(xci.Print());

                if (ctx.Options.RootDir != null)
                {
                    xci.RootPartition?.Extract(ctx.Options.RootDir, ctx.Logger);
                }

                if (ctx.Options.UpdateDir != null)
                {
                    xci.UpdatePartition?.Extract(ctx.Options.UpdateDir, ctx.Logger);
                }

                if (ctx.Options.NormalDir != null)
                {
                    xci.NormalPartition?.Extract(ctx.Options.NormalDir, ctx.Logger);
                }

                if (ctx.Options.SecureDir != null)
                {
                    xci.SecurePartition?.Extract(ctx.Options.SecureDir, ctx.Logger);
                }

                if (ctx.Options.LogoDir != null)
                {
                    xci.LogoPartition?.Extract(ctx.Options.LogoDir, ctx.Logger);
                }

                if (ctx.Options.OutDir != null && xci.RootPartition != null)
                {
                    XciPartition root = xci.RootPartition;
                    if (root == null)
                    {
                        ctx.Logger.LogMessage("Could not find root partition");
                        return;
                    }

                    foreach (PfsFileEntry sub in root.Files)
                    {
                        var subPfs = new Pfs(root.OpenFile(sub));
                        string subDir = Path.Combine(ctx.Options.OutDir, sub.Name);

                        subPfs.Extract(subDir, ctx.Logger);
                    }
                }

                if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
                {
                    Nca mainNca = GetXciMainNca(xci, ctx);

                    if (mainNca == null)
                    {
                        ctx.Logger.LogMessage("Could not find Program NCA");
                        return;
                    }

                    NcaSection exefsSection = mainNca.Sections[(int)ProgramPartitionType.Code];

                    if (exefsSection == null)
                    {
                        ctx.Logger.LogMessage("NCA has no ExeFS section");
                        return;
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        mainNca.ExtractSection(exefsSection.SectionNum, ctx.Options.ExefsOutDir, ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOut != null)
                    {
                        mainNca.ExportSection(exefsSection.SectionNum, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
                    }
                }

                if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
                {
                    Nca mainNca = GetXciMainNca(xci, ctx);

                    if (mainNca == null)
                    {
                        ctx.Logger.LogMessage("Could not find Program NCA");
                        return;
                    }

                    NcaSection romfsSection = mainNca.Sections.FirstOrDefault(x => x.Type == SectionType.Romfs);

                    if (romfsSection == null)
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    if (ctx.Options.RomfsOutDir != null)
                    {
                        var romfs = new Romfs(mainNca.OpenSection(romfsSection.SectionNum, false, ctx.Options.IntegrityLevel, true));
                        romfs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                    }

                    if (ctx.Options.RomfsOut != null)
                    {
                        mainNca.ExportSection(romfsSection.SectionNum, ctx.Options.RomfsOut, ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
                    }
                }
            }
        }

        private static Nca GetXciMainNca(Xci xci, Context ctx)
        {
            if (xci.SecurePartition == null)
            {
                ctx.Logger.LogMessage("Could not find secure partition");
                return null;
            }

            Nca mainNca = null;

            foreach (PfsFileEntry fileEntry in xci.SecurePartition.Files.Where(x => x.Name.EndsWith(".nca")))
            {
                IStorage ncaStorage = xci.SecurePartition.OpenFile(fileEntry);
                var nca = new Nca(ctx.Keyset, ncaStorage, true);

                if (nca.Header.ContentType == ContentType.Program)
                {
                    mainNca = nca;
                }
            }

            return mainNca;
        }

        private static string Print(this Xci xci)
        {
            const int colLen = 36;

            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("XCI:");

            PrintItem(sb, colLen, "Magic:", xci.Header.Magic);
            PrintItem(sb, colLen, $"Header Signature{xci.Header.SignatureValidity.GetValidityString()}:", xci.Header.Signature);
            PrintItem(sb, colLen, $"Header Hash{xci.Header.PartitionFsHeaderValidity.GetValidityString()}:", xci.Header.PartitionFsHeaderHash);
            PrintItem(sb, colLen, "Cartridge Type:", GetCartridgeType(xci.Header.RomSize));
            PrintItem(sb, colLen, "Cartridge Size:", $"0x{Util.MediaToReal(xci.Header.ValidDataEndPage + 1):x12}");
            PrintItem(sb, colLen, "Header IV:", xci.Header.AesCbcIv);

            foreach (XciPartition partition in xci.Partitions.OrderBy(x => x.Offset))
            {
                PrintPartition(sb, colLen, partition);
            }

            return sb.ToString();
        }

        private static void PrintPartition(StringBuilder sb, int colLen, XciPartition partition)
        {
            const int fileNameLen = 57;

            sb.AppendLine($"{GetDisplayName(partition.Name)} Partition:{partition.HashValidity.GetValidityString()}");
            PrintItem(sb, colLen, "    Magic:", partition.Header.Magic);
            PrintItem(sb, colLen, "    Offset:", $"{partition.Offset:x12}");
            PrintItem(sb, colLen, "    Number of files:", partition.Files.Length);

            if (partition.Files.Length > 0 && partition.Files.Length < 100)
            {
                for (int i = 0; i < partition.Files.Length; i++)
                {
                    PfsFileEntry file = partition.Files[i];

                    string label = i == 0 ? "    Files:" : "";
                    string offsets = $"{file.Offset:x12}-{file.Offset + file.Size:x12}{file.HashValidity.GetValidityString()}";
                    string data = $"{partition.Name}:/{file.Name}".PadRight(fileNameLen) + offsets;

                    PrintItem(sb, colLen, label, data);
                }
            }
        }

        private static string GetDisplayName(string name)
        {
            switch (name)
            {
                case "rootpt": return "Root";
                case "update": return "Update";
                case "normal": return "Normal";
                case "secure": return "Secure";
                case "logo": return "Logo";
                default: return name;
            }
        }

        private static string GetCartridgeType(RomSize size)
        {
            switch (size)
            {
                case RomSize.Size1Gb: return "1GB";
                case RomSize.Size2Gb: return "2GB";
                case RomSize.Size4Gb: return "4GB";
                case RomSize.Size8Gb: return "8GB";
                case RomSize.Size16Gb: return "16GB";
                case RomSize.Size32Gb: return "32GB";
                default: return string.Empty;
            }
        }
    }
}
