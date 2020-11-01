using System;
using LibHac.Fs;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Indexes save data metadata, holding key-value pairs of types <see cref="SaveDataAttribute"/> and
    /// <see cref="SaveDataIndexerValue"/> respectively. 
    /// </summary>
    /// <remarks>
    /// Each <see cref="SaveDataIndexerValue"/> value contains information about the save data
    /// including its size and current state, as well as its <see cref="SaveDataSpaceId"/> and save data
    /// ID which represent the save data's storage location on disk.
    /// </remarks>
    public interface ISaveDataIndexer : IDisposable
    {
        /// <summary>
        /// Commit any changes made to the save data index.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Commit();

        /// <summary>
        /// Rollback any changes made to the save data index since the last commit.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Rollback();

        /// <summary>
        /// Remove all entries from the save data index and set the index to its initial state.
        /// </summary>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Reset();

        /// <summary>
        /// Adds a new key to the index and returns the save data ID assigned to it.
        /// The created value will only contain the assigned save data ID.
        /// Fails if the key already exists.
        /// </summary>
        /// <param name="saveDataId">If the method returns successfully, contains the 
        /// save data ID assigned to the new entry.
        /// Save data IDs are assigned using a counter that is incremented for each added save.</param>
        /// <param name="key">The key to add.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Publish(out ulong saveDataId, in SaveDataAttribute key);

        /// <summary>
        /// Retrieves the <see cref="SaveDataIndexerValue"/> for the specified <see cref="SaveDataAttribute"/> key.
        /// </summary>
        /// <param name="value">If the method returns successfully, contains the 
        /// save data ID assigned to the new entry.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Get(out SaveDataIndexerValue value, in SaveDataAttribute key);

        /// <summary>
        /// Adds a key with a pre-specified static save data ID to the index.
        /// </summary>
        /// <remarks>
        /// Adding a save data ID that is already in the index is not allowed. Adding a static ID that might
        /// conflict with a future dynamically-assigned ID should be avoided, otherwise there will be two saves
        /// with the same ID.
        /// FileSystemProxy avoids this by setting the high bit on static IDs. e.g. 0x8000000000000015
        /// </remarks>
        /// <param name="key">The key to add.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result PutStaticSaveDataIdIndex(in SaveDataAttribute key);

        /// <summary>
        /// Determines if there are any non-reserved entry slots remaining in the index.
        /// </summary>
        /// <remarks>If the <see cref="ISaveDataIndexer"/> has a fixed number of entries, a portion of
        /// those entries may be reserved for internal use, </remarks>
        /// <returns><see langword="true"/> if there are any non-reserved entries remaining,
        /// otherwise <see langword="false"/>.</returns>
        bool IsRemainedReservedOnly();

        /// <summary>
        /// Removes the save data with the specified save data ID from the index.
        /// </summary>
        /// <param name="saveDataId">The ID of the save to be removed.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result Delete(ulong saveDataId);

        /// <summary>
        /// Sets the <see cref="SaveDataSpaceId"/> in the specified save data's value.
        /// </summary>
        /// <param name="saveDataId">The save data ID of the save data to modify.</param>
        /// <param name="spaceId">The new <see cref="SaveDataSpaceId"/> for the specified save data.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result SetSpaceId(ulong saveDataId, SaveDataSpaceId spaceId);

        /// <summary>
        /// Sets the size in the specified save data's value.
        /// </summary>
        /// <param name="saveDataId">The save data ID of the save data to modify.</param>
        /// <param name="size">The new size for the specified save data.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result SetSize(ulong saveDataId, long size);

        /// <summary>
        /// Sets the <see cref="SaveDataState"/> in the specified save data's value.
        /// </summary>
        /// <param name="saveDataId">The save data ID of the save data to modify.</param>
        /// <param name="state">The new <see cref="SaveDataState"/> for the specified save data.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result SetState(ulong saveDataId, SaveDataState state);

        /// <summary>
        /// Gets the key of the specified save data ID.
        /// </summary>
        /// <param name="key">If the method returns successfully, contains the <see cref="SaveDataAttribute"/>
        /// key of the specified save data ID.</param>
        /// <param name="saveDataId">The save data ID to locate.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result GetKey(out SaveDataAttribute key, ulong saveDataId);

        /// <summary>
        /// Gets the value of the specified save data ID.
        /// </summary>
        /// <param name="value">If the method returns successfully, contains the <see cref="SaveDataIndexerValue"/>
        /// value of the specified save data ID.</param>
        /// <param name="saveDataId">The save data ID to locate.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result GetValue(out SaveDataIndexerValue value, ulong saveDataId);

        /// <summary>
        /// Sets a new value to a key that already exists in the index.
        /// </summary>
        /// <param name="key">The key of the value to set.</param>
        /// <param name="value">The new value to associate with the specified key.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result SetValue(in SaveDataAttribute key, in SaveDataIndexerValue value);

        /// <summary>
        /// Gets the number of elements currently in the <see cref="SaveDataIndexer"/>.
        /// </summary>
        /// <returns>The current element count.</returns>
        int GetIndexCount();

        /// <summary>
        /// Returns an <see cref="SaveDataInfoReaderImpl"/> that iterates through the <see cref="SaveDataIndexer"/>.
        /// </summary>
        /// <param name="infoReader">If the method returns successfully, contains the created <see cref="SaveDataInfoReaderImpl"/>.</param>
        /// <returns>The <see cref="Result"/> of the operation.</returns>
        Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<SaveDataInfoReaderImpl> infoReader);
    }
}