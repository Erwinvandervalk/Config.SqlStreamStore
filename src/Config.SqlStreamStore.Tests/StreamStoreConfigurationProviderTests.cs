﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SqlStreamStore;
using Xunit;

namespace Config.SqlStreamStore.Tests
{

    public class StreamStoreConfigurationProviderTests
    {
        [Fact]
        public async Task Can_build_config_source_with_instance_from_lambda()
        {
            var instance = await BuildSteamStoreWithSettings(("setting1", "value1"));

            var config = new ConfigurationBuilder()
                .Add(new StreamStoreConfigurationSource(() => instance))
                .Build();

            Assert.Equal("value1", config.GetValue<string>("setting1"));
        }


        [Fact]
        public async Task Can_build_config_source_from_inner_config()
        {
            const string connectionStringKey = "Config.SqlStreamStore.ConnectionString";
            const string expectedConnectionString = "not really a conection string, but as good as it gets";

            var instance = await BuildSteamStoreWithSettings(("setting1", "value1"));

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { connectionStringKey , expectedConnectionString}
                })
                .Add(new StreamStoreConfigurationSource(
                    (innerConfig) =>
                    {
                        Assert.Equal(expectedConnectionString, innerConfig[connectionStringKey]);
                        return instance;
                    }))
                .Build();


            // Ensure setting 1was read from stream store
            Assert.Equal("value1", config.GetValue<string>("setting1"));
        }

        [Fact]
        public async Task Can_build_config_source_with_connection_string()
        {
            const string connectionStringKey = "Config.SqlStreamStore.ConnectionString";
            const string expectedConnectionString = "not really a conection string, but as good as it gets";

            // Create a SSS instance with some settings
            var instance = await BuildSteamStoreWithSettings(("setting1", "value1"));

            
            // Set up the factory to capture the used connection string when used
            string capturedConnectionString = null;
            IStreamStore FakeStreamStoreFactory(string providedConnectionString)
            {
                capturedConnectionString = providedConnectionString;
                return instance;
            }

            // Build the config with deafult values
            var config = new ConfigurationBuilder()
                // Setup default values
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { connectionStringKey , expectedConnectionString}
                })

                // Setup Stream store implementation
                .Add(new StreamStoreConfigurationSource(
                    connectionStringKey: connectionStringKey, 
                    streamStoreFactory: FakeStreamStoreFactory))
                .Build();

            // Ensure the factory actually used the configured connection string key
            Assert.Equal(expectedConnectionString, capturedConnectionString);

            // Ensure setting 1was read from stream store
            Assert.Equal("value1", config.GetValue<string>("setting1"));
        }

        [Fact]
        public async Task Can_reload_when_data_chagnes()
        {
            var instance = await BuildSteamStoreWithSettings(("setting1", "value1"));

            var config = new ConfigurationBuilder()
                .Add(new StreamStoreConfigurationSource(() => instance))
                .Build();



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
