using System;

namespace LibHac.IO.NcaUtils
{
    public static class NcaExtensions
    {
        public static IStorage OpenStorage(this Nca nca, int index, IntegrityCheckLevel integrityCheckLevel, bool openRaw)
        {
            if (openRaw) return nca.OpenRawStorage(index);
            return nca.OpenStorage(index, integrityCheckLevel);
        }

        public static IStorage OpenStorage(this Nca nca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel, bool openRaw)
        {
            if (openRaw) return nca.OpenRawStorage(type);
            return nca.OpenStorage(type, integrityCheckLevel);
        }

        public static void ExportSection(this Nca nca, int index, string filename, bool raw = false,
            IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            nca.OpenStorage(index, integrityCheckLevel, raw)
                .WriteAllBytes(filename, logger);
        }

        public static void ExtractSection(this Nca nca, int index, string outputDir, IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            if (index < 0 || index > 3) throw new IndexOutOfRangeException();
            if (!nca.SectionIsDecryptable(index)) return;

            IFileSystem fs = nca.OpenFileSystem(index, integrityCheckLevel);
            fs.Extract(outputDir, logger);
        }

        public static Validity VerifyNca(this Nca nca, IProgressReport logger = null, bool quiet = false)
        {
            for (int i = 0; i < 3; i++)
            {
                if (nca.Sections[i] != null)
                {
                    Validity sectionValidity = VerifySection(nca, i, logger, quiet);

                    if (sectionValidity == Validity.Invalid) return Validity.Invalid;
                }
            }

            return Validity.Valid;
        }

        public static Validity VerifySection(this Nca nca, int index, IProgressReport logger = null, bool quiet = false)
        {
            if (nca.Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));

            NcaSection sect = nca.Sections[index];
            NcaHashType hashType = sect.Header.HashType;
            if (hashType != NcaHashType.Sha256 && hashType != NcaHashType.Ivfc) return Validity.Unchecked;

            var stream = nca.OpenStorage(index, IntegrityCheckLevel.IgnoreOnInvalid, false) as HierarchicalIntegrityVerificationStorage;
            if (stream == null) return Validity.Unchecked;

            if (!quiet) logger?.LogMessage($"Verifying section {index}...");
            Validity validity = stream.Validate(true, logger);

            if (hashType == NcaHashType.Ivfc)
            {
                stream.SetLevelValidities(sect.Header.IvfcInfo);
            }
            else if (hashType == NcaHashType.Sha256)
            {
                sect.Header.Sha256Info.HashValidity = validity;
            }

            return validity;
        }
    }
}
