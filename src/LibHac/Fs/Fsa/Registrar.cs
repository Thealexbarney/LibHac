using System;
using LibHac.Common;

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

    internal static class Registrar
    {
        public static Result Register(U8Span name, IFileSystem fileSystem)
        {
            throw new NotImplementedException();
        }

        public static Result Register(U8Span name, IFileSystem fileSystem, ICommonMountNameGenerator mountNameGenerator)
        {
            throw new NotImplementedException();
        }

        public static Result Register(U8Span name, IMultiCommitTarget multiCommitTarget, IFileSystem fileSystem,
            ICommonMountNameGenerator mountNameGenerator, bool useDataCache, bool usePathCache)
        {
            throw new NotImplementedException();
        }

        public static Result Register(U8Span name, IMultiCommitTarget multiCommitTarget, IFileSystem fileSystem,
            ICommonMountNameGenerator mountNameGenerator, ISaveDataAttributeGetter saveAttributeGetter,
            bool useDataCache, bool usePathCache)
        {
            throw new NotImplementedException();
        }

        public static void Unregister(U8Span name)
        {

        }
    }
}

