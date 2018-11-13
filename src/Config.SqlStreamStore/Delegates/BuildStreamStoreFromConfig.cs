using Microsoft.Extensions.Configuration;
using SqlStreamStore;

namespace Config.SqlStreamStore.Delegates
{
    /// <summary>
    /// Builds a stream store from the config. Typically used to read the connection string from the config files. 
    /// </summary>
    /// <param name="configurationRoot"></param>
    /// <returns></returns>
    public delegate IStreamStore BuildStreamStoreFromConfig(IConfigurationRoot configurationRoot);
}