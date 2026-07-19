using System.Data;
using DatabaseBenchmark.Core;
using Microsoft.Data.SqlClient;

namespace DatabaseBenchmark.Providers;

public class MsSqlServerProvider : IConnectionProvider {
    private readonly string _connectionString;
    public string DbName => "MS SQL Server";

    public MsSqlServerProvider(string connectionString) => _connectionString = connectionString;

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public string GetExplainQuery(string query) => query;

    public string GetPlanPrefix() => "SET SHOWPLAN_XML ON;";

    public async Task ClearCacheAsync(IDbConnection connection) {
        try {
            using var cmd = new SqlCommand(@"
                CHECKPOINT;
                DBCC DROPCLEANBUFFERS;
                DBCC FREEPROCCACHE;", (SqlConnection)connection);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) {
            Console.WriteLine($"[WARNING] Failed to clear cache: {ex.Message}. " +
                "Results may include cached data effects.");
        }
    }
}