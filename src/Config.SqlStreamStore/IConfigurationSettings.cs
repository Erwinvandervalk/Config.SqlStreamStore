using System.Collections.Generic;

namespace Config.SqlStreamStore
{
    public interface IConfigurationSettings : IReadOnlyDictionary<string, string>
    {
        int Version { get; }

        ModifiedConfigurationSettings WithModifiedSettings(params (string Key, string Value)[] modifications);
        ModifiedConfigurationSettings WithAllSettingsReplaced(IReadOnlyDictionary<string, string> replacement);
        ModifiedConfigurationSettings WithDeletedKeys(params string[] deletions);
    }
}