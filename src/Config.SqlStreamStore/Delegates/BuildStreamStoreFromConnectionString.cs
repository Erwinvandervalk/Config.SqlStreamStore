using SqlStreamStore;

namespace Config.SqlStreamStore.Delegates
{
    /// <summary>
    /// Builds a stream store from the connection string that's read from config. 
    /// </summary>
    /// <param name="connectionString">The connection string</param>
    /// <returns></returns>
    public delegate IStreamStore BuildStreamStoreFromConnectionString(string connectionString);
}