using System.Diagnostics;

namespace LibHac.Ncm
{
    [DebuggerDisplay("{" + nameof(Value) + "}")]
    public struct TitleId
    {
        public readonly ulong Value;

        public TitleId(ulong value)
        {
            Value = value;
        }

        public static explicit operator ulong(TitleId titleId) => titleId.Value;
    }
}
