using System;

namespace LibHac
{
    [Serializable]
    public struct Result : IEquatable<Result>
    {
        public readonly int Value;

        public Result(int value)
        {
            Value = value;
        }

        public Result(int module, int description)
        {
            Value = (description << 9) | module;
        }

        public int Description => (Value >> 9) & 0x1FFF;
        public int Module => Value & 0x1FF;
        public string ErrorCode => $"{2000 + Module:d4}-{Description:d4}";

        public bool IsSuccess() => Value == 0;
        public bool IsFailure() => Value != 0;

        public void ThrowIfFailure()
        {
            if (IsFailure())
            {
                ThrowHelper.ThrowResult(this);
            }
        }

        public override bool Equals(object obj)
        {
            return obj is Result result && Equals(result);
        }

        public bool Equals(Result other)
        {
            return Value == other.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(Result left, Result right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Result left, Result right)
        {
            return !left.Equals(right);
        }

        public static Result Success => new Result(0);
    }
}
