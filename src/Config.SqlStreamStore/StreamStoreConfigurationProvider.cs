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
        private readonly IConfigRepository _configRepository;

        public StreamStoreConfigurationProvider(IConfigRepository configRepository)
        {
            _configRepository = configRepository;
        }
        public override void Load()
        {
            var settings = _configRepository
                .GetLatest(CancellationToken.None).GetAwaiter().GetResult();

            Data = settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}