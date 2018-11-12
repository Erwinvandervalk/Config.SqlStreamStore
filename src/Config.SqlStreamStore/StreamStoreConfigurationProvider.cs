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
    public class StreamStoreConfigurationProvider : ConfigurationProvider
    {
        private readonly StreamStoreConfigurationSource _source;
        private readonly IStreamStoreConfigRepository _streamStoreConfigRepository;
        private ConfigurationSettings _configurationSettings;
        
        public StreamStoreConfigurationProvider(StreamStoreConfigurationSource source,
            IStreamStoreConfigRepository streamStoreConfigRepository)
        {
            _source = source;
            _streamStoreConfigRepository = streamStoreConfigRepository;

        }
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

        public IDisposable SubscribeToChanges(CancellationToken ct)
        {
            return _streamStoreConfigRepository.SubscribeToChanges(
                version: _configurationSettings?.Version ?? 0, 
                onSettingsChanged: OnChanged, 
                ct: CancellationToken.None);
        }

        private Task OnChanged(ConfigurationSettings settings, CancellationToken ct)
        {
            Data = settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

            OnReload();

            return Task.CompletedTask;
        }
    }
}