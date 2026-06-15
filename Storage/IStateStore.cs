namespace PetNestMonitor.Storage;

/// <summary>
/// Abstraction for persisting the set of already-evaluated dog listing IDs.
/// Swap implementations via StorageType config: "File" (default) or "TableStorage" (cloud).
/// </summary>
public interface IStateStore
{
    /// <summary>Loads the set of listing IDs that have already been processed.</summary>
    Task<HashSet<string>> LoadSeenIdsAsync();

    /// <summary>Persists the updated set of processed listing IDs.</summary>
    Task SaveSeenIdsAsync(HashSet<string> ids);
}
