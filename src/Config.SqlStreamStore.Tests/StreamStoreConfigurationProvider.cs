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

    // Ability to read connection string from config. 
    // Reload config when new data arrives. 

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

        [Fact]
        public async Task Can_build_config_source_with_connection_string()
        {
            const string key = "Config.SqlStreamStore.ConnectionString";

            string usedConnectionString = null;

            Func<string, IStreamStore> factory = (s) =>
            {
                usedConnectionString = s;
                return new InMemoryStreamStore();
            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>()
                {
                    { key , "sql server"}
                })
                .Add(new StreamStoreConfigurationSource(connectionStringKey: key, streamStoreFactory: factory))
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
        private readonly string _connectionStringKey;
        private readonly Func<IConfigRepository> _getConfigRepository;
        private Func<string, IStreamStore> _streamStoreFactory;

        public StreamStoreConfigurationSource(string connectionStringKey, Func<string, IStreamStore> streamStoreFactory)
        {
            _connectionStringKey = connectionStringKey;
            _streamStoreFactory = streamStoreFactory;
        }

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
            var getConfigRepository = _getConfigRepository;
            if (getConfigRepository == null)
            {
                getConfigRepository = () =>
                {
                    var connectionString = GetConnectionString(builder);
                    return new ConfigRepository(_streamStoreFactory(connectionString));
                };
            }

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
