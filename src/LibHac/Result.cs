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
        public static Result Success => new Result(SuccessValue);

        private static IResultLogger Logger { get; set; }
        private static IResultNameResolver NameResolver { get; set; }

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

        public void ThrowIfFailure()
        {
            if (IsFailure())
            {
                ThrowHelper.ThrowResult(this);
            }
        }

        /// <summary>
        /// Performs no action in release mode.
        /// In debug mode, logs returned results using the <see cref="IResultLogger"/> set by <see cref="SetLogger"/>.
        /// <br/>Intended to always be used when returning a non-zero <see cref="Result"/>.
        /// <br/><br/>Example:
        /// <code>return result.Log();</code>
        /// </summary>
        /// <returns>The called <see cref="Result"/> value.</returns>
        public Result Log()
        {
            LogImpl();

            return this;
        }

        /// <summary>
        /// In debug mode, logs converted results using the <see cref="IResultLogger"/> set by <see cref="SetLogger"/>.
        /// </summary>
        /// <param name="originalResult">The original <see cref="Result"/> value.</param>
        /// <returns>The called <see cref="Result"/> value.</returns>
        public Result LogConverted(Result originalResult)
        {
            LogConvertedImpl(originalResult);

            return this;
        }

        public bool TryGetResultName(out string name)
        {
            IResultNameResolver resolver = NameResolver;

            if (resolver == null)
            {
                name = default;
                return false;
            }

            return resolver.TryResolveName(this, out name);
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

        public static void SetLogger(IResultLogger logger)
        {
            Logger = logger;
        }

        public static void SetNameResolver(IResultNameResolver nameResolver)
        {
            NameResolver = nameResolver;
        }

        [Conditional("DEBUG")]
        private void LogImpl()
        {
            Logger?.LogResult(this);
        }

        [Conditional("DEBUG")]
        private void LogConvertedImpl(Result originalResult)
        {
            Logger?.LogConvertedResult(this, originalResult);
        }

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
            /// If returning a <see cref="Result"/> from a function, use <see cref="Log"/> instead.
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
            /// Performs no action in release mode.
            /// In debug mode, logs returned results using the <see cref="IResultLogger"/> set by <see cref="SetLogger"/>.
            /// <br/>Intended to always be used when returning a non-zero <see cref="Result"/>.
            /// <br/><br/>Example:
            /// <code>return ResultFs.PathNotFound.Log();</code>
            /// </summary>
            /// <returns>The <see cref="Result"/> representing the start of this result range.</returns>
            public Result Log()
            {
                return Value.Log();
            }

            /// <summary>
            /// In debug mode, logs converted results using the <see cref="IResultLogger"/> set by <see cref="SetLogger"/>.
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

        public interface IResultLogger
        {
            public void LogResult(Result result);
            public void LogConvertedResult(Result result, Result originalResult);
        }

        public interface IResultNameResolver
        {
            public bool TryResolveName(Result result, out string name);
        }
    }
}
