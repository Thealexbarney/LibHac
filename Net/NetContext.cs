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
        private X509Certificate2 CertificateCommon { get; set; }
        private string Eid { get; } = "lp1";
        private ulong Did { get; }
        private string Firmware { get; } = "5.1.0-3.0";
        private string CachePath { get; } = "titles";
        private Context ToolCtx { get; }
        public Database Db { get; }

        private const string VersionUrl = "https://tagaya.hac.lp1.eshop.nintendo.net/tagaya/hac_versionlist";

        public NetContext(Context ctx)
        {
            ToolCtx = ctx;
            Did = ctx.Options.DeviceId;
            if (ctx.Options.CertFile != null)
            {
                SetCertificate(ctx.Options.CertFile);
            }

            if (ctx.Options.CommonCertFile != null)
            {
                CertificateCommon = new X509Certificate2(ctx.Options.CommonCertFile, "shop");
            }

            var databaseFile = Path.Combine(CachePath, "database.json");
            if (!File.Exists(databaseFile))
            {
                File.WriteAllText(databaseFile, new Database().Serialize());
            }
            Db = Database.Deserialize(databaseFile);
        }

        public void Save()
        {
            var databaseFile = Path.Combine(CachePath, "database.json");

            File.WriteAllText(databaseFile, Db.Serialize());
        }

        public void SetCertificate(string filename)
        {
            Certificate = new X509Certificate2(filename, "switch");
        }

        public Cnmt GetCnmt(ulong titleId, int version)
        {
            using (var stream = GetCnmtFile(titleId, version))
            {
                if (stream == null) return null;

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
                throw new FileNotFoundException($"More than 1 cnmt file exists for {titleId:x16}v{version}");
            }

            return null;
        }

        public Nacp GetControl(ulong titleId, int version)
        {
            var cnmt = GetCnmt(titleId, version);
            var controlEntry = cnmt?.ContentEntries.FirstOrDefault(x => x.Type == CnmtContentType.Control);
            if (controlEntry == null) return null;

            var controlNca = GetNcaFile(titleId, version, controlEntry.NcaId.ToHexString());
            if (controlNca == null) return null;

            var nca = new Nca(ToolCtx.Keyset, controlNca, true);
            var romfs = new Romfs(nca.OpenSection(0, false));
            var controlNacp = romfs.GetFile("/control.nacp");

            var reader = new BinaryReader(new MemoryStream(controlNacp));
            var control = new Nacp(reader);
            return control;
        }

        public Stream GetNcaFile(ulong titleId, int version, string ncaId)
        {
            string titleDir = GetTitleDir(titleId, version);
            var filePath = Path.Combine(titleDir, $"{ncaId.ToLower()}.nca");
            if (!File.Exists(filePath))
            {
                DownloadFile(GetContentUrl(ncaId), filePath);
            }

            if (!File.Exists(filePath)) return null;

            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private void DownloadCnmt(ulong titleId, int version)
        {
            var titleDir = GetTitleDir(titleId, version);

            var ncaId = GetMetadataNcaId(titleId, version);
            if (ncaId == null)
            {
                Console.WriteLine($"Could not get {titleId:x16}v{version} metadata");
                return;
            }

            var filename = $"{ncaId.ToLower()}.cnmt.nca";
            var filePath = Path.Combine(titleDir, filename);
            DownloadFile(GetMetaUrl(ncaId), filePath);
        }

        public void DownloadFile(string url, string filePath)
        {
            var response = Request("GET", url);
            if (response == null) return;
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

        public string GetMetaUrl(string ncaId)
        {
            string url = $"{GetAtumUrl()}/c/a/{ncaId.ToLower()}";
            return url;
        }

        public string GetContentUrl(string ncaId)
        {
            string url = $"{GetAtumUrl()}/c/c/{ncaId.ToLower()}";
            return url;
        }

        public string GetMetadataNcaId(ulong titleId, int version)
        {
            string url = $"{GetAtumUrl()}/t/a/{titleId:x16}/{version}?deviceid={Did}";

            using (WebResponse response = Request("HEAD", url))
            {
                return response?.Headers.Get("X-Nintendo-Content-ID");
            }
        }

        public VersionList GetVersionList()
        {
            var filename = Path.Combine(CachePath, "hac_versionlist");
            VersionList list = null;
            if (Db.IsVersionListCurrent() && File.Exists(filename))
            {
                return Json.ReadVersionList(filename);
            }

            DownloadVersionList();
            if (File.Exists(filename))
            {
                list = Json.ReadVersionList(filename);
            }

            return list;
        }

        public void DownloadVersionList()
        {
            DownloadFile(VersionUrl, Path.Combine(CachePath, "hac_versionlist"));
            Db.VersionListTime = DateTime.UtcNow;
        }

        private string GetAtumUrl()
        {
            return $"https://atum.hac.{Eid}.d4c.nintendo.net";
        }

        public WebResponse Request(string method, string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.ClientCertificates.Add(Certificate);
            request.UserAgent = $"NintendoSDK Firmware/{Firmware} (platform:NX; did:{Did}; eid:{Eid})";
            request.Method = method;
            ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

            try
            {
                if (((HttpWebResponse)request.GetResponse()).StatusCode == HttpStatusCode.OK)
                    return request.GetResponse();
            }
            catch (WebException ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("http error");
            return null;
        }
    }
}
