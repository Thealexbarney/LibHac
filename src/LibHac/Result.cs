using System;
using System.Diagnostics;

namespace LibHac
{
    [Serializable]
    [DebuggerDisplay("{ToStringWithName(),nq}")]
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

        /// <summary>
        /// A function that can contain code for logging or debugging returned results.
        /// Intended to be used when returning a non-zero Result:
        /// <code>return result.Log();</code>
        /// </summary>
        /// <returns>The called <see cref="Result"/> value.</returns>
        public Result Log()
        {
            LogImpl();

            return this;
        }

        /// <summary>
        /// Same as <see cref="Log"/>, but for when one result is converted to another.
        /// </summary>
        /// <param name="originalResult">The original <see cref="Result"/> value.</param>
        /// <returns>The called <see cref="Result"/> value.</returns>
        public Result LogConverted(Result originalResult)
        {
            LogConvertedImpl(originalResult);

            return this;
        }

        [Conditional("DEBUG")]
        private void LogImpl()
        {
            LogCallback?.Invoke(this);
        }

        [Conditional("DEBUG")]
        private void LogConvertedImpl(Result originalResult)
        {
            ConvertedLogCallback?.Invoke(this, originalResult);
        }

        public delegate void ResultLogger(Result result);
        public delegate void ConvertedResultLogger(Result result, Result originalResult);
        public delegate bool ResultNameGetter(Result result, out string name);

        public static ResultLogger LogCallback { get; set; }
        public static ConvertedResultLogger ConvertedLogCallback { get; set; }
        public static ResultNameGetter GetResultNameHandler { get; set; }

        public bool TryGetResultName(out string name)
        {
            ResultNameGetter func = GetResultNameHandler;

            if (func == null)
            {
                name = default;
                return false;
            }

            return func(this, out name);
        }

        public string ToStringWithName()
        {
            if (TryGetResultName(out string name))
            {
                return $"{name} ({ErrorCode})";
            }

            return ErrorCode;
        }

        public override string ToString()
        {
            return IsSuccess() ? "Success" : ErrorCode;
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
