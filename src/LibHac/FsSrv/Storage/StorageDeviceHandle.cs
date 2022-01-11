using System;
using LibHac.Common.FixedArrays;

namespace LibHac.FsSrv.Storage;

public readonly struct StorageDeviceHandle : IEquatable<StorageDeviceHandle>
{
    public readonly uint Value;
    public readonly StorageDevicePortId PortId;
    public readonly Array11<byte> Reserved;

    public StorageDeviceHandle(uint value, StorageDevicePortId portId)
    {
        Value = value;
        PortId = portId;
        Reserved = default;
    }

    public override bool Equals(object obj) => obj is StorageDeviceHandle other && Equals(other);
    public bool Equals(StorageDeviceHandle other) => Value == other.Value && PortId == other.PortId;

    public static bool operator ==(StorageDeviceHandle left, StorageDeviceHandle right) => left.Equals(right);
    public static bool operator !=(StorageDeviceHandle left, StorageDeviceHandle right) => !(left == right);

    public override int GetHashCode() => HashCode.Combine(Value, (int)PortId);
}