using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using DatabaseBenchmark.Core;

namespace DatabaseBenchmark.Drivers;

public class AdoNetDriver : IDbDriver {
    public string Name => "AdoNet";

    public async Task<IEnumerable<T>> ExecuteQueryAsync<T>(IDbConnection connection, string query) {
        var results = new List<T>();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;

        // Приводим команду к асинхронному типу, если провайдер это поддерживает
        if (cmd is DbCommand dbCommand) {
            using var reader = await dbCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                // Добавляем "пустышку", чтобы BenchmarkRunner мог посчитать 
                // итоговое количество строк (ReturnedRows)
                results.Add(default!);
            }
        } 
        else {
            // Фолбэк для синхронного чтения
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(default!);
            }
        }

        return results;
    }

    public void GetExecutionPlan(IDbConnection connection, string query, string outputPath) {
        // Логика получения планов вынесена в BenchmarkRunner, 
        // поэтому здесь оставляем метод пустым.
    }
}