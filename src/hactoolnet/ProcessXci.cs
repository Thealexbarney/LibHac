using System;
using System.IO;
using System.Linq;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.FsSystem;
using LibHac.Gc.Impl;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;

namespace hactoolnet;

internal static class ProcessXci
{
    public static void Process(Context ctx)
    {
        using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
        {
            var xci = new Xci(ctx.KeySet, file);

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
                    string subDir = System.IO.Path.Combine(ctx.Options.OutDir, sub.Name);

                    subPfs.Extract(subDir, ctx.Logger);
                }
            }

            if (xci.HasPartition(XciPartitionType.Secure))
            {
                ProcessAppFs.Process(ctx, xci.OpenPartition(XciPartitionType.Secure));
            }
        }
    }

    private static string Print(this Xci xci)
    {
        const int colLen = 52;

        var sb = new IndentingStringBuilder(colLen);

        if (!xci.Header.HasInitialData)
        {
            sb.AppendLine("Warning: Game card is missing key area/initial data header. Re-dump?");
        }

        using ScopedIndentation xciScope = sb.AppendHeader("XCI:");

        if (xci.Header.HasInitialData)
        {
            using ScopedIndentation initialDataScope = sb.AppendHeader("Initial Data:");

            sb.PrintItem("Package Id:", xci.Header.InitialDataPackageId);
            sb.PrintItem("Encrypted Title Key:", xci.Header.InitialDataAuthData);

            if (xci.Header.DecryptedTitleKey is not null)
            {
                sb.PrintItem("Decrypted Title Key:", xci.Header.DecryptedTitleKey);
            }
        }
        else
        {
            sb.PrintItem("Initial Data:", "Missing/Not Dumped");
        }

        using (sb.AppendHeader("Main Header:"))
        {
            sb.PrintItem("Magic:", xci.Header.Magic);
            sb.PrintItem($"Signature{xci.Header.SignatureValidity.GetValidityString()}:",
                xci.Header.Signature);
            sb.PrintItem("Package Id:", BitConverter.GetBytes(xci.Header.PackageId));

            sb.PrintItem("Memory Capacity:", new U8Span(new IdString().ToString((MemoryCapacity)xci.Header.GameCardSize)).ToString());
            sb.PrintItem("Rom Area Start:", $"0x{Utilities.MediaToReal(xci.Header.RomAreaStartPage):X12}");
            sb.PrintItem("Backup Area Start:", $"0x{Utilities.MediaToReal((uint)xci.Header.BackupAreaStartPage):X12}");
            sb.PrintItem("Valid Data End:", $"0x{Utilities.MediaToReal(xci.Header.ValidDataEndPage):X12}");
            sb.PrintItem("Limit Area:", $"0x{Utilities.MediaToReal(xci.Header.LimAreaPage):X12}");
            sb.PrintItem("Kek Index:", new U8Span(new IdString().ToString((KekIndex)xci.Header.KekIndex)).ToString());
            sb.PrintItem("Title Key Dec Index:", xci.Header.TitleKeyDecIndex);

            using (sb.AppendHeader("Flags:"))
            {
                sb.PrintItem("Auto Boot:", xci.Header.Flags.HasFlag(GameCardAttribute.AutoBootFlag) ? 1 : 0);
                sb.PrintItem("History Erase:", xci.Header.Flags.HasFlag(GameCardAttribute.HistoryEraseFlag) ? 1 : 0);
                sb.PrintItem("Repair Tool:", xci.Header.Flags.HasFlag(GameCardAttribute.RepairToolFlag) ? 1 : 0);
                sb.PrintItem("Different Region Cup to Terra Device:", xci.Header.Flags.HasFlag(GameCardAttribute.DifferentRegionCupToTerraDeviceFlag) ? 1 : 0);
                sb.PrintItem("Different Region Cup to Global Device:", xci.Header.Flags.HasFlag(GameCardAttribute.DifferentRegionCupToGlobalDeviceFlag) ? 1 : 0);
                sb.PrintItem("Has Ca10 Certificate:", xci.Header.Flags.HasFlag(GameCardAttribute.HasCa10CertificateFlag) ? 1 : 0);
            }

            sb.PrintItem("Sel Sec:", new U8Span(new IdString().ToString((SelSec)xci.Header.SelSec)).ToString());
            sb.PrintItem("Sel T1 Key:", xci.Header.SelT1Key);
            sb.PrintItem("Sel Key:", xci.Header.SelKey);

            sb.PrintItem($"Initial Data Hash{xci.Header.InitialDataValidity.GetValidityString()}:", xci.Header.InitialDataHash);
            sb.PrintItem($"Partition Header Hash{xci.Header.PartitionFsHeaderValidity.GetValidityString()}:", xci.Header.RootPartitionHeaderHash);
            sb.PrintItem("Encrypted Data Iv:", xci.Header.AesCbcIv.Reverse().ToArray());

            if (xci.Header.IsHeaderDecrypted)
            {
                using ScopedIndentation infoScope = sb.AppendHeader("Card Info:");

                sb.PrintItem("Card Fw Version:", new U8Span(new IdString().ToString((FwVersion)xci.Header.FwVersion)).ToString());
                sb.PrintItem("Clock Rate:", new U8Span(new IdString().ToString((AccessControl1ClockRate)xci.Header.AccCtrl1)).ToString());
                sb.PrintItem("Wait1 Time Read:", xci.Header.Wait1TimeRead);
                sb.PrintItem("Wait2 Time Read:", xci.Header.Wait2TimeRead);
                sb.PrintItem("Wait1 Time Write:", xci.Header.Wait1TimeWrite);
                sb.PrintItem("Wait2 Time Write:", xci.Header.Wait2TimeWrite);
                sb.PrintItem("Fw Mode:", $"0x{xci.Header.FwMode:X8}");
                sb.PrintItem("Compatibility Type:", new U8Span(new IdString().ToString((GameCardCompatibilityType)xci.Header.CompatibilityType)).ToString());

                int cv = xci.Header.UppVersion;
                sb.PrintItem("Cup Version:", $"{(cv >> 26) & 0x3F}.{(cv >> 20) & 0x3F}.{(cv >> 16) & 0xF}.{(cv >> 0) & 0xFFFF} ({cv})");
                sb.PrintItem("Cup Id:", $"{xci.Header.UppId:X16}");
                sb.PrintItem("Upp Hash:", xci.Header.UppHash);
            }
        }

        PrintPartition(sb, colLen, xci.OpenPartition(XciPartitionType.Root), XciPartitionType.Root);

        for (int i = 0; i <= (int)XciPartitionType.Root; i++)
        {
            var type = (XciPartitionType)i;
            if (type == XciPartitionType.Root || !xci.HasPartition(type)) continue;

            XciPartition partition = xci.OpenPartition(type);
            PrintPartition(sb, colLen, partition, type);
        }

        return sb.ToString();
    }

    private static void PrintPartition(IndentingStringBuilder sb, int colLen, XciPartition partition, XciPartitionType type)
    {
        using ScopedIndentation mainHeader =
            sb.AppendHeader($"{type.Print()} Partition:{partition.HashValidity.GetValidityString()}");

        sb.PrintItem("Magic:", partition.Header.Magic);
        sb.PrintItem("Number of files:", partition.Files.Length);

        string name = type.GetFileName();

        if (partition.Files.Length > 0 && partition.Files.Length < 100)
        {
            for (int i = 0; i < partition.Files.Length; i++)
            {
                PartitionFileEntry file = partition.Files[i];

                string label = i == 0 ? "Files:" : "";
                string data = $"{name}:/{file.Name}";

                sb.PrintItem(label, data);
            }
        }
    }
}