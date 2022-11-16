using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Common.Keys;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Util;

namespace LibHac.Boot;

public struct Package1MarikoOemHeader
{
    public Array16<byte> AesMac;
    public Array256<byte> RsaSig;
    public Array32<byte> Salt;
    public Array32<byte> Hash;
    public int Version;
    public int Size;
    public int LoadAddress;
    public int EntryPoint;
    public Array16<byte> Reserved;
}

public struct Package1MetaData
{
    public uint LoaderHash;
    public uint SecureMonitorHash;
    public uint BootloaderHash;
    public uint Reserved;
    private Array14<byte> _buildDate;
    public byte KeyGeneration;
    public byte Version;

    [UnscopedRef] public U8Span BuildDate => new U8Span(_buildDate);
    [UnscopedRef] public ReadOnlySpan<byte> Iv => SpanHelpers.CreateSpan(ref MemoryMarshal.GetReference(_buildDate.Items), 0x10);
}

public struct Package1Stage1Footer
{
    public int Pk11Size;
    public Array12<byte> Reserved;
    public Array16<byte> Iv;
}

public struct Package1Pk11Header
{
    public static readonly uint ExpectedMagic = 0x31314B50; // PK11

    public uint Magic;
    public int WarmBootSize;
    public int WarmBootOffset;
    public int Reserved;
    public int BootloaderSize;
    public int BootloaderOffset;
    public int SecureMonitorSize;
    public int SecureMonitorOffset;
}

public enum Package1Section
{
    Bootloader,
    SecureMonitor,
    WarmBoot
}

public class Package1
{
    private const int LegacyStage1Size = 0x4000;
    private const int ModernStage1Size = 0x7000;
    private const int MarikoWarmBootPlainTextSectionSize = 0x330;

    private SharedRef<IStorage> _baseStorage;
    private SubStorage _pk11Storage;
    private SubStorage _bodyStorage;

    private KeySet KeySet { get; set; }

    public bool IsModern { get; private set; }
    public bool IsMariko { get; private set; }

    /// <summary>
    /// Returns <see langword="true"/> if the package1 can be decrypted.
    /// </summary>
    public bool IsDecrypted { get; private set; }
    public byte KeyRevision { get; private set; }

    public int Pk11Size { get; private set; }

    private Package1MarikoOemHeader _marikoOemHeader;
    private Package1MetaData _metaData;
    private Package1Stage1Footer _stage1Footer;
    private Package1Pk11Header _pk11Header;
    private Array16<byte> _pk11Mac;

    public ref readonly Package1MarikoOemHeader MarikoOemHeader => ref _marikoOemHeader;
    public ref readonly Package1MetaData MetaData => ref _metaData;
    public ref readonly Package1Stage1Footer Stage1Footer => ref _stage1Footer;
    public ref readonly Package1Pk11Header Pk11Header => ref _pk11Header;
    public ref readonly Array16<byte> Pk11Mac => ref _pk11Mac;

    public Result Initialize(KeySet keySet, in SharedRef<IStorage> storage)
    {
        KeySet = keySet;
        _baseStorage.SetByCopy(in storage);

        // Read what might be a mariko header and check if it actually is a mariko header
        Result res = _baseStorage.Get.Read(0, SpanHelpers.AsByteSpan(ref _marikoOemHeader));
        if (res.IsFailure()) return res.Miss();

        IsMariko = IsMarikoImpl();

        if (IsMariko)
        {
            res = InitMarikoBodyStorage();
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = _baseStorage.Get.GetSize(out long baseStorageSize);
            if (res.IsFailure()) return res.Miss();

            _bodyStorage = new SubStorage(in _baseStorage, 0, baseStorageSize);
            res = _bodyStorage.Read(0, SpanHelpers.AsByteSpan(ref _metaData));
            if (res.IsFailure()) return res.Miss();
        }

        res = ParseStage1();
        if (res.IsFailure()) return res.Miss();

        res = ReadPk11Header();
        if (res.IsFailure()) return res.Miss();

        if (!IsMariko && IsModern)
        {
            res = ReadModernEristaMac();
            if (res.IsFailure()) return res.Miss();
        }

        res = SetPk11Storage();
        if (res.IsFailure()) return res.Miss();

        // Make sure the PK11 section sizes add up to the expected size
        if (IsDecrypted && !VerifyPk11Sizes())
        {
            return ResultLibHac.InvalidPackage1Pk11Size.Log();
        }

        return Result.Success;
    }

    /// <summary>
    /// Read the encrypted section of a Mariko Package1 and try to decrypt it.
    /// </summary>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultLibHac.InvalidPackage1MarikoBodySize"/>: The package1 is
    /// too small or the size in the OEM header is incorrect.</returns>
    private Result InitMarikoBodyStorage()
    {
        // Body must be large enough to hold at least one metadata struct
        if (MarikoOemHeader.Size < Unsafe.SizeOf<Package1MetaData>())
            return ResultLibHac.InvalidPackage1MarikoBodySize.Log();

        // Verify the body storage size is not smaller than the size in the header
        Result res = _baseStorage.Get.GetSize(out long totalSize);
        if (res.IsFailure()) return res.Miss();

        long bodySize = totalSize - Unsafe.SizeOf<Package1MarikoOemHeader>();
        if (bodySize < MarikoOemHeader.Size)
            return ResultLibHac.InvalidPackage1MarikoBodySize.Log();

        // Create body SubStorage and metadata buffers
        var bodySubStorage = new SubStorage(in _baseStorage, Unsafe.SizeOf<Package1MarikoOemHeader>(), bodySize);

        Span<Package1MetaData> metaData = stackalloc Package1MetaData[2];
        Span<byte> metaData1 = SpanHelpers.AsByteSpan(ref metaData[0]);
        Span<byte> metaData2 = SpanHelpers.AsByteSpan(ref metaData[1]);

        // Read both the plaintext metadata and encrypted metadata
        res = bodySubStorage.Read(0, MemoryMarshal.Cast<Package1MetaData, byte>(metaData));
        if (res.IsFailure()) return res.Miss();

        // Set the body storage and decrypted metadata
        _metaData = metaData[0];
        _bodyStorage = bodySubStorage;

        // The plaintext metadata is followed by an encrypted copy
        // If these two match then the body is already decrypted
        IsDecrypted = metaData1.SequenceEqual(metaData2);

        if (IsDecrypted)
        {
            return Result.Success;
        }

        // If encrypted, check if the body can be decrypted
        Crypto.Aes.DecryptCbc128(metaData2, metaData2, KeySet.MarikoBek, _metaData.Iv);
        IsDecrypted = metaData2.SequenceEqual(SpanHelpers.AsByteSpan(ref _metaData));

        // Get a decrypted body storage if we have the correct key
        if (IsDecrypted)
        {
            var decStorage = new AesCbcStorage(bodySubStorage, KeySet.MarikoBek, _metaData.Iv, true);
            var cachedStorage = new CachedStorage(decStorage, 0x4000, 1, true);
            _bodyStorage = new SubStorage(cachedStorage, 0, bodySize);
        }

        return Result.Success;
    }

    private Result ParseStage1()
    {
        // Erista package1ldr is stored unencrypted, so we can always directly read the size
        // field at the end of package1ldr.

        // Mariko package1ldr is stored encrypted. If we're able to decrypt it,
        // directly read the size field at the end of package1ldr.

        IsModern = !IsLegacyImpl();
        int stage1Size = IsModern ? ModernStage1Size : LegacyStage1Size;

        if (IsMariko && !IsDecrypted)
        {
            // If we're not able to decrypt the Mariko package1ldr, calculate the size by subtracting
            // the known package1ldr size from the total size in the OEM header.
            Pk11Size = MarikoOemHeader.Size - stage1Size;
            return Result.Success;
        }

        // Read the package1ldr footer
        int footerOffset = stage1Size - Unsafe.SizeOf<Package1Stage1Footer>();

        Result res = _bodyStorage.Read(footerOffset, SpanHelpers.AsByteSpan(ref _stage1Footer));
        if (res.IsFailure()) return res.Miss();

        // Get the PK11 size from the field in the unencrypted stage 1 footer
        Pk11Size = _stage1Footer.Pk11Size;

        return Result.Success;
    }

    private Result ReadPk11Header()
    {
        int pk11Offset = IsModern ? ModernStage1Size : LegacyStage1Size;

        return _bodyStorage.Read(pk11Offset, SpanHelpers.AsByteSpan(ref _pk11Header));
    }

    private Result ReadModernEristaMac()
    {
        return _baseStorage.Get.Read(ModernStage1Size + Pk11Size, _pk11Mac.Items);
    }

    private Result SetPk11Storage()
    {
        // Read the PK11 header from the body storage
        int pk11Offset = IsModern ? ModernStage1Size : LegacyStage1Size;

        Result res = _bodyStorage.Read(pk11Offset, SpanHelpers.AsByteSpan(ref _pk11Header));
        if (res.IsFailure()) return res.Miss();

        // Check if PK11 is already decrypted, creating the PK11 storage if it is
        IsDecrypted = _pk11Header.Magic == Package1Pk11Header.ExpectedMagic;

        if (IsDecrypted)
        {
            _pk11Storage = new SubStorage(_bodyStorage, pk11Offset, Pk11Size);
            return Result.Success;
        }

        var encPk11Storage = new SubStorage(_bodyStorage, pk11Offset, Pk11Size);

        // See if we have an Erista package1 key that can decrypt this PK11
        if (!IsMariko && TryFindEristaKeyRevision())
        {
            IsDecrypted = true;
            IStorage decPk11Storage;

            if (IsModern)
            {
                decPk11Storage = new AesCbcStorage(encPk11Storage, KeySet.Package1Keys[KeyRevision],
                    _stage1Footer.Iv, true);
            }
            else
            {
                decPk11Storage = new Aes128CtrStorage(encPk11Storage,
                    KeySet.Package1Keys[KeyRevision].DataRo.ToArray(), _stage1Footer.Iv.ItemsRo.ToArray(), true);
            }

            _pk11Storage = new SubStorage(new CachedStorage(decPk11Storage, 0x4000, 1, true), 0, Pk11Size);

            return Result.Success;
        }

        // We can't decrypt the PK11. Set Pk11Storage to the encrypted PK11 storage
        _pk11Storage = encPk11Storage;
        return Result.Success;
    }

    private delegate int Decryptor(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false);

    private bool TryFindEristaKeyRevision()
    {
        // Package1 has no indication of which key it's encrypted with,
        // so we must test each known key to find one that works

        var decHeader = new Package1Pk11Header();

        int start = IsModern ? 6 : 0;
        int end = IsModern ? 0x20 : 6;
        Decryptor decryptor = IsModern ? Crypto.Aes.DecryptCbc128 : Crypto.Aes.DecryptCtr128;

        for (int i = start; i < end; i++)
        {
            decryptor(SpanHelpers.AsByteSpan(ref _pk11Header), SpanHelpers.AsByteSpan(ref decHeader),
                KeySet.Package1Keys[i], _stage1Footer.Iv);

            if (decHeader.Magic == Package1Pk11Header.ExpectedMagic)
            {
                KeyRevision = (byte)i;
                _pk11Header = decHeader;
                return true;
            }
        }

        return false;
    }

    private bool VerifyPk11Sizes()
    {
        Assert.SdkRequires(IsDecrypted);

        int pk11Size = Unsafe.SizeOf<Package1Pk11Header>() + GetSectionSize(Package1Section.WarmBoot) +
                GetSectionSize(Package1Section.Bootloader) + GetSectionSize(Package1Section.SecureMonitor);

        pk11Size = Alignment.AlignUp(pk11Size, 0x10);

        return pk11Size == Pk11Size;
    }

    // MetaData must be read first
    private bool IsLegacyImpl()
    {
        return _metaData.Version < 0xE || StringUtils.Compare(_metaData.BuildDate, LegacyDateCutoff) < 0;
    }

    // MarikoOemHeader must be read first
    private bool IsMarikoImpl()
    {
        return MarikoOemHeader.AesMac.ItemsRo.IsZeros() && MarikoOemHeader.Reserved.ItemsRo.IsZeros();
    }

    /// <summary>
    /// Opens an <see cref="IStorage"/> of the entire package1, decrypting any encrypted data.
    /// </summary>
    /// <returns>If the package1 can be decrypted, an <see cref="IStorage"/>
    /// of the package1, <see langword="null"/>.</returns>
    public IStorage OpenDecryptedPackage1Storage()
    {
        if (!IsDecrypted)
            return null;

        var storages = new List<IStorage>();

        if (IsMariko)
        {
            int metaSize = Unsafe.SizeOf<Package1MetaData>();

            // The metadata at the start of the body is unencrypted, so don't take its data from the decrypted
            // body storage
            storages.Add(new SubStorage(in _baseStorage, 0, Unsafe.SizeOf<Package1MarikoOemHeader>() + metaSize));
            storages.Add(new SubStorage(_bodyStorage, metaSize, _marikoOemHeader.Size - metaSize));
        }
        else
        {
            int stage1Size = IsModern ? ModernStage1Size : LegacyStage1Size;

            storages.Add(new SubStorage(in _baseStorage, 0, stage1Size));
            storages.Add(_pk11Storage);

            if (IsModern)
            {
                storages.Add(new MemoryStorage(_pk11Mac.ItemsRo.ToArray()));
            }
        }

        return new ConcatenationStorage(storages, true);
    }

    /// <summary>
    /// Opens an <see cref="IStorage"/> of the warmboot section.
    /// </summary>
    /// <returns>If the section can be decrypted, an <see cref="IStorage"/>of the
    /// warmboot section; otherwise, <see langword="null"/>.</returns>
    public IStorage OpenWarmBootStorage() => OpenSectionStorage(Package1Section.WarmBoot);

    /// <summary>
    /// Opens an <see cref="IStorage"/> of the bootloader section.
    /// </summary>
    /// <returns>If the section can be decrypted, an <see cref="IStorage"/>of the
    /// bootloader section; otherwise, <see langword="null"/>.</returns>
    public IStorage OpenNxBootloaderStorage() => OpenSectionStorage(Package1Section.Bootloader);

    /// <summary>
    /// Opens an <see cref="IStorage"/> of the secure monitor section.
    /// </summary>
    /// <returns>If the section can be decrypted, an <see cref="IStorage"/>of the
    /// secure monitor section; otherwise, <see langword="null"/>.</returns>
    public IStorage OpenSecureMonitorStorage() => OpenSectionStorage(Package1Section.SecureMonitor);

    /// <summary>
    /// Opens an <see cref="IStorage"/> for the specified <see cref="Package1Section"/>.
    /// </summary>
    /// <param name="sectionType">The section to open.</param>
    /// <returns>If the section can be decrypted, an <see cref="IStorage"/>of that
    /// section; otherwise, <see langword="null"/>.</returns>
    public IStorage OpenSectionStorage(Package1Section sectionType)
    {
        if (!IsDecrypted)
            return null;

        int offset = Unsafe.SizeOf<Package1Pk11Header>() + GetSectionOffset(sectionType);
        int size = GetSectionSize(sectionType);

        return new SubStorage(_pk11Storage, offset, size);
    }

    public IStorage OpenDecryptedWarmBootStorage()
    {
        if (!IsDecrypted)
            return null;

        IStorage warmBootStorage = OpenWarmBootStorage();

        // Only Mariko warmboot storage is encrypted
        if (!IsMariko)
        {
            return warmBootStorage;
        }

        int size = GetSectionSize(Package1Section.WarmBoot);
        int encryptedSectionSize = size - MarikoWarmBootPlainTextSectionSize;

        var plainTextSection = new SubStorage(warmBootStorage, 0, MarikoWarmBootPlainTextSectionSize);
        var encryptedSubStorage =
            new SubStorage(warmBootStorage, MarikoWarmBootPlainTextSectionSize, encryptedSectionSize);

        var zeroIv = new Buffer16();
        IStorage decryptedSection = new AesCbcStorage(encryptedSubStorage, KeySet.MarikoBek, zeroIv.Bytes, true);

        decryptedSection = new CachedStorage(decryptedSection, 0x200, 1, true);

        return new ConcatenationStorage(new List<IStorage> { plainTextSection, decryptedSection }, true);
    }

    public int GetSectionSize(Package1Section sectionType)
    {
        if (!IsDecrypted)
            return 0;

        return sectionType switch
        {
            Package1Section.Bootloader => _pk11Header.BootloaderSize,
            Package1Section.SecureMonitor => _pk11Header.SecureMonitorSize,
            Package1Section.WarmBoot => _pk11Header.WarmBootSize,
            _ => 0
        };
    }

    public int GetSectionOffset(Package1Section sectionType)
    {
        if (!IsDecrypted)
            return 0;

        switch (GetSectionIndex(sectionType))
        {
            case 0:
                return 0;
            case 1:
                return GetSectionSize(GetSectionType(0));
            case 2:
                return GetSectionSize(GetSectionType(0)) + GetSectionSize(GetSectionType(1));
            default:
                return -1;
        }
    }

    private int GetSectionIndex(Package1Section sectionType)
    {
        if (_metaData.Version >= 0x07)
        {
            return sectionType switch
            {
                Package1Section.Bootloader => 0,
                Package1Section.SecureMonitor => 1,
                Package1Section.WarmBoot => 2,
                _ => -1
            };
        }

        if (_metaData.Version >= 0x02)
        {
            return sectionType switch
            {
                Package1Section.Bootloader => 1,
                Package1Section.SecureMonitor => 2,
                Package1Section.WarmBoot => 0,
                _ => -1
            };
        }

        return sectionType switch
        {
            Package1Section.Bootloader => 1,
            Package1Section.SecureMonitor => 0,
            Package1Section.WarmBoot => 2,
            _ => -1
        };
    }

    private Package1Section GetSectionType(int index)
    {
        if (GetSectionIndex(Package1Section.Bootloader) == index)
            return Package1Section.Bootloader;

        if (GetSectionIndex(Package1Section.SecureMonitor) == index)
            return Package1Section.SecureMonitor;

        if (GetSectionIndex(Package1Section.WarmBoot) == index)
            return Package1Section.WarmBoot;

        return (Package1Section)(-1);
    }

    private static ReadOnlySpan<byte> LegacyDateCutoff => // 20181107
        new[]
        {
            (byte) '2', (byte) '0', (byte) '1', (byte) '8', (byte) '1', (byte) '1', (byte) '0', (byte) '7'
        };
}