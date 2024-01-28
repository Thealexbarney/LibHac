using System;

namespace LibHac.FsSystem.Save;

public static class InternalStorageFileNames
{
    public static ReadOnlySpan<byte> InternalStorageFileNameIntegrity => "InternalStorageFileNameIntegrity"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameIntegrityExtraData => "InternalStorageFileNameIntegrityExtraData"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameIntegrityHashAlgorithm => "InternalStorageFileNameHashAlgorithm"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameIntegritySeed => "InternalStorageFileNameIntegritySeed"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameIntegritySeedEnabled => "InternalStorageFileNameIntegrity"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameIntegritySha2Salt => "InternalStorageFileNameIntegritySha2Salt"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameIntegritySha2SaltWithZeroFree => "InternalStorageFileNameIntegritySha2SaltWithZeroFree"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameIntegrityWithZeroFree => "InternalStorageFileNameIntegrityWithZeroFree"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameJournal => "InternalStorageFileNameJournal"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameMasterHeaderMac => "MasterHeaderMac"u8;
    public static ReadOnlySpan<byte> InternalStorageFileNameSha2Salt => "InternalStorageFileNameSha2Salt"u8;
}