using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Config.SqlStreamStore
{
    public class StreamStoreConfigurationProvider : ConfigurationProvider
    {
        private bool _loaded = false;
        private readonly Func<IConfigRepository> _getConfigRepository;
        private IConfigRepository _configRepository;

        public StreamStoreConfigurationProvider(Func<IConfigRepository> getConfigRepository)
        {
            _getConfigRepository = getConfigRepository;
        }

        public override bool TryGet(string key, out string value)
        {
            if (!_loaded)
            {
                LoadSettings();
            }

            return Data.TryGetValue(key, out value);
        }

        private void LoadSettings()
        {
            if (GetConfigRepository() == null)
            {
                throw new InvalidOperationException("The configuration repository has not yet been initialized.");
            }

            var settings = GetConfigRepository()
                .GetLatest(CancellationToken.None).GetAwaiter().GetResult();

            Data = settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

            _loaded = true;
        }


        IConfigRepository GetConfigRepository()
        {
            if (_configRepository == null)
            {
                _configRepository = _getConfigRepository();
            }

            return _configRepository;
        }

        public override void Load()
        {
            if (GetConfigRepository() != null)
            {
                LoadSettings();
            }

        }
    }
}