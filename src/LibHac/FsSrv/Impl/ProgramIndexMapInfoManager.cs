using System;
using System.Collections.Generic;
using LibHac.Fs;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Keeps track of the program IDs and program indexes of each program in a multi-program application.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class ProgramIndexMapInfoManager : IDisposable
{
    private LinkedList<ProgramIndexMapInfo> _mapEntries;
    private SdkMutexType _mutex;

    public ProgramIndexMapInfoManager()
    {
        _mapEntries = new LinkedList<ProgramIndexMapInfo>();
        _mutex = new SdkMutexType();
    }

    public void Dispose()
    {
        Clear();
    }

    /// <summary>
    /// Unregisters any previously registered program index map info and registers the provided map info.
    /// </summary>
    /// <param name="programIndexMapInfo">The program map info entries to register.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result Reset(ReadOnlySpan<ProgramIndexMapInfo> programIndexMapInfo)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        ClearImpl();

        for (int i = 0; i < programIndexMapInfo.Length; i++)
        {
            var entry = new ProgramIndexMapInfo
            {
                ProgramId = programIndexMapInfo[i].ProgramId,
                MainProgramId = programIndexMapInfo[i].MainProgramId,
                ProgramIndex = programIndexMapInfo[i].ProgramIndex
            };

            _mapEntries.AddLast(entry);
        }

        // We skip running ClearImpl() if the allocation failed because we don't need to worry about that in C#

        return Result.Success;
    }

    /// <summary>
    /// Unregisters all currently registered program index map info.
    /// </summary>
    public void Clear()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        ClearImpl();
    }

    /// <summary>
    /// Gets the <see cref="ProgramIndexMapInfo"/> associated with the specified program ID.
    /// </summary>
    /// <param name="programId">The program ID of the map info to get.</param>
    /// <returns>If the program ID was found, the <see cref="ProgramIndexMapInfo"/> associated
    /// with that ID; otherwise, <see langword="null"/>.</returns>
    public Optional<ProgramIndexMapInfo> Get(ProgramId programId)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        return GetImpl((in ProgramIndexMapInfo x) => x.ProgramId == programId);
    }

    /// <summary>
    /// Gets the <see cref="ProgramId"/> of the program with index <paramref name="programIndex"/> in the
    /// multi-program app <paramref name="programId"/> is part of.
    /// </summary>
    /// <param name="programId">A program ID in the multi-program app to query.</param>
    /// <param name="programIndex">The index of the program to get.</param>
    /// <returns>If the program exists, the ID of the program with the specified index,
    /// otherwise <see cref="ProgramId.InvalidId"/></returns>
    public ProgramId GetProgramId(ProgramId programId, byte programIndex)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Optional<ProgramIndexMapInfo> programIndexMapInfo =
            GetImpl((in ProgramIndexMapInfo x) => x.ProgramId == programId);

        if (!programIndexMapInfo.HasValue)
            return ProgramId.InvalidId;

        Optional<ProgramIndexMapInfo> targetProgramIndexMapInfo = GetImpl((in ProgramIndexMapInfo x) =>
            x.MainProgramId == programIndexMapInfo.Value.MainProgramId && x.ProgramIndex == programIndex);

        if (!targetProgramIndexMapInfo.HasValue)
            return ProgramId.InvalidId;

        return targetProgramIndexMapInfo.Value.ProgramId;
    }

    /// <summary>
    /// Gets the number of currently registered programs,
    /// </summary>
    /// <returns>The number of registered programs.</returns>
    public int GetProgramCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        return _mapEntries.Count;
    }

    private delegate bool EntrySelector(in ProgramIndexMapInfo candidate);

    private Optional<ProgramIndexMapInfo> GetImpl(EntrySelector selector)
    {
        var returnValue = new Optional<ProgramIndexMapInfo>();

        foreach (ProgramIndexMapInfo entry in _mapEntries)
        {
            if (selector(in entry))
            {
                returnValue.Set(default);

                returnValue.Value.ProgramId = entry.ProgramId;
                returnValue.Value.MainProgramId = entry.MainProgramId;
                returnValue.Value.ProgramIndex = entry.ProgramIndex;

                break;
            }
        }

        return returnValue;
    }

    private void ClearImpl()
    {
        _mapEntries.Clear();
    }
}