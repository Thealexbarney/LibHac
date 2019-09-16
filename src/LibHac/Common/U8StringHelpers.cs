namespace LibHac.Common
{
    public static class U8StringHelpers
    {
        public static U8String AsU8String(this string value)
        {
            return new U8String(value);
        }

        public static U8Span AsU8Span(this string value)
        {
            return new U8Span(value);
        }
    }
}
