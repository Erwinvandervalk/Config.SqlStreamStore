using System.Collections.Generic;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Interface for the configuration settings. 
    /// </summary>
    public interface IConfigurationSettings : IReadOnlyDictionary<string, string>
    {
        /// <summary>
        /// The version 
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Creates a modified version of the confiration settings, that can be saved. 
        /// </summary>
        /// <param name="modifications"></param>
        /// <returns></returns>
        ModifiedConfigurationSettings WithModifiedSettings(params (string Key, string Value)[] modifications);

        /// <summary>
        /// Creates a modified version of the configuration settings with all key / values replaced. This can be saved. 
        /// </summary>
        /// <param name="replacement"></param>
        /// <returns></returns>
        ModifiedConfigurationSettings WithAllSettingsReplaced(IReadOnlyDictionary<string, string> replacement);

        /// <summary>
        /// Deletes certain keys. 
        /// </summary>
        /// <param name="deletions"></param>
        /// <returns></returns>
        ModifiedConfigurationSettings WithDeletedKeys(params string[] deletions);



        /// <summary>
        /// The keys that where modified in this version
        /// </summary>
        IReadOnlyList<string> ModifiedKeys { get; }

        /// <summary>
        /// The keys that where deleted in this version. 
        /// </summary>
        IReadOnlyList<string> DeletedKeys { get; }
    }
}