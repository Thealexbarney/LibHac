using System;
using LibHac.Fs;
using LibHac.Sf;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Iterates through the <see cref="SaveDataInfo"/> of the save data
    /// in a single save data space.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    // Kinda weird to name an interface / pure abstract class SaveDataInfoReaderImpl. Ask Nintendo, not me.
    //
    public interface SaveDataInfoReaderImpl : IDisposable
    {
        /// <summary>
        /// Returns the next <see cref="SaveDataInfo"/> entries. This method will continue writing
        /// entries to <paramref name="saveDataInfoBuffer"/> until there is either no more space
        /// in the buffer, or until there are no more entries to iterate.
        /// </summary>
        /// <param name="readCount">If the method returns successfully, contains the number
        /// of <see cref="SaveDataInfo"/> written to <paramref name="saveDataInfoBuffer"/>.
        /// A value of 0 indicates that there are no more entries to iterate, or the buffer is too small.</param>
        /// <param name="saveDataInfoBuffer">The buffer in which to write the <see cref="SaveDataInfo"/>.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Read(out long readCount, OutBuffer saveDataInfoBuffer);
    }
}