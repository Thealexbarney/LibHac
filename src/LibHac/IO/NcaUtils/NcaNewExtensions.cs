using System;

namespace LibHac.IO.NcaUtils
{
    public static class NcaNewExtensions
    {
        public static IStorage OpenStorage(this NcaNew nca, int index, IntegrityCheckLevel integrityCheckLevel,
            bool openRaw)
        {
            if (openRaw) return nca.OpenRawStorage(index);
            return nca.OpenStorage(index, integrityCheckLevel);
        }

        public static IStorage OpenStorage(this NcaNew nca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel,
            bool openRaw)
        {
            if (openRaw) return nca.OpenRawStorage(type);
            return nca.OpenStorage(type, integrityCheckLevel);
        }

        public static IFileSystem OpenFileSystem(this NcaNew nca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return nca.OpenFileSystem(nca.GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public static IFileSystem OpenFileSystemWithPatch(this NcaNew nca, NcaNew patchNca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return nca.OpenFileSystemWithPatch(patchNca, nca.GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public static IStorage OpenRawStorage(this NcaNew nca, NcaSectionType type)
        {
            return nca.OpenRawStorage(nca.GetSectionIndexFromType(type));
        }

        public static IStorage OpenRawStorageWithPatch(this NcaNew nca, NcaNew patchNca, NcaSectionType type)
        {
            return nca.OpenRawStorageWithPatch(patchNca, nca.GetSectionIndexFromType(type));
        }

        public static IStorage OpenStorage(this NcaNew nca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return nca.OpenStorage(nca.GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public static IStorage OpenStorageWithPatch(this NcaNew nca, NcaNew patchNca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return nca.OpenStorageWithPatch(patchNca, nca.GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public static void ExportSection(this NcaNew nca, int index, string filename, bool raw = false,
            IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            nca.OpenStorage(index, integrityCheckLevel, raw)
                .WriteAllBytes(filename, logger);
        }

        public static void ExportSection(this NcaNew nca, NcaSectionType type, string filename, bool raw = false,
            IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            nca.OpenStorage(type, integrityCheckLevel, raw)
                .WriteAllBytes(filename, logger);
        }

        public static void ExtractSection(this NcaNew nca, int index, string outputDir,
            IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            IFileSystem fs = nca.OpenFileSystem(index, integrityCheckLevel);
            fs.Extract(outputDir, logger);
        }

        public static void ExtractSection(this NcaNew nca, NcaSectionType type, string outputDir,
            IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            IFileSystem fs = nca.OpenFileSystem(type, integrityCheckLevel);
            fs.Extract(outputDir, logger);
        }

        public static IStorage OpenDecryptedNca(this NcaNew nca)
        {
            var builder = new ConcatenationStorageBuilder();
            builder.Add(nca.OpenDecryptedHeaderStorage(), 0);

            for (int i = 0; i < NcaHeaderNew.SectionCount; i++)
            {
                if (nca.Header.IsSectionEnabled(i))
                {
                    builder.Add(nca.OpenRawStorage(i), nca.Header.GetSectionStartOffset(i));
                }
            }

            return builder.Build();
        }

        public static int GetSectionIndexFromType(this NcaNew nca, NcaSectionType type)
        {
            return SectionIndexFromType(type, nca.Header.ContentType);
        }

        public static int SectionIndexFromType(NcaSectionType type, ContentType contentType)
        {
            switch (type)
            {
                case NcaSectionType.Code when contentType == ContentType.Program: return 0;
                case NcaSectionType.Data when contentType == ContentType.Program: return 1;
                case NcaSectionType.Logo when contentType == ContentType.Program: return 2;
                case NcaSectionType.Data: return 0;
                default: throw new ArgumentOutOfRangeException(nameof(type), "NCA does not contain this section type.");
            }
        }
    }
}
