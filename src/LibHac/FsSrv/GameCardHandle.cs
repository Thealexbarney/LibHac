using System;

namespace LibHac.FsSrv
{
    public readonly struct GameCardHandle : IEquatable<GameCardHandle>
    {
        public readonly int Value;

        public GameCardHandle(int value)
        {
            Value = value;
        }

        public override bool Equals(object obj) => obj is GameCardHandle handle && Equals(handle);
        public bool Equals(GameCardHandle other) => Value == other.Value;
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(GameCardHandle left, GameCardHandle right) => left.Equals(right);
        public static bool operator !=(GameCardHandle left, GameCardHandle right) => !(left == right);
    }
}
