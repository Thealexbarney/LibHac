using System;
using System.Buffers.Binary;
using System.Diagnostics;

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

        public static Validity ValidateSectionMasterHash(this NcaNew nca, int index)
        {
            if (!nca.SectionExists(index)) throw new ArgumentException(nameof(index), Messages.NcaSectionMissing);
            if (!nca.CanOpenSection(index)) return Validity.MissingKey;

            NcaFsHeaderNew header = nca.Header.GetFsHeader(index);

            // The base data is needed to validate the hash, so use a trick involving the AES-CTR extended
            // encryption table to check if the decryption is invalid.
            // todo: If the patch replaces the data checked by the master hash, use that directly
            if (header.IsPatchSection())
            {
                if (header.EncryptionType != NcaEncryptionType.AesCtrEx) return Validity.Unchecked;

                Validity ctrExValidity = ValidateCtrExDecryption(nca, index);
                return ctrExValidity == Validity.Invalid ? Validity.Invalid : Validity.Unchecked;
            }

            byte[] expectedHash;
            long offset;
            long size;

            switch (header.HashType)
            {
                case NcaHashType.Ivfc:
                    NcaFsIntegrityInfoIvfc ivfcInfo = header.GetIntegrityInfoIvfc();

                    expectedHash = ivfcInfo.MasterHash.ToArray();
                    offset = ivfcInfo.GetLevelOffset(0);
                    size = 1 << ivfcInfo.GetLevelBlockSize(0);

                    break;
                case NcaHashType.Sha256:
                    NcaFsIntegrityInfoSha256 sha256Info = header.GetIntegrityInfoSha256();
                    expectedHash = sha256Info.MasterHash.ToArray();

                    offset = sha256Info.GetLevelOffset(0);
                    size = sha256Info.GetLevelSize(0);

                    break;
                default:
                    return Validity.Unchecked;
            }

            IStorage storage = nca.OpenRawStorage(index);

            var data = new byte[size];
            storage.Read(data, offset);

            byte[] actualHash = Crypto.ComputeSha256(data, 0, data.Length);

            if (Util.ArraysEqual(expectedHash, actualHash)) return Validity.Valid;

            return Validity.Invalid;
        }

        private static Validity ValidateCtrExDecryption(NcaNew nca, int index)
        {
            // The encryption subsection table in an AesCtrEx-encrypted partition contains the length of the entire partition.
            // The encryption table is always located immediately following the partition data, so the offset value of the encryption
            // table located in the NCA header should be the same as the size read from the encryption table.

            Debug.Assert(nca.CanOpenSection(index));

            NcaFsPatchInfo header = nca.Header.GetFsHeader(index).GetPatchInfo();
            IStorage decryptedStorage = nca.OpenRawStorage(index);

            Span<byte> buffer = stackalloc byte[sizeof(long)];
            decryptedStorage.Read(buffer, header.EncryptionTreeOffset + 8);
            long readDataSize = BinaryPrimitives.ReadInt64LittleEndian(buffer);

            if (header.EncryptionTreeOffset != readDataSize) return Validity.Invalid;

            return Validity.Valid;
        }

        public static Validity VerifyNca(this NcaNew nca, IProgressReport logger = null, bool quiet = false)
        {
            for (int i = 0; i < 3; i++)
            {
                if (nca.CanOpenSection(i))
                {
                    Validity sectionValidity = VerifySection(nca, i, logger, quiet);

                    if (sectionValidity == Validity.Invalid) return Validity.Invalid;
                }
            }

            return Validity.Valid;
        }

        public static Validity VerifySection(this NcaNew nca, int index, IProgressReport logger = null, bool quiet = false)
        {
            NcaFsHeaderNew sect = nca.Header.GetFsHeader(index);
            NcaHashType hashType = sect.HashType;
            if (hashType != NcaHashType.Sha256 && hashType != NcaHashType.Ivfc) return Validity.Unchecked;

            var stream = nca.OpenStorage(index, IntegrityCheckLevel.IgnoreOnInvalid, false)
                as HierarchicalIntegrityVerificationStorage;
            if (stream == null) return Validity.Unchecked;

            if (!quiet) logger?.LogMessage($"Verifying section {index}...");
            Validity validity = stream.Validate(true, logger);

            return validity;
        }
    }
}
