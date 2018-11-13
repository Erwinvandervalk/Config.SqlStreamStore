# ConfigurationProvider backed by Sql Stream Store  [![Build Status](https://travis-ci.org/Erwinvandervalk/Config.SqlStreamStore.svg?branch=master)](https://travis-ci.org/Erwinvandervalk/Config.SqlStreamStore)

This library allows you to store configuration settings (key-value pairs) in a SQl Stream Store stream. 

This allows the following scenario's:

1. All application instances in a webfarm can share the same configuration settings. 
1. The settings are audited, where the last x (defualt == 10) versions are stored. It's possible to revert back to a previous version of the settings. 
1. If multiple instances attempt to write the same setting, the writing is idempotent. 
1. The application can be notified of changes in the settings. 
1. The settings can be encrypted 'at rest'. (The actual encryption is up to the consumer)

This functionality is somewhat similar to using a key value store like Consul. If you are not yet ready to embrace consul, but do require a centralized key value store, then this might be the library for you, especially if you already use SQL Stream Store. 

## Getting started

### Dependencies

To get started, add a reference to Config.StreamStore and to your stream store implementation of choice, such as SqlStreamStore.MsSql. 

### Building

To build the solution, execute the build.cmd / build.sh

### Registering SQL Stream store
Then you register your configuration source like this:

``` c#

    /* 
    This example adds SQL Stream Store as the provider with the lowest priority. 
    This means you can override the values in SQL Stream Store with settings defined
    on the application server. (recommended)
    
    */

    // IN this example, the connection string is read from a configuration setting called 'ConnectionString'. This
    // has to be defined either as a value in the ini file, as a command line setting or as an environment variable. 

    var config = new ConfigurationBuilder()
                // Get the connection string from the config and build a new SSS implementation
                .AddStreamStore((c) => new MsSqlStreamStore(new MsSqlStreamStoreSettings(c["connectionString"])));

                .AddIniFile("Settings.ini") 
                .AddCommandLine(args)
                .AddEnvironmentVariables()

                .Build();

    config["a_setting_from_sss"];

```

This code assumes that the SQL Stream Store database and schema have been created. If not, it will fail with a SQL Exception

### Writing configuration changes

To write changes, you should create an instance of the **StreamStoreConfigRepository** class. This class allows you to modify the configuration:

Note, the actual writing of configuration settings is idempotent. If you try write the same configuration twice, it will not fail. 

``` c#

var repo = new StreamStoreConfigRepository(store);

// Get the latest version, modify it, then write it back again. 
var settings = repo.GetLatest(ct);

// The configuration settings object is immutable, but you can create a modified 
// version and write that back. 
var modified = settings.WithModifiedSettings(("setting1", "newValue"));
await _streamStoreConfigRepository.WriteChanges(modified, ct);

// Modify individual settings.
// Note, in the case of a concurrency error, this will throw a SSS Version Mismatch exception
await repo.Modify(ct, 
    ("setting1", "new value"),
    ("setting2", "newer value"));

// To handle concurrency errors, you can also use the following method. When a concurrency error occurs, 
// you can retry a number of times. 
    _streamStoreConfigRepository.Modify(

        // Delegate that allows you to modify the data
        changeSettings: async (currentSettings, ct) =>
        {
            return currentSettings.WithModifiedSettings(("setting1", value));
        },

        // Error handling logic, including retries. 
        errorHandler: (exception, retryCount) =>
        {
            // do some logging / determine if you want to retry. 
            return Task.FromResult(true); // returning true, which means retrying. 
        },
        ct: CancellationToken.None);

```

### Monitoring for changes in config

It's not difficult to monitor the configuration for changes. 

``` c#
    var sssConfig = new ConfigurationBuilder()

        // Add the stream store configuration data
        .AddStreamStore(
            (c) => new MsSqlStreamStore(new MsSqlStreamStoreSettings(c["connectionString"])),
            subscribeToChanges: true)
            .Build();

    // Then use the ChangeToken class to monitor for changes:
    ChangeToken.OnChange(sssConfig.GetReloadToken, () =>
    {
        Console.WriteLine("Settings changed:");
    });

```

Note, you can also use your own **IChangeMonitor** implementation.

### Capturing SSS instance or bring your own

The Microsoft.Extensions.Config classes don't implement IDisposable, so it's recommended to either inject your own version of sql stream store
or capture it while it's being built. 

``` c#
IStreamStore _captured;

var sssConfig = new ConfigurationBuilder()
    .AddStreamStore(
        (c) => _captured = new MsSqlStreamStore(new MsSqlStreamStoreSettings(c["connectionString"])))
        .Build();

// This allows you to dispose it when you need to. This also cancels any subscriptions:
    _captured.Dispose();

```

### Encrypting data at rest

Use the **IConfigurationSettingsHooks** interface to handle your own encryption. Don't roll your own, use a trusted encryption mechanism. Like (https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.aes?view=netframework-4.7.2)


### Handling database not yet available during startup. 

Sometimes the database is not available during startup. Ideally, I'd just like to terminate the process 'gracefully' and have some form of restart logic in the process orchestrator. However, if this is not possible, then you can also implement an error handler:

``` c# 

            var sssConfig = new ConfigurationBuilder()

                // Add the stream store configuration data
                .AddStreamStore(
                    // When an error occurs while connecting to the database
                    (c) => _captured = new MsSqlStreamStore(new MsSqlStreamStoreSettings(c["connectionString"])),
                    errorHandler: OnConnectError

        private static async Task<bool> OnConnectError(Exception ex, int retryCount)
        {
            // delay before retrying???
            await Task.Delay(2000);
            
            // Logging?

            // Retry? returning true like this retries indefinitely. 
            return true;
        }

```

### retrieving historic settings and reverting. 

``` c#

// Gets a list of all stored versions. 
var history = await _streamStoreConfigRepository.GetSettingsHistory(CancellationToken.None);

// Gets a specific version. 
var version1 = await _streamStoreConfigRepository.GetSpecificVersion(1, CancellationToken.None);

// Reverts config back to a previous version
await _streamStoreConfigRepository.RevertToVersion(version1, CancellationToken.None);

```

## Licencing

Licenced under [MIT](https://opensource.org/licenses/MIT).



