using System;
using System.Collections.Generic;
using System.Text;

namespace Net
{
    public static class Urls
    {
        private const string Eid = "lp1";
        public static string Did { get; set; }

        public static string GetSuperflyUrl(ulong titleId)
        {
            return $"https://superfly.hac.{Eid}.d4c.nintendo.net/v1/a/{titleId:x16}/dv";
        }

        public static string GetMetaQueryUrl(ulong titleId, int version)
        {
            return $"https://atum.hac.{Eid}.d4c.nintendo.net/t/a/{titleId:x16}/{version}?deviceid={Did}";
        }

        public static string GetMetaContentUrl(string ncaId)
        {
            return $"https://atum.hac.{Eid}.d4c.nintendo.net/c/a/{ncaId}";
        }

        public static string GetContentUrl(string ncaId)
        {
            return $"https://atum.hac.{Eid}.d4c.nintendo.net/c/c/{ncaId}";
        }
    }
}
