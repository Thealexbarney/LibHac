using System.IO;
using System.Text;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
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
                var nca = new Nca(ctx.KeySet, file);
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
                    baseNca = new Nca(ctx.KeySet, baseFile);
                }

                for (int i = 0; i < 3; i++)
                {
                    if (ctx.Options.SectionOut[i] != null)
                    {
                        OpenStorage(i).WriteAllBytes(ctx.Options.SectionOut[i], ctx.Logger);
                    }

                    if (ctx.Options.SectionOutDir[i] != null)
                    {
                        FileSystemClient fs = ctx.Horizon.Fs;

                        string mountName = $"section{i}";

                        fs.Register(mountName.ToU8Span(), OpenFileSystem(i));
                        fs.Register("output".ToU8Span(), new LocalFileSystem(ctx.Options.SectionOutDir[i]));

                        fs.Impl.EnableFileSystemAccessorAccessLog(mountName.ToU8Span());
                        fs.Impl.EnableFileSystemAccessorAccessLog("output".ToU8Span());

                        FsUtils.CopyDirectoryWithProgress(fs, (mountName + ":/").ToU8Span(), "output:/".ToU8Span(), logger: ctx.Logger).ThrowIfFailure();

                        fs.Unmount(mountName.ToU8Span());
                        fs.Unmount("output".ToU8Span());
                    }

                    if (ctx.Options.Validate && nca.SectionExists(i))
                    {
                        if (nca.GetFsHeader(i).IsPatchSection() && baseNca != null)
                        {
                            ncaHolder.Validities[i] = baseNca.VerifySection(nca, i, ctx.Logger);
                        }
                        else
                        {
                            ncaHolder.Validities[i] = nca.VerifySection(i, ctx.Logger);
                        }
                    }
                }

                if (ctx.Options.ListRomFs && nca.CanOpenSection(NcaSectionType.Data))
                {
                    IFileSystem romfs = OpenFileSystemByType(NcaSectionType.Data);

                    foreach (DirectoryEntryEx entry in romfs.EnumerateEntries())
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
                        FileSystemClient fs = ctx.Horizon.Fs;

                        fs.Register("rom".ToU8Span(), OpenFileSystemByType(NcaSectionType.Data));
                        fs.Register("output".ToU8Span(), new LocalFileSystem(ctx.Options.RomfsOutDir));

                        fs.Impl.EnableFileSystemAccessorAccessLog("rom".ToU8Span());
                        fs.Impl.EnableFileSystemAccessorAccessLog("output".ToU8Span());

                        FsUtils.CopyDirectoryWithProgress(fs, "rom:/".ToU8Span(), "output:/".ToU8Span(), logger: ctx.Logger).ThrowIfFailure();

                        fs.Unmount("rom".ToU8Span());
                        fs.Unmount("output".ToU8Span());
                    }

                    if (ctx.Options.ReadBench)
                    {
                        long bytesToRead = 1024L * 1024 * 1024 * 5;
                        IStorage storage = OpenStorageByType(NcaSectionType.Data);

                        storage.GetSize(out long sectionSize).ThrowIfFailure();

                        var dest = new NullStorage(sectionSize);

                        int iterations = (int)(bytesToRead / sectionSize) + 1;
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
                    if (nca.Header.ContentType != NcaContentType.Program)
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
                        FileSystemClient fs = ctx.Horizon.Fs;

                        fs.Register("code".ToU8Span(), OpenFileSystemByType(NcaSectionType.Code));
                        fs.Register("output".ToU8Span(), new LocalFileSystem(ctx.Options.ExefsOutDir));

                        fs.Impl.EnableFileSystemAccessorAccessLog("code".ToU8Span());
                        fs.Impl.EnableFileSystemAccessorAccessLog("output".ToU8Span());

                        FsUtils.CopyDirectoryWithProgress(fs, "code:/".ToU8Span(), "output:/".ToU8Span(), logger: ctx.Logger).ThrowIfFailure();

                        fs.Unmount("code".ToU8Span());
                        fs.Unmount("output".ToU8Span());
                    }
                }

                if (ctx.Options.PlaintextOut != null)
                {
                    nca.OpenDecryptedNca().WriteAllBytes(ctx.Options.PlaintextOut, ctx.Logger);
                }

                if (ctx.Options.CiphertextOut != null)
                {
                    nca.OpenEncryptedNca().WriteAllBytes(ctx.Options.CiphertextOut, ctx.Logger);
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
                    return OpenStorage(Nca.GetSectionIndexFromType(type, nca.Header.ContentType));
                }

                IFileSystem OpenFileSystemByType(NcaSectionType type)
                {
                    return OpenFileSystem(Nca.GetSectionIndexFromType(type, nca.Header.ContentType));
                }
            }
        }

        private static Validity VerifySignature2(this Nca nca)
        {
            if (nca.Header.ContentType != NcaContentType.Program) return Validity.Unchecked;

            IFileSystem pfs = nca.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.ErrorOnInvalid);
            if (!pfs.FileExists("main.npdm")) return Validity.Unchecked;

            pfs.OpenFile(out IFile npdmFile, "main.npdm".ToU8String(), OpenMode.Read).ThrowIfFailure();
            var npdm = new NpdmBinary(npdmFile.AsStream());

            return nca.Header.VerifySignature2(npdm.AciD.Rsa2048Modulus);
        }

        public static int GetMasterKeyRevisionFromKeyGeneration(int keyGeneration)
        {
            if (keyGeneration == 0) return 0;

            return keyGeneration - 1;
        }

        private static string Print(this NcaHolder ncaHolder)
        {
            Nca nca = ncaHolder.Nca;
            int masterKey = GetMasterKeyRevisionFromKeyGeneration(nca.Header.KeyGeneration);

            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("NCA:");
            PrintItem(sb, colLen, "Magic:", MagicToString(nca.Header.Magic));
            PrintItem(sb, colLen, $"Fixed-Key Signature{nca.VerifyHeaderSignature().GetValidityString()}:", nca.Header.Signature1.ToArray());
            PrintItem(sb, colLen, $"NPDM Signature{nca.VerifySignature2().GetValidityString()}:", nca.Header.Signature2.ToArray());
            PrintItem(sb, colLen, "Content Size:", $"0x{nca.Header.NcaSize:x12}");
            PrintItem(sb, colLen, "TitleID:", $"{nca.Header.TitleId:X16}");
            if (nca.CanOpenSection(NcaSectionType.Code))
            {
                IFileSystem fs = nca.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.None);
                Result r = fs.OpenFile(out IFile file, "/main.npdm".ToU8String(), OpenMode.Read);
                if (r.IsSuccess())
                {
                    var npdm = new NpdmBinary(file.AsStream(), null);
                    PrintItem(sb, colLen, "Title Name:", npdm.TitleName);
                }
            }

            PrintItem(sb, colLen, "SDK Version:", nca.Header.SdkVersion);
            PrintItem(sb, colLen, "Distribution type:", nca.Header.DistributionType.Print());
            PrintItem(sb, colLen, "Content Type:", nca.Header.ContentType.Print());
            PrintItem(sb, colLen, "Master Key Revision:", $"{masterKey} ({Utilities.GetKeyRevisionSummary(masterKey)})");
            PrintItem(sb, colLen, "Encryption Type:", $"{(nca.Header.HasRightsId ? "Titlekey crypto" : "Standard crypto")}");

            if (nca.Header.HasRightsId)
            {
                PrintItem(sb, colLen, "Rights ID:", nca.Header.RightsId.ToArray());
            }
            else
            {
                PrintKeyArea();
            }

            PrintSections();

            return sb.ToString();

            void PrintKeyArea()
            {
                NcaVersion version = nca.Header.FormatVersion;

                if (version == NcaVersion.Nca0RsaOaep)
                {
                    sb.AppendLine("Key Area (Encrypted):");
                    PrintItem(sb, colLen, "Key (RSA-OAEP Encrypted):", nca.Header.GetKeyArea().ToArray());

                    sb.AppendLine("Key Area (Decrypted):");
                    for (int i = 0; i < 2; i++)
                    {
                        PrintItem(sb, colLen, $"    Key {i} (Decrypted):", nca.GetDecryptedKey(i));
                    }
                }
                else if (version == NcaVersion.Nca0FixedKey)
                {
                    sb.AppendLine("Key Area:");
                    for (int i = 0; i < 2; i++)
                    {
                        PrintItem(sb, colLen, $"    Key {i}:", nca.Header.GetEncryptedKey(i).ToArray());
                    }
                }
                else
                {
                    int keyCount = version == NcaVersion.Nca0 ? 2 : 4;

                    PrintItem(sb, colLen, "Key Area Encryption Key:", nca.Header.KeyAreaKeyIndex);
                    sb.AppendLine("Key Area (Encrypted):");
                    for (int i = 0; i < keyCount; i++)
                    {
                        PrintItem(sb, colLen, $"    Key {i} (Encrypted):", nca.Header.GetEncryptedKey(i).ToArray());
                    }

                    sb.AppendLine("Key Area (Decrypted):");
                    for (int i = 0; i < keyCount; i++)
                    {
                        PrintItem(sb, colLen, $"    Key {i} (Decrypted):", nca.GetDecryptedKey(i));
                    }
                }
            }

            void PrintSections()
            {
                sb.AppendLine("Sections:");

                for (int i = 0; i < 4; i++)
                {
                    if (!nca.Header.IsSectionEnabled(i)) continue;

                    NcaFsHeader sectHeader = nca.GetFsHeader(i);
                    bool isExefs = nca.Header.ContentType == NcaContentType.Program && i == 0;

                    sb.AppendLine($"    Section {i}:");
                    PrintItem(sb, colLen, "        Offset:", $"0x{nca.Header.GetSectionStartOffset(i):x12}");
                    PrintItem(sb, colLen, "        Size:", $"0x{nca.Header.GetSectionSize(i):x12}");
                    PrintItem(sb, colLen, "        Partition Type:", GetPartitionType(sectHeader, isExefs, nca.Header.IsNca0()));
                    PrintItem(sb, colLen, "        Section CTR:", $"{sectHeader.Counter:x16}");
                    PrintItem(sb, colLen, "        Section Validity:", $"{ncaHolder.Validities[i].Print()}");

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

            static string GetPartitionType(NcaFsHeader fsHeader, bool isExefs, bool isNca0)
            {
                if (isExefs) return "ExeFS";
                if (isNca0 && fsHeader.FormatType == NcaFormatType.Romfs) return "NCA0 RomFS";

                return fsHeader.FormatType.Print() + (fsHeader.IsPatchSection() ? " patch" : "");
            }

            void PrintSha256Hash(NcaFsHeader sect, int index)
            {
                NcaFsIntegrityInfoSha256 hashInfo = sect.GetIntegrityInfoSha256();

                PrintItem(sb, colLen, $"        Master Hash{nca.ValidateSectionMasterHash(index).GetValidityString()}:", hashInfo.MasterHash.ToArray());
                sb.AppendLine("        Hash Table:");

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
            public Validity[] Validities = new Validity[4];
        }
    }
}
