using Newtonsoft.Json;

namespace Config.SqlStreamStore
{
    /// <summary>
    /// Interface for hooks that allow you to transform the config data before writing / reading.
    ///
    /// Typically used to implement at-rest encryption, though the method of encryption is totally
    /// up to the consumer of this library. 
    /// </summary>
    public interface IConfigurationSettingsHooks
    {
        /// <summary>
        /// Allows you to transform a previously written message back into the original form.
        /// (IE: decrypt)
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        string OnReadMessage(string message);

        /// <summary>
        /// Allows you to transform a message into a persistance format. (IE: encrypt)
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        string OnWriteMessage(string message);

        /// <summary>
        /// The json serialization settings used to write the data into SSS. 
        /// </summary>
        JsonSerializerSettings JsonSerializerSettings { get; }
    }
}