using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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


            SqlStreamStoreInstance = await BuildSteamStoreWithSettings(("setting1", "value1"));

            Assert.Equal("value1", config.GetValue<string>("setting1"));
        }

        [Fact]
        public async Task Can_build_config_source_with_connection_string()
        {
            const string connectionStringKey = "Config.SqlStreamStore.ConnectionString";
            const string expectedConnectionString = "not really a conection string, but as good as it gets";

            string usedConnectionString = null;

            var instance = await BuildSteamStoreWithSettings(("setting1", "value1"));

            IStreamStore BuildStreamStore(string s)
            {
                usedConnectionString = s;
                return instance;
            }


            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { connectionStringKey , expectedConnectionString}
                })
                .Add(new StreamStoreConfigurationSource(connectionStringKey: connectionStringKey, streamStoreFactory: BuildStreamStore))
                .Build();

            // Ensure the factory actually used the configured connection string key
            Assert.Equal(expectedConnectionString, usedConnectionString);

            // Ensure setting 1was read from stream store
            Assert.Equal("value1", config.GetValue<string>("setting1"));
        }

        private static async Task<InMemoryStreamStore> BuildSteamStoreWithSettings(params (string key, string value)[] settings)
        {
            var instance = new InMemoryStreamStore();

            var repo = new ConfigRepository(instance);
            await repo.WriteChanges(new ModifiedConfigurationSettings(
                settings), CancellationToken.None);

            return instance;
        }
    }
}
