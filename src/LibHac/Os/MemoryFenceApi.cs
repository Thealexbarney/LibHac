using System.Runtime.CompilerServices;
using LibHac.Os.Impl;

namespace LibHac.Os;

public static class MemoryFenceApi
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryStoreStore() => MemoryFence.FenceMemoryStoreStore();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryStoreLoad() => MemoryFence.FenceMemoryStoreLoad();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryStoreAny() => MemoryFence.FenceMemoryStoreAny();

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryLoadStore() => MemoryFence.FenceMemoryLoadStore();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryLoadLoad() => MemoryFence.FenceMemoryLoadLoad();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryLoadAny() => MemoryFence.FenceMemoryLoadAny();

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryAnyStore() => MemoryFence.FenceMemoryAnyStore();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryAnyLoad() => MemoryFence.FenceMemoryAnyLoad();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void FenceMemoryAnyAny() => MemoryFence.FenceMemoryAnyAny();
}