using System;
using System.Linq;
using System.Text;
using LibHac;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Util;
using static hactoolnet.Print;

namespace hactoolnet
{
    internal static class ProcessNax0
    {
        public static void Process(Context ctx)
        {
            if (ctx.Options.SdPath == null)
            {
                ctx.Logger.LogMessage("SD path must be specified.");
                return;
            }

            Span<AesXtsKey> keys = ctx.KeySet.SdCardEncryptionKeys;

            using var baseFile = new LocalFile(ctx.Options.InFile, OpenMode.Read);

            AesXtsFile xtsFile = null;
            int contentType = 0;

            for (int i = 0; i < keys.Length; i++)
            {
                byte[] kekSource = keys[i].SubKeys[0].DataRo.ToArray();
                byte[] validationKey = keys[i].SubKeys[1].DataRo.ToArray();

                try
                {
                    xtsFile = new AesXtsFile(OpenMode.Read, baseFile, ctx.Options.SdPath.ToU8String(), kekSource, validationKey, 0x4000);
                    contentType = i;

                    break;
                }
                catch (HorizonResultException) { }
            }

            if (xtsFile == null)
            {
                ctx.Logger.LogMessage("Error: NAX0 key derivation failed. Check SD card seed and relative path.");
                return;
            }

            ctx.Logger.LogMessage(xtsFile.Print(contentType));

            if (string.IsNullOrWhiteSpace(ctx.Options.PlaintextOut)) return;

            xtsFile.AsStorage().WriteAllBytes(ctx.Options.PlaintextOut, ctx.Logger);
            ctx.Logger.LogMessage($"Saved Decrypted NAX0 Content to {ctx.Options.PlaintextOut}...");
        }

        private static string Print(this AesXtsFile xtsFile, int contentType)
        {
            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("NAX0:");

            AesXtsFileHeader header = xtsFile.Header;
            uint magic = header.Magic;

            PrintItem(sb, colLen, "    Magic:", StringUtils.Utf8ToString(SpanHelpers.AsReadOnlyByteSpan(in magic)));
            PrintItem(sb, colLen, "    Content Type:", GetContentType(contentType));
            PrintItem(sb, colLen, "    Content Size:", $"{header.Size:x12}");
            PrintItem(sb, colLen, "    Header HMAC:", header.Signature);
            PrintItem(sb, colLen, "    Encrypted Keys:", header.EncryptedKey1.Concat(header.EncryptedKey2).ToArray());
            PrintItem(sb, colLen, "    Decrypted Keys:", header.DecryptedKey1.Concat(header.DecryptedKey2).ToArray());

            return sb.ToString();
        }

        private static string GetContentType(int type) => type switch
        {
            0 => "Save",
            1 => "NCA",
            2 => "Custom storage",
            _ => "Unknown"
        };
    }
}
