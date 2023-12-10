using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using LibHac.Boot;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.FsSrv;
using LibHac.Util;

namespace LibHac.Common.Keys;

public class KeySet
{
    public enum Mode
    {
        Dev,
        Prod
    }

    /// <summary>
    /// The number of keyblobs that were used for &lt; 6.2.0 crypto
    /// </summary>
    internal const int UsedKeyBlobCount = 6;
    internal const int SdCardKeyIdCount = 3;
    internal const int KeyRevisionCount = 0x20;
    internal const int TsecSecretCount = 0x40;
    internal const int MarikoAesClassKeyCount = 0xC;
    
    /// <summary>
    /// The number of slots reserved for current and future TSEC FW revisions.
    /// 8 was semi-arbitrarily chosen because there are only 3 FW revisions used for &gt;= 6.2.0 crypto as of Jan 2023,
    /// and it's unlikely that many more will be issued.
    /// </summary>
    internal const int TsecKeyRevisionCount = 8;

    private AllKeys _keys;
    private Mode _mode = Mode.Prod;

    public ref AllKeys KeyStruct => ref _keys;
    public Mode CurrentMode => _mode;

    private ref TsecSecrets Secrets => ref _keys.TsecSecrets;
    private ref RootKeys RootKeys => ref _mode == Mode.Dev ? ref _keys.RootKeysDev : ref _keys.RootKeysProd;
    private ref StoredKeys StoredKeys => ref _mode == Mode.Dev ? ref _keys.StoredKeysDev : ref _keys.StoredKeysProd;
    private ref DerivedKeys DerivedKeys => ref _mode == Mode.Dev ? ref _keys.DerivedKeysDev : ref _keys.DerivedKeysProd;
    private ref DerivedDeviceKeys DerivedDeviceKeys => ref _mode == Mode.Dev ? ref _keys.DerivedDeviceKeysDev : ref _keys.DerivedDeviceKeysProd;
    private ref RsaSigningKeys RsaSigningKeys => ref _mode == Mode.Dev ? ref _keys.RsaSigningKeysDev : ref _keys.RsaSigningKeysProd;
    private ref RsaKeys RsaKeys => ref _keys.RsaKeys;
    private ref DeviceRsaKeys DeviceRsaKeys => ref _keys.DeviceRsaKeys;

    private ref RsaSigningKeyParameters RsaSigningKeyParams => ref _mode == Mode.Dev
        ? ref _rsaSigningKeyParamsDev
        : ref _rsaSigningKeyParamsProd;

    public ExternalKeySet ExternalKeySet { get; } = new ExternalKeySet();

    public Span<AesKey> MarikoAesClassKeys => RootKeys.MarikoAesClassKeys;
    public ref AesKey MarikoKek => ref RootKeys.MarikoKek;
    public ref AesKey MarikoBek => ref RootKeys.MarikoBek;
    public Span<KeyBlob> KeyBlobs => RootKeys.KeyBlobs;
    public Span<AesKey> KeyBlobKeySources => _keys.KeySeeds.KeyBlobKeySources;
    public ref AesKey KeyBlobMacKeySource => ref _keys.KeySeeds.KeyBlobMacKeySource;
    
    public Span<AesKey> TsecSecrets => Secrets.Secrets;
    public Span<AesKey> TsecRootKeks => RootKeys.TsecRootKeks;
    public Span<AesKey> Package1MacKeks => RootKeys.Package1MacKeks;
    public Span<AesKey> Package1Keks => RootKeys.Package1Keks;
    public Span<AesKey> TsecAuthSignatures => _keys.KeySeeds.TsecAuthSignatures;
    public Span<AesKey> TsecRootKeys => RootKeys.TsecRootKeys;
    public Span<AesKey> MasterKekSources => _keys.KeySeeds.MasterKekSources;
    public Span<AesKey> GcTitleKeyKeks => RootKeys.GcTitleKeyKeks;

    public Span<AesKey> MarikoMasterKekSources => _mode == Mode.Dev
        ? _keys.KeySeeds.MarikoMasterKekSourcesDev[..]
        : _keys.KeySeeds.MarikoMasterKekSources[..];

    public Span<AesKey> MasterKeks => DerivedKeys.MasterKeks;
    public ref AesKey MasterKeySource => ref _keys.KeySeeds.MasterKeySource;
    public Span<AesKey> MasterKeys => DerivedKeys.MasterKeys;
    public Span<AesKey> Package1MacKeys => DerivedKeys.Package1MacKeys;
    public Span<AesKey> Package1Keys => DerivedKeys.Package1Keys;
    public Span<AesKey> Package2Keys => DerivedKeys.Package2Keys;
    public ref AesKey Package2KeySource => ref _keys.KeySeeds.Package2KeySource;
    public ref AesKey PerConsoleKeySource => ref _keys.KeySeeds.PerConsoleKeySource;
    public ref AesKey RetailSpecificAesKeySource => ref _keys.KeySeeds.RetailSpecificAesKeySource;
    public ref AesKey BisKekSource => ref _keys.KeySeeds.BisKekSource;
    public Span<AesXtsKey> BisKeySources => _keys.KeySeeds.BisKeySources;
    public ref AesKey AesKekGenerationSource => ref _keys.KeySeeds.AesKekGenerationSource;
    public ref AesKey AesKeyGenerationSource => ref _keys.KeySeeds.AesKeyGenerationSource;
    public ref AesKey KeyAreaKeyApplicationSource => ref _keys.KeySeeds.KeyAreaKeyApplicationSource;
    public ref AesKey KeyAreaKeyOceanSource => ref _keys.KeySeeds.KeyAreaKeyOceanSource;
    public ref AesKey KeyAreaKeySystemSource => ref _keys.KeySeeds.KeyAreaKeySystemSource;
    public ref AesKey TitleKekSource => ref _keys.KeySeeds.TitleKekSource;
    public ref AesKey HeaderKekSource => ref _keys.KeySeeds.HeaderKekSource;
    public ref AesKey SdCardKekSource => ref _keys.KeySeeds.SdCardKekSource;
    public Span<AesXtsKey> SdCardKeySources => _keys.KeySeeds.SdCardKeySources;
    public ref AesKey DeviceUniqueSaveMacKekSource => ref _keys.KeySeeds.DeviceUniqueSaveMacKekSource;
    public Span<AesKey> DeviceUniqueSaveMacKeySources => _keys.KeySeeds.DeviceUniqueSaveMacKeySources;
    public ref AesKey SeedUniqueSaveMacKekSource => ref _keys.KeySeeds.SeedUniqueSaveMacKekSource;
    public ref AesKey SeedUniqueSaveMacKeySource => ref _keys.KeySeeds.SeedUniqueSaveMacKeySource;
    public ref AesXtsKey HeaderKeySource => ref _keys.KeySeeds.HeaderKeySource;
    public ref AesXtsKey HeaderKey => ref DerivedKeys.HeaderKey;
    public Span<AesKey> TitleKeks => DerivedKeys.TitleKeks;
    public Span<Array3<AesKey>> KeyAreaKeys => DerivedKeys.KeyAreaKeys;
    public ref AesKey XciHeaderKey => ref StoredKeys.XciHeaderKey;
    public ref AesKey ETicketRsaKek => ref DerivedKeys.ETicketRsaKek;
    public ref AesKey SslRsaKek => ref DerivedKeys.SslRsaKek;

    public ref AesKey SecureBootKey => ref _keys.DeviceKeys.SecureBootKey;
    public ref AesKey TsecKey => ref _keys.DeviceKeys.TsecKey;
    public ref AesKey SdCardEncryptionSeed => ref _keys.DeviceKeys.SdCardEncryptionSeed;
    public Span<EncryptedKeyBlob> EncryptedKeyBlobs => _keys.DeviceKeys.EncryptedKeyBlobs;

    public Span<AesKey> KeyBlobKeys => DerivedDeviceKeys.KeyBlobKeys;
    public Span<AesKey> KeyBlobMacKeys => DerivedDeviceKeys.KeyBlobMacKeys;
    public ref AesKey DeviceKey => ref DerivedDeviceKeys.DeviceKey;
    public Span<AesXtsKey> BisKeys => DerivedDeviceKeys.BisKeys;
    public Span<AesKey> DeviceUniqueSaveMacKeys => DerivedDeviceKeys.DeviceUniqueSaveMacKeys;
    public ref AesKey SeedUniqueSaveMacKey => ref DerivedDeviceKeys.SeedUniqueSaveMacKey;

    // Todo: Make a separate type? Not actually an AES-XTS key, but it's still the same shape.
    public Span<AesXtsKey> SdCardEncryptionKeys => DerivedDeviceKeys.SdCardEncryptionKeys;

    public Span<RsaKey> NcaHeaderSigningKeys => RsaSigningKeys.NcaHeaderSigningKeys;
    public Span<RsaKey> AcidSigningKeys => RsaSigningKeys.AcidSigningKeys;
    public ref RsaKey Package2SigningKey => ref RsaSigningKeys.Package2SigningKey;
    public ref RsaFullKey BetaNca0KeyAreaKey => ref RsaKeys.BetaNca0KeyAreaKey;
    public ref RsaKeyPair ETicketRsaKey => ref DeviceRsaKeys.ETicketRsaKey;

    private RsaSigningKeyParameters _rsaSigningKeyParamsDev;
    private RsaSigningKeyParameters _rsaSigningKeyParamsProd;
    private RsaKeyParameters _rsaKeyParams;


    public Span<RSAParameters> NcaHeaderSigningKeyParams
    {
        get
        {
            ref Optional<Array2<RSAParameters>> keys = ref RsaSigningKeyParams.NcaHeaderSigningKeys;

            if (!keys.HasValue)
            {
                keys.Set(new Array2<RSAParameters>());
                keys.Value[0] = CreateRsaParameters(in NcaHeaderSigningKeys[0]);
                keys.Value[1] = CreateRsaParameters(in NcaHeaderSigningKeys[1]);
            }

            // Todo: Remove local variable after Roslyn issue #67697 is fixed
            ref Array2<RSAParameters> array = ref keys.Value;
            return array;
        }
    }

    public Span<RSAParameters> AcidSigningKeyParams
    {
        get
        {
            ref Optional<Array2<RSAParameters>> keys = ref RsaSigningKeyParams.AcidSigningKeys;

            if (!keys.HasValue)
            {
                keys.Set(new Array2<RSAParameters>());
                keys.Value[0] = CreateRsaParameters(in AcidSigningKeys[0]);
                keys.Value[1] = CreateRsaParameters(in AcidSigningKeys[1]);
            }

            // Todo: Remove local variable after Roslyn issue #67697 is fixed
            ref Array2<RSAParameters> array = ref keys.Value;
            return array;
        }
    }

    public ref RSAParameters Package2SigningKeyParams
    {
        get
        {
            ref Optional<RSAParameters> keys = ref RsaSigningKeyParams.Package2SigningKey;

            if (!keys.HasValue)
            {
                keys.Set(new RSAParameters());
                keys.Value = CreateRsaParameters(in Package2SigningKey);
            }

            return ref keys.Value;
        }
    }

    public ref RSAParameters BetaNca0KeyAreaKeyParams
    {
        get
        {
            ref Optional<RSAParameters> keys = ref _rsaKeyParams.BetaNca0KeyAreaKey;

            if (!keys.HasValue)
            {
                keys.Set(CreateRsaParameters(in BetaNca0KeyAreaKey));
            }

            return ref keys.Value;
        }
    }

    public ref RSAParameters ETicketRsaKeyParams
    {
        get
        {
            ref Optional<RSAParameters> keys = ref _rsaKeyParams.ETicketRsaKey;

            if (!keys.HasValue && !ETicketRsaKey.PublicExponent[..].IsZeros())
            {
                RSAParameters rsaParams = Rsa.RecoverParameters(ETicketRsaKey.Modulus, ETicketRsaKey.PublicExponent, ETicketRsaKey.PrivateExponent);
                keys.Set(rsaParams);
            }

            return ref keys.Value;
        }
    }

    public void SetSdSeed(ReadOnlySpan<byte> sdSeed)
    {
        if (sdSeed.Length != 0x10)
            throw new ArgumentException("Sd card encryption seed must be 16 bytes long.");

        sdSeed.CopyTo(SdCardEncryptionSeed);
        DeriveSdCardKeys();
    }

    public void SetMode(Mode mode) => _mode = mode;

    /// <summary>
    /// Returns a new <see cref="KeySet"/> containing any keys that have been compiled into the library.
    /// </summary>
    /// <returns>The created <see cref="KeySet"/>,</returns>
    public static KeySet CreateDefaultKeySet()
    {
        return DefaultKeySet.CreateDefaultKeySet();
    }

    public static List<KeyInfo> CreateKeyInfoList()
    {
        return DefaultKeySet.CreateKeyList();
    }

    public void DeriveKeys(IProgressReport logger = null)
    {
        Mode originalMode = CurrentMode;

        SetMode(Mode.Prod);
        KeyDerivation.DeriveAllKeys(this, logger);

        SetMode(Mode.Dev);
        KeyDerivation.DeriveAllKeys(this, logger);

        SetMode(originalMode);
    }

    public void DeriveSdCardKeys() => KeyDerivation.DeriveSdCardKeys(this);

    private static RSAParameters CreateRsaParameters(ref readonly RsaKey key)
    {
        return new RSAParameters
        {
            Exponent = key.PublicExponent[..].ToArray(),
            Modulus = key.Modulus[..].ToArray()
        };
    }

    private static RSAParameters CreateRsaParameters(ref readonly RsaFullKey key)
    {
        return new RSAParameters
        {
            D = key.PrivateExponent[..].ToArray(),
            DP = key.Dp[..].ToArray(),
            DQ = key.Dq[..].ToArray(),
            Exponent = key.PublicExponent[..].ToArray(),
            InverseQ = key.InverseQ[..].ToArray(),
            Modulus = key.Modulus[..].ToArray(),
            P = key.P[..].ToArray(),
            Q = key.Q[..].ToArray()
        };
    }

    private struct RsaSigningKeyParameters
    {
        public Optional<Array2<RSAParameters>> NcaHeaderSigningKeys;
        public Optional<Array2<RSAParameters>> AcidSigningKeys;
        public Optional<RSAParameters> Package2SigningKey;
    }

    private struct RsaKeyParameters
    {
        public Optional<RSAParameters> BetaNca0KeyAreaKey;
        public Optional<RSAParameters> ETicketRsaKey;
    }
}

public struct AllKeys
{
    public TsecSecrets TsecSecrets;
    public RootKeys RootKeysDev;
    public RootKeys RootKeysProd;
    public KeySeeds KeySeeds;
    public StoredKeys StoredKeysDev;
    public StoredKeys StoredKeysProd;
    public DerivedKeys DerivedKeysDev;
    public DerivedKeys DerivedKeysProd;
    public DeviceKeys DeviceKeys;
    public DerivedDeviceKeys DerivedDeviceKeysDev;
    public DerivedDeviceKeys DerivedDeviceKeysProd;
    public RsaSigningKeys RsaSigningKeysDev;
    public RsaSigningKeys RsaSigningKeysProd;
    public RsaKeys RsaKeys;
    public DeviceRsaKeys DeviceRsaKeys;
}

public struct TsecSecrets
{
    public Array64<AesKey> Secrets;
}

public struct RootKeys
{
    // Mariko keys. The AES class keys are currently unused.
    public AesKey MarikoKek;
    public AesKey MarikoBek;
    public Array12<AesKey> MarikoAesClassKeys;

    // The key blobs are technically derived from the encrypted key blobs and their keys,
    // however those keys are device-unique. The decrypted key blobs are basically the common root
    // keys used by pre-6.2.0 Erista.
    public Array32<KeyBlob> KeyBlobs;

    // Used by TSEC in >= 6.2.0 Erista firmware
    public Array8<AesKey> TsecRootKeks;
    public Array8<AesKey> Package1MacKeks;
    public Array8<AesKey> Package1Keks;

    // Derived by TSEC. This is the first public root key for >= 6.2.0 Erista
    public Array8<AesKey> TsecRootKeys;

    // Used to decrypt the title keys found in an XCI's initial data
    public Array16<AesKey> GcTitleKeyKeks;
}

public struct KeySeeds
{
    public Array8<AesKey> TsecAuthSignatures;
    public Array32<AesKey> KeyBlobKeySources;
    public AesKey KeyBlobMacKeySource;
    public Array32<AesKey> MasterKekSources;
    public Array32<AesKey> MarikoMasterKekSources;
    public Array32<AesKey> MarikoMasterKekSourcesDev;
    public AesKey MasterKeySource;
    public AesKey Package2KeySource;
    public AesKey PerConsoleKeySource;
    public AesKey RetailSpecificAesKeySource;
    public AesKey BisKekSource;
    public Array4<AesXtsKey> BisKeySources;
    public AesKey AesKekGenerationSource;
    public AesKey AesKeyGenerationSource;
    public AesKey KeyAreaKeyApplicationSource;
    public AesKey KeyAreaKeyOceanSource;
    public AesKey KeyAreaKeySystemSource;
    public AesKey TitleKekSource;
    public AesKey HeaderKekSource;
    public AesKey SdCardKekSource;
    public Array3<AesXtsKey> SdCardKeySources;
    public AesKey DeviceUniqueSaveMacKekSource;
    public Array2<AesKey> DeviceUniqueSaveMacKeySources;
    public AesKey SeedUniqueSaveMacKekSource;
    public AesKey SeedUniqueSaveMacKeySource;
    public AesXtsKey HeaderKeySource;
}

/// <summary>
/// Holds keys that are stored directly in Horizon programs.
/// </summary>
public struct StoredKeys
{
    public AesKey XciHeaderKey;
}

public struct DerivedKeys
{
    public Array32<AesKey> MasterKeks;
    public Array32<AesKey> MasterKeys;
    public Array32<AesKey> Package1MacKeys;
    public Array32<AesKey> Package1Keys;
    public Array32<AesKey> Package2Keys;
    public Array32<Array3<AesKey>> KeyAreaKeys;
    public Array32<AesKey> TitleKeks;
    public AesXtsKey HeaderKey;
    public AesKey ETicketRsaKek;
    public AesKey SslRsaKek;
}

public struct DeviceKeys
{
    public AesKey SecureBootKey;
    public AesKey TsecKey;
    public AesKey SdCardEncryptionSeed;
    public Array32<EncryptedKeyBlob> EncryptedKeyBlobs;
}

public struct DerivedDeviceKeys
{
    public Array32<AesKey> KeyBlobKeys;
    public Array32<AesKey> KeyBlobMacKeys;
    public AesKey DeviceKey;
    public Array4<AesXtsKey> BisKeys;
    public Array2<AesKey> DeviceUniqueSaveMacKeys;
    public AesKey SeedUniqueSaveMacKey;
    public Array3<AesXtsKey> SdCardEncryptionKeys;
}

public struct RsaSigningKeys
{
    public Array2<RsaKey> NcaHeaderSigningKeys;
    public Array2<RsaKey> AcidSigningKeys;
    public RsaKey Package2SigningKey;
}

public struct RsaKeys
{
    public RsaFullKey BetaNca0KeyAreaKey;
}

public struct DeviceRsaKeys
{
    public RsaKeyPair ETicketRsaKey;
}