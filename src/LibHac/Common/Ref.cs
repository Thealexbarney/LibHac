// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.Common
{
    /// <summary>
    /// A <see langword="struct"/> that can store a reference to a value of a specified type.
    /// </summary>
    /// <typeparam name="T">The type of value to reference.</typeparam>
    public readonly ref struct Ref<T>
    {
        /// <summary>
        /// The 1-length <see cref="Span{T}"/> instance used to track the target <typeparamref name="T"/> value.
        /// </summary>
        private readonly Span<T> _span;

        /// <summary>
        /// Initializes a new instance of the <see cref="Ref{T}"/> struct.
        /// </summary>
        /// <param name="value">The reference to the target <typeparamref name="T"/> value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Ref(ref T value)
        {
            _span = MemoryMarshal.CreateSpan(ref value, 1);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Ref{T}"/> struct.
        /// </summary>
        /// <param name="pointer">The pointer to the target value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Ref(void* pointer)
            : this(ref Unsafe.AsRef<T>(pointer))
        {
        }

        /// <summary>
        /// Gets the <typeparamref name="T"/> reference represented by the current <see cref="Ref{T}"/> instance.
        /// </summary>
        public ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref MemoryMarshal.GetReference(_span);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="Ref{T}"/> is <see langword="null"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the held reference is <see langword="null"/>;
        /// otherwise <see langword="false"/>.</returns>
        public bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.IsNullRef(ref Value);
        }

        /// <summary>
        /// Implicitly gets the <typeparamref name="T"/> value from a given <see cref="Ref{T}"/> instance.
        /// </summary>
        /// <param name="reference">The input <see cref="Ref{T}"/> instance.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(Ref<T> reference)
        {
            return reference.Value;
        }
    }

    /// <summary>
    /// A <see langword="struct"/> that can store a reference to a value of a specified type.
    /// </summary>
    /// <typeparam name="T">The type of value to reference.</typeparam>
    public readonly ref struct ReadOnlyRef<T>
    {
        /// <summary>
        /// The 1-length <see cref="ReadOnlySpan{T}"/> instance used to track the target <typeparamref name="T"/> value.
        /// </summary>
        private readonly ReadOnlySpan<T> _span;

        /// <summary>
        /// Initializes a new instance of the <see cref="Ref{T}"/> struct.
        /// </summary>
        /// <param name="value">The reference to the target <typeparamref name="T"/> value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyRef(in T value)
        {
            _span = MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in value), 1);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Ref{T}"/> struct.
        /// </summary>
        /// <param name="pointer">The pointer to the target value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ReadOnlyRef(void* pointer)
            : this(in Unsafe.AsRef<T>(pointer))
        {
        }

        /// <summary>
        /// Gets the <typeparamref name="T"/> reference represented by the current <see cref="Ref{T}"/> instance.
        /// </summary>
        public ref readonly T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref MemoryMarshal.GetReference(_span);
        }

        /// <summary>
        /// Returns a value that indicates whether the current <see cref="Ref{T}"/> is <see langword="null"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the held reference is <see langword="null"/>;
        /// otherwise <see langword="false"/>.</returns>
        public bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Unsafe.IsNullRef(ref Unsafe.AsRef(in Value));
        }

        /// <summary>
        /// Implicitly gets the <typeparamref name="T"/> value from a given <see cref="Ref{T}"/> instance.
        /// </summary>
        /// <param name="reference">The input <see cref="Ref{T}"/> instance.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(ReadOnlyRef<T> reference)
        {
            return reference.Value;
        }
    }
}