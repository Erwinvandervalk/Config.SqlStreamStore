using System;
using System.Threading.Tasks;

namespace Config.SqlStreamStore.Delegates
{
    /// <summary>
    /// Allows you to implement error handling logic. 
    /// </summary>
    /// <param name="ex">The exception that occurred. </param>
    /// <param name="retryCount">How many times already retried. </param>
    /// <returns>boolean to indicate if supposed to retry. </returns>
    public delegate Task<bool> ErrorHandler(Exception ex, int retryCount);
}