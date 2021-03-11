using System;
using LibHac.Common;
using LibHac.Fs.Impl;

namespace LibHac.Fs.Fsa
{
    public interface ICommonMountNameGenerator : IDisposable
    {
        Result GenerateCommonMountName(Span<byte> nameBuffer);
    }

    public interface ISaveDataAttributeGetter : IDisposable
    {
        Result GetSaveDataAttribute(out SaveDataAttribute attribute);
    }

    public static class Registrar
    {
        public static Result Register(this FileSystemClient fs, U8Span name, IFileSystem fileSystem)
        {
            var accessor = new FileSystemAccessor(fs, name, null, fileSystem, null, null);
            fs.Impl.Register(accessor);

            return Result.Success;
        }

        public static Result Register(this FileSystemClient fs, U8Span name, IFileSystem fileSystem,
            ICommonMountNameGenerator mountNameGenerator)
        {
            var accessor = new FileSystemAccessor(fs, name, null, fileSystem, mountNameGenerator, null);
            fs.Impl.Register(accessor);

            return Result.Success;
        }

        public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
            IFileSystem fileSystem, ICommonMountNameGenerator mountNameGenerator, bool useDataCache, bool usePathCache)
        {
            return fs.Register(name, multiCommitTarget, fileSystem, mountNameGenerator, null, useDataCache,
                usePathCache);
        }

        public static Result Register(this FileSystemClient fs, U8Span name, IMultiCommitTarget multiCommitTarget,
            IFileSystem fileSystem, ICommonMountNameGenerator mountNameGenerator,
            ISaveDataAttributeGetter saveAttributeGetter, bool useDataCache, bool usePathCache)
        {
            var accessor = new FileSystemAccessor(fs, name, multiCommitTarget, fileSystem, mountNameGenerator,
                saveAttributeGetter);

            accessor.SetFileDataCacheAttachable(useDataCache);
            accessor.SetPathBasedFileDataCacheAttachable(usePathCache);
            fs.Impl.Register(accessor);

            return Result.Success;
        }

        public static void Unregister(this FileSystemClient fs, U8Span name)
        {
            fs.Impl.Unregister(name);
        }
    }
}

