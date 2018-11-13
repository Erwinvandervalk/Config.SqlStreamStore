using Config.SqlStreamStore.Delegates;
using Microsoft.Extensions.Configuration;

namespace Config.SqlStreamStore
{

    public static class ConfigurationBuilerExtensions
    {
        /// <summary>
        /// Adds SQL Stream store backed configuraiton provider
        /// </summary>
        /// <param name="builder">The configuration builer that SSS will be aded to </param>
        /// <param name="streamStoreFactory">Builds the stream store implementation</param>
        /// <param name="subscribeToChanges">Should we subscribe to changes?</param>
        /// <param name="errorHandler">What to do if database is not available. </param>
        /// <param name="messageHooks">Message hooks that help you to implement encryption</param>
        /// <returns></returns>
        public static IConfigurationBuilder AddStreamStore(this IConfigurationBuilder builder,
            BuildStreamStoreFromConfig streamStoreFactory,
            bool subscribeToChanges = false,
            ErrorHandler errorHandler = null, 
            IConfigurationSettingsHooks messageHooks = null)
        {
            builder.Add(new StreamStoreConfigurationSource(streamStoreFactory)
            {
                SubscribeToChanges = subscribeToChanges,
                ErrorHandler = errorHandler,
                MessageHooks = messageHooks
            });
            return builder;
        }

        /// <summary>
        /// Adds a stream store backed configuration provider. 
        /// </summary>
        /// <param name="builder">The configuration builer that SSS will be aded to </param>
        /// <param name="connectionStringKey">The key that holds the connection string</param>
        /// <param name="streamStoreFactory">Builds the stream store implementation</param>
        /// <param name="subscribeToChanges">Should we subscribe to changes?</param>
        /// <param name="errorHandler">What to do if database is not available. </param>
        /// <param name="messageHooks">Message hooks that help you to implement encryption</param>

        /// <returns></returns>
        public static IConfigurationBuilder AddStreamStore(this IConfigurationBuilder builder,
            string connectionStringKey,
            BuildStreamStoreFromConnectionString streamStoreFactory, 
            bool subscribeToChanges = false,
            ErrorHandler errorHandler = null,
            IConfigurationSettingsHooks messageHooks = null)
        { 
            builder.Add(new StreamStoreConfigurationSource(connectionStringKey, streamStoreFactory)
            {
                SubscribeToChanges = subscribeToChanges,
                ErrorHandler = errorHandler,
                MessageHooks = messageHooks
            });
            return builder;
        }
    }
}