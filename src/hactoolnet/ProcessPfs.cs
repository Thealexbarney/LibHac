using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.FsSystem.Impl;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Util;
using static hactoolnet.Print;
using Path = LibHac.Fs.Path;

namespace hactoolnet;

using NspRootFileSystemCore =
    PartitionFileSystemCore<NintendoSubmissionPackageRootFileSystemMeta, NintendoSubmissionPackageRootFileSystemFormat,
        NintendoSubmissionPackageRootFileSystemFormat.PartitionFileSystemHeaderImpl,
        NintendoSubmissionPackageRootFileSystemFormat.PartitionEntry>;

internal static class ProcessPfs
{
    public static void Process(Context ctx)
    {
        using var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read);

        IFileSystem fs = null;
        using UniqueRef<PartitionFileSystem> pfs = new UniqueRef<PartitionFileSystem>();
        using UniqueRef<Sha256PartitionFileSystem> hfs = new UniqueRef<Sha256PartitionFileSystem>();
        using UniqueRef<NspRootFileSystemCore> nsp = new UniqueRef<NspRootFileSystemCore>();

        using var sharedFile = new SharedRef<IStorage>(file);

        pfs.Reset(new PartitionFileSystem());
        Result res = pfs.Get.Initialize(in sharedFile);
        if (res.IsSuccess())
        {
            fs = pfs.Get;
            ctx.Logger.LogMessage(pfs.Get.Print());
        }
        else if (!ResultFs.PartitionSignatureVerificationFailed.Includes(res))
        {
            res.ThrowIfFailure();
        }
        else
        {
            // Reading the input as a PartitionFileSystem didn't work. Try reading it as an Sha256PartitionFileSystem
            hfs.Reset(new Sha256PartitionFileSystem());
            res = hfs.Get.Initialize(file);
            if (res.IsSuccess())
            {
                fs = hfs.Get;
                ctx.Logger.LogMessage(hfs.Get.Print());
            }
            else if (!ResultFs.Sha256PartitionSignatureVerificationFailed.Includes(res))
            {
                res.ThrowIfFailure();
            }
            else
            {
                nsp.Reset(new NspRootFileSystemCore());
                res = nsp.Get.Initialize(file);

                if (res.IsSuccess())
                {
                    fs = nsp.Get;
                    ctx.Logger.LogMessage(nsp.Get.Print());
                }
                else
                {
                    res.ThrowIfFailure();
                }
            }
        }

        if (ctx.Options.OutDir != null)
        {
            fs.Extract(ctx.Options.OutDir, ctx.Logger);
        }

        if (fs.EnumerateEntries("*.nca", SearchOptions.Default).Any())
        {
            ProcessAppFs.Process(ctx, fs);
        }
    }

    private static string Print<TMetaData, TFormat, THeader, TEntry>(this PartitionFileSystemCore<TMetaData, TFormat, THeader, TEntry> pfs)
        where TMetaData : PartitionFileSystemMetaCore<TFormat, THeader, TEntry>, new()
        where TFormat : IPartitionFileSystemFormat
        where THeader : unmanaged, IPartitionFileSystemHeader
        where TEntry : unmanaged, IPartitionFileSystemEntry
    {
        const int colLen = 36;

        var sb = new StringBuilder();
        sb.AppendLine();

        sb.AppendLine("PFS0:");

        using (var rootDir = new UniqueRef<IDirectory>())
        {
            using var rootPath = new Path();
            PathFunctions.SetUpFixedPath(ref rootPath.Ref(), "/"u8).ThrowIfFailure();
            pfs.OpenDirectory(ref rootDir.Ref, in rootPath, OpenDirectoryMode.All).ThrowIfFailure();
            rootDir.Get.GetEntryCount(out long entryCount).ThrowIfFailure();

            PrintItem(sb, colLen, "Magic:", StringUtils.Utf8ZToString(TFormat.VersionSignature));
            PrintItem(sb, colLen, "Number of files:", entryCount);

            var dirEntry = new DirectoryEntry();
            bool isFirstFile = true;

            while (true)
            {
                rootDir.Get.Read(out long entriesRead, new Span<DirectoryEntry>(ref dirEntry)).ThrowIfFailure();
                if (entriesRead == 0)
                    break;

                string label = isFirstFile ? "Files:" : "";
                string printedFilePath = $"pfs0:/{StringUtils.Utf8ZToString(dirEntry.Name)}";

                PrintItem(sb, colLen, label, printedFilePath);
                isFirstFile = false;
            }
        }

        return sb.ToString();
    }

    public static void CreateNsp(Context ctx, SwitchFs switchFs)
    {
        ulong id = ctx.Options.TitleId;
        if (id == 0)
        {
            ctx.Logger.LogMessage("Title ID must be specified to save title");
            return;
        }

        if (!switchFs.Titles.TryGetValue(id, out Title title))
        {
            ctx.Logger.LogMessage($"Could not find title {id:X16}");
            return;
        }

        var builder = new PartitionFileSystemBuilder();

        foreach (SwitchFsNca nca in title.Ncas)
        {
            builder.AddFile(nca.Filename, nca.Nca.BaseStorage.AsFile(OpenMode.Read));
        }

        var ticket = new Ticket
        {
            SignatureType = TicketSigType.Rsa2048Sha256,
            Signature = new byte[0x200],
            Issuer = "Root-CA00000003-XS00000020",
            FormatVersion = 2,
            RightsId = title.MainNca.Nca.Header.RightsId.ToArray(),
            TitleKeyBlock = title.MainNca.Nca.GetDecryptedTitleKey(),
            CryptoType = title.MainNca.Nca.Header.KeyGeneration,
            SectHeaderOffset = 0x2C0
        };
        byte[] ticketBytes = ticket.GetBytes();
        builder.AddFile($"{ticket.RightsId.ToHexString()}.tik", new MemoryStream(ticketBytes).AsIFile(OpenMode.ReadWrite));

        var thisAssembly = Assembly.GetExecutingAssembly();
        Stream cert = thisAssembly.GetManifestResourceStream("hactoolnet.CA00000003_XS00000020");
        builder.AddFile($"{ticket.RightsId.ToHexString()}.cert", cert.AsIFile(OpenMode.Read));

        using (var outStream = new FileStream(ctx.Options.NspOut, FileMode.Create, FileAccess.ReadWrite))
        {
            IStorage builtPfs = builder.Build(PartitionFileSystemType.Standard);
            builtPfs.GetSize(out long pfsSize).ThrowIfFailure();

            builtPfs.CopyToStream(outStream, pfsSize, ctx.Logger);
        }
    }
}