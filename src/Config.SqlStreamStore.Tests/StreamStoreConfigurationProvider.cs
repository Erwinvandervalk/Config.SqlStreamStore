using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using SqlStreamStore;
using Xunit;

namespace Config.SqlStreamStore.Tests
{
    public class StreamStoreConfigurationProviderTests
    {
        protected internal InMemoryStreamStore SqlStreamStoreInstance { get; set; }


        [Fact]
        public async Task Can_build_config_source_with_instance_from_lambda()
        {
            SqlStreamStoreInstance = null;

            var config = new ConfigurationBuilder()
                .Add(new StreamStoreConfigurationSource(() => SqlStreamStoreInstance))
                .Build();



            SqlStreamStoreInstance = new InMemoryStreamStore();

            var repo = new ConfigRepository(SqlStreamStoreInstance);
            await repo.WriteChanges(new ModifiedConfigurationSettings(
                ("setting1", "value1")), CancellationToken.None);

            Assert.Equal("value1", config.GetValue<string>("setting1"));
        }
    }

    public class StreamStoreConfigurationSource : IConfigurationSource
    {
        private readonly Func<IConfigRepository> _getConfigRepository;

        public StreamStoreConfigurationSource(Func<IStreamStore> getStreamStore) :
            this(() => new ConfigRepository(getStreamStore()))
        {
            
        }

        public StreamStoreConfigurationSource(Func<IConfigRepository> getConfigRepository)
        {
            _getConfigRepository = getConfigRepository;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new StreamStoreConfigurationProvider(_getConfigRepository);
        }
    }

    public class StreamStoreConfigurationProvider : IConfigurationProvider
    {
        private readonly Func<IConfigRepository> _getConfigRepository;

        private IConfigurationSettings _settings;

        public StreamStoreConfigurationProvider(Func<IConfigRepository> getConfigRepository)
        {
            _getConfigRepository = getConfigRepository;
        }

        public bool TryGet(string key, out string value)
        {
            if (_settings == null)
            {
                _settings = _getConfigRepository().GetLatest(CancellationToken.None).GetAwaiter().GetResult();
            }

            return _settings.TryGetValue(key, out value);
        }

        public void Set(string key, string value)
        {
            // Not going to write to SSS this way..
        }

        public IChangeToken GetReloadToken()
        {
            return new ConfigurationReloadToken();
        }

        public void Load()
        {

            //_settings = _getConfigRepository()?.GetLatest(CancellationToken.None).GetAwaiter().GetResult();
        }

        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath)
        {
            throw new NotImplementedException();
        }
    }
}
