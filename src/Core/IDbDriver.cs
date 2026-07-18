using System.Data;

namespace DatabaseBenchmark.Core;

public interface IDbDriver {
    string Name { get; }
    Task<IEnumerable<T>> ExecuteQueryAsync<T>(IDbConnection connection, string query);
    void GetExecutionPlan(IDbConnection connection, string query, string outputPath);
}