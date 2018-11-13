namespace Config.SqlStreamStore.Delegates
{
    /// <summary>
    /// Builds a SQL Stream STore config repository
    /// </summary>
    /// <returns></returns>
    public delegate IStreamStoreConfigRepository BuildConfigRepository();
}