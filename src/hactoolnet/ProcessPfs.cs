using System.IO;
using System.Reflection;
using System.Text;
using LibHac;
using LibHac.IO;
using LibHac.IO.NcaUtils;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessPfs
    {
        public static void Process(Context ctx)
        {
            using (var file = new LocalStorage(ctx.Options.InFile, FileAccess.Read))
            {
                var pfs = new PartitionFileSystem(file);
                ctx.Logger.LogMessage(pfs.Print());

                if (ctx.Options.OutDir != null)
                {
                    pfs.Extract(ctx.Options.OutDir, ctx.Logger);
                }
            }
        }

        private static string Print(this PartitionFileSystem pfs)
        {
            const int colLen = 36;
            const int fileNameLen = 39;

            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("PFS0:");

            PrintItem(sb, colLen, "Magic:", pfs.Header.Magic);
            PrintItem(sb, colLen, "Number of files:", pfs.Header.NumFiles);

            for (int i = 0; i < pfs.Files.Length; i++)
            {
                PartitionFileEntry file = pfs.Files[i];

                string label = i == 0 ? "Files:" : "";
                string offsets = $"{file.Offset:x12}-{file.Offset + file.Size:x12}{file.HashValidity.GetValidityString()}";
                string data = $"pfs0:/{file.Name}".PadRight(fileNameLen) + offsets;

                PrintItem(sb, colLen, label, data);
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

            foreach (Nca nca in title.Ncas)
            {
                builder.AddFile(nca.Filename, nca.GetStorage().AsFile(OpenMode.Read));
            }

            var ticket = new Ticket
            {
                SignatureType = TicketSigType.Rsa2048Sha256,
                Signature = new byte[0x200],
                Issuer = "Root-CA00000003-XS00000020",
                FormatVersion = 2,
                RightsId = title.MainNca.Header.RightsId,
                TitleKeyBlock = title.MainNca.TitleKey,
                CryptoType = title.MainNca.Header.CryptoType2,
                SectHeaderOffset = 0x2C0
            };
            byte[] ticketBytes = ticket.GetBytes();
            builder.AddFile($"{ticket.RightsId.ToHexString()}.tik", new MemoryStream(ticketBytes).AsIFile(OpenMode.ReadWrite));

            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            Stream cert = thisAssembly.GetManifestResourceStream("hactoolnet.CA00000003_XS00000020");
            builder.AddFile($"{ticket.RightsId.ToHexString()}.cert", cert.AsIFile(OpenMode.Read));
            
            using (var outStream = new FileStream(ctx.Options.NspOut, FileMode.Create, FileAccess.ReadWrite))
            {
                IStorage builtPfs = builder.Build(PartitionFileSystemType.Standard);
                builtPfs.CopyToStream(outStream, builtPfs.GetSize(), ctx.Logger);
            }
        }
    }
}
