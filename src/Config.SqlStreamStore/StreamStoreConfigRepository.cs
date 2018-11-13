using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Config.SqlStreamStore.Delegates;
using Newtonsoft.Json;
using SqlStreamStore;
using SqlStreamStore.Streams;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Class responsible for reading / writing / modifying / watching configuration settings from a stream store implementation. 
    /// </summary>
    public class StreamStoreConfigRepository : IStreamStoreConfigRepository
    {
        private readonly IStreamStore _streamStore;
        private readonly StreamId _streamId;
        private readonly IChangeWatcher _changeWatcher;
        private readonly IConfigurationSettingsHooks _messageHooks;

        public StreamStoreConfigRepository(
            IStreamStore streamStore, 
            string streamId = Constants.DefaultStreamName,
            IConfigurationSettingsHooks messageHooks = null,
            IChangeWatcher changeWatcher = null)
        {
            _streamStore = streamStore;
            _streamId = streamId;
            _changeWatcher = changeWatcher;
            _messageHooks = messageHooks ?? new NoOpHooks();
            _changeWatcher = changeWatcher ?? new SubscriptionBasedChangeWatcher(_streamStore, _streamId, _messageHooks);
        }

        /// <inheritdoc />
        public async Task<IConfigurationSettings> GetLatest(CancellationToken ct)
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
            return await BuildConfigurationSettingsFromMessage(lastMessage, _messageHooks, ct);
        }

        /// <summary>
        /// Builds the configuration settings from the stream message. 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageHooks"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task<IConfigurationSettings> BuildConfigurationSettingsFromMessage(
            StreamMessage message, IConfigurationSettingsHooks messageHooks, CancellationToken ct)
        {
            var data = messageHooks.OnReadMessage(await message.GetJsonData(ct));

            var configChanged = JsonConvert.DeserializeObject<ConfigChanged>(data, messageHooks.JsonSerializerSettings);

            return new ConfigurationSettings(
                message.StreamVersion, 
                message.CreatedUtc, 
                configChanged.AllSettings, 
                configChanged.ModifiedSettings, 
                configChanged.DeletedSettings);
        }

        /// <inheritdoc />
        public async Task<IConfigurationSettings> Modify(CancellationToken ct,
            params (string Key, string Value)[] modifications)
        {
            var currentData = await GetLatest(ct);
            var modified = currentData.WithModifiedSettings(modifications);
            return await WriteChanges(modified, ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<IConfigurationSettings>> GetSettingsHistory(CancellationToken ct)
        {
            var maxCount = await GetMaxCount(ct) ?? Constants.DefaultMaxCount;
            var stream = await _streamStore.ReadStreamBackwards(_streamId, StreamVersion.End, maxCount, true, ct);

            var result = new List<IConfigurationSettings> ();
            foreach (var message in stream.Messages.OrderBy(x => x.StreamVersion))
            {
                var setting = await BuildConfigurationSettingsFromMessage(message, _messageHooks, ct);
                result.Add(setting);
            }

            return result;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<int?> GetMaxCount(CancellationToken ct)
        {
            var metaData = await _streamStore.GetStreamMetadata(_streamId, ct);
            return metaData?.MaxCount;
        }

        /// <inheritdoc />
        public async Task SetMaxCount(int maxCount, CancellationToken ct)
        {
            await _streamStore.SetStreamMetadata(_streamId, maxCount: maxCount, cancellationToken: ct);
        }

        /// <inheritdoc />
        public async Task<IConfigurationSettings> WriteChanges(ModifiedConfigurationSettings settings, CancellationToken ct)
        {
            if (await GetMaxCount(ct) == null)
            {
                // Ensure default value is set
                await SetMaxCount(Constants.DefaultMaxCount, CancellationToken.None);
            }

            var changes = settings.GetChanges();

            if (changes == null)
                // Nothing to save
                return settings;

            var serializeObject = _messageHooks.OnWriteMessage(JsonConvert.SerializeObject(changes));

            var result = await _streamStore.AppendToStream(
                streamId: _streamId, 
                expectedVersion: settings.Version, 
                message: new NewStreamMessage(Guid.NewGuid(), Constants.ConfigChangedMessageName, serializeObject), 
                cancellationToken:ct);

            return new ConfigurationSettings(result.CurrentVersion, null, changes.AllSettings, changes.ModifiedSettings, changes.DeletedSettings);
        }

        /// <inheritdoc />
        public IDisposable WatchForChanges(int version, 
            OnSettingsChanged onSettingsChanged,
            CancellationToken ct)
        {
            return _changeWatcher.WatchForChanges(version, onSettingsChanged, ct);
        }

        /// <inheritdoc />
        public async Task<IConfigurationSettings> GetSpecificVersion(int version, CancellationToken ct)
        {
            var streamPage = await _streamStore.ReadStreamBackwards(_streamId, version, 1, ct);

            if (!streamPage.Messages.Any())
                return null;

            var msg = streamPage.Messages.First();
            
            return await BuildConfigurationSettingsFromMessage(msg, _messageHooks, ct);
        }

        /// <inheritdoc />
        public async Task RevertToVersion(IConfigurationSettings settings, CancellationToken ct)
        {
            var latest = await GetLatest(ct);

            var modified = latest.WithAllSettingsReplaced(settings);

            await WriteChanges(modified, ct);
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

        private class NoOpHooks : IConfigurationSettingsHooks
        {
            public string OnReadMessage(string message)
            {
                return message;
            }

            public string OnWriteMessage(string message)
            {
                return message;
            }

            public JsonSerializerSettings JsonSerializerSettings => new JsonSerializerSettings();
        }
    }
}