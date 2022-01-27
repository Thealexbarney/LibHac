// ReSharper disable InconsistentNaming
using System;
using LibHac.Common;
using LibHac.Diag;

namespace LibHac.FsSystem;

/// <summary>
/// Generates a hash for a stream of data. The data can be given to the <see cref="IHash256Generator"/>
/// as multiple, smaller sequential blocks of data.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public abstract class IHash256Generator : IDisposable
{
    public static readonly long HashSize = 256 / 8;

    public virtual void Dispose() { }

    public void Initialize()
    {
        DoInitialize();
    }

    public void Update(ReadOnlySpan<byte> data)
    {
        DoUpdate(data);
    }

    public void GetHash(Span<byte> hashBuffer)
    {
        Assert.SdkRequiresEqual(HashSize, hashBuffer.Length);

        DoGetHash(hashBuffer);
    }

    protected abstract void DoInitialize();
    protected abstract void DoUpdate(ReadOnlySpan<byte> data);
    protected abstract void DoGetHash(Span<byte> hashBuffer);
}

/// <summary>
/// Creates <see cref="IHash256Generator"/> objects and can generate a hash for a single, in-memory block of data.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public abstract class IHash256GeneratorFactory : IDisposable
{
    public virtual void Dispose() { }

    public UniqueRef<IHash256Generator> Create()
    {
        return DoCreate();
    }

    public void GenerateHash(Span<byte> hashBuffer, ReadOnlySpan<byte> data)
    {
        Assert.SdkRequiresEqual(IHash256Generator.HashSize, hashBuffer.Length);

        DoGenerateHash(hashBuffer, data);
    }

    protected abstract UniqueRef<IHash256Generator> DoCreate();
    protected abstract void DoGenerateHash(Span<byte> hashBuffer, ReadOnlySpan<byte> data);
}

/// <summary>
/// Creates <see cref="IHash256GeneratorFactory"/> objects.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public abstract class IHash256GeneratorFactorySelector : IDisposable
{
    public virtual void Dispose() { }

    public IHash256GeneratorFactory GetFactory()
    {
        return DoGetFactory();
    }

    protected abstract IHash256GeneratorFactory DoGetFactory();
}