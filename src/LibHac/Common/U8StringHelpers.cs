namespace LibHac.Common
{
    public static class U8StringHelpers
    {
        public static U8String ToU8String(this string value)
        {
            return new U8String(value);
        }

        public static U8Span ToU8Span(this string value)
        {
            return new U8Span(value);
        }
    }
}
