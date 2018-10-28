using System;
using System.IO;
using System.Text;
using LibHac;
using LibHac.IO;
using LibHac.Save;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessSave
    {
        public static void Process(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.ReadWrite))
            {
                var save = new Savefile(ctx.Keyset, file, ctx.Options.IntegrityLevel);

                if (ctx.Options.Validate)
                {
                    save.Verify(ctx.Logger);
                }

                if (ctx.Options.OutDir != null)
                {
                    save.Extract(ctx.Options.OutDir, ctx.Logger);
                }

                if (ctx.Options.DebugOutDir != null)
                {
                    string dir = ctx.Options.DebugOutDir;
                    Directory.CreateDirectory(dir);

                    FsLayout layout = save.Header.Layout;

                    File.WriteAllBytes(Path.Combine(dir, "L0_0_MasterHashA"), save.Header.MasterHashA);
                    File.WriteAllBytes(Path.Combine(dir, "L0_1_MasterHashB"), save.Header.MasterHashB);
                    File.WriteAllBytes(Path.Combine(dir, "L0_2_DuplexMasterA"), save.Header.DuplexMasterA);
                    File.WriteAllBytes(Path.Combine(dir, "L0_3_DuplexMasterB"), save.Header.DuplexMasterB);

                    Stream duplexL1A = save.DataRemapStorage.OpenStream(layout.DuplexL1OffsetA, layout.DuplexL1Size);
                    Stream duplexL1B = save.DataRemapStorage.OpenStream(layout.DuplexL1OffsetB, layout.DuplexL1Size);
                    Stream duplexDataA = save.DataRemapStorage.OpenStream(layout.DuplexDataOffsetA, layout.DuplexDataSize);
                    Stream duplexDataB = save.DataRemapStorage.OpenStream(layout.DuplexDataOffsetB, layout.DuplexDataSize);
                    Stream journalData = save.DataRemapStorage.OpenStream(layout.JournalDataOffset, layout.JournalDataSizeB + layout.SizeReservedArea);

                    duplexL1A.WriteAllBytes(Path.Combine(dir, "L0_4_DuplexL1A"), ctx.Logger);
                    duplexL1B.WriteAllBytes(Path.Combine(dir, "L0_5_DuplexL1B"), ctx.Logger);
                    duplexDataA.WriteAllBytes(Path.Combine(dir, "L0_6_DuplexDataA"), ctx.Logger);
                    duplexDataB.WriteAllBytes(Path.Combine(dir, "L0_7_DuplexDataB"), ctx.Logger);
                    journalData.WriteAllBytes(Path.Combine(dir, "L0_9_JournalData"), ctx.Logger);
                    save.DuplexData.WriteAllBytes(Path.Combine(dir, "L1_0_DuplexData"), ctx.Logger);

                    Stream journalTable = save.MetaRemapStorage.OpenStream(layout.JournalTableOffset, layout.JournalTableSize);
                    Stream journalBitmapUpdatedPhysical = save.MetaRemapStorage.OpenStream(layout.JournalBitmapUpdatedPhysicalOffset, layout.JournalBitmapUpdatedPhysicalSize);
                    Stream journalBitmapUpdatedVirtual = save.MetaRemapStorage.OpenStream(layout.JournalBitmapUpdatedVirtualOffset, layout.JournalBitmapUpdatedVirtualSize);
                    Stream journalBitmapUnassigned = save.MetaRemapStorage.OpenStream(layout.JournalBitmapUnassignedOffset, layout.JournalBitmapUnassignedSize);
                    Stream journalLayer1Hash = save.MetaRemapStorage.OpenStream(layout.IvfcL1Offset, layout.IvfcL1Size);
                    Stream journalLayer2Hash = save.MetaRemapStorage.OpenStream(layout.IvfcL2Offset, layout.IvfcL2Size);
                    Stream journalLayer3Hash = save.MetaRemapStorage.OpenStream(layout.IvfcL3Offset, layout.IvfcL3Size);
                    Stream journalFat = save.MetaRemapStorage.OpenStream(layout.FatOffset, layout.FatSize);

                    journalTable.WriteAllBytes(Path.Combine(dir, "L2_0_JournalTable"), ctx.Logger);
                    journalBitmapUpdatedPhysical.WriteAllBytes(Path.Combine(dir, "L2_1_JournalBitmapUpdatedPhysical"), ctx.Logger);
                    journalBitmapUpdatedVirtual.WriteAllBytes(Path.Combine(dir, "L2_2_JournalBitmapUpdatedVirtual"), ctx.Logger);
                    journalBitmapUnassigned.WriteAllBytes(Path.Combine(dir, "L2_3_JournalBitmapUnassigned"), ctx.Logger);
                    journalLayer1Hash.WriteAllBytes(Path.Combine(dir, "L2_4_Layer1Hash"), ctx.Logger);
                    journalLayer2Hash.WriteAllBytes(Path.Combine(dir, "L2_5_Layer2Hash"), ctx.Logger);
                    journalLayer3Hash.WriteAllBytes(Path.Combine(dir, "L2_6_Layer3Hash"), ctx.Logger);
                    journalFat.WriteAllBytes(Path.Combine(dir, "L2_7_FAT"), ctx.Logger);

                    save.IvfcStreamSource.CreateStream().WriteAllBytes(Path.Combine(dir, "L3_0_SaveData"), ctx.Logger);
                }

                if (ctx.Options.SignSave)
                {
                    if (save.CommitHeader(ctx.Keyset))
                    {
                        ctx.Logger.LogMessage("Successfully signed save file");
                    }
                    else
                    {
                        ctx.Logger.LogMessage("Unable to sign save file. Do you have all the required keys?");
                    }
                }

                if (ctx.Options.ListFiles)
                {
                    foreach (FileEntry fileEntry in save.Files)
                    {
                        ctx.Logger.LogMessage(fileEntry.FullPath);
                    }
                }

                ctx.Logger.LogMessage(save.Print());
            }
        }

        private static string Print(this Savefile save)
        {
            int colLen = 25;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("Savefile:");
            PrintItem(sb, colLen, $"CMAC Signature{save.Header.SignatureValidity.GetValidityString()}:", save.Header.Cmac);
            PrintItem(sb, colLen, "Title ID:", $"{save.Header.ExtraData.TitleId:x16}");
            PrintItem(sb, colLen, "User ID:", save.Header.ExtraData.UserId);
            PrintItem(sb, colLen, "Save ID:", $"{save.Header.ExtraData.SaveId:x16}");
            PrintItem(sb, colLen, "Save Type:", $"{save.Header.ExtraData.Type}");
            PrintItem(sb, colLen, "Owner ID:", $"{save.Header.ExtraData.SaveOwnerId:x16}");
            PrintItem(sb, colLen, "Timestamp:", $"{DateTimeOffset.FromUnixTimeSeconds(save.Header.ExtraData.Timestamp):yyyy-MM-dd HH:mm:ss} UTC");
            PrintItem(sb, colLen, "Save Data Size:", $"0x{save.Header.ExtraData.DataSize:x16} ({Util.GetBytesReadable(save.Header.ExtraData.DataSize)})");
            PrintItem(sb, colLen, "Journal Size:", $"0x{save.Header.ExtraData.JournalSize:x16} ({Util.GetBytesReadable(save.Header.ExtraData.JournalSize)})");
            PrintItem(sb, colLen, $"Header Hash{save.Header.HeaderHashValidity.GetValidityString()}:", save.Header.Layout.Hash);
            PrintItem(sb, colLen, "Number of Files:", save.Files.Length);

            PrintIvfcHash(sb, colLen, 4, save.Header.Ivfc, IntegrityStorageType.Save);

            return sb.ToString();
        }
    }
}
