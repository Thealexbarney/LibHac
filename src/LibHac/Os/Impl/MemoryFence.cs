using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace LibHac.Os.Impl;

public static class MemoryFence
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryStoreStore()
    {
        if (Sse.IsSupported)
        {
            Sse.StoreFence();
        }
        else
        {
            // This only needs to be a store barrier on aarch64
            System.Threading.Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryStoreLoad()
    {
        if (Sse2.IsSupported)
        {
            Sse2.MemoryFence();
        }
        else
        {
            System.Threading.Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryStoreAny()
    {
        if (Sse2.IsSupported)
        {
            Sse2.MemoryFence();
        }
        else
        {
            System.Threading.Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryLoadStore()
    {
        if (Sse2.IsSupported)
        {
            Sse2.MemoryFence();
        }
        else
        {
            // This only needs to be a load barrier on aarch64
            System.Threading.Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryLoadLoad()
    {
        if (Sse2.IsSupported)
        {
            Sse2.LoadFence();
        }
        else
        {
            // This only needs to be a load barrier on aarch64
            System.Threading.Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryLoadAny()
    {
        if (Sse2.IsSupported)
        {
            Sse2.MemoryFence();
        }
        else
        {
            // This only needs to be a load barrier on aarch64
            System.Threading.Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryAnyStore()
    {
        if (Sse2.IsSupported)
        {
            Sse2.MemoryFence();
        }
        else
        {
            System.Threading.Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryAnyLoad()
    {
        if (Sse2.IsSupported)
        {
            Sse2.MemoryFence();
        }
        else
        {
            System.Threading.Thread.MemoryBarrier();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void FenceMemoryAnyAny()
    {
        if (Sse2.IsSupported)
        {
            Sse2.MemoryFence();
        }
        else
        {
            System.Threading.Thread.MemoryBarrier();
        }
    }
}