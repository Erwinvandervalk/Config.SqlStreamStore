using Microsoft.Extensions.Configuration;

namespace Config.SqlStreamStore
{
    public static class ConfigurationBuilerExtensions
    {
        public static IConfigurationBuilder AddStreamStore(this IConfigurationBuilder builder,
            BuildStreamStoreFromConfig factory,
            bool subscribeToChanges = false,
            ErrorHandler errorHandler = null)
        {
            builder.Add(new StreamStoreConfigurationSource(factory)
            {
                SubscribeToChanges = subscribeToChanges,
                ErrorHandler = errorHandler
            });
            return builder;
        }

        public static IConfigurationBuilder AddStreamStore(this IConfigurationBuilder builder,
            string connectionStringKey,
            BuildStreamStoreFromConnectionString factory, 
            bool subscribeToChanges = false,
            ErrorHandler errorHandler = null)
        {
            builder.Add(new StreamStoreConfigurationSource(connectionStringKey, factory)
            {
                SubscribeToChanges = subscribeToChanges,
                ErrorHandler = errorHandler
            });
            return builder;
        }
    }
}