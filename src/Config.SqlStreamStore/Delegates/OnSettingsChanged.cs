using System.Threading;
using System.Threading.Tasks;

namespace Config.SqlStreamStore.Delegates
{
    /// <summary>
    /// Delegate that's invoked when the configuration settings have changed. 
    /// </summary>
    /// <param name="settings">The changed settings. </param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public delegate Task OnSettingsChanged(IConfigurationSettings settings, CancellationToken ct);
}