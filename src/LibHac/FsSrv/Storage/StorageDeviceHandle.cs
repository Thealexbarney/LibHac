using System;
using System.Runtime.InteropServices;

namespace LibHac.FsSrv.Storage
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10)]
    public readonly struct StorageDeviceHandle : IEquatable<StorageDeviceHandle>
    {
        public readonly uint Value;
        public readonly StorageDevicePortId PortId;

        public StorageDeviceHandle(uint value, StorageDevicePortId portId)
        {
            Value = value;
            PortId = portId;
        }

        public override bool Equals(object obj) => obj is StorageDeviceHandle other && Equals(other);
        public bool Equals(StorageDeviceHandle other) => Value == other.Value && PortId == other.PortId;

        public static bool operator ==(StorageDeviceHandle left, StorageDeviceHandle right) => left.Equals(right);
        public static bool operator !=(StorageDeviceHandle left, StorageDeviceHandle right) => !(left == right);

        public override int GetHashCode() => HashCode.Combine(Value, (int) PortId);
    }
}
