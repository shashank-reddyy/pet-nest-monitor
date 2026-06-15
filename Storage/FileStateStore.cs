using System.Text.Json;

namespace PetNestMonitor.Storage;

/// <summary>
/// Local filesystem implementation of <see cref="IStateStore"/>.
/// Default store; suitable for single-instance deployments and local testing.
/// State is persisted to a JSON file at the configured path.
/// </summary>
public sealed class FileStateStore : IStateStore
{
    private readonly string _filePath;

    public FileStateStore(string filePath = "seen_dogs.json")
    {
        _filePath = filePath;
    }

    public Task<HashSet<string>> LoadSeenIdsAsync()
    {
        if (!File.Exists(_filePath))
            return Task.FromResult(new HashSet<string>());

        try
        {
            string json = File.ReadAllText(_filePath);
            return Task.FromResult(
                JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>()
            );
        }
        catch
        {
            return Task.FromResult(new HashSet<string>());
        }
    }

    public Task SaveSeenIdsAsync(HashSet<string> ids)
    {
        try
        {
            string json = JsonSerializer.Serialize(ids);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"State storage serialization failure: {ex.Message}");
        }

        return Task.CompletedTask;
    }
}
