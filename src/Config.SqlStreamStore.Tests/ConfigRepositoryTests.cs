using System;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore;
using Xunit;

namespace Config.SqlStreamStore.Tests
{

    // Optimistic concurrency checks
    // Can roll back to a version
    // wait until sql is available
    // configure stream name

    public class ConfigRepositoryTests
    {
        private StreamStoreConfigRepository _streamStoreConfigRepository;

        public ConfigRepositoryTests()
        {
            _streamStoreConfigRepository = new StreamStoreConfigRepository(new InMemoryStreamStore());
        }
        
        [Fact]
        public async Task Can_save_new_settings()
        {
            var settings = new ModifiedConfigurationSettings(
                ("setting1", "value1"),
                ("setting2", "setting2"));

            var result = await _streamStoreConfigRepository.WriteChanges(settings, CancellationToken.None);
            Assert.Equal(0, result.Version);

            var saved = await _streamStoreConfigRepository.GetLatest(CancellationToken.None);

            Assert.Equal(settings, saved);
        }

        [Fact]
        public async Task Can_modify_existing_settings()
        {
            var settings = await SaveSettings(BuildNewSettings());

            var modified = settings.WithModifiedSettings(("setting1", "newValue"));

            Assert.NotEqual(settings, modified);

            var saved = await SaveSettings(modified);

            Assert.Equal(modified, saved);
            Assert.Equal("newValue", saved["setting1"]);
        }

        [Fact]
        public async Task Can_delete_existing_settings()
        {
            var settings = await SaveSettings(BuildNewSettings());

            var modified = settings.WithDeletedKeys("setting1");

            Assert.NotEqual(settings, modified);

            var saved = await SaveSettings(modified);

            Assert.Equal(modified, saved);
            Assert.False(saved.ContainsKey("setting1"));
        }

        private async Task<ConfigurationSettings> SaveSettings(ModifiedConfigurationSettings settings)
        {
            await _streamStoreConfigRepository.WriteChanges(settings, CancellationToken.None);

            return await _streamStoreConfigRepository.GetLatest(CancellationToken.None);
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

            var subscription = _streamStoreConfigRepository.SubscribeToChanges(settings.Version, OnSettingsChanged,
                ct: CancellationToken.None);

            var modified = await _streamStoreConfigRepository.WriteChanges(settings.WithModifiedSettings(("setting1", "newValue")), CancellationToken.None);

            var noftifiedSettings = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(1)));

            Assert.True(tcs.Task.IsCompleted);
            Assert.Equal(modified, tcs.Task.Result);
        }

        [Fact]
        public async Task Can_modify_concurrently()
        {
            var delayWriting = new TaskCompletionSource<bool>();
            int count = 0;
            var waitUntilBothStarted = new TaskCompletionSource<bool>();
            bool errorHandlerInvoked = false;
            Task<IConfigurationSettings> StartModification(string value)
            {
                return _streamStoreConfigRepository.Modify(
                    changeSettings: async (currentSettings, ct) =>
                    {
                        if (++count == 2)
                        {
                            waitUntilBothStarted.SetResult(true);
                        }
                        await delayWriting.Task;
                        return currentSettings.WithModifiedSettings(("setting1", value));
                    },
                    errorHandler: (e, i) =>
                    {
                        errorHandlerInvoked = true;
                        return Task.FromResult(true);
                    },
                    ct: CancellationToken.None);
            }

            // save initial set of data
            var saved = await SaveSettings(BuildNewSettings());
            Assert.Equal(0, saved.Version);

            var t1 = Task.Run(() => StartModification("modified by t1"));
            var t2 = Task.Run(() => StartModification("modified by t2"));

            await waitUntilBothStarted.Task;
            delayWriting.SetResult(true);

            await Task.WhenAll(t1, t2);

            saved = await _streamStoreConfigRepository.GetLatest(CancellationToken.None);

            Assert.Equal(2, saved.Version);
            Assert.True(errorHandlerInvoked);
        }

        private static ModifiedConfigurationSettings BuildNewSettings()
        {
            var settings = new ModifiedConfigurationSettings(
                ("setting1", "value1"),
                ("setting2", "setting2"));
            return settings;
        }
    }
}
