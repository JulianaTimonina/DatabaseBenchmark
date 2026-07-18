using System.Data;

namespace DatabaseBenchmark.Core;

public interface IConnectionProvider {
    string DbName { get; }
    IDbConnection CreateConnection();
    string GetExplainQuery(string query);
    Task ClearCacheAsync(IDbConnection connection);
}