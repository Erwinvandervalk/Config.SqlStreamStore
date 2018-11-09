using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore;
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

        private static IConfigurationSettings BuildNewSettings()
        {
            var settings = new ModifiedConfigurationSettings(
                ("setting1", "value1"),
                ("setting2", "setting2"));
            return settings;
        }
    }
}
