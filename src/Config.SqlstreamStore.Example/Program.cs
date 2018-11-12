using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Config.SqlStreamStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Ini;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SqlStreamStore;

namespace Config.SqlstreamStore.Example
{
    class Program
    {
        private static ILogger _logger;
        static void Main(string[] args)
        {

            var cts = new CancellationTokenSource();

            // We're first building configuraiton from the ini file
            // This contains the connection string
            var ini = new ConfigurationBuilder()
                .AddIniFile("Settings.ini")
                .Build();

            // On a background thread, create the database and start initializing 
            Task.Run(async () => await CreateDatabase(ini["ConnectionString"], cts.Token), cts.Token);

            IStreamStore streamStore = null;

            var sssConfig = new ConfigurationBuilder()

                // Add the stream store configuration data
                .AddStreamStore(
                    subscribeToChanges: true,

                    // When an error occurs while connecting to the database
                    errorHandler: OnConnectError,

                    factory: config => 
                        // Create sql stream store instance. Uses connection string from ini file. 
                        // Note, we're assigning an instance variable here, so we can dispose the instance later
                        streamStore = new MsSqlStreamStore(new MsSqlStreamStoreSettings(config["connectionString"]))
                    )

                // Also add the INI configuration data. This is where the connection string is read from
                .AddConfiguration(ini)
                .Build();

            // Listen for configuration changes
            ChangeToken.OnChange(sssConfig.GetReloadToken, () =>
            {
                Console.WriteLine("Settings changed:");
                PrintSettings(sssConfig);
            });

            // Print out the configuration settings present at startup
            Console.WriteLine("Found settings at startup:");
            PrintSettings(sssConfig);


            // Keep going until enter is pressed. 
            Console.ReadLine();

            // Stop the background thread and dispose SSS
            cts.Cancel();
            streamStore.Dispose();
        }

        private static async Task<bool> OnConnectError(Exception ex, int retryCount)
        {
            await Task.Delay(2000);
            Console.WriteLine($"Attempt {retryCount}, Sql Server is not yet available.. Retrying...");
            return true;
        }

        private static void PrintSettings(IConfigurationRoot sssConfig)
        {
            foreach (var setting in sssConfig.GetChildren())
            {
                Console.WriteLine(setting.Key + "=" + setting.Value);
            }
            Console.WriteLine();
        }

        private static async Task CreateDatabase(string connectionString, CancellationToken ct)
        {
            while(true)
                try
                {
                    await Task.Delay(1000);
                    Console.WriteLine("Creating database schema");
                    await Task.Delay(3000);

                    var mgr = new DatabaseManager(connectionString);
                    mgr.EnsureDatabaseExists(10, 10);

                    await Task.Delay(1000, ct);

                    var msSqlStreamStoreSettings = new MsSqlStreamStoreSettings(connectionString);

                    var store = new MsSqlStreamStore(msSqlStreamStoreSettings);
                    if (!(await store.CheckSchema(ct)).IsMatch())
                    {
                        await store.CreateSchema(ct);
                    }

                    var repo = new StreamStoreConfigRepository(store);

                    while (!ct.IsCancellationRequested)
                    {
                        await Task.Delay(1000, ct);
                        await repo.Modify(ct, ("setting1", DateTime.Now.ToLongTimeString()));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error while creating database: " + ex.Message);
                }
        }
    }

    public class DatabaseManager
    {

        public DatabaseManager(string connectionString)
        {
            _connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
        }

        private readonly SqlConnectionStringBuilder _connectionStringBuilder;

        private string DatabaseName
        {
            get
            {
                var databaseName = _connectionStringBuilder.InitialCatalog;
                return databaseName;
            }
        }

        public void EnsureDatabaseExists(int size, int fileGrowth)
        {
            var masterConnectionString = GetMasterConnectionString();

            using (var connection = new SqlConnection(masterConnectionString.ConnectionString))
            {
                connection.Open();

                bool exists;
                var dbExists = $"SELECT count(*) FROM master.dbo.sysdatabases where name ='{DatabaseName}'";
                using (var command = new SqlCommand(dbExists, connection))
                {
                    exists = (int)command.ExecuteScalar() > 0;
                }

                if (exists)
                {
                    Console.WriteLine($"Database instance {DatabaseName} already exists.");
                }
                else
                {
                    string databasePath;
                    using (var command = new SqlCommand(@"
                        SELECT SUBSTRING(physical_name, 1, CHARINDEX(N'master.mdf', LOWER(physical_name)) - 1)  
                        FROM master.sys.master_files
                        WHERE database_id = 1 AND file_id = 1",
                        connection))
                    {
                        databasePath = command.ExecuteScalar() as string;
                    }

                    //_writeOutput($"Database instance {databaseName} does not exist. Creating...");
                    var sql = $@"CREATE DATABASE [{DatabaseName}]
                              ON PRIMARY (
                                NAME = N'{DatabaseName}',
                                FILENAME = N'{databasePath}{DatabaseName}.mdf',
                                SIZE = {size}MB,
                                FILEGROWTH = {fileGrowth}MB
                              )
                              LOG ON (
                                NAME = N'{DatabaseName}_log',
                                FILENAME = N'{databasePath}{DatabaseName}_log.ldf'
                              )";

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private SqlConnectionStringBuilder GetMasterConnectionString()
        {
            var masterConnectionString = new SqlConnectionStringBuilder(_connectionStringBuilder.ConnectionString);
            masterConnectionString.InitialCatalog = "master";
            return masterConnectionString;
        }

    }
}
