// Azure Table Storage implementation of IStateStore.
//
// To activate:
//   1. Add the NuGet package:
//        <PackageReference Include="Azure.Data.Tables" Version="12.9.0" />
//   2. Uncomment all code blocks below marked with [UNCOMMENT].
//   3. Set StorageType = "TableStorage" in appsettings.json.
//   4. Provide the connection string via:
//        - appsettings.json key  : "AzureStorageConnectionString"
//        - Environment variable  : AZURE_STORAGE_CONNECTION_STRING
//
// This store persists seen dog IDs as a single comma-delimited entity in one Table row,
// avoiding per-listing row overhead for small-to-medium state sets (< ~10 KB).

// [UNCOMMENT] using Azure;
// [UNCOMMENT] using Azure.Data.Tables;
// [UNCOMMENT] using System.Text.Json;

namespace PetNestMonitor.Storage;

/// <summary>
/// Azure Table Storage implementation of <see cref="IStateStore"/>.
/// Safe for multi-region Lambda/Function deployments where local filesystem is ephemeral.
/// </summary>
public sealed class TableStorageStateStore : IStateStore
{
    // [UNCOMMENT] private readonly TableClient _tableClient;
    private const string PartitionKey = "PetNestMonitor";
    private const string RowKey = "SeenDogs";

    public TableStorageStateStore(string connectionString, string tableName = "PetNestMonitorState")
    {
        // [UNCOMMENT] _tableClient = new TableClient(connectionString, tableName);
        // [UNCOMMENT] _tableClient.CreateIfNotExists();

        throw new NotImplementedException(
            "TableStorageStateStore requires the Azure.Data.Tables NuGet package. " +
            "Follow the activation steps in the file header comment."
        );
    }

    public async Task<HashSet<string>> LoadSeenIdsAsync()
    {
        // [UNCOMMENT]
        // try
        // {
        //     var response = await _tableClient.GetEntityAsync<TableEntity>(PartitionKey, RowKey);
        //     string? raw = response.Value.GetString("Ids");
        //     if (string.IsNullOrEmpty(raw)) return new HashSet<string>();
        //     return raw.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        // }
        // catch (RequestFailedException ex) when (ex.Status == 404)
        // {
        //     return new HashSet<string>();
        // }

        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    public async Task SaveSeenIdsAsync(HashSet<string> ids)
    {
        // [UNCOMMENT]
        // var entity = new TableEntity(PartitionKey, RowKey)
        // {
        //     { "Ids", string.Join(',', ids) }
        // };
        // await _tableClient.UpsertEntityAsync(entity);

        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}
