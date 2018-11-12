using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Config.SqlStreamStore
{

    public class ModifiedConfigurationSettings : IConfigurationSettings
    {
        public readonly ConfigurationSettings OriginalSettings;

        public readonly IReadOnlyDictionary<string, string> Changes;

        public int Version => OriginalSettings.Version;

        public ModifiedConfigurationSettings(params (string Key, string Value)[] settings) :
            this(originalSettings: null, changes: settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase))
        {
        }


        public ModifiedConfigurationSettings(ConfigurationSettings originalSettings, IReadOnlyDictionary<string, string> changes)
        {
            OriginalSettings = originalSettings ?? new ConfigurationSettings();
            Changes = changes ?? throw new ArgumentNullException(nameof(changes));
        }

        public IConfigurationSettings Modify(params (string Key, string Value)[] modifications)
        {
            var modified = Changes.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var modification in modifications)
            {
                modified[modification.Key] = modification.Value;
            }
            return new ModifiedConfigurationSettings(OriginalSettings, modified);
        }


        public IConfigurationSettings Set(IReadOnlyDictionary<string, string> replacement)
        {
            return new ModifiedConfigurationSettings(OriginalSettings, replacement);
        }

        public IConfigurationSettings Delete(params string[] deletions)
        {
            var modified = Changes.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var deletion in deletions)
            {
                modified.Remove(deletion);
            }
            return new ModifiedConfigurationSettings(OriginalSettings, modified);
        }

        public ConfigChanged GetChanges()
        {
            var deleted = new HashSet<string>(
                collection: OriginalSettings.Settings.Keys.Where(x => !Changes.ContainsKey(x)),
                comparer: StringComparer.InvariantCultureIgnoreCase);

            var modified = new HashSet<string>(
                collection: Changes.Where(IsModified).Select(x => x.Key), 
                comparer: StringComparer.InvariantCultureIgnoreCase);

            if (!deleted.Any() && !modified.Any())
            {
                // no changes
                return null;
            }
            return new ConfigChanged(
                allSettings: Changes.ToDictionary(x => x.Key, x => x.Value), 
                modifiedSettings: modified, 
                deletedSettings: deleted);
        }

        private bool IsModified(KeyValuePair<string, string> modifiedKvp)
        {
            if (!OriginalSettings.Settings.TryGetValue(modifiedKvp.Key, out var value))
            {
                // it's modified if the modified setting is not in the original list
                return true;
            }

            // it's modified if the value is different. 
            return value != modifiedKvp.Value;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            return Changes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) Changes).GetEnumerator();
        }

        public int Count => Changes.Count;

        public bool ContainsKey(string key)
        {
            return Changes.ContainsKey(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return Changes.TryGetValue(key, out value);
        }

        public string this[string key] => Changes[key];

        public IEnumerable<string> Keys => Changes.Keys;

        public IEnumerable<string> Values => Changes.Values;
    }
}