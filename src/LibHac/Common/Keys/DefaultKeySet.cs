using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common.Keys
{
    internal static partial class DefaultKeySet
    {
        public static KeySet CreateDefaultKeySet()
        {
            var keySet = new KeySet();

            // Fill the key set with any key structs included in the library.
            if (RootKeysDev.Length == Unsafe.SizeOf<RootKeys>())
            {
                keySet.KeyStruct._rootKeysDev = MemoryMarshal.Cast<byte, RootKeys>(RootKeysDev)[0];
            }

            if (RootKeysProd.Length == Unsafe.SizeOf<RootKeys>())
            {
                keySet.KeyStruct._rootKeysProd = MemoryMarshal.Cast<byte, RootKeys>(RootKeysProd)[0];
            }

            if (KeySeeds.Length == Unsafe.SizeOf<KeySeeds>())
            {
                keySet.KeyStruct._keySeeds = MemoryMarshal.Cast<byte, KeySeeds>(KeySeeds)[0];
            }

            if (StoredKeysDev.Length == Unsafe.SizeOf<StoredKeys>())
            {
                keySet.KeyStruct._storedKeysDev = MemoryMarshal.Cast<byte, StoredKeys>(StoredKeysDev)[0];
            }

            if (StoredKeysProd.Length == Unsafe.SizeOf<StoredKeys>())
            {
                keySet.KeyStruct._storedKeysProd = MemoryMarshal.Cast<byte, StoredKeys>(StoredKeysProd)[0];
            }

            if (DerivedKeysDev.Length == Unsafe.SizeOf<DerivedKeys>())
            {
                keySet.KeyStruct._derivedKeysDev = MemoryMarshal.Cast<byte, DerivedKeys>(DerivedKeysDev)[0];
            }

            if (DerivedKeysProd.Length == Unsafe.SizeOf<DerivedKeys>())
            {
                keySet.KeyStruct._derivedKeysProd = MemoryMarshal.Cast<byte, DerivedKeys>(DerivedKeysProd)[0];
            }

            if (DeviceKeys.Length == Unsafe.SizeOf<DeviceKeys>())
            {
                keySet.KeyStruct._deviceKeys = MemoryMarshal.Cast<byte, DeviceKeys>(DeviceKeys)[0];
            }

            if (RsaSigningKeysDev.Length == Unsafe.SizeOf<RsaSigningKeys>())
            {
                keySet.KeyStruct._rsaSigningKeysDev = MemoryMarshal.Cast<byte, RsaSigningKeys>(RsaSigningKeysDev)[0];
            }

            if (RsaSigningKeysProd.Length == Unsafe.SizeOf<RsaSigningKeys>())
            {
                keySet.KeyStruct._rsaSigningKeysProd = MemoryMarshal.Cast<byte, RsaSigningKeys>(RsaSigningKeysProd)[0];
            }

            if (RsaKeys.Length == Unsafe.SizeOf<RsaKeys>())
            {
                keySet.KeyStruct._rsaKeys = MemoryMarshal.Cast<byte, RsaKeys>(RsaKeys)[0];
            }

            return keySet;
        }
    }
}
