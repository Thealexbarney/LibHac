using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using libhac;

namespace Net
{
    internal class NetContext
    {
        private X509Certificate2 Certificate { get; set; }
        private string Eid { get; } = "lp1";
        private ulong Did { get; }
        private string Firmware { get; } = "5.1.0-3.0";
        private string CachePath { get; } = "titles";
        private Context ToolCtx { get; }

        public NetContext(Context ctx)
        {
            ToolCtx = ctx;
            Did = ctx.Options.DeviceId;
            if (ctx.Options.CertFile != null)
            {
                SetCertificate(ctx.Options.CertFile);
            }
        }

        public void SetCertificate(string filename)
        {
            Certificate = new X509Certificate2(filename, "switch");
        }

        public Cnmt GetCnmt(ulong titleId, int version)
        {
            using (var stream = GetCnmtFile(titleId, version))
            {
                var nca = new Nca(ToolCtx.Keyset, stream, true);
                Stream sect = nca.OpenSection(0, false);
                var pfs0 = new Pfs0(sect);
                var file = pfs0.GetFile(0);

                var cnmt = new Cnmt(new MemoryStream(file));
                return cnmt;
            }
        }

        public Stream GetCnmtFile(ulong titleId, int version)
        {
            var cnmt = GetCnmtFileFromCache(titleId, version);
            if (cnmt != null) return cnmt;

            if (Certificate == null) return null;

            DownloadCnmt(titleId, version);
            return GetCnmtFileFromCache(titleId, version);
        }

        public Stream GetCnmtFileFromCache(ulong titleId, int version)
        {
            string titleDir = GetTitleDir(titleId, version);
            var cnmtFiles = Directory.GetFiles(titleDir, "*.cnmt.nca").ToArray();

            if (cnmtFiles.Length == 1)
            {
                return new FileStream(cnmtFiles[0], FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            if (cnmtFiles.Length > 1)
            {
                throw new FileNotFoundException($"More than cnmt file exists for {titleId:x16}v{version}");
            }

            return null;
        }

        private void DownloadCnmt(ulong titleId, int version)
        {
            var titleDir = GetTitleDir(titleId, version);

            var ncaId = GetMetadataNcaId(titleId, version);
            var filename = $"{ncaId}.cnmt.nca";
            var filePath = Path.Combine(titleDir, filename);
            DownloadFile(GetContentUrl(ncaId), filePath);
        }

        public void DownloadFile(string url, string filePath)
        {
            var response = Request("GET", url);
            using (var responseStream = response.GetResponseStream())
            using (var outStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
            {
                var dir = Path.GetDirectoryName(filePath) ?? throw new DirectoryNotFoundException();
                Directory.CreateDirectory(dir);
                responseStream.CopyStream(outStream, response.ContentLength, ToolCtx.Logger);
            }
        }

        private string GetTitleDir(ulong titleId, int version)
        {
            var titleDir = Path.Combine(CachePath, $"{titleId:x16}", $"{version}");
            Directory.CreateDirectory(titleDir);
            return titleDir;
        }

        public string GetContentUrl(string ncaId)
        {
            string url = $"{GetAtumUrl()}/c/a/{ncaId}";
            return url;
        }

        public string GetMetadataNcaId(ulong titleId, int version)
        {
            string url = $"{GetAtumUrl()}/t/a/{titleId:x16}/{version}?deviceid={Did}";

            using (WebResponse response = Request("HEAD", url))
            {
                return response.Headers.Get("X-Nintendo-Content-ID");
            }
        }

        private string GetAtumUrl()
        {
            return $"https://atum.hac.{Eid}.d4c.nintendo.net";
        }

        public WebResponse Request(string method, string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ClientCertificates.Add(Certificate);
            request.UserAgent = string.Format("NintendoSDK Firmware/{0} (platform:NX; did:{1}; eid:{2})", Firmware, Did, Eid);
            request.Method = method;
            ServicePointManager.ServerCertificateValidationCallback = ((sender, certificate, chain, sslPolicyErrors) => true);
            if (((HttpWebResponse)request.GetResponse()).StatusCode != HttpStatusCode.OK) { Console.WriteLine("http error"); return null; }
            return request.GetResponse();
        }
    }
}
