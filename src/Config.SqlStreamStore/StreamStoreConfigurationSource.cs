using System;
using Config.SqlStreamStore.Delegates;
using Microsoft.Extensions.Configuration;
using SqlStreamStore;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Wires up a stream store confiuration setings provider. 
    /// </summary>
    public class StreamStoreConfigurationSource : IConfigurationSource
    {

        private readonly BuildStreamStoreFromConfig _buildStreamStoreFromConfig;

        /// <summary>
        /// The name of the stream to store settings in. 
        /// </summary>
        public string StreamId { get; set; } = Constants.DefaultStreamName;


        /// <summary>
        /// Should we subscribe to changes? Use ChangeToken.OnChange to monitor settings
        /// </summary>
        public bool SubscribeToChanges { get; set; }

        /// <summary>
        /// Error handler for when SSS is not available
        /// </summary>
        public ErrorHandler ErrorHandler { get; set; }

        /// <summary>
        /// Hook to implement encryption at rest
        /// </summary>
        public IConfigurationSettingsHooks MessageHooks { get; set; }

        /// <summary>
        /// Hook to implement custom stream watching. 
        /// </summary>
        public IChangeWatcher ChangeWatcher { get; set; }

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

        public StreamStoreConfigurationSource(Func<IStreamStore> getStreamStore)
        {
            _getConfigRepository = () => BuildRepository(getStreamStore());
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

            var streamStore = _buildStreamStoreFromConfig(innerBuilder.Build());
            var streamStoreConfigRepository = BuildRepository(streamStore);

            return new StreamStoreConfigurationProvider(this, streamStoreConfigRepository);
        }

        private StreamStoreConfigRepository BuildRepository(IStreamStore streamStore)
        {
            var repo = new StreamStoreConfigRepository(
                streamStore: streamStore,
                streamId: StreamId,
                messageHooks: MessageHooks,
                changeWatcher: ChangeWatcher);
            return repo;
        }
    }
}