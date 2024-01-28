using System;

namespace LibHac.Fs.Save;

public static class InternalStorageFileNames
{
    public static ReadOnlySpan<byte> InternalStorageFileNameAllocationTableMeta => "AllocationTableMeta"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameRawSaveData => "RawSaveData"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameRawSaveDataWithZeroFree => "RawSaveDataWithZeroFree"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameSaveDataControlArea => "SaveDataControlArea"u8;
}