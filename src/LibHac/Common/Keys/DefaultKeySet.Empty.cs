﻿using System;

namespace LibHac.Common.Keys;

internal static partial class DefaultKeySet
{
    private static ReadOnlySpan<byte> TsecSecrets => new byte[] { };
    private static ReadOnlySpan<byte> RootKeysDev => new byte[] { };
    private static ReadOnlySpan<byte> RootKeysProd => new byte[] { };
    private static ReadOnlySpan<byte> KeySeeds => new byte[] { };
    private static ReadOnlySpan<byte> StoredKeysDev => new byte[] { };
    private static ReadOnlySpan<byte> StoredKeysProd => new byte[] { };
    private static ReadOnlySpan<byte> DerivedKeysDev => new byte[] { };
    private static ReadOnlySpan<byte> DerivedKeysProd => new byte[] { };
    private static ReadOnlySpan<byte> DeviceKeys => new byte[] { };
    private static ReadOnlySpan<byte> DerivedDeviceKeysDev => new byte[] { };
    private static ReadOnlySpan<byte> DerivedDeviceKeysProd => new byte[] { };
    private static ReadOnlySpan<byte> RsaSigningKeysDev => new byte[] { };
    private static ReadOnlySpan<byte> RsaSigningKeysProd => new byte[] { };
    private static ReadOnlySpan<byte> RsaKeys => new byte[] { };
    private static ReadOnlySpan<byte> DeviceRsaKeys => new byte[] { };
}