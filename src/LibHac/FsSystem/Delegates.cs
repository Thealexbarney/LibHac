using System;

namespace LibHac.FsSystem;

public delegate Result RandomDataGenerator(Span<byte> buffer);