using System.Data;
using DatabaseBenchmark.Core;

namespace DatabaseBenchmark.Data;

public class DatabaseInitializer {
    private readonly IConnectionProvider _provider;
    public DatabaseInitializer(IConnectionProvider provider) => _provider = provider;

    public void RecreateDatabase(string scriptPath) {
        var sql = File.ReadAllText(scriptPath);
        using var conn = _provider.CreateConnection();
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}