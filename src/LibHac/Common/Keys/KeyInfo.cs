using System;
using System.Diagnostics;
using LibHac.Diag;
using LibHac.Util;

namespace LibHac.Common.Keys
{
    [DebuggerDisplay("{" + nameof(Name) + "}")]
    public readonly struct KeyInfo
    {
        public enum KeyRangeType : byte
        {
            Single,
            Range
        }

        [Flags]
        public enum KeyType : byte
        {
            Common = 1 << 0,
            Device = 1 << 1,
            Root = 1 << 2,
            Seed = 1 << 3,
            Derived = 1 << 4,

            /// <summary>Specifies that a seed is different in prod and dev.</summary>
            DifferentDev = 1 << 5,

            CommonRoot = Common | Root,
            CommonSeed = Common | Seed,
            CommonSeedDiff = Common | Seed | DifferentDev,
            CommonDrvd = Common | Derived,
            DeviceRoot = Device | Root,
            DeviceDrvd = Device | Derived
        }

        public readonly string Name;
        public readonly KeyGetter Getter;
        public readonly int Group;
        public readonly KeyRangeType RangeType;
        public readonly KeyType Type;
        public readonly byte RangeStart;
        public readonly byte RangeEnd;

        public int NameLength => Name.Length + (RangeType == KeyRangeType.Range ? 3 : 0);

        public delegate Span<byte> KeyGetter(KeySet keySet, int i);

        public KeyInfo(int group, KeyType type, string name, KeyGetter retrieveFunc)
        {
            Assert.SdkRequires(IsKeyTypeValid(type));

            Name = name;
            RangeType = KeyRangeType.Single;
            Type = type;
            RangeStart = default;
            RangeEnd = default;
            Group = group;
            Getter = retrieveFunc;
        }

        public KeyInfo(int group, KeyType type, string name, byte rangeStart, byte rangeEnd, KeyGetter retrieveFunc)
        {
            Assert.SdkRequires(IsKeyTypeValid(type));

            Name = name;
            RangeType = KeyRangeType.Range;
            Type = type;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
            Group = group;
            Getter = retrieveFunc;
        }

        public bool Matches(ReadOnlySpan<char> keyName, out int keyIndex, out bool isDev)
        {
            keyIndex = default;
            isDev = default;

            return RangeType switch
            {
                KeyRangeType.Single => MatchesSingle(keyName, out isDev),
                KeyRangeType.Range => MatchesRangedKey(keyName, ref keyIndex, out isDev),
                _ => false
            };
        }

        private bool MatchesSingle(ReadOnlySpan<char> keyName, out bool isDev)
        {
            Assert.SdkRequiresEqual((int)KeyRangeType.Single, (int)RangeType);

            isDev = false;

            if (keyName.Length == NameLength + 4)
            {
                // Might be a dev key. Check if "_dev" comes after the base key name
                if (!keyName.Slice(Name.Length, 4).SequenceEqual("_dev"))
                    return false;

                isDev = true;
            }
            else if (keyName.Length != NameLength)
            {
                return false;
            }

            // Check if the base name matches
            if (!keyName.Slice(0, Name.Length).SequenceEqual(Name))
                return false;

            return true;
        }

        private bool MatchesRangedKey(ReadOnlySpan<char> keyName, ref int keyIndex, out bool isDev)
        {
            Assert.SdkRequiresEqual((int)KeyRangeType.Range, (int)RangeType);

            isDev = false;

            // Check if this is an explicit dev key
            if (keyName.Length == Name.Length + 7)
            {
                // Check if "_dev" comes after the base key name
                if (!keyName.Slice(Name.Length, 4).SequenceEqual("_dev"))
                    return false;

                isDev = true;
            }
            // Not a dev key. Check that the length of the key name with the trailing index matches
            else if (keyName.Length != Name.Length + 3)
                return false;

            // Check if the name before the "_XX" index matches
            if (!keyName.Slice(0, Name.Length).SequenceEqual(Name))
                return false;

            // The name should have an underscore before the index value
            if (keyName[keyName.Length - 3] != '_')
                return false;

            byte index = default;

            // Try to get the index of the key name
            if (!StringUtils.TryFromHexString(keyName.Slice(keyName.Length - 2, 2), SpanHelpers.AsSpan(ref index)))
                return false;

            // Check if the index is in this key's range
            if (index < RangeStart || index >= RangeEnd)
                return false;

            keyIndex = index;
            return true;
        }

        private static bool IsKeyTypeValid(KeyType type)
        {
            // Make sure the type has exactly one flag set for each type
            KeyType type1 = type & (KeyType.Common | KeyType.Device);
            KeyType type2 = type & (KeyType.Root | KeyType.Seed | KeyType.Derived);

            bool isValid1 = type1 == KeyType.Common || type1 == KeyType.Device;
            bool isValid2 = type2 == KeyType.Root || type2 == KeyType.Seed || type2 == KeyType.Derived;

            return isValid1 && isValid2;
        }
    }
}
