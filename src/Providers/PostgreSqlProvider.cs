using System.Data;
using DatabaseBenchmark.Core;
using Npgsql;

namespace DatabaseBenchmark.Providers;

public class PostgreSqlProvider : IConnectionProvider {
    private readonly string _connectionString;
    public string DbName => "PostgreSQL";

    public PostgreSqlProvider(string connectionString) => _connectionString = connectionString;

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    public string GetExplainQuery(string query) => $"EXPLAIN (FORMAT JSON) {query}";

    public string GetPlanPrefix() => "";

    public async Task ClearCacheAsync(IDbConnection connection) {
        using var cmd = new NpgsqlCommand("DISCARD ALL; CHECKPOINT; SELECT pg_stat_reset();", (NpgsqlConnection)connection);
        await cmd.ExecuteNonQueryAsync();
    }
}