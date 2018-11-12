using System;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore;
using Xunit;

namespace Config.SqlStreamStore.Tests
{

    // Optimistic concurrency checks
    // Can read settings from sql stream
    // Can write settings to a stream
    // Can roll back to a version
    // 

    public class ConfigRepositoryTests
    {
        private ConfigRepository _configRepository;

        public ConfigRepositoryTests()
        {
            _configRepository = new ConfigRepository(new InMemoryStreamStore());
        }
        
        [Fact]
        public async Task Can_save_new_settings()
        {
            var settings = new ModifiedConfigurationSettings(
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

            var modified = settings.Modify(("setting1", "newValue"));

            Assert.NotEqual(settings, modified);

            var saved = await SaveSettings(modified);

            Assert.Equal(modified, saved);
            Assert.Equal("newValue", saved["setting1"]);
        }

        [Fact]
        public async Task Can_delete_existing_settings()
        {
            var settings = await SaveSettings(BuildNewSettings());

            var modified = settings.Delete("setting1");

            Assert.NotEqual(settings, modified);

            var saved = await SaveSettings(modified);

            Assert.Equal(modified, saved);
            Assert.False(saved.ContainsKey("setting1"));
        }

        private async Task<ConfigurationSettings> SaveSettings(IConfigurationSettings settings)
        {
            await _configRepository.WriteChanges(settings, CancellationToken.None);

            return await _configRepository.GetLatest(CancellationToken.None);
        }

        [Fact]
        public async Task Can_subscribe_to_changes()
        {
            var settings = await SaveSettings(BuildNewSettings());

            var tcs = new TaskCompletionSource<ConfigurationSettings>();

            Task OnSettingsChanged(ConfigurationSettings configurationSettings, CancellationToken ct)
            {
                tcs.SetResult(configurationSettings);
                return Task.CompletedTask;
            }

            var subscription = _configRepository.SubscribeToChanges(settings.Version, OnSettingsChanged,
                ct: CancellationToken.None);

            var modified = await _configRepository.WriteChanges(settings.Modify(("setting1", "newValue")), CancellationToken.None);

            var noftifiedSettings = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));

            Assert.True(tcs.Task.IsCompleted);
            Assert.Equal(modified, tcs.Task.Result);
        }


        private static IConfigurationSettings BuildNewSettings()
        {
            var settings = new ModifiedConfigurationSettings(
                ("setting1", "value1"),
                ("setting2", "setting2"));
            return settings;
        }
    }
}
