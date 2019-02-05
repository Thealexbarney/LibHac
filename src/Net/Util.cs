using System;

namespace Net
{
    public static class Util
    {
        public static long ToUnixTime(this DateTime inputTime) => (long)(inputTime - new DateTime(1970, 1, 1)).TotalSeconds;
    }
}
