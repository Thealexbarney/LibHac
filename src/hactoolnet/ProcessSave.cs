using System;
using System.IO;
using System.Text;
using LibHac;
using LibHac.IO;
using LibHac.IO.Save;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessSave
    {
        public static void Process(Context ctx)
        {
            using (var file = new FileStream(ctx.Options.InFile, FileMode.Open, FileAccess.ReadWrite))
            {
                var save = new SaveDataFileSystem(ctx.Keyset, file.AsStorage(), ctx.Options.IntegrityLevel, true);

                if (ctx.Options.Validate)
                {
                    save.Verify(ctx.Logger);
                }

                if (ctx.Options.OutDir != null)
                {
                    save.SaveDataFileSystemCore.Extract(ctx.Options.OutDir, ctx.Logger);
                }

                if (ctx.Options.DebugOutDir != null)
                {
                    // todo
                    string dir = ctx.Options.DebugOutDir;
                    Directory.CreateDirectory(dir);

                    FsLayout layout = save.Header.Layout;

                    string mainRemapDir = Path.Combine(dir, "main_remap");
                    Directory.CreateDirectory(mainRemapDir);

                    save.DataRemapStorage.GetBaseStorage().WriteAllBytes(Path.Combine(mainRemapDir, "Data"));
                    save.DataRemapStorage.GetHeaderStorage().WriteAllBytes(Path.Combine(mainRemapDir, "Header"));
                    save.DataRemapStorage.GetMapEntryStorage().WriteAllBytes(Path.Combine(mainRemapDir, "Map entries"));

                    string metadataRemapDir = Path.Combine(dir, "metadata_remap");
                    Directory.CreateDirectory(metadataRemapDir);

                    save.MetaRemapStorage.GetBaseStorage().WriteAllBytes(Path.Combine(metadataRemapDir, "Data"));
                    save.MetaRemapStorage.GetHeaderStorage().WriteAllBytes(Path.Combine(metadataRemapDir, "Header"));
                    save.MetaRemapStorage.GetMapEntryStorage().WriteAllBytes(Path.Combine(metadataRemapDir, "Map entries"));

                    string journalDir = Path.Combine(dir, "journal");
                    Directory.CreateDirectory(journalDir);

                    save.JournalStorage.GetBaseStorage().WriteAllBytes(Path.Combine(journalDir, "Data"));
                    save.JournalStorage.GetHeaderStorage().WriteAllBytes(Path.Combine(journalDir, "Header"));
                    save.JournalStorage.Map.GetHeaderStorage().WriteAllBytes(Path.Combine(journalDir, "Map_header"));
                    save.JournalStorage.Map.GetMapStorage().WriteAllBytes(Path.Combine(journalDir, "Map"));
                    save.JournalStorage.Map.GetModifiedPhysicalBlocksStorage().WriteAllBytes(Path.Combine(journalDir, "ModifiedPhysicalBlocks"));
                    save.JournalStorage.Map.GetModifiedVirtualBlocksStorage().WriteAllBytes(Path.Combine(journalDir, "ModifiedVirtualBlocks"));
                    save.JournalStorage.Map.GetFreeBlocksStorage().WriteAllBytes(Path.Combine(journalDir, "FreeBlocks"));

                    string saveDir = Path.Combine(dir, "save");
                    Directory.CreateDirectory(saveDir);

                    save.SaveDataFileSystemCore.GetHeaderStorage().WriteAllBytes(Path.Combine(saveDir, "Save_Header"));
                    save.SaveDataFileSystemCore.GetBaseStorage().WriteAllBytes(Path.Combine(saveDir, "Save_Data"));
                    save.SaveDataFileSystemCore.AllocationTable.GetHeaderStorage().WriteAllBytes(Path.Combine(saveDir, "FAT_header"));
                    save.SaveDataFileSystemCore.AllocationTable.GetBaseStorage().WriteAllBytes(Path.Combine(saveDir, "FAT_Data"));

                    save.Header.DataIvfcMaster.WriteAllBytes(Path.Combine(saveDir, "Save_MasterHash"));

                    IStorage saveLayer1Hash = save.MetaRemapStorage.Slice(layout.IvfcL1Offset, layout.IvfcL1Size);
                    IStorage saveLayer2Hash = save.MetaRemapStorage.Slice(layout.IvfcL2Offset, layout.IvfcL2Size);
                    IStorage saveLayer3Hash = save.MetaRemapStorage.Slice(layout.IvfcL3Offset, layout.IvfcL3Size);

                    saveLayer1Hash.WriteAllBytes(Path.Combine(saveDir, "Save_Layer1Hash"), ctx.Logger);
                    saveLayer2Hash.WriteAllBytes(Path.Combine(saveDir, "Save_Layer2Hash"), ctx.Logger);
                    saveLayer3Hash.WriteAllBytes(Path.Combine(saveDir, "Save_Layer3Hash"), ctx.Logger);

                    string duplexDir = Path.Combine(dir, "duplex");
                    Directory.CreateDirectory(duplexDir);

                    save.Header.DuplexMasterBitmapA.WriteAllBytes(Path.Combine(duplexDir, "MasterBitmapA"));
                    save.Header.DuplexMasterBitmapB.WriteAllBytes(Path.Combine(duplexDir, "MasterBitmapB"));

                    IStorage duplexL1A = save.DataRemapStorage.Slice(layout.DuplexL1OffsetA, layout.DuplexL1Size);
                    IStorage duplexL1B = save.DataRemapStorage.Slice(layout.DuplexL1OffsetB, layout.DuplexL1Size);
                    IStorage duplexDataA = save.DataRemapStorage.Slice(layout.DuplexDataOffsetA, layout.DuplexDataSize);
                    IStorage duplexDataB = save.DataRemapStorage.Slice(layout.DuplexDataOffsetB, layout.DuplexDataSize);

                    duplexL1A.WriteAllBytes(Path.Combine(duplexDir, "L1BitmapA"), ctx.Logger);
                    duplexL1B.WriteAllBytes(Path.Combine(duplexDir, "L1BitmapB"), ctx.Logger);
                    duplexDataA.WriteAllBytes(Path.Combine(duplexDir, "DataA"), ctx.Logger);
                    duplexDataB.WriteAllBytes(Path.Combine(duplexDir, "DataB"), ctx.Logger);
                }

                if (ctx.Options.ReplaceFileDest != null && ctx.Options.ReplaceFileSource != null)
                {
                    string destFilename = ctx.Options.ReplaceFileDest;
                    if (!destFilename.StartsWith("/")) destFilename = '/' + destFilename;

                    using (IFile inFile = new FileStream(ctx.Options.ReplaceFileSource, FileMode.Open, FileAccess.Read).AsIFile(OpenMode.ReadWrite))
                    {
                        using (IFile outFile = save.OpenFile(destFilename, OpenMode.ReadWrite))
                        {
                            if (inFile.GetSize() != outFile.GetSize())
                            {
                                ctx.Logger.LogMessage($"Replacement file must be the same size as the original file. ({outFile.GetSize()} bytes)");
                                return;
                            }

                            inFile.CopyTo(outFile, ctx.Logger);

                            ctx.Logger.LogMessage($"Replaced file {destFilename}");
                        }
                    }

                    if (save.Commit(ctx.Keyset))
                    {
                        ctx.Logger.LogMessage("Successfully signed save file");
                    }
                    else
                    {
                        ctx.Logger.LogMessage("ERROR: Unable to sign save file. Do you have all the required keys?");
                    }

                    return;
                }

                if (ctx.Options.SignSave)
                {
                    if (save.Commit(ctx.Keyset))
                    {
                        ctx.Logger.LogMessage("Successfully signed save file");
                    }
                    else
                    {
                        ctx.Logger.LogMessage("Unable to sign save file. Do you have all the required keys?");
                    }

                    return;
                }

                if (ctx.Options.ListFiles)
                {
                    IDirectory dir = save.SaveDataFileSystemCore.OpenDirectory("/", OpenDirectoryMode.All);
                    foreach (DirectoryEntry entry in dir.EnumerateEntries())
                    {
                        ctx.Logger.LogMessage(entry.FullPath);

                    }
                }

                ctx.Logger.LogMessage(save.Print());
            }
        }

        private static string Print(this SaveDataFileSystem save)
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
            PrintItem(sb, colLen, "Number of Files:", save.GetEntryCount(OpenDirectoryMode.Files));

            PrintIvfcHash(sb, colLen, 4, save.Header.Ivfc, IntegrityStorageType.Save);

            return sb.ToString();
        }
    }
}
