using System.Data;

namespace DatabaseBenchmark.Core;

public interface IConnectionProvider {
    string DbName { get; }
    IDbConnection CreateConnection();
    string GetExplainQuery(string query);
    string GetPlanPrefix();
    Task ClearCacheAsync(IDbConnection connection);
}