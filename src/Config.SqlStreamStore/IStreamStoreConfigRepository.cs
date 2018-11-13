using System;
using System.Threading;
using System.Threading.Tasks;

namespace Config.SqlStreamStore
{
    public interface IStreamStoreConfigRepository
    {
        Task<IConfigurationSettings> GetLatest(CancellationToken ct);
        Task<IConfigurationSettings> WriteChanges(ModifiedConfigurationSettings settings, CancellationToken ct);

        IDisposable SubscribeToChanges(int version, 
            StreamStoreConfigRepository.OnSettingsChanged onSettingsChanged,
            CancellationToken ct);

        Task<int?> GetMaxCount(CancellationToken ct);
        Task SetMaxCount(int maxCount, CancellationToken ct);
        Task<IConfigurationSettings> GetSpecificVersion(int version, CancellationToken ct);
        Task RevertToVersion(IConfigurationSettings settings, CancellationToken ct);
    }
}