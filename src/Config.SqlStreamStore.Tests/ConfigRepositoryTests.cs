using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlStreamStore;
using Xunit;

namespace Config.SqlStreamStore.Tests
{

    // Max number of versions
    // Can roll back to a version
    // List versions
    // Hooks for encryption / decryption
    // 

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
                        if (Interlocked.Increment(ref count) == 2)
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

        [Fact]
        public async Task Can_get_history()
        {
            // write 100 modifications, 0 .. 99
            for (int i = 0; i < 100; i++)
            {
                await _streamStoreConfigRepository.Modify(CancellationToken.None, 
                    ("setting", i.ToString()),
                    ("othersetting", "constant")
                    );
            }

            var history = await _streamStoreConfigRepository.GetSettingsHistory(CancellationToken.None);

            Assert.Equal(Constants.DefaultMaxCount, history.Count);

            // Check first and last entry
            Assert.Equal("90", history.First()["setting"]); 
            Assert.Equal("constant", history.First()["othersetting"]); 
            Assert.Equal(90, history.First().Version);
            Assert.Equal("99", history.Last()["setting"]);
            Assert.Equal(99, history.Last().Version);
            Assert.Equal("constant", history.Last()["othersetting"]);
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
