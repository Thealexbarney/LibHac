using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac.Boot;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.FsSrv;
using LibHac.Util;

namespace LibHac.Common.Keys
{
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

        private ref RootKeys RootKeys => ref _mode == Mode.Dev ? ref _keys._rootKeysDev : ref _keys._rootKeysProd;
        private ref StoredKeys StoredKeys => ref _mode == Mode.Dev ? ref _keys._storedKeysDev : ref _keys._storedKeysProd;
        private ref DerivedKeys DerivedKeys => ref _mode == Mode.Dev ? ref _keys._derivedKeysDev : ref _keys._derivedKeysProd;
        private ref RsaSigningKeys RsaSigningKeys => ref _mode == Mode.Dev ? ref _keys._rsaSigningKeysDev : ref _keys._rsaSigningKeysProd;
        private ref RsaKeys RsaKeys => ref _keys._rsaKeys;

        private ref RsaSigningKeyParameters RsaSigningKeyParams => ref _mode == Mode.Dev
            ? ref _rsaSigningKeyParamsDev
            : ref _rsaSigningKeyParamsProd;

        public ExternalKeySet ExternalKeySet { get; } = new ExternalKeySet();

        public Span<AesKey> MarikoAesClassKeys => RootKeys.MarikoAesClassKeys.Items;
        public ref AesKey MarikoKek => ref RootKeys.MarikoKek;
        public ref AesKey MarikoBek => ref RootKeys.MarikoBek;
        public Span<KeyBlob> KeyBlobs => RootKeys.KeyBlobs.Items;
        public Span<AesKey> KeyBlobKeySources => _keys._keySeeds.KeyBlobKeySources.Items;
        public ref AesKey KeyBlobMacKeySource => ref _keys._keySeeds.KeyBlobMacKeySource;
        public ref AesKey TsecRootKek => ref RootKeys.TsecRootKek;
        public ref AesKey Package1MacKek => ref RootKeys.Package1MacKek;
        public ref AesKey Package1Kek => ref RootKeys.Package1Kek;
        public Span<AesKey> TsecAuthSignatures => RootKeys.TsecAuthSignatures.Items;
        public Span<AesKey> TsecRootKeys => RootKeys.TsecRootKeys.Items;
        public Span<AesKey> MasterKekSources => _keys._keySeeds.MasterKekSources.Items;

        public Span<AesKey> MarikoMasterKekSources => _mode == Mode.Dev
            ? _keys._keySeeds.MarikoMasterKekSources_dev.Items
            : _keys._keySeeds.MarikoMasterKekSources.Items;

        public Span<AesKey> MasterKeks => DerivedKeys.MasterKeks.Items;
        public ref AesKey MasterKeySource => ref _keys._keySeeds.MasterKeySource;
        public Span<AesKey> MasterKeys => DerivedKeys.MasterKeys.Items;
        public Span<AesKey> Package1MacKeys => DerivedKeys.Package1MacKeys.Items;
        public Span<AesKey> Package1Keys => DerivedKeys.Package1Keys.Items;
        public Span<AesKey> Package2Keys => DerivedKeys.Package2Keys.Items;
        public ref AesKey Package2KeySource => ref _keys._keySeeds.Package2KeySource;
        public ref AesKey PerConsoleKeySource => ref _keys._keySeeds.PerConsoleKeySource;
        public ref AesKey RetailSpecificAesKeySource => ref _keys._keySeeds.RetailSpecificAesKeySource;
        public ref AesKey BisKekSource => ref _keys._keySeeds.BisKekSource;
        public Span<AesXtsKey> BisKeySources => _keys._keySeeds.BisKeySources.Items;
        public ref AesKey AesKekGenerationSource => ref _keys._keySeeds.AesKekGenerationSource;
        public ref AesKey AesKeyGenerationSource => ref _keys._keySeeds.AesKeyGenerationSource;
        public ref AesKey KeyAreaKeyApplicationSource => ref _keys._keySeeds.KeyAreaKeyApplicationSource;
        public ref AesKey KeyAreaKeyOceanSource => ref _keys._keySeeds.KeyAreaKeyOceanSource;
        public ref AesKey KeyAreaKeySystemSource => ref _keys._keySeeds.KeyAreaKeySystemSource;
        public ref AesKey TitleKekSource => ref _keys._keySeeds.TitleKekSource;
        public ref AesKey HeaderKekSource => ref _keys._keySeeds.HeaderKekSource;
        public ref AesKey SdCardKekSource => ref _keys._keySeeds.SdCardKekSource;
        public Span<AesXtsKey> SdCardKeySources => _keys._keySeeds.SdCardKeySources.Items;
        public ref AesKey DeviceUniqueSaveMacKekSource => ref _keys._keySeeds.DeviceUniqueSaveMacKekSource;
        public Span<AesKey> DeviceUniqueSaveMacKeySources => _keys._keySeeds.DeviceUniqueSaveMacKeySources.Items;
        public ref AesKey SeedUniqueSaveMacKekSource => ref _keys._keySeeds.SeedUniqueSaveMacKekSource;
        public ref AesKey SeedUniqueSaveMacKeySource => ref _keys._keySeeds.SeedUniqueSaveMacKeySource;
        public ref AesXtsKey HeaderKeySource => ref _keys._keySeeds.HeaderKeySource;
        public ref AesXtsKey HeaderKey => ref DerivedKeys.HeaderKey;
        public Span<AesKey> TitleKeks => DerivedKeys.TitleKeks.Items;
        public Span<Array3<AesKey>> KeyAreaKeys => DerivedKeys.KeyAreaKeys.Items;
        public ref AesKey XciHeaderKey => ref StoredKeys.XciHeaderKey;
        public ref AesKey ETicketRsaKek => ref DerivedKeys.ETicketRsaKek;
        public ref AesKey SslRsaKek => ref DerivedKeys.SslRsaKek;

        public ref AesKey SecureBootKey => ref _keys._deviceKeys.SecureBootKey;
        public ref AesKey TsecKey => ref _keys._deviceKeys.TsecKey;
        public Span<AesKey> KeyBlobKeys => _keys._deviceKeys.KeyBlobKeys.Items;
        public Span<AesKey> KeyBlobMacKeys => _keys._deviceKeys.KeyBlobMacKeys.Items;
        public Span<EncryptedKeyBlob> EncryptedKeyBlobs => _keys._deviceKeys.EncryptedKeyBlobs.Items;
        public ref AesKey DeviceKey => ref _keys._deviceKeys.DeviceKey;
        public Span<AesXtsKey> BisKeys => _keys._deviceKeys.BisKeys.Items;
        public Span<AesKey> DeviceUniqueSaveMacKeys => _keys._deviceKeys.DeviceUniqueSaveMacKeys.Items;
        public ref AesKey SeedUniqueSaveMacKey => ref _keys._deviceKeys.SeedUniqueSaveMacKey;
        public ref AesKey SdCardEncryptionSeed => ref _keys._deviceKeys.SdCardEncryptionSeed;

        // Todo: Make a separate type? Not actually an AES-XTS key, but it's still the same shape.
        public Span<AesXtsKey> SdCardEncryptionKeys => _keys._deviceKeys.SdCardEncryptionKeys.Items;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct AllKeys
    {
        public RootKeys _rootKeysDev;
        public RootKeys _rootKeysProd;
        public KeySeeds _keySeeds;
        public StoredKeys _storedKeysDev;
        public StoredKeys _storedKeysProd;
        public DerivedKeys _derivedKeysDev;
        public DerivedKeys _derivedKeysProd;
        public DeviceKeys _deviceKeys;
        public RsaSigningKeys _rsaSigningKeysDev;
        public RsaSigningKeys _rsaSigningKeysProd;
        public RsaKeys _rsaKeys;
    }

    [StructLayout(LayoutKind.Sequential)]
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

    [StructLayout(LayoutKind.Sequential)]
    public struct KeySeeds
    {
        public Array32<AesKey> KeyBlobKeySources;
        public AesKey KeyBlobMacKeySource;
        public Array32<AesKey> MasterKekSources;
        public Array32<AesKey> MarikoMasterKekSources;
        public Array32<AesKey> MarikoMasterKekSources_dev;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct StoredKeys
    {
        public AesKey XciHeaderKey;
    }

    [StructLayout(LayoutKind.Sequential)]
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

    [StructLayout(LayoutKind.Sequential)]
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

    [StructLayout(LayoutKind.Sequential)]
    public struct RsaSigningKeys
    {
        public Array2<RsaKey> NcaHeaderSigningKeys;
        public Array2<RsaKey> AcidSigningKeys;
        public RsaKey Package2SigningKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RsaKeys
    {
        public RsaFullKey BetaNca0KeyAreaKey;
    }
}
