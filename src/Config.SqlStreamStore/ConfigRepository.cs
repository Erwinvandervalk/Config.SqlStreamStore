using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlStreamStore;
using SqlStreamStore.Infrastructure;
using SqlStreamStore.Streams;

namespace Config.SqlStreamStore
{
    public class ConfigRepository
    {
        private readonly IStreamStore _streamStore;
        private readonly GetUtcNow _getUtcNow;
        private readonly StreamId _streamId;

        public ConfigRepository(IStreamStore streamStore, GetUtcNow getUtcNow, string streamId = Constants.DefaultStreamName)
        {
            _streamStore = streamStore;
            _getUtcNow = getUtcNow;
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
                return new ConfigurationSettings();
            }

            var lastMessage = lastPage.Messages.First();
            var data = await lastMessage.GetJsonData(ct);

            var configChanged = JsonConvert.DeserializeObject<ConfigChanged>(data);

            return new ConfigurationSettings(lastMessage.StreamVersion, lastMessage.CreatedUtc, configChanged.AllSettings);
        }

        public async Task<IConfigurationSettings> WriteChanges(IConfigurationSettings settings, CancellationToken ct)
        {
            var changes = (settings as ModifiedConfigurationSettings)?.GetChanges();

            if (changes == null)
                // Nothing to save
                return settings;

            var result = await _streamStore.AppendToStream(
                streamId: _streamId, 
                expectedVersion: settings.Version, 
                message: new NewStreamMessage(Guid.NewGuid(), Constants.ConfigChangedMessageName, JsonConvert.SerializeObject(changes)), 
                cancellationToken:ct);

            return new ConfigurationSettings(result.CurrentVersion, _getUtcNow(), changes.AllSettings);
        }
        
    }
}