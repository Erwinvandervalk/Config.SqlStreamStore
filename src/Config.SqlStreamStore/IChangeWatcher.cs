using System;
using System.Threading;
using Config.SqlStreamStore.Delegates;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Interface for montoring changes in the configuration
    /// </summary>
    public interface IChangeWatcher
    {
        IDisposable WatchForChanges(int version,
            OnSettingsChanged onSettingsChanged,
            CancellationToken ct);
    }
}