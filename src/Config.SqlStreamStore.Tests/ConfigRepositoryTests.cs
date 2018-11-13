using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SqlStreamStore;
using Xunit;

namespace Config.SqlStreamStore.Tests
{

    public class ConfigRepositoryTests
    {
        private StreamStoreConfigRepository _streamStoreConfigRepository;

        public ConfigRepositoryTests()
        {
            _streamStoreConfigRepository = new StreamStoreConfigRepository(new InMemoryStreamStore(),
                messageHooks: new Base64Hook());
        }
        
        [Fact]
        public async Task Can_save_new_settings()
        {
            var settings = new ModifiedConfigurationSettings(
                ("setting1", "value1"),
                ("setting2", "setting2"));

            var result = await _streamStoreConfigRepository.WriteChanges(settings, CancellationToken.None);
            Assert.Equal(0, result.Version);
            Assert.Equal(new []{"setting1", "setting2"}, result.ModifiedKeys);
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
            Assert.Equal(new[] { "setting1" }, saved.DeletedKeys);
            Assert.Equal(modified, saved);
            Assert.False(saved.ContainsKey("setting1"));
        }

        private async Task<IConfigurationSettings> SaveSettings(ModifiedConfigurationSettings settings)
        {
            await _streamStoreConfigRepository.WriteChanges(settings, CancellationToken.None);

            return await _streamStoreConfigRepository.GetLatest(CancellationToken.None);
        }

        [Fact]
        public async Task Can_subscribe_to_changes()
        {
            var settings = await SaveSettings(BuildNewSettings());

            var tcs = new TaskCompletionSource<IConfigurationSettings>();

            Task OnSettingsChanged(IConfigurationSettings configurationSettings, CancellationToken ct)
            {
                tcs.SetResult(configurationSettings);
                return Task.CompletedTask;
            }

            var subscription = _streamStoreConfigRepository.WatchForChanges(settings.Version, OnSettingsChanged,
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


        [Fact]
        public async Task Can_get_history_when_maxcount_is_set()
        {
            const int expectedMaxCount = 5;
            await _streamStoreConfigRepository.SetMaxCount(expectedMaxCount, CancellationToken.None);

            // write 10 modifications, 0 .. 10
            for (int i = 0; i < 10; i++)
            {
                await _streamStoreConfigRepository.Modify(CancellationToken.None,
                    ("setting", i.ToString()),
                    ("othersetting", "constant")
                );
            }

            var history = await _streamStoreConfigRepository.GetSettingsHistory(CancellationToken.None);

            Assert.Equal(expectedMaxCount, history.Count);
        }

        [Fact]
        public async Task Can_revert_to_previous_version()
        {
            // Write 5 modifications. 
            for (int i = 0; i < 5; i++)
            {
                await _streamStoreConfigRepository.Modify(CancellationToken.None,
                    ("setting", i.ToString()),
                    ("othersetting", "constant")
                );
            }

            var version1 = await _streamStoreConfigRepository.GetSpecificVersion(1, CancellationToken.None);

            await _streamStoreConfigRepository.RevertToVersion(version1, CancellationToken.None);

            var latest = await _streamStoreConfigRepository.GetLatest(CancellationToken.None);
            Assert.Equal(version1["setting"], latest["setting"]);
        }

        private static ModifiedConfigurationSettings BuildNewSettings()
        {
            var settings = new ModifiedConfigurationSettings(
                ("setting1", "value1"),
                ("setting2", "setting2"));
            return settings;
        }

        /// <summary>
        /// This hook converts the input / output to and from Base64, just to see if the
        /// hook actually works.
        ///
        /// Note, this is NOT encryption, nor should it ever be confused with encryption.
        /// If you consider using this as a form of encryption, you should reconsider your
        /// life choices that lead you up to this. 
        /// </summary>
        private class Base64Hook : IConfigurationSettingsHooks
        {
            public string OnReadMessage(string message)
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(message));
            }

            public string OnWriteMessage(string message)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
                
            }

            public JsonSerializerSettings JsonSerializerSettings => new JsonSerializerSettings();
        }
    }
}
