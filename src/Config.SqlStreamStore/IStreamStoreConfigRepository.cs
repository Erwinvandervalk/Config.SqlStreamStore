using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Config.SqlStreamStore.Delegates;
using SqlStreamStore.Streams;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Responsible for reading / writing / modifying / watching configuration settings from a stream store implementation. 
    /// </summary>
    public interface IStreamStoreConfigRepository
    {
        /// <summary>
        /// Get's the latest version of the confiugraiton
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IConfigurationSettings> GetLatest(CancellationToken ct);


        /// <summary>
        /// WRites modifications to the latest version. 
        /// </summary>
        /// <param name="ct"></param>
        /// <param name="modifications"></param>
        /// <returns></returns>
        Task<IConfigurationSettings> Modify(CancellationToken ct,
            params (string Key, string Value)[] modifications);

        /// <summary>
        /// Get's the list of historic settings. 
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IReadOnlyList<IConfigurationSettings>> GetSettingsHistory(CancellationToken ct);

        /// <summary>
        /// Modifies the latest version of the confiugration but provides error handling
        /// </summary>
        /// <param name="changeSettings"></param>
        /// <param name="errorHandler"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IConfigurationSettings> Modify(
            Func<IConfigurationSettings, CancellationToken, Task<ModifiedConfigurationSettings>> changeSettings, 
            ErrorHandler errorHandler,
            CancellationToken ct);

        /// <summary>
        /// Get the maximum number of messages in the stream. 
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<int?> GetMaxCount(CancellationToken ct);

        /// <summary>
        /// Sets the maximum number of messages in the stream. 
        /// </summary>
        /// <param name="maxCount"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task SetMaxCount(int maxCount, CancellationToken ct);

        /// <summary>
        /// Writes confiugration changes. 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IConfigurationSettings> WriteChanges(ModifiedConfigurationSettings settings, CancellationToken ct);


        /// <summary>
        /// WAtches the SSS for modifications. 
        /// </summary>
        /// <param name="version"></param>
        /// <param name="onSettingsChanged"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        IDisposable WatchForChanges(int version, 
            OnSettingsChanged onSettingsChanged,
            CancellationToken ct);

        /// <summary>
        /// Retrieves a specific version of the config. 
        /// </summary>
        /// <param name="version"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IConfigurationSettings> GetSpecificVersion(int version, CancellationToken ct);

        /// <summary>
        /// Reverts the config to a specific version. Note, this will create a new version, not truncate
        /// the history.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task RevertToVersion(IConfigurationSettings settings, CancellationToken ct);
    }
}