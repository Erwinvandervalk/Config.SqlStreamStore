using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// The class that provides the configuration settings to be used inside the ConfigurationRoot class. 
    /// </summary>
    public class StreamStoreConfigurationProvider : ConfigurationProvider
    {
        private readonly StreamStoreConfigurationSource _source;
        private readonly IStreamStoreConfigRepository _streamStoreConfigRepository;
        private IConfigurationSettings _configurationSettings;
        
        public StreamStoreConfigurationProvider(StreamStoreConfigurationSource source,
            IStreamStoreConfigRepository streamStoreConfigRepository)
        {
            _source = source;
            _streamStoreConfigRepository = streamStoreConfigRepository;

        }

        /// <summary>
        /// Loads the settings from SSS. 
        /// </summary>
        public override void Load()
        {
            int retryCount = 0;
            while (true)
            {
                

                try
                {
                    _configurationSettings = _streamStoreConfigRepository
                        .GetLatest(CancellationToken.None).GetAwaiter().GetResult();

                    break;
                }
                catch (Exception ex)
                {
                    if (_source.ErrorHandler == null)
                        throw;

                    if (!_source.ErrorHandler(ex, retryCount++).GetAwaiter().GetResult())
                    {
                        throw;
                    }
                }
            }

            Data = _configurationSettings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

            if (_source.SubscribeToChanges)
            {
                SubscribeToChanges(CancellationToken.None);
            }

        }


        /// <summary>
        /// Subscribes to changes. 
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public IDisposable SubscribeToChanges(CancellationToken ct)
        {
            return _streamStoreConfigRepository.WatchForChanges(
                version: _configurationSettings?.Version ?? 0, 
                onSettingsChanged: OnChanged, 
                ct: CancellationToken.None);
        }

        /// <summary>
        /// Invoked when changes have been detected. 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private Task OnChanged(IConfigurationSettings settings, CancellationToken ct)
        {
            Data = settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

            OnReload();

            return Task.CompletedTask;
        }
    }
}