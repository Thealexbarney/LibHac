using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using BaseType = System.UInt32;

namespace LibHac
{
    [Serializable]
    [DebuggerDisplay("{ToStringWithName(),nq}")]
    public struct Result : IEquatable<Result>
    {
        private const BaseType SuccessValue = default;

        private const int ModuleBitsOffset = 0;
        private const int ModuleBitsCount = 9;
        private const int ModuleBegin = 1;
        private const int ModuleEnd = 1 << ModuleBitsCount;
        private const int DescriptionBitsOffset = ModuleBitsOffset + ModuleBitsCount;
        private const int DescriptionBitsCount = 13;
        private const int DescriptionBegin = 0;
        private const int DescriptionEnd = 1 << DescriptionBitsCount;
        private const int ReservedBitsOffset = DescriptionBitsOffset + DescriptionBitsCount;
        private const int ReservedBitsCount = sizeof(BaseType) * 8 - ReservedBitsOffset;
        // ReSharper disable once UnusedMember.Local
        private const int EndOffset = ReservedBitsOffset + ReservedBitsCount;

        private readonly BaseType _value;

        public static Result Success => new Result(SuccessValue);

        public Result(BaseType value)
        {
            _value = GetBitsValue(value, ModuleBitsOffset, ModuleBitsCount + DescriptionBitsCount);
        }

        public Result(int module, int description)
        {
            Debug.Assert(ModuleBegin <= module && module < ModuleEnd, "Invalid Module");
            Debug.Assert(DescriptionBegin <= description && description < DescriptionEnd, "Invalid Description");

            _value = SetBitsValue(module, ModuleBitsOffset, ModuleBitsCount) |
                     SetBitsValue(description, DescriptionBitsOffset, DescriptionBitsCount);
        }

        public BaseType Module => GetBitsValue(_value, ModuleBitsOffset, ModuleBitsCount);
        public BaseType Description => GetBitsValue(_value, DescriptionBitsOffset, DescriptionBitsCount);

        public BaseType Value => GetBitsValue(_value, ModuleBitsOffset, ModuleBitsCount + DescriptionBitsCount);

        public string ErrorCode => $"{2000 + Module:d4}-{Description:d4}";

        public bool IsSuccess() => _value == SuccessValue;
        public bool IsFailure() => !IsSuccess();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BaseType GetBitsValue(BaseType value, int bitsOffset, int bitsCount)
        {
            return (value >> bitsOffset) & ~(~default(BaseType) << bitsCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BaseType SetBitsValue(int value, int bitsOffset, int bitsCount)
        {
            return ((uint)value & ~(~default(BaseType) << bitsCount)) << bitsOffset;
        }

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

        public override string ToString() => IsSuccess() ? "Success" : ToStringWithName();

        public override bool Equals(object obj) => obj is Result result && Equals(result);
        public bool Equals(Result other) => _value == other._value;
        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(Result left, Result right) => left.Equals(right);
        public static bool operator !=(Result left, Result right) => !left.Equals(right);

        public struct Base
        {
            private const int DescriptionEndBitsOffset = ReservedBitsOffset;
            private readonly ulong _value;

            public Base(int module, int description) : this(module, description, description) { }

            public Base(int module, int descriptionStart, int descriptionEnd)
            {
                Debug.Assert(ModuleBegin <= module && module < ModuleEnd, "Invalid Module");
                Debug.Assert(DescriptionBegin <= descriptionStart && descriptionStart < DescriptionEnd, "Invalid Description");
                Debug.Assert(DescriptionBegin <= descriptionEnd && descriptionEnd < DescriptionEnd, "Invalid Description");
                Debug.Assert(descriptionStart <= descriptionEnd, "descriptionStart must be <= descriptionEnd");

                _value = SetBitsValueLong(module, ModuleBitsOffset, ModuleBitsCount) |
                         SetBitsValueLong(descriptionStart, DescriptionBitsOffset, DescriptionBitsCount) |
                         SetBitsValueLong(descriptionEnd, DescriptionEndBitsOffset, DescriptionBitsCount);
            }

            public BaseType Module => GetBitsValueLong(_value, ModuleBitsOffset, ModuleBitsCount);
            public BaseType DescriptionRangeStart => GetBitsValueLong(_value, DescriptionBitsOffset, DescriptionBitsCount);
            public BaseType DescriptionRangeEnd => GetBitsValueLong(_value, DescriptionEndBitsOffset, DescriptionBitsCount);

            /// <summary>
            /// The <see cref="Result"/> representing the start of this result range.
            /// </summary>
            public Result Value => new Result((BaseType)_value);

            /// <summary>
            /// Checks if the range of this <see cref="Result.Base"/> includes the provided <see cref="Result"/>.
            /// </summary>
            /// <param name="result">The <see cref="Result"/> to check.</param>
            /// <returns><see langword="true"/> if the range includes <paramref name="result"/>. Otherwise, <see langword="false"/>.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Includes(Result result)
            {
                // 99% of the time the values in this struct will be constants.
                // This check allows the compiler to optimize this method down to a simple comparison when possible.
                if (DescriptionRangeStart == DescriptionRangeEnd)
                {
                    return result.Value == Value.Value;
                }

                return result.Module == Module &&
                       result.Description - DescriptionRangeStart <= DescriptionRangeEnd - DescriptionRangeStart;
            }

            /// <summary>
            /// A function that can contain code for logging or debugging returned results.
            /// Intended to be used when returning a non-zero Result:
            /// <code>return result.Log();</code>
            /// </summary>
            /// <returns>The <see cref="Result"/> representing the start of this result range.</returns>
            public Result Log()
            {
                return Value.Log();
            }

            /// <summary>
            /// Same as <see cref="Log"/>, but for when one result is converted to another.
            /// </summary>
            /// <param name="originalResult">The original <see cref="Result"/> value.</param>
            /// <returns>The <see cref="Result"/> representing the start of this result range.</returns>
            public Result LogConverted(Result originalResult)
            {
                return Value.LogConverted(originalResult);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static BaseType GetBitsValueLong(ulong value, int bitsOffset, int bitsCount)
            {
                return (BaseType)(value >> bitsOffset) & ~(~default(BaseType) << bitsCount);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ulong SetBitsValueLong(int value, int bitsOffset, int bitsCount)
            {
                return ((uint)value & ~(~default(ulong) << bitsCount)) << bitsOffset;
            }
        }
    }
}
