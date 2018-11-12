using System;
using Microsoft.Extensions.Configuration;
using SqlStreamStore;

namespace Config.SqlStreamStore
{
    public static class ConfigurationBuilerExtensions
    {
        public static IConfigurationBuilder AddStreamStore(this IConfigurationBuilder builder,
            BuildStreamStoreFromConfig factory,
            bool subscribeToChanges = false)
        {
            builder.Add(new StreamStoreConfigurationSource(factory)
            {
                SubscribeToChanges = subscribeToChanges
            });
            return builder;
        }

        public static IConfigurationBuilder AddStreamStore(this IConfigurationBuilder builder,
            string connectionStringKey,
            BuildStreamStoreFromConnectionString factory, 
            bool subscribeToChanges = false)
        {
            builder.Add(new StreamStoreConfigurationSource(connectionStringKey, factory)
            {
                SubscribeToChanges = subscribeToChanges
            });
            return builder;
        }
    }

    public delegate IStreamStore BuildStreamStoreFromConnectionString(string connectionString);
    public delegate IStreamStore BuildStreamStoreFromConfig(IConfigurationRoot configurationRoot);
    public delegate IConfigRepository BuildConfigRepository();

    public class StreamStoreConfigurationSource : IConfigurationSource
    {

        private readonly BuildStreamStoreFromConfig _buildStreamStoreFromConfig;

        public bool SubscribeToChanges { get; set; }

        private readonly BuildConfigRepository _getConfigRepository;

        public StreamStoreConfigurationSource(BuildStreamStoreFromConfig buildStreamStoreFromConfig)
        {
            _buildStreamStoreFromConfig = buildStreamStoreFromConfig;
        }

        public StreamStoreConfigurationSource(string connectionStringKey, BuildStreamStoreFromConnectionString streamStoreFactory)
        {
            _buildStreamStoreFromConfig = (c =>
            {
                var connectionString = c[connectionStringKey];

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException(
                        $"Cannot create SqlStreamStore repository, becuase connection string (key: '{connectionStringKey}') has not been configured.");
                }

                return streamStoreFactory(connectionString);
            });
        }

        public StreamStoreConfigurationSource(Func<IStreamStore> getStreamStore) :
            this(() => new ConfigRepository(getStreamStore()))
        {
            
        }

        public StreamStoreConfigurationSource(BuildConfigRepository getConfigRepository)
        {
            _getConfigRepository = getConfigRepository;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            if (_getConfigRepository != null)
            {
                return new StreamStoreConfigurationProvider(this, _getConfigRepository());
            }

            var innerBuilder = new ConfigurationBuilder();
            foreach (var source in builder.Sources)
            {
                if (source != this)
                {
                    innerBuilder.Add(source);
                }
            }

            var repo = new ConfigRepository(_buildStreamStoreFromConfig(innerBuilder.Build()));

            return new StreamStoreConfigurationProvider(this, repo);
        
        }

    }
}