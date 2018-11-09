using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Config.SqlStreamStore
{
    public class ConfigurationSettings : IConfigurationSettings
    {
        internal ConfigurationSettings(int version = -1, DateTime? lastModified = null,
            IReadOnlyDictionary<string, string> settings = null)
        {
            Version = version;
            LastModified = lastModified;
            Settings = settings
                    ?.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public readonly IReadOnlyDictionary<string, string> Settings;

        public int Version { get; }
        public readonly DateTime? LastModified;

        public IConfigurationSettings Modify(params (string Key, string Value)[] modifications)
        {
            var modified = Settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var modification in modifications)
            {
                modified[modification.Key] = modification.Value;
            }
            return new ModifiedConfigurationSettings(this, modified);
        }

        public IConfigurationSettings Set(IReadOnlyDictionary<string, string> replacement)
        {
            return new ModifiedConfigurationSettings(this, replacement ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));
        }

        public IConfigurationSettings Delete(params string[] deletions)
        {
            if (deletions == null || !deletions.Any())
                return this;

            var modified = Settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var deletion in deletions)
            {
                modified.Remove(deletion);
            }
            return new ModifiedConfigurationSettings(this, modified);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return Settings.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) Settings).GetEnumerator();
        }

        public int Count => Settings.Count;

        public bool ContainsKey(string key)
        {
            return Settings.ContainsKey(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return Settings.TryGetValue(key, out value);
        }

        public string this[string key] => Settings[key];

        public IEnumerable<string> Keys => Settings.Keys;

        public IEnumerable<string> Values => Settings.Values;
    }
}