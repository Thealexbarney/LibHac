using System.Text;
using LibHac;

namespace hactoolnet
{
    internal static class Print
    {
        public static void PrintItem(StringBuilder sb, int colLen, string prefix, object data)
        {
            if (data is byte[] byteData)
            {
                sb.MemDump(prefix.PadRight(colLen), byteData);
            }
            else
            {
                sb.AppendLine(prefix.PadRight(colLen) + data);
            }
        }

        public static string GetValidityString(this Validity validity)
        {
            switch (validity)
            {
                case Validity.Invalid: return " (FAIL)";
                case Validity.Valid: return " (GOOD)";
                default: return string.Empty;
            }
        }
    }
}
