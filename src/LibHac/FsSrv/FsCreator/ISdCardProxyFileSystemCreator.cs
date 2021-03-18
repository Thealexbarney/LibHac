using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.FsCreator
{
    public interface ISdCardProxyFileSystemCreator
    {
        Result Create(out IFileSystem fileSystem, bool isCaseSensitive);

        /// <summary>
        /// Formats the SD card.
        /// </summary>
        /// <param name="removeFromFatFsCache">Should the SD card file system be removed from the
        /// FAT file system cache?</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Format(bool removeFromFatFsCache);

        /// <summary>
        /// Automatically closes all open proxy file system entries and formats the SD card.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Format();
    }
}
