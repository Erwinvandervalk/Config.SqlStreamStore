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
            Task.Run(() => CreateDatabase(ini["ConnectionString"], cts.Token), cts.Token);

            IStreamStore store = null;

            var sssConfig = new ConfigurationBuilder()
                .AddStreamStore(
                    subscribeToChanges: true,
                    factory: config => 
                        store = new MsSqlStreamStore(new MsSqlStreamStoreSettings(config["connectionString"]))
                    )
                .AddConfiguration(ini)
                .Build();

            ChangeToken.OnChange(sssConfig.GetReloadToken, () =>
            {
                Console.WriteLine("Settings changed:");
                PrintSettings(sssConfig);
            });

            Console.WriteLine("Found settings at startup:");
            PrintSettings(sssConfig);

            Console.ReadLine();
            cts.Cancel();
            store.Dispose();
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
            try
            {
                await Task.Delay(1000);
                Console.WriteLine("Creating database schema");
                await Task.Delay(3000);

                var mgr = new DatabaseManager(connectionString);
                mgr.EnsureDatabaseExists(10, 10);

                var msSqlStreamStoreSettings = new MsSqlStreamStoreSettings(connectionString);

                var store = new MsSqlStreamStore(msSqlStreamStoreSettings);
                if (!(await store.CheckSchema(ct)).IsMatch())
                {
                    await store.CreateSchema(ct);
                }

                var repo = new ConfigRepository(store);

                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(1000, ct);
                    await repo.Modify(ct, ("setting1", DateTime.Now.ToLongTimeString()));
                }
            }
            
            catch (Exception ex)
            {
                Console.WriteLine("error while creating database: " + ex.Message);
                throw;
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
        public void DropDatabase()
        {

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
