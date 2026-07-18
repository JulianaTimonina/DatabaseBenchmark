using System.Data;
using Dapper;
using DatabaseBenchmark.Core;

namespace DatabaseBenchmark.Drivers;

public class DapperDriver : IDbDriver {
    public string Name => "Dapper";

    public async Task<IEnumerable<T>> ExecuteQueryAsync<T>(IDbConnection connection, string query) {
       var result = await connection.QueryAsync<T>(query);
        return result.ToList(); // Материализуем в список
    }

    public void GetExecutionPlan(IDbConnection connection, string query, string outputPath) {
        // Получение плана делегировано в BenchmarkRunner через GetExplainQuery у ConnectionProvider-а
    }
}