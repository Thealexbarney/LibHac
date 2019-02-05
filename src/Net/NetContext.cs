using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using LibHac;
using LibHac.IO;
using LibHac.IO.RomFs;

namespace Net
{
    internal class NetContext
    {
        private X509Certificate2 Certificate { get; set; }
        private X509Certificate2 CertificateCommon { get; set; }
        private string Token { get; }
        private string Eid { get; } = "lp1";
        private ulong Did { get; }
        private string Firmware { get; } = "6.0.0-5.0";
        private string CachePath { get; } = "titles";
        private Context ToolCtx { get; }
        public Database Db { get; }

        private const string VersionUrl = "https://tagaya.hac.lp1.eshop.nintendo.net/tagaya/hac_versionlist";

        public NetContext(Context ctx)
        {
            ToolCtx = ctx;
            Did = ctx.Options.DeviceId;
            Token = ctx.Options.Token;

            if (ctx.Options.CertFile != null)
            {
                SetCertificate(ctx.Options.CertFile);
            }

            if (ctx.Options.CommonCertFile != null)
            {
                CertificateCommon = new X509Certificate2(ctx.Options.CommonCertFile, "shop");
            }

            Directory.CreateDirectory(CachePath);
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
                IStorage sect = nca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid, true);
                var pfs0 = new PartitionFileSystem(sect);
                IFile file = pfs0.OpenFile(pfs0.Files[0], OpenMode.Read);

                var cnmt = new Cnmt(file.AsStream());
                return cnmt;
            }
        }

        public IStorage GetCnmtFile(ulong titleId, int version)
        {
            var cnmt = GetCnmtFileFromCache(titleId, version);
            if (cnmt != null) return cnmt;

            if (Certificate == null) return null;

            DownloadCnmt(titleId, version);
            return GetCnmtFileFromCache(titleId, version);
        }

        public IStorage GetCnmtFileFromCache(ulong titleId, int version)
        {
            string titleDir = GetTitleDir(titleId, version);
            if (!Directory.Exists(titleDir)) return null;

            var cnmtFiles = Directory.GetFiles(titleDir, "*.cnmt.nca").ToArray();

            if (cnmtFiles.Length == 1)
            {
                return new FileStream(cnmtFiles[0], FileMode.Open, FileAccess.Read, FileShare.Read).AsStorage();
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
            var romfs = new RomFsFileSystem(nca.OpenSection(0, false, IntegrityCheckLevel.ErrorOnInvalid, true));
            IFile controlNacp = romfs.OpenFile("/control.nacp", OpenMode.Read);

            var control = new Nacp(controlNacp.AsStream());
            return control;
        }

        public IStorage GetNcaFile(ulong titleId, int version, string ncaId)
        {
            string titleDir = GetTitleDir(titleId, version);
            if (!Directory.Exists(titleDir)) return null;

            var filePath = Path.Combine(titleDir, $"{ncaId.ToLower()}.nca");
            if (!File.Exists(filePath))
            {
                DownloadFile(GetContentUrl(ncaId), filePath);
            }

            if (!File.Exists(filePath)) return null;

            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read).AsStorage(false);
        }

        public List<SuperflyInfo> GetSuperfly(ulong titleId)
        {
            var filename = GetSuperflyFile(titleId);
            return Json.ReadSuperfly(filename);
        }

        public string GetSuperflyFile(ulong titleId)
        {
            string titleDir = GetTitleDir(titleId);

            var filePath = Path.Combine(titleDir, $"{titleId:x16}.json");
            if (!File.Exists(filePath))
            {
                DownloadFile(GetSuperflyUrl(titleId), filePath);
            }

            if (!File.Exists(filePath)) return null;

            return filePath;
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

            var dir = Path.GetDirectoryName(filePath) ?? throw new DirectoryNotFoundException();
            Directory.CreateDirectory(dir);

            try
            {
                using (var responseStream = response.GetResponseStream())
                using (var outStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    responseStream.CopyStream(outStream, response.ContentLength, ToolCtx.Logger);
                }
            }
            catch (Exception)
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }

                throw;
            }
        }

        private string GetTitleDir(ulong titleId, int version = -1)
        {
            if (version >= 0)
            {
                return Path.Combine(CachePath, $"{titleId:x16}", $"{version}");
            }

            return Path.Combine(CachePath, $"{titleId:x16}");
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

        public string GetSuperflyUrl(ulong titleId)
        {
            string url = $"https://superfly.hac.{Eid}.d4c.nintendo.net/v1/a/{titleId:x16}/dv";
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
            request.Accept = "*/*";
            request.UserAgent = $"NintendoSDK Firmware/{Firmware} (platform:NX; did:{Did}; eid:{Eid})";
            request.Headers.Add("X-Nintendo-DenebEdgeToken", Token);
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
