namespace LibHac.Ncm
{
    public struct TitleId
    {
        public ulong Value;

        public static explicit operator ulong(TitleId titleId) => titleId.Value;
    }
}
