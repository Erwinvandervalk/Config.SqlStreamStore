using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Config.SqlStreamStore
{

    public class ModifiedConfigurationSettings : IConfigurationSettings
    {
        public readonly ConfigurationSettings OriginalSettings;

        public readonly IReadOnlyDictionary<string, string> NewValues;

        public int Version => OriginalSettings.Version;

        public ModifiedConfigurationSettings(params (string Key, string Value)[] settings) :
            this(originalSettings: null, changes: settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase))
        {
        }


        public ModifiedConfigurationSettings(ConfigurationSettings originalSettings, IReadOnlyDictionary<string, string> changes)
        {
            OriginalSettings = originalSettings ?? ConfigurationSettings.Empty();
            NewValues = changes ?? throw new ArgumentNullException(nameof(changes));
        }

        public ModifiedConfigurationSettings WithModifiedSettings(params (string Key, string Value)[] modifications)
        {
            var modified = NewValues.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var modification in modifications)
            {
                modified[modification.Key] = modification.Value;
            }
            return new ModifiedConfigurationSettings(OriginalSettings, modified);
        }


        public ModifiedConfigurationSettings WithAllSettingsReplaced(IReadOnlyDictionary<string, string> replacement)
        {
            return new ModifiedConfigurationSettings(OriginalSettings, replacement);
        }

        public ModifiedConfigurationSettings WithDeletedKeys(params string[] deletions)
        {
            var modified = NewValues.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var deletion in deletions)
            {
                modified.Remove(deletion);
            }
            return new ModifiedConfigurationSettings(OriginalSettings, modified);
        }

        public ConfigChanged GetChanges()
        {
            var deleted = new HashSet<string>(
                collection: OriginalSettings.Settings.Keys.Where(x => !NewValues.ContainsKey(x)),
                comparer: StringComparer.InvariantCultureIgnoreCase);

            var modified = new HashSet<string>(
                collection: NewValues.Where(IsModified).Select(x => x.Key), 
                comparer: StringComparer.InvariantCultureIgnoreCase);

            if (!deleted.Any() && !modified.Any())
            {
                // no changes
                return null;
            }
            return new ConfigChanged(
                allSettings: NewValues.ToDictionary(x => x.Key, x => x.Value), 
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
            return NewValues.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) NewValues).GetEnumerator();
        }

        public int Count => NewValues.Count;

        public bool ContainsKey(string key)
        {
            return NewValues.ContainsKey(key);
        }

        public bool TryGetValue(string key, out string value)
        {
            return NewValues.TryGetValue(key, out value);
        }

        public string this[string key] => NewValues[key];

        public IEnumerable<string> Keys => NewValues.Keys;

        public IEnumerable<string> Values => NewValues.Values;
    }
}