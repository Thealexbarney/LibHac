using System.IO;
using System.Text;
using LibHac;
using LibHac.IO;
using LibHac.IO.NcaUtils;
using LibHac.Npdm;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessNca
    {
        public static void Process(Context ctx)
        {
            using (IStorage file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var nca = new Nca(ctx.Keyset, file);
                Nca baseNca = null;

                var ncaHolder = new NcaHolder { Nca = nca };

                if (ctx.Options.HeaderOut != null)
                {
                    using (var outHeader = new FileStream(ctx.Options.HeaderOut, FileMode.Create, FileAccess.ReadWrite))
                    {
                        nca.OpenDecryptedHeaderStorage().Slice(0, 0xc00).CopyToStream(outHeader);
                    }
                }

                if (ctx.Options.BaseNca != null)
                {
                    IStorage baseFile = new LocalStorage(ctx.Options.BaseNca, FileAccess.Read);
                    baseNca = new Nca(ctx.Keyset, baseFile);
                    ncaHolder.BaseNca = baseNca;
                }

                for (int i = 0; i < 3; i++)
                {
                    if (ctx.Options.SectionOut[i] != null)
                    {
                        OpenStorage(i).WriteAllBytes(ctx.Options.SectionOut[i], ctx.Logger);
                    }

                    if (ctx.Options.SectionOutDir[i] != null)
                    {
                        IFileSystem fs = OpenFileSystem(i);
                        fs.Extract(ctx.Options.SectionOutDir[i], ctx.Logger);
                    }

                    if (ctx.Options.Validate && nca.SectionExists(i))
                    {
                        ncaHolder.Validities[i] = nca.VerifySection(i, ctx.Logger);
                    }
                }

                if (ctx.Options.ListRomFs && nca.CanOpenSection(NcaSectionType.Data))
                {
                    IFileSystem romfs = OpenFileSystemByType(NcaSectionType.Data);

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
                        OpenStorageByType(NcaSectionType.Data).WriteAllBytes(ctx.Options.RomfsOut, ctx.Logger);
                    }

                    if (ctx.Options.RomfsOutDir != null)
                    {
                        IFileSystem fs = OpenFileSystemByType(NcaSectionType.Data);
                        fs.Extract(ctx.Options.RomfsOutDir, ctx.Logger);
                    }

                    if (ctx.Options.ReadBench)
                    {
                        long bytesToRead = 1024L * 1024 * 1024 * 5;
                        IStorage storage = OpenStorageByType(NcaSectionType.Data);
                        var dest = new NullStorage(storage.GetSize());

                        int iterations = (int)(bytesToRead / storage.GetSize()) + 1;
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
                        OpenStorageByType(NcaSectionType.Code).WriteAllBytes(ctx.Options.ExefsOut, ctx.Logger);
                    }

                    if (ctx.Options.ExefsOutDir != null)
                    {
                        IFileSystem fs = OpenFileSystemByType(NcaSectionType.Code);
                        fs.Extract(ctx.Options.ExefsOutDir, ctx.Logger);
                    }
                }

                if (ctx.Options.PlaintextOut != null)
                {
                    nca.OpenDecryptedNca().WriteAllBytes(ctx.Options.PlaintextOut, ctx.Logger);
                }

                if (!ctx.Options.ReadBench) ctx.Logger.LogMessage(ncaHolder.Print());

                IStorage OpenStorage(int index)
                {
                    if (ctx.Options.Raw)
                    {
                        if (baseNca != null) return baseNca.OpenRawStorageWithPatch(nca, index);

                        return nca.OpenRawStorage(index);
                    }

                    if (baseNca != null) return baseNca.OpenStorageWithPatch(nca, index, ctx.Options.IntegrityLevel);

                    return nca.OpenStorage(index, ctx.Options.IntegrityLevel);
                }

                IFileSystem OpenFileSystem(int index)
                {
                    if (baseNca != null) return baseNca.OpenFileSystemWithPatch(nca, index, ctx.Options.IntegrityLevel);

                    return nca.OpenFileSystem(index, ctx.Options.IntegrityLevel);
                }

                IStorage OpenStorageByType(NcaSectionType type)
                {
                    return OpenStorage(Nca.SectionIndexFromType(type, nca.Header.ContentType));
                }

                IFileSystem OpenFileSystemByType(NcaSectionType type)
                {
                    return OpenFileSystem(Nca.SectionIndexFromType(type, nca.Header.ContentType));
                }
            }
        }

        private static Validity VerifySignature2(this Nca nca)
        {
            if (nca.Header.ContentType != ContentType.Program) return Validity.Unchecked;

            IFileSystem pfs = nca.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.ErrorOnInvalid);
            if (!pfs.FileExists("main.npdm")) return Validity.Unchecked;

            IFile npdmStorage = pfs.OpenFile("main.npdm", OpenMode.Read);
            var npdm = new NpdmBinary(npdmStorage.AsStream());

            return nca.Header.VerifySignature2(npdm.AciD.Rsa2048Modulus);
        }

        private static string Print(this NcaHolder ncaHolder)
        {
            Nca nca = ncaHolder.Nca;
            int masterKey = Keyset.GetMasterKeyRevisionFromKeyGeneration(nca.Header.KeyGeneration);

            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("NCA:");
            PrintItem(sb, colLen, "Magic:", MagicToString(nca.Header.Magic));
            PrintItem(sb, colLen, $"Fixed-Key Signature{nca.VerifyHeaderSignature().GetValidityString()}:", nca.Header.Signature1.ToArray());
            PrintItem(sb, colLen, $"NPDM Signature{nca.VerifySignature2().GetValidityString()}:", nca.Header.Signature2.ToArray());
            PrintItem(sb, colLen, "Content Size:", $"0x{nca.Header.NcaSize:x12}");
            PrintItem(sb, colLen, "TitleID:", $"{nca.Header.TitleId:X16}");
            PrintItem(sb, colLen, "SDK Version:", nca.Header.SdkVersion);
            PrintItem(sb, colLen, "Distribution type:", nca.Header.DistributionType);
            PrintItem(sb, colLen, "Content Type:", nca.Header.ContentType);
            PrintItem(sb, colLen, "Master Key Revision:", $"{masterKey} ({Util.GetKeyRevisionSummary(masterKey)})");
            PrintItem(sb, colLen, "Encryption Type:", $"{(nca.Header.HasRightsId ? "Titlekey crypto" : "Standard crypto")}");

            if (nca.Header.HasRightsId)
            {
                PrintItem(sb, colLen, "Rights ID:", nca.Header.RightsId.ToArray());
            }
            else
            {
                PrintItem(sb, colLen, "Key Area Encryption Key:", nca.Header.KeyAreaKeyIndex);
                sb.AppendLine("Key Area (Encrypted):");
                for (int i = 0; i < 4; i++)
                {
                    PrintItem(sb, colLen, $"    Key {i} (Encrypted):", nca.Header.GetEncryptedKey(i).ToArray());
                }

                sb.AppendLine("Key Area (Decrypted):");
                for (int i = 0; i < 4; i++)
                {
                    PrintItem(sb, colLen, $"    Key {i} (Decrypted):", nca.GetDecryptedKey(i));
                }
            }

            PrintSections();

            return sb.ToString();

            void PrintSections()
            {
                sb.AppendLine("Sections:");

                for (int i = 0; i < 4; i++)
                {
                    if (!nca.Header.IsSectionEnabled(i)) continue;

                    NcaFsHeader sectHeader = nca.Header.GetFsHeader(i);
                    bool isExefs = nca.Header.ContentType == ContentType.Program && i == 0;

                    sb.AppendLine($"    Section {i}:");
                    PrintItem(sb, colLen, "        Offset:", $"0x{nca.Header.GetSectionStartOffset(i):x12}");
                    PrintItem(sb, colLen, "        Size:", $"0x{nca.Header.GetSectionSize(i):x12}");
                    PrintItem(sb, colLen, "        Partition Type:", (isExefs ? "ExeFS" : sectHeader.FormatType.ToString()) + (sectHeader.IsPatchSection() ? " patch" : ""));
                    PrintItem(sb, colLen, "        Section CTR:", $"{sectHeader.Counter:x16}");
                    PrintItem(sb, colLen, "        Section Validity:", $"{ncaHolder.Validities[i]}");

                    switch (sectHeader.HashType)
                    {
                        case NcaHashType.Sha256:
                            PrintSha256Hash(sectHeader, i);
                            break;
                        case NcaHashType.Ivfc:
                            Validity masterHashValidity = nca.ValidateSectionMasterHash(i);

                            PrintIvfcHashNew(sb, colLen, 8, sectHeader.GetIntegrityInfoIvfc(), IntegrityStorageType.RomFs, masterHashValidity);
                            break;
                        default:
                            sb.AppendLine("        Unknown/invalid superblock!");
                            break;
                    }
                }
            }

            void PrintSha256Hash(NcaFsHeader sect, int index)
            {
                NcaFsIntegrityInfoSha256 hashInfo = sect.GetIntegrityInfoSha256();

                PrintItem(sb, colLen, $"        Master Hash{nca.ValidateSectionMasterHash(index).GetValidityString()}:", hashInfo.MasterHash.ToArray());
                sb.AppendLine($"        Hash Table:");

                PrintItem(sb, colLen, "            Offset:", $"0x{hashInfo.GetLevelOffset(0):x12}");
                PrintItem(sb, colLen, "            Size:", $"0x{hashInfo.GetLevelSize(0):x12}");
                PrintItem(sb, colLen, "            Block Size:", $"0x{hashInfo.BlockSize:x}");
                PrintItem(sb, colLen, "        PFS0 Offset:", $"0x{hashInfo.GetLevelOffset(1):x12}");
                PrintItem(sb, colLen, "        PFS0 Size:", $"0x{hashInfo.GetLevelSize(1):x12}");
            }
        }

        private class NcaHolder
        {
            public Nca Nca;
            public Nca BaseNca;
            public Validity[] Validities = new Validity[4];
        }
    }
}
