using System.Runtime.CompilerServices;

namespace LibHac.Common
{
    public static class UnsafeHelpers
    {
        /// <summary>
        /// Bypasses definite assignment rules for a given unmanaged value,
        /// or zeros a managed value to avoid having invalid references.
        /// <br/>Used in instances where an out value in the original code isn't set due to an error condition.
        /// <br/>Behaves the same as <see cref="Unsafe.SkipInit{T}"/>, except it zeros managed values.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="value">The object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipParamInit<T>(out T value)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                Unsafe.SkipInit(out value);
            }
            else
            {
                value = default;
            }
        }

        /// <summary>
        /// Bypasses definite assignment rules for the given unmanaged values,
        /// zeroing any managed values to avoid having invalid references.
        /// <br/>Used in instances where out values in the original code aren't set due to an error condition.
        /// <br/>Behaves the same as calling <see cref="Unsafe.SkipInit{T}"/>
        /// on each value, except managed values will be zeroed.
        /// </summary>
        /// <typeparam name="T1">The type of the first object.</typeparam>
        /// <typeparam name="T2">The type of the second object.</typeparam>
        /// <param name="value1">The first object.</param>
        /// <param name="value2">The second object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipParamInit<T1, T2>(out T1 value1, out T2 value2)
        {
            SkipParamInit(out value1);
            SkipParamInit(out value2);
        }

        /// <summary>
        /// Bypasses definite assignment rules for the given unmanaged values,
        /// zeroing any managed values to avoid having invalid references.
        /// <br/>Used in instances where out values in the original code aren't set due to an error condition.
        /// <br/>Behaves the same as calling <see cref="Unsafe.SkipInit{T}"/>
        /// on each value, except managed values will be zeroed.
        /// </summary>
        /// <typeparam name="T1">The type of the first object.</typeparam>
        /// <typeparam name="T2">The type of the second object.</typeparam>
        /// <typeparam name="T3">The type of the third object.</typeparam>
        /// <param name="value1">The first object.</param>
        /// <param name="value2">The second object.</param>
        /// <param name="value3">The third object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SkipParamInit<T1, T2, T3>(out T1 value1, out T2 value2, out T3 value3)
        {
            SkipParamInit(out value1);
            SkipParamInit(out value2);
            SkipParamInit(out value3);
        }
    }
}
