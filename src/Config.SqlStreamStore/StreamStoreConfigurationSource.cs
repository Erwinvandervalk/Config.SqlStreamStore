using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SqlStreamStore;

namespace Config.SqlStreamStore
{
    public delegate IStreamStore BuildStreamStoreFromConnectionString(string connectionString);
    public delegate IStreamStore BuildStreamStoreFromConfig(IConfigurationRoot configurationRoot);
    public delegate IStreamStoreConfigRepository BuildConfigRepository();

    public delegate Task<bool> ErrorHandler(Exception ex, int retryCount);

    public class StreamStoreConfigurationSource : IConfigurationSource
    {

        private readonly BuildStreamStoreFromConfig _buildStreamStoreFromConfig;

        public string StreamId { get; set; } = Constants.DefaultStreamName;

        public bool SubscribeToChanges { get; set; }

        public ErrorHandler ErrorHandler { get; set; }

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
                streamId: StreamId);
            return repo;
        }
    }
}