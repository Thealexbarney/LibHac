using System;
using System.Collections.Generic;
using LibHac.Fs;
using LibHac.Ncm;

namespace LibHac.FsSrv
{
    public class ProgramIndexMapInfoManager
    {
        private LinkedList<ProgramIndexMapInfo> MapEntries { get; } = new LinkedList<ProgramIndexMapInfo>();

        /// <summary>
        /// Clears any existing 
        /// </summary>
        /// <param name="entries">The entries </param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
        public Result Reset(ReadOnlySpan<ProgramIndexMapInfo> entries)
        {
            lock (MapEntries)
            {
                ClearImpl();

                for (int i = 0; i < entries.Length; i++)
                {
                    MapEntries.AddLast(entries[i]);
                }

                return Result.Success;
            }
        }

        public void Clear()
        {
            lock (MapEntries)
            {
                ClearImpl();
            }
        }

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
