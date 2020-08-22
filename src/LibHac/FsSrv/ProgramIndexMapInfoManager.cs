using System;
using System.Collections.Generic;
using LibHac.Fs;
using LibHac.Ncm;

namespace LibHac.FsSrv
{
    /// <summary>
    /// Keeps track of the program IDs and program indexes of each program in a multi-program application.
    /// </summary>
    public class ProgramIndexMapInfoManager
    {
        private LinkedList<ProgramIndexMapInfo> MapEntries { get; } = new LinkedList<ProgramIndexMapInfo>();

        /// <summary>
        /// Unregisters any previously registered program index map info and registers the provided map info.
        /// </summary>
        /// <param name="programIndexMapInfo">The program map info entries to register.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
        public Result Reset(ReadOnlySpan<ProgramIndexMapInfo> programIndexMapInfo)
        {
            lock (MapEntries)
            {
                ClearImpl();

                for (int i = 0; i < programIndexMapInfo.Length; i++)
                {
                    MapEntries.AddLast(programIndexMapInfo[i]);
                }

                return Result.Success;
            }
        }

        /// <summary>
        /// Unregisters all currently registered program index map info.
        /// </summary>
        public void Clear()
        {
            lock (MapEntries)
            {
                ClearImpl();
            }
        }

        /// <summary>
        /// Gets the <see cref="ProgramIndexMapInfo"/> associated with the specified program ID.
        /// </summary>
        /// <param name="programId">The program ID of the map info to get.</param>
        /// <returns>If the program ID was found, the <see cref="ProgramIndexMapInfo"/> associated
        /// with that ID; otherwise, <see langword="null"/>.</returns>
        public ProgramIndexMapInfo? Get(ProgramId programId)
        {
            lock (MapEntries)
            {
                return GetImpl((in ProgramIndexMapInfo x) => x.ProgramId == programId);
            }
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
            lock (MapEntries)
            {
                ProgramIndexMapInfo? mainProgramMapInfo = GetImpl((in ProgramIndexMapInfo x) => x.ProgramId == programId);

                if(!mainProgramMapInfo.HasValue)
                    return ProgramId.InvalidId;

                ProgramIndexMapInfo? requestedMapInfo = GetImpl((in ProgramIndexMapInfo x) =>
                    x.MainProgramId == mainProgramMapInfo.Value.MainProgramId && x.ProgramIndex == programIndex);

                if (!requestedMapInfo.HasValue)
                    return ProgramId.InvalidId;

                return requestedMapInfo.Value.ProgramId;
            }
        }

        /// <summary>
        /// Gets the number of currently registered programs,
        /// </summary>
        /// <returns>The number of registered programs.</returns>
        public int GetProgramCount()
        {
            lock (MapEntries)
            {
                return MapEntries.Count;
            }
        }

        private delegate bool EntrySelector(in ProgramIndexMapInfo candidate);

        private ProgramIndexMapInfo? GetImpl(EntrySelector selector)
        {
            foreach (ProgramIndexMapInfo entry in MapEntries)
            {
                if (selector(in entry))
                {
                    return entry;
                }
            }

            return null;
        }

        private void ClearImpl()
        {
            MapEntries.Clear();
        }
    }
}
