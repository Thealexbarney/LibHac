using System;

namespace LibHac.FsSrv.Sf;

/// <summary>
/// Bulk moves save data from one save data space to another.
/// </summary>
/// <remarks><para>To use this class, call <see cref="Register"/> for each save data to be moved. After all save data
/// have been registered, repeatedly call <see cref="Process"/> until it returns 0 for the remaining size.</para>
/// <para>Based on nnSdk 18.3.0 (FS 18.0.0)</para></remarks>
public interface ISaveDataMover : IDisposable
{
    Result Register(ulong saveDataId);
    Result Process(out long outRemainingSize, long sizeToProcess);
    Result Cancel();
}