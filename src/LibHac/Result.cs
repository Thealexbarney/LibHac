using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LibHac.Common;
using BaseType = System.UInt32;

namespace LibHac
{
    /// <summary>
    /// Represents a code used to report the result of a returned function.
    /// </summary>
    [Serializable]
    [DebuggerDisplay("{" + nameof(ToStringWithName) + "(),nq}")]
    public readonly struct Result : IEquatable<Result>
    {
        private const BaseType SuccessValue = default;
        /// <summary>
        /// The <see cref="Result"/> signifying success.
        /// </summary>
        public static Result Success => new Result(SuccessValue);

        private static IResultLogger Logger { get; set; }
        private static IResultNameResolver NameResolver { get; set; } = new ResultNameResolver();

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

        /// <summary>
        /// Creates a new <see cref="Result"/> from the internal result value.
        /// </summary>
        /// <param name="value">The value used internally by <see cref="Result"/>.</param>
        public Result(BaseType value)
        {
            _value = GetBitsValue(value, ModuleBitsOffset, ModuleBitsCount + DescriptionBitsCount);
        }

        /// <summary>
        /// Creates a new <see cref="Result"/> from a module and description.
        /// </summary>
        /// <param name="module">The module this result is from. Must be in the range 1 through 511.</param>
        /// <param name="description">The description value of the result. Must be in the range 0 through 8191.</param>
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

        /// <summary>
        /// Specifies that the <see cref="Result"/> from a returned function is explicitly ignored.
        /// </summary>
        public void IgnoreResult() { }

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
                UnsafeHelpers.SkipParamInit(out name);
                return false;
            }

            return resolver.TryResolveName(this, out name);
        }

        /// <summary>
        /// If a <see cref="IResultNameResolver"/> has been set via <see cref="SetNameResolver"/>, attempts to
        /// return the name and error code of this <see cref="Result"/>, otherwise it only returns <see cref="ErrorCode"/>.
        /// </summary>
        /// <returns>If a name was found, the name and error code, otherwise just the error code.</returns>
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

        /// <summary>
        /// Sets a <see cref="IResultLogger"/> to be called when <see cref="Log"/> is called in debug mode.
        /// </summary>
        public static void SetLogger(IResultLogger logger)
        {
            Logger = logger;
        }

        /// <summary>
        /// Sets a <see cref="IResultNameResolver"/> that will be used by methods like <see cref="ToStringWithName"/>
        /// or <see cref="TryGetResultName"/> to resolve the names of <see cref="Result"/>s.
        /// </summary>
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

        /// <summary>
        /// Represents a range of <see cref="Result"/>s.
        /// This range is defined by a single module value and a range of description values.
        /// See the documentation remarks for additional information.
        /// </summary>
        /// <remarks>
        /// Due to C# not having templates, we can't define results like Horizon and Atmosphere do.
        /// Compared to those Result classes, this struct generates identical code and uses identical syntax.
        /// <br/>A Result definition should look like this: <c>public static Result.Base PathNotFound => new Result.Base(ModuleFs, 1);</c>
        /// <br/>Being a computed property like this will allow the compiler to do constant propagation to optimize comparisons.
        /// <br/><br/>This is an example of how a Result should be returned from a function: <c>return PathNotFound.Log();</c>
        /// <br/>The <see cref="Log()"/> method will return the <see cref="Result"/> for the specified <see cref="Base"/>, and
        /// will optionally log the returned Result when running in debug mode for easier debugging. All Result logging functionality
        /// is removed from release builds.
        /// If the <see cref="Result"/> is not being used as a return value, <see cref="Value"/> will get the Result without logging anything.
        /// <br/><br/><see cref="Includes"/> is used to check if a provided <see cref="Result"/> is contained within the range of the <see cref="Base"/>.
        /// If the <see cref="Base"/> is a computed property as shown above, the compiler will be able to properly optimize the code.
        /// The following pairs of lines will produce the same code given <c>Result result;</c>
        /// <code>
        /// bool a1 = ResultFs.TargetNotFound.Includes(result); // Comparing a single value
        /// bool a2 = result.Value == 0x7D402;
        ///
        /// bool b1 = ResultFs.InsufficientFreeSpace.Includes(result); // Comparing a range of values
        /// bool b2 = return result.Module == 2 &amp;&amp; (result.Description - 30 &lt;= 45 - 30);
        /// </code>
        /// Unfortunately RyuJIT will not automatically inline the property when the compiled CIL is 16 bytes or larger as in cases like
        /// <c>new Result.Base(ModuleFs, 2000, 2499)</c>. The property will need to have the aggressive inlining flag set like so:
        /// <c>public static Result.Base SdCardAccessFailed { [MethodImpl(MethodImplOptions.AggressiveInlining)] get =&gt; new Result.Base(ModuleFs, 2000, 2499); }</c>
        /// </remarks>
        [DebuggerDisplay("{" + nameof(ToStringWithName) + "(),nq}")]
        public readonly struct Base
        {
            private const int DescriptionEndBitsOffset = ReservedBitsOffset;
            private readonly ulong _value;

            /// <summary>
            /// Creates a Result <see cref="Base"/> containing a single value.
            /// </summary>
            /// <param name="module">The module this result is from. Must be in the range 1 through 511.</param>
            /// <param name="description">The description value of the result. Must be in the range 0 through 8191.</param>
            public Base(int module, int description) : this(module, description, description) { }

            /// <summary>
            /// Creates a Result <see cref="Base"/> containing a range of values.
            /// </summary>
            /// <param name="module">The module this result is from. Must be in the range 1 through 511.</param>
            /// <param name="descriptionStart">The inclusive start description value of the range. Must be in the range 0 through 8191.</param>
            /// <param name="descriptionEnd">The inclusive end description value of the range. Must be in the range 0 through 8191.</param>
            public Base(int module, int descriptionStart, int descriptionEnd)
            {
                Debug.Assert(ModuleBegin <= module && module < ModuleEnd, "Invalid Module");
                Debug.Assert(DescriptionBegin <= descriptionStart && descriptionStart < DescriptionEnd, "Invalid Description Start");
                Debug.Assert(DescriptionBegin <= descriptionEnd && descriptionEnd < DescriptionEnd, "Invalid Description End");
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

            /// <summary>
            /// A <see cref="Base"/> that can't be used as a <see cref="Result"/> itself, but can check if its
            /// range includes other <see cref="Result"/>s.
            /// </summary>
            public readonly struct Abstract
            {
                private readonly ulong _value;

                public Abstract(int module, int description) : this(module, description, description) { }

                public Abstract(int module, int descriptionStart, int descriptionEnd)
                {
                    Debug.Assert(ModuleBegin <= module && module < ModuleEnd, "Invalid Module");
                    Debug.Assert(DescriptionBegin <= descriptionStart && descriptionStart < DescriptionEnd, "Invalid Description Start");
                    Debug.Assert(DescriptionBegin <= descriptionEnd && descriptionEnd < DescriptionEnd, "Invalid Description End");
                    Debug.Assert(descriptionStart <= descriptionEnd, "descriptionStart must be <= descriptionEnd");

                    _value = SetBitsValueLong(module, ModuleBitsOffset, ModuleBitsCount) |
                             SetBitsValueLong(descriptionStart, DescriptionBitsOffset, DescriptionBitsCount) |
                             SetBitsValueLong(descriptionEnd, DescriptionEndBitsOffset, DescriptionBitsCount);
                }

                public BaseType Module => GetBitsValueLong(_value, ModuleBitsOffset, ModuleBitsCount);
                public BaseType DescriptionRangeStart => GetBitsValueLong(_value, DescriptionBitsOffset, DescriptionBitsCount);
                public BaseType DescriptionRangeEnd => GetBitsValueLong(_value, DescriptionEndBitsOffset, DescriptionBitsCount);

                private Result Value => new Result((BaseType)_value);

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
