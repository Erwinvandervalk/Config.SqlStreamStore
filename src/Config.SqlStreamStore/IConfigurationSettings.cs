using System.Collections.Generic;

namespace Config.SqlStreamStore
{
    public interface IConfigurationSettings : IReadOnlyDictionary<string, string>
    {
        int Version { get; }

        IConfigurationSettings Modify(params (string Key, string Value)[] modifications);
        IConfigurationSettings Set(IReadOnlyDictionary<string, string> replacement);
        IConfigurationSettings Delete(params string[] deletions);
    }
}