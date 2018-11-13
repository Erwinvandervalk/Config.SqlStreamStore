using System.Collections.Generic;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Message that's saved in SSS to record configuration has changed. 
    /// </summary>
    public class ConfigChanged
    {
        public ConfigChanged()
        {
            
        }

        public ConfigChanged(Dictionary<string, string> allSettings, HashSet<string> modifiedSettings, HashSet<string> deletedSettings)
        {
            AllSettings = allSettings;
            ModifiedSettings = modifiedSettings;
            DeletedSettings = deletedSettings;
        }

        /// <summary>
        /// Memento of all settings. 
        /// </summary>
        public Dictionary<string, string> AllSettings { get; set; }

        /// <summary>
        /// Pointers to the settings that have changed in this version
        /// </summary>
        public HashSet<string> ModifiedSettings { get; set; }

        /// <summary>
        /// Names of the keys that have been deleted in this version. 
        /// </summary>
        public HashSet<string> DeletedSettings { get; set; }
    }
}