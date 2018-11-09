using System.Collections.Generic;

namespace Config.SqlStreamStore
{
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

        public Dictionary<string, string> AllSettings { get; set; }
        public HashSet<string> ModifiedSettings { get; set; }
        public HashSet<string> DeletedSettings { get; set; }
    }
}