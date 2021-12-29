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

    private AllKeys _keys;
    private Mode _mode = Mode.Prod;

    public ref AllKeys KeyStruct => ref _keys;
    public Mode CurrentMode => _mode;

    private ref RootKeys RootKeys => ref _mode == Mode.Dev ? ref _keys.RootKeysDev : ref _keys.RootKeysProd;
    private ref StoredKeys StoredKeys => ref _mode == Mode.Dev ? ref _keys.StoredKeysDev : ref _keys.StoredKeysProd;
    private ref DerivedKeys DerivedKeys => ref _mode == Mode.Dev ? ref _keys.DerivedKeysDev : ref _keys.DerivedKeysProd;
    private ref RsaSigningKeys RsaSigningKeys => ref _mode == Mode.Dev ? ref _keys.RsaSigningKeysDev : ref _keys.RsaSigningKeysProd;
    private ref RsaKeys RsaKeys => ref _keys.RsaKeys;

    private ref RsaSigningKeyParameters RsaSigningKeyParams => ref _mode == Mode.Dev
        ? ref _rsaSigningKeyParamsDev
        : ref _rsaSigningKeyParamsProd;

    public ExternalKeySet ExternalKeySet { get; } = new ExternalKeySet();

    public Span<AesKey> MarikoAesClassKeys => RootKeys.MarikoAesClassKeys.Items;
    public ref AesKey MarikoKek => ref RootKeys.MarikoKek;
    public ref AesKey MarikoBek => ref RootKeys.MarikoBek;
    public Span<KeyBlob> KeyBlobs => RootKeys.KeyBlobs.Items;
    public Span<AesKey> KeyBlobKeySources => _keys.KeySeeds.KeyBlobKeySources.Items;
    public ref AesKey KeyBlobMacKeySource => ref _keys.KeySeeds.KeyBlobMacKeySource;
    public ref AesKey TsecRootKek => ref RootKeys.TsecRootKek;
    public ref AesKey Package1MacKek => ref RootKeys.Package1MacKek;
    public ref AesKey Package1Kek => ref RootKeys.Package1Kek;
    public Span<AesKey> TsecAuthSignatures => RootKeys.TsecAuthSignatures.Items;
    public Span<AesKey> TsecRootKeys => RootKeys.TsecRootKeys.Items;
    public Span<AesKey> MasterKekSources => _keys.KeySeeds.MasterKekSources.Items;

    public Span<AesKey> MarikoMasterKekSources => _mode == Mode.Dev
        ? _keys.KeySeeds.MarikoMasterKekSourcesDev.Items
        : _keys.KeySeeds.MarikoMasterKekSources.Items;

    public Span<AesKey> MasterKeks => DerivedKeys.MasterKeks.Items;
    public ref AesKey MasterKeySource => ref _keys.KeySeeds.MasterKeySource;
    public Span<AesKey> MasterKeys => DerivedKeys.MasterKeys.Items;
    public Span<AesKey> Package1MacKeys => DerivedKeys.Package1MacKeys.Items;
    public Span<AesKey> Package1Keys => DerivedKeys.Package1Keys.Items;
    public Span<AesKey> Package2Keys => DerivedKeys.Package2Keys.Items;
    public ref AesKey Package2KeySource => ref _keys.KeySeeds.Package2KeySource;
    public ref AesKey PerConsoleKeySource => ref _keys.KeySeeds.PerConsoleKeySource;
    public ref AesKey RetailSpecificAesKeySource => ref _keys.KeySeeds.RetailSpecificAesKeySource;
    public ref AesKey BisKekSource => ref _keys.KeySeeds.BisKekSource;
    public Span<AesXtsKey> BisKeySources => _keys.KeySeeds.BisKeySources.Items;
    public ref AesKey AesKekGenerationSource => ref _keys.KeySeeds.AesKekGenerationSource;
    public ref AesKey AesKeyGenerationSource => ref _keys.KeySeeds.AesKeyGenerationSource;
    public ref AesKey KeyAreaKeyApplicationSource => ref _keys.KeySeeds.KeyAreaKeyApplicationSource;
    public ref AesKey KeyAreaKeyOceanSource => ref _keys.KeySeeds.KeyAreaKeyOceanSource;
    public ref AesKey KeyAreaKeySystemSource => ref _keys.KeySeeds.KeyAreaKeySystemSource;
    public ref AesKey TitleKekSource => ref _keys.KeySeeds.TitleKekSource;
    public ref AesKey HeaderKekSource => ref _keys.KeySeeds.HeaderKekSource;
    public ref AesKey SdCardKekSource => ref _keys.KeySeeds.SdCardKekSource;
    public Span<AesXtsKey> SdCardKeySources => _keys.KeySeeds.SdCardKeySources.Items;
    public ref AesKey DeviceUniqueSaveMacKekSource => ref _keys.KeySeeds.DeviceUniqueSaveMacKekSource;
    public Span<AesKey> DeviceUniqueSaveMacKeySources => _keys.KeySeeds.DeviceUniqueSaveMacKeySources.Items;
    public ref AesKey SeedUniqueSaveMacKekSource => ref _keys.KeySeeds.SeedUniqueSaveMacKekSource;
    public ref AesKey SeedUniqueSaveMacKeySource => ref _keys.KeySeeds.SeedUniqueSaveMacKeySource;
    public ref AesXtsKey HeaderKeySource => ref _keys.KeySeeds.HeaderKeySource;
    public ref AesXtsKey HeaderKey => ref DerivedKeys.HeaderKey;
    public Span<AesKey> TitleKeks => DerivedKeys.TitleKeks.Items;
    public Span<Array3<AesKey>> KeyAreaKeys => DerivedKeys.KeyAreaKeys.Items;
    public ref AesKey XciHeaderKey => ref StoredKeys.XciHeaderKey;
    public ref AesKey ETicketRsaKek => ref DerivedKeys.ETicketRsaKek;
    public ref AesKey SslRsaKek => ref DerivedKeys.SslRsaKek;

    public ref AesKey SecureBootKey => ref _keys.DeviceKeys.SecureBootKey;
    public ref AesKey TsecKey => ref _keys.DeviceKeys.TsecKey;
    public Span<AesKey> KeyBlobKeys => _keys.DeviceKeys.KeyBlobKeys.Items;
    public Span<AesKey> KeyBlobMacKeys => _keys.DeviceKeys.KeyBlobMacKeys.Items;
    public Span<EncryptedKeyBlob> EncryptedKeyBlobs => _keys.DeviceKeys.EncryptedKeyBlobs.Items;
    public ref AesKey DeviceKey => ref _keys.DeviceKeys.DeviceKey;
    public Span<AesXtsKey> BisKeys => _keys.DeviceKeys.BisKeys.Items;
    public Span<AesKey> DeviceUniqueSaveMacKeys => _keys.DeviceKeys.DeviceUniqueSaveMacKeys.Items;
    public ref AesKey SeedUniqueSaveMacKey => ref _keys.DeviceKeys.SeedUniqueSaveMacKey;
    public ref AesKey SdCardEncryptionSeed => ref _keys.DeviceKeys.SdCardEncryptionSeed;

    // Todo: Make a separate type? Not actually an AES-XTS key, but it's still the same shape.
    public Span<AesXtsKey> SdCardEncryptionKeys => _keys.DeviceKeys.SdCardEncryptionKeys.Items;

    public Span<RsaKey> NcaHeaderSigningKeys => RsaSigningKeys.NcaHeaderSigningKeys.Items;
    public Span<RsaKey> AcidSigningKeys => RsaSigningKeys.AcidSigningKeys.Items;
    public ref RsaKey Package2SigningKey => ref RsaSigningKeys.Package2SigningKey;
    public ref RsaFullKey BetaNca0KeyAreaKey => ref RsaKeys.BetaNca0KeyAreaKey;

    private RsaSigningKeyParameters _rsaSigningKeyParamsDev;
    private RsaSigningKeyParameters _rsaSigningKeyParamsProd;
    private RsaKeyParameters _rsaKeyParams;

    public RSAParameters ETicketExtKeyRsa { get; set; }

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

            return keys.Value.Items;
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

            return keys.Value.Items;
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

    private static RSAParameters CreateRsaParameters(in RsaKey key)
    {
        return new RSAParameters
        {
            Exponent = key.PublicExponent.DataRo.ToArray(),
            Modulus = key.Modulus.DataRo.ToArray()
        };
    }

    private static RSAParameters CreateRsaParameters(in RsaFullKey key)
    {
        return new RSAParameters
        {
            D = key.PrivateExponent.DataRo.ToArray(),
            DP = key.Dp.DataRo.ToArray(),
            DQ = key.Dq.DataRo.ToArray(),
            Exponent = key.PublicExponent.DataRo.ToArray(),
            InverseQ = key.InverseQ.DataRo.ToArray(),
            Modulus = key.Modulus.DataRo.ToArray(),
            P = key.P.DataRo.ToArray(),
            Q = key.Q.DataRo.ToArray()
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
    }
}

public struct AllKeys
{
    public RootKeys RootKeysDev;
    public RootKeys RootKeysProd;
    public KeySeeds KeySeeds;
    public StoredKeys StoredKeysDev;
    public StoredKeys StoredKeysProd;
    public DerivedKeys DerivedKeysDev;
    public DerivedKeys DerivedKeysProd;
    public DeviceKeys DeviceKeys;
    public RsaSigningKeys RsaSigningKeysDev;
    public RsaSigningKeys RsaSigningKeysProd;
    public RsaKeys RsaKeys;
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
    public Array32<AesKey> TsecAuthSignatures;
    public AesKey TsecRootKek;
    public AesKey Package1MacKek;
    public AesKey Package1Kek;

    // Derived by TSEC. This is the first public root key for >= 6.2.0 Erista
    public Array32<AesKey> TsecRootKeys;
}

public struct KeySeeds
{
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
    public Array32<AesKey> KeyBlobKeys;
    public Array32<AesKey> KeyBlobMacKeys;
    public Array32<EncryptedKeyBlob> EncryptedKeyBlobs;
    public AesKey DeviceKey;
    public Array4<AesXtsKey> BisKeys;
    public Array2<AesKey> DeviceUniqueSaveMacKeys;
    public AesKey SeedUniqueSaveMacKey;
    public AesKey SdCardEncryptionSeed;
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