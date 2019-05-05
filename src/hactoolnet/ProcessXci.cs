using System;
using System.IO;
using System.Linq;
using System.Text;
using LibHac;
using LibHac.IO;
using LibHac.IO.NcaUtils;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessXci
    {
        public static void Process(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var xci = new Xci(ctx.Keyset, file);

                ctx.Logger.LogMessage(xci.Print());

                if (ctx.Options.RootDir != null)
                {
                    xci.OpenPartition(XciPartitionType.Root).Extract(ctx.Options.RootDir, ctx.Logger);
                }

                if (ctx.Options.UpdateDir != null && xci.HasPartition(XciPartitionType.Update))
                {
                    xci.OpenPartition(XciPartitionType.Update).Extract(ctx.Options.UpdateDir, ctx.Logger);
                }

                if (ctx.Options.NormalDir != null && xci.HasPartition(XciPartitionType.Normal))
                {
                    xci.OpenPartition(XciPartitionType.Normal).Extract(ctx.Options.NormalDir, ctx.Logger);
                }

                if (ctx.Options.SecureDir != null && xci.HasPartition(XciPartitionType.Secure))
                {
                    xci.OpenPartition(XciPartitionType.Secure).Extract(ctx.Options.SecureDir, ctx.Logger);
                }

                if (ctx.Options.LogoDir != null && xci.HasPartition(XciPartitionType.Logo))
                {
                    xci.OpenPartition(XciPartitionType.Logo).Extract(ctx.Options.LogoDir, ctx.Logger);
                }

                if (ctx.Options.OutDir != null)
                {
                    XciPartition root = xci.OpenPartition(XciPartitionType.Root);
                    if (root == null)
                    {
                        ctx.Logger.LogMessage("Could not find root partition");
                        return;
                    }

                    foreach (PartitionFileEntry sub in root.Files)
                    {
                        var subPfs = new PartitionFileSystem(root.OpenFile(sub, OpenMode.Read).AsStorage());
                        string subDir = Path.Combine(ctx.Options.OutDir, sub.Name);

                        subPfs.Extract(subDir, ctx.Logger);
                    }
                }

                if (ctx.Options.ExefsOutDir != null || ctx.Options.ExefsOut != null)
                {
                    NcaNew mainNca = GetXciMainNca(xci, ctx);

                    if (mainNca == null)
                    {
                        ctx.Logger.LogMessage("Could not find Program NCA");
                        return;
                    }

                    if (!mainNca.SectionExists(NcaSectionType.Code))
                    {
                        ctx.Logger.LogMessage("NCA has no ExeFS section");
                        return;
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        mainNca.ExtractSection(NcaSectionType.Code, ctx.Options.ExefsOutDir, ctx.Options.IntegrityLevel, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOut != null)
                    {
                        mainNca.ExportSection(NcaSectionType.Code, ctx.Options.ExefsOut, ctx.Options.Raw, ctx.Options.IntegrityLevel, ctx.Logger);
                    }
                }

                if (ctx.Options.RomfsOutDir != null || ctx.Options.RomfsOut != null)
                {
                    NcaNew mainNca = GetXciMainNca(xci, ctx);

                    if (mainNca == null)
                    {
                        ctx.Logger.LogMessage("Could not find Program NCA");
                        return;
                    }

                    if (!mainNca.SectionExists(NcaSectionType.Data))
                    {
                        ctx.Logger.LogMessage("NCA has no RomFS section");
                        return;
                    }

                    ProcessRomfs.Process(ctx, mainNca.OpenStorage(NcaSectionType.Data, ctx.Options.IntegrityLevel, false));
                }
            }
        }

        private static NcaNew GetXciMainNca(Xci xci, Context ctx)
        {
            XciPartition partition = xci.OpenPartition(XciPartitionType.Secure);

            if (partition == null)
            {
                ctx.Logger.LogMessage("Could not find secure partition");
                return null;
            }

            NcaNew mainNca = null;

            foreach (PartitionFileEntry fileEntry in partition.Files.Where(x => x.Name.EndsWith(".nca")))
            {
                IStorage ncaStorage = partition.OpenFile(fileEntry, OpenMode.Read).AsStorage();
                var nca = new NcaNew(ctx.Keyset, ncaStorage);

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
            PrintItem(sb, colLen, $"Header Hash{xci.Header.PartitionFsHeaderValidity.GetValidityString()}:", xci.Header.RootPartitionHeaderHash);
            PrintItem(sb, colLen, "Cartridge Type:", GetCartridgeType(xci.Header.RomSize));
            PrintItem(sb, colLen, "Cartridge Size:", $"0x{Util.MediaToReal(xci.Header.ValidDataEndPage + 1):x12}");
            PrintItem(sb, colLen, "Header IV:", xci.Header.AesCbcIv);

            PrintPartition(sb, colLen, xci.OpenPartition(XciPartitionType.Root), XciPartitionType.Root);

            foreach (XciPartitionType type in Enum.GetValues(typeof(XciPartitionType)))
            {
                if (type == XciPartitionType.Root || !xci.HasPartition(type)) continue;

                XciPartition partition = xci.OpenPartition(type);
                PrintPartition(sb, colLen, partition, type);
            }

            return sb.ToString();
        }

        private static void PrintPartition(StringBuilder sb, int colLen, XciPartition partition, XciPartitionType type)
        {
            const int fileNameLen = 57;

            sb.AppendLine($"{type.ToString()} Partition:{partition.HashValidity.GetValidityString()}");
            PrintItem(sb, colLen, "    Magic:", partition.Header.Magic);
            PrintItem(sb, colLen, "    Offset:", $"{partition.Offset:x12}");
            PrintItem(sb, colLen, "    Number of files:", partition.Files.Length);

            string name = type.GetFileName();

            if (partition.Files.Length > 0 && partition.Files.Length < 100)
            {
                for (int i = 0; i < partition.Files.Length; i++)
                {
                    PartitionFileEntry file = partition.Files[i];

                    string label = i == 0 ? "    Files:" : "";
                    string offsets = $"{file.Offset:x12}-{file.Offset + file.Size:x12}{file.HashValidity.GetValidityString()}";
                    string data = $"{name}:/{file.Name}".PadRight(fileNameLen) + offsets;

                    PrintItem(sb, colLen, label, data);
                }
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
