using System;
using System.Collections.Generic;
using System.Text;
using LibHac;
using LibHac.IO;

namespace Net
{
    public class ContentManager
    {
        private IFileSystem ContentFs { get; }
        private Cache ContentCache { get; }

        public ContentManager(IFileSystem contentFs)
        {
            ContentFs = contentFs;
            ContentCache = new Cache(contentFs);
        }

        public bool OpenCnmtFile(ulong titleId, int version, out IFile file)
        {
            if (ContentCache.TryOpenMetaNca(titleId, version, out file))
            {
                return true;
            }
            if (cnmt != null) return cnmt;

            if (Certificate == null) return null;

            DownloadCnmt(titleId, version);
            return GetCnmtFileFromCache(titleId, version);
        }
    }
}
