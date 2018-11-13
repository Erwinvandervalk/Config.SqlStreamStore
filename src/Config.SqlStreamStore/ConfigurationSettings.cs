using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Aggregate for the configuration settings. It's immutable, but you can create a modified one that can later be saved back. 
    /// </summary>
    public class ConfigurationSettings : IConfigurationSettings
    {
        /// <summary>
        /// Creates empty configuration. NOte, this can only be used to save the initial version, due to version number conflicts. 
        /// </summary>
        /// <returns></returns>
        public static ConfigurationSettings Empty() => new ConfigurationSettings();

        /// <summary>
        /// Creates configuration from a list of key value pairs. Note, this can only be used to save the initial version, due to
        /// version number constraints. . 
        /// </summary>
        /// <param name="initialValues"></param>
        /// <returns></returns>
        public static ConfigurationSettings Create(params (string Key, string Value)[] initialValues) => new ConfigurationSettings(
            settings: initialValues.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase));

        internal ConfigurationSettings(
            int version = -1, 
            DateTime? lastModified = null,
            IReadOnlyDictionary<string, string> settings = null, 
            HashSet<string> modifiedKeys = null, 
            HashSet<string> deletedKeys = null)
        {
            Version = version;
            ModifiedKeys = modifiedKeys?.ToArray() ?? Array.Empty<string>();
            DeletedKeys = deletedKeys?.ToArray() ?? Array.Empty<string>(); ;
            LastModified = lastModified;
            Settings = settings
                    ?.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }


        /// <summary>
        /// The list of confiugration settings
        /// </summary>
        public readonly IReadOnlyDictionary<string, string> Settings;

        /// <summary>
        /// The keys that where modified in this version
        /// </summary>
        public IReadOnlyList<string> ModifiedKeys { get; }

        /// <summary>
        /// The keys that where deleted in this version. 
        /// </summary>
        public IReadOnlyList<string> DeletedKeys { get; }

        /// <summary>
        /// The version
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// When was the config last modified. 
        /// </summary>
        public readonly DateTime? LastModified;

        /// <summary>
        /// Creates a modified version of the confiration settings, that can be saved. 
        /// </summary>
        /// <param name="modifications"></param>
        /// <returns></returns>
        public ModifiedConfigurationSettings WithModifiedSettings(params (string Key, string Value)[] modifications)
        {
            var modified = Settings.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var modification in modifications)
            {
                modified[modification.Key] = modification.Value;
            }
            return new ModifiedConfigurationSettings(this, modified);
        }

        /// <summary>
        /// Creates a modified version of the configuration settings with all key / values replaced. This can be saved. 
        /// </summary>
        /// <param name="replacement"></param>
        /// <returns></returns>
        public ModifiedConfigurationSettings WithAllSettingsReplaced(IReadOnlyDictionary<string, string> replacement)
        {
            return new ModifiedConfigurationSettings(this, replacement ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));
        }
        
        /// <summary>
        /// Deletes certain keys. 
        /// </summary>
        /// <param name="deletions"></param>
        /// <returns></returns>
        public ModifiedConfigurationSettings WithDeletedKeys(params string[] deletions)
        {
            if (deletions == null) throw new ArgumentNullException(nameof(deletions));
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