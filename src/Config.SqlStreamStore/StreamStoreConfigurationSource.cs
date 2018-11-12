using System;
using Microsoft.Extensions.Configuration;
using SqlStreamStore;

namespace Config.SqlStreamStore
{
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
            this(() =>
            {
                var streamStore = getStreamStore();
                if (streamStore == null) return null;

                return new ConfigRepository(streamStore);
            })
        {
            
        }

        public StreamStoreConfigurationSource(Func<IConfigRepository> getConfigRepository)
        {
            _getConfigRepository = getConfigRepository;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            if (_getConfigRepository == null)
            {
                var innerBuilder = new ConfigurationBuilder();
                foreach (var source in builder.Sources)
                {
                    if (source != this)
                    {
                        innerBuilder.Add(source);
                    }
                }

                var connectionString = innerBuilder.Build()[_connectionStringKey];

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException($"Cannot create SqlStreamStore repository, becuase connection string (key: '{_connectionStringKey}') has not been configured.");
                }

                var repo = new ConfigRepository(_streamStoreFactory(connectionString));

                return new StreamStoreConfigurationProvider(() => repo);
            }

            return new StreamStoreConfigurationProvider(_getConfigRepository);
        }


    }
}