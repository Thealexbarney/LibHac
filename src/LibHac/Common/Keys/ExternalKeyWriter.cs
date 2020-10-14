using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibHac.Fs;
using LibHac.Spl;
using LibHac.Util;

using Type = LibHac.Common.Keys.KeyInfo.KeyType;
using RangeType = LibHac.Common.Keys.KeyInfo.KeyRangeType;

namespace LibHac.Common.Keys
{
    public static class ExternalKeyWriter
    {
        public static void PrintKeys(KeySet keySet, StringBuilder sb, List<KeyInfo> keys, Type filter, bool isDev)
        {
            if (keys.Count == 0) return;

            string devSuffix = isDev ? "_dev" : string.Empty;
            int maxNameLength = keys.Max(x => x.NameLength);
            int currentGroup = 0;

            // Todo: Better filtering
            bool FilterMatches(KeyInfo keyInfo)
            {
                Type filter1 = filter & (Type.Common | Type.Device);
                Type filter2 = filter & (Type.Root | Type.Seed | Type.Derived);
                Type filter3 = filter & Type.DifferentDev;

                // Skip sub-filters that have no flags set
                return (filter1 == 0 || (filter1 & keyInfo.Type) != 0) &&
                       (filter2 == 0 || (filter2 & keyInfo.Type) != 0) &&
                       filter3 == (filter3 & keyInfo.Type) ||
                       isDev && keyInfo.Type.HasFlag(Type.DifferentDev);
            }

            bool isFirstPrint = true;

            foreach (KeyInfo info in keys.Where(x => x.Group >= 0).Where(FilterMatches)
                .OrderBy(x => x.Group).ThenBy(x => x.Name))
            {
                bool isNewGroup = false;

                if (info.Group == currentGroup + 1)
                {
                    currentGroup = info.Group;
                }
                else if (info.Group > currentGroup)
                {
                    // Don't update the current group yet because if this key is empty and the next key
                    // is in the same group, we need to be able to know to add a blank line before printing it.
                    isNewGroup = !isFirstPrint;
                }

                if (info.RangeType == RangeType.Single)
                {
                    Span<byte> key = info.Getter(keySet, 0);
                    if (key.IsZeros())
                        continue;

                    if (isNewGroup)
                    {
                        sb.AppendLine();
                    }

                    string keyName = $"{info.Name}{devSuffix}";

                    string line = $"{keyName.PadRight(maxNameLength)} = {key.ToHexString()}";
                    sb.AppendLine(line);
                    isFirstPrint = false;
                    currentGroup = info.Group;
                }
                else if (info.RangeType == RangeType.Range)
                {
                    bool hasPrintedKey = false;

                    for (int i = info.RangeStart; i < info.RangeEnd; i++)
                    {
                        Span<byte> key = info.Getter(keySet, i);
                        if (key.IsZeros())
                            continue;

                        if (hasPrintedKey == false)
                        {
                            if (isNewGroup)
                            {
                                sb.AppendLine();
                            }

                            hasPrintedKey = true;
                        }

                        string keyName = $"{info.Name}{devSuffix}_{i:x2}";

                        string line = $"{keyName.PadRight(maxNameLength)} = {key.ToHexString()}";
                        sb.AppendLine(line);
                        isFirstPrint = false;
                        currentGroup = info.Group;
                    }
                }
            }
        }

        public static string PrintTitleKeys(KeySet keySet)
        {
            var sb = new StringBuilder();

            foreach ((RightsId rightsId, AccessKey key) kv in keySet.ExternalKeySet.ToList()
                .OrderBy(x => x.rightsId.ToString()))
            {
                string line = $"{kv.rightsId} = {kv.key}";
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        public static string PrintCommonKeys(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, DefaultKeySet.CreateKeyList(), Type.Common | Type.Root | Type.Seed | Type.Derived,
                false);
            return sb.ToString();
        }

        public static string PrintDeviceKeys(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, DefaultKeySet.CreateKeyList(), Type.Device, false);
            return sb.ToString();
        }

        public static string PrintAllKeys(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, DefaultKeySet.CreateKeyList(), 0, false);
            return sb.ToString();
        }

        public static string PrintAllSeeds(KeySet keySet)
        {
            var sb = new StringBuilder();
            PrintKeys(keySet, sb, DefaultKeySet.CreateKeyList(), Type.Common | Type.Seed, false);

            if (keySet.CurrentMode == KeySet.Mode.Prod)
            {
                sb.AppendLine();
                keySet.SetMode(KeySet.Mode.Dev);
                PrintKeys(keySet, sb, DefaultKeySet.CreateKeyList(), Type.Common | Type.Seed | Type.DifferentDev, true);
                keySet.SetMode(KeySet.Mode.Prod);
            }
            return sb.ToString();
        }

        public static string PrintCommonKeysWithDev(KeySet keySet)
        {
            KeySet.Mode originalMode = keySet.CurrentMode;
            var sb = new StringBuilder();

            keySet.SetMode(KeySet.Mode.Prod);
            PrintKeys(keySet, sb, DefaultKeySet.CreateKeyList(), Type.Common | Type.Root | Type.Seed | Type.Derived,
                false);

            sb.AppendLine();
            keySet.SetMode(KeySet.Mode.Dev);
            PrintKeys(keySet, sb, DefaultKeySet.CreateKeyList(), Type.Common | Type.Root | Type.Derived, true);

            keySet.SetMode(originalMode);
            return sb.ToString();
        }
    }
}
