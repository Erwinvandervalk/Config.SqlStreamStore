using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlStreamStore;
using SqlStreamStore.Streams;
using SqlStreamStore.Subscriptions;

namespace Config.SqlStreamStore
{
    public interface IStreamStoreConfigRepository
    {
        Task<ConfigurationSettings> GetLatest(CancellationToken ct);
        Task<IConfigurationSettings> WriteChanges(ModifiedConfigurationSettings settings, CancellationToken ct);

        IDisposable SubscribeToChanges(int version, 
            StreamStoreConfigRepository.OnSettingsChanged onSettingsChanged,
            CancellationToken ct);
    }

    public class StreamStoreConfigRepository : IStreamStoreConfigRepository
    {
        public delegate Task OnSettingsChanged(ConfigurationSettings settings, CancellationToken ct);

        private readonly IStreamStore _streamStore;
        private readonly StreamId _streamId;

        public StreamStoreConfigRepository(IStreamStore streamStore, string streamId = Constants.DefaultStreamName)
        {
            _streamStore = streamStore;
            _streamId = streamId;
        }

        public async Task<ConfigurationSettings> GetLatest(CancellationToken ct)
        {
            var lastPage = await _streamStore.ReadStreamBackwards(
                streamId: new StreamId(_streamId), 
                fromVersionInclusive: StreamVersion.End, 
                maxCount: 1, 
                cancellationToken:ct);

            if (lastPage.Status == PageReadStatus.StreamNotFound)
            {
                return ConfigurationSettings.Empty();
            }

            var lastMessage = lastPage.Messages.First();
            return await BuildConfigurationSettingsFromMessage(lastMessage, ct);
        }

        private static async Task<ConfigurationSettings> BuildConfigurationSettingsFromMessage(
            StreamMessage message, CancellationToken ct)
        {
            var data = await message.GetJsonData(ct);

            var configChanged = JsonConvert.DeserializeObject<ConfigChanged>(data);

            return new ConfigurationSettings(message.StreamVersion, message.CreatedUtc, configChanged.AllSettings);
        }

        public async Task<IConfigurationSettings> Modify(CancellationToken ct,
            params (string Key, string Value)[] modifications)
        {
            var currentData = await GetLatest(ct);
            var modified = currentData.WithModifiedSettings(modifications);
            return await WriteChanges(modified, ct);
        }

        public async Task<IConfigurationSettings> Modify(
            Func<IConfigurationSettings, CancellationToken, Task<ModifiedConfigurationSettings>> changeSettings, 
            ErrorHandler errorHandler,
            CancellationToken ct)
        {
            int retryCount = 0;
            while (!ct.IsCancellationRequested)
            {
                var latest = await GetLatest(ct);
                var changed = await changeSettings(latest, ct);
                try
                {
                    var saved = await WriteChanges(changed, ct);
                    return saved;
                }
                catch (Exception e)
                {
                    if (!await errorHandler(e, retryCount++))
                        throw;
                }
            }

            throw new InvalidOperationException("Failed to write configuration settings");
        }


        public async Task<IConfigurationSettings> WriteChanges(ModifiedConfigurationSettings settings, CancellationToken ct)
        {
            var changes = settings.GetChanges();

            if (changes == null)
                // Nothing to save
                return settings;

            var result = await _streamStore.AppendToStream(
                streamId: _streamId, 
                expectedVersion: settings.Version, 
                message: new NewStreamMessage(Guid.NewGuid(), Constants.ConfigChangedMessageName, JsonConvert.SerializeObject(changes)), 
                cancellationToken:ct);

            return new ConfigurationSettings(result.CurrentVersion, null, changes.AllSettings);
        }

        public IDisposable SubscribeToChanges(int version, 
            OnSettingsChanged onSettingsChanged,
            CancellationToken ct)
        {
            IStreamSubscription subscription = null;


            async Task StreamMessageReceived(IStreamSubscription _, StreamMessage streamMessage,
                CancellationToken cancellationToken)
            {
                var settings = await BuildConfigurationSettingsFromMessage(streamMessage, ct);
                await onSettingsChanged(settings, ct);
            };

            void SubscriptionDropped(IStreamSubscription _, SubscriptionDroppedReason reason,
                Exception exception = null)
            {
                if (reason != SubscriptionDroppedReason.Disposed)
                {
                    SetupSubscription();
                }
            };

            void SetupSubscription()
            {
                subscription = _streamStore.SubscribeToStream(
                    streamId: _streamId,
                    continueAfterVersion: version,
                    streamMessageReceived: StreamMessageReceived,
                    subscriptionDropped: SubscriptionDropped);
            }

            SetupSubscription();

            return subscription;
        }

        private class DelegateDisposable : IDisposable
        {
            public readonly Action Action;
            public DelegateDisposable(Action action)
            {
                Action = action;
            }

            public void Dispose()
            {
                Action();
            }
        }
    }
}