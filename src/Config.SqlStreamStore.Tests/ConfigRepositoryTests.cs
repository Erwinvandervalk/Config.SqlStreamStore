using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlStreamStore;
using SqlStreamStore.Infrastructure;
using SqlStreamStore.Streams;
using Xunit;

namespace Config.SqlStreamStore.Tests
{

    // Can read settings from sql stream
    // Can write settings to a stream
    // Can roll back to a version
    // 


    public class ConfigRepositoryTests
    {
        private ConfigRepository _configRepository;

        public ConfigRepositoryTests()
        {
            _configRepository = new ConfigRepository(new InMemoryStreamStore(), () => DateTime.Now);
        }
        
        [Fact]
        public async Task Can_save_new_settings()
        {
            var settings = new ConfigurationSettings(
                ("setting1", "value1"),
                ("setting2", "setting2"));

            var result = await _configRepository.WriteChanges(settings, CancellationToken.None);
            Assert.Equal(0, result.Version);

            var saved = await _configRepository.GetLatest(CancellationToken.None);

            Assert.Equal(settings, saved);
        }

        [Fact]
        public async Task Can_modify_existing_settings()
        {
            var settings = await SaveSettings(BuildNewSettings());

            settings.Modify(("setting1", "newValue"));

            var saved = await SaveSettings()
            
        }

        private async Task<ConfigurationSettings> SaveSettings(ConfigurationSettings settings)
        {
            await _configRepository.WriteChanges(settings, CancellationToken.None);

            return await _configRepository.GetLatest(CancellationToken.None);
        }

        private static ConfigurationSettings BuildNewSettings()
        {
            var settings = new ConfigurationSettings(
                ("setting1", "value1"),
                ("setting2", "setting2"));
            return settings;
        }
    }

    public static class Constants
    {
        public const string DefaultStreamName = "Config.SqlStreamStore";
        public const string ConfigChangedMessageName = "Config.SqlStreamStore.ConfigChanged";
    }

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

        public async Task<ConfigurationSettings> WriteChanges(ConfigurationSettings settings, CancellationToken ct)
        {
            var changes = settings.GetChanges();

            if (changes == null)
                // Nothing to save
                return settings;

            var result = await _streamStore.AppendToStream(
                streamId: _streamId, 
                expectedVersion: settings.Version, 
                message: new NewStreamMessage(Guid.NewGuid(), Constants.ConfigChangedMessageName, JsonConvert.SerializeObject(changes)), 
                cancellationToken: ct);

            return new ConfigurationSettings(result.CurrentVersion, _getUtcNow(), changes.AllSettings);
        }
        
    }

    

    public class ConfigurationSettings : IReadOnlyDictionary<string, string>
    {
        public ConfigurationSettings(params (string Key, string Value)[] settings) : this()
        {
            Modify(settings);
        }

        public ConfigurationSettings(int version = -1, DateTime? lastModified = null, IReadOnlyDictionary<string, string> settings = null)
        {
            Version = version;
            LastModified = lastModified;
            _settings = settings == null ?
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : 
                    new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase)
;
        }

        public int Version { get; }

        public DateTime? LastModified { get; }

        private HashSet<string> _modifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _deletions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, string> _settings;
        public void Modify(string key, string value)
        {
            if (!_settings.ContainsKey(key) || _settings[key] != value)
            {
                _modifications.Add(key);
                _settings[key] = value;
                _deletions.Remove(key);
            }
        }

        public void Modify(params (string key, string value)[] modifiedSettings)
        {
            foreach (var kvp in modifiedSettings)
            {
                Modify(kvp.key, kvp.value);
            }
        }

        public void Remove(params string[] keys)
        {
            if (keys == null)
                return;

            foreach (var key in keys)
            {
                // Record the deletion
                _deletions.Add(key);

                // Remove it from the settings
                _settings.Remove(key);

                // remove it from modifications (only happens if not saved yet)
                _modifications.Remove(key);
            }
        }

        /// <summary>
        /// Override all settings with the current list of settings
        /// </summary>
        /// <param name="newSettings"></param>
        public void Set(IReadOnlyDictionary<string, string> newSettings)
        {
            foreach (var removedSetting in _settings.Where(x => !newSettings.ContainsKey(x.Key)))
            {
                Remove(removedSetting.Key);
            }

            foreach (var potentiallyModifiedSetting in newSettings)
            {
                Modify(potentiallyModifiedSetting.Key, potentiallyModifiedSetting.Value);
            }
            
        }

        internal ConfigChanged GetChanges()
        {
            if (_deletions.Any() || _modifications.Any())
            {
                return new ConfigChanged(
                    allSettings: new Dictionary<string, string>(_settings), 
                    modifiedSettings: new HashSet<string>(_modifications), 
                    deletedSettings: new HashSet<string>(_deletions));
            }

            return null;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return _settings.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _settings).GetEnumerator();
        }

        public int Count => _settings.Count;

        public bool ContainsKey(string key)
        {
            return _settings.ContainsKey(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return _settings.TryGetValue(key, out value);
        }

        public string this[string key] => _settings[key];

        public IEnumerable<string> Keys => _settings.Keys;

        public IEnumerable<string> Values => _settings.Values;
    }

    public class ConfigChanged
    {
        public ConfigChanged()
        {
            
        }

        public ConfigChanged(Dictionary<string, string> allSettings, HashSet<string> modifiedSettings, HashSet<string> deletedSettings)
        {
            AllSettings = allSettings;
            ModifiedSettings = modifiedSettings;
            DeletedSettings = deletedSettings;
        }

        public Dictionary<string, string> AllSettings { get; set; }
        public HashSet<string> ModifiedSettings { get; set; }
        public HashSet<string> DeletedSettings { get; set; }
    }

}
