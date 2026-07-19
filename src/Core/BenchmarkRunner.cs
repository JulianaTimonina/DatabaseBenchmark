using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DatabaseBenchmark.Core;
using DatabaseBenchmark.Models;

namespace DatabaseBenchmark.Core;

public class BenchmarkRunner {
    private const int WarmUpIterations = 3; // 3-5 раз для прогрева
    private const int TargetIterations = 10; // 10 раз для замеров
    private readonly string _resultsDir;
    private readonly string _plansDir;
    private readonly string _csvPath;

    public BenchmarkRunner() {
        // Создаем структуры папок согласно архитектуре
        var baseDir = AppContext.BaseDirectory;
        _resultsDir = Path.Combine(baseDir, "results");
        _plansDir = Path.Combine(_resultsDir, "plans");
        _csvPath = Path.Combine(_resultsDir, "results.csv");

        Directory.CreateDirectory(_resultsDir);
        Directory.CreateDirectory(_plansDir);
    }

    public async Task Run(IConnectionProvider provider, IDbDriver driver, string queryName, string query, int datasetSize) {
        Console.WriteLine($"\n[Бенчмарк] Запуск: {provider.DbName} | {driver.Name} | Запрос: {queryName} (Объем: {datasetSize})");

        using var conn = provider.CreateConnection();
        conn.Open();

        // --- ШАГ 1. Очистка кэша СУБД (1 раз на блок) ---
        try {
            await provider.ClearCacheAsync(conn);
            Console.WriteLine("  -> Кэш СУБД успешно очищен.");
        }
        catch (Exception ex) {
            Console.WriteLine($"  [!] Предупреждение при очистке кэша: {ex.Message}");
        }

        // --- ШАГ 2. Цикл прогрева (Warm-up, без замера времени) ---
        int returnedRowsCount = 0;
        try {
            for (int i = 1; i <= WarmUpIterations; i++) {
                var warmUpResult = await driver.ExecuteQueryAsync<dynamic>(conn, query);

                // Считаем количество строк (только на последней итерации прогрева)
                if (i == WarmUpIterations) {
                    if (warmUpResult is System.Collections.ICollection collection) {
                        returnedRowsCount = collection.Count;
                    } else {
                        returnedRowsCount = warmUpResult.Count();
                    }
                }
            }
            Console.WriteLine($"  -> Прогрев выполнен ({WarmUpIterations} итераций). Запрос вернул строк: {returnedRowsCount:N0}");
        }
        catch (Exception ex) {
            Console.WriteLine($"  [ERROR] Ошибка при прогреве запроса: {ex.Message}");
            return;
        }

        // --- ШАГ 3. Серия из 10 чистых замеров (без очистки кэша между ними) ---
        var timings = new List<double>();
        var sw = new Stopwatch();

        for (int i = 1; i <= TargetIterations; i++) {
            sw.Restart();
            var _ = await driver.ExecuteQueryAsync<dynamic>(conn, query);
            sw.Stop();

            timings.Add(sw.Elapsed.TotalMilliseconds);
        }

        // --- ШАГ 4. Расчет статистических метрик ---
        double min = timings.Min();
        double max = timings.Max();
        double avg = timings.Average();
        double median = StatisticsHelper.GetMedian(timings);
        double stdDev = StatisticsHelper.GetStandardDeviation(timings, avg);

        // Вывод результатов в консоль
        Console.WriteLine($"  -> РЕЗУЛЬТАТЫ:");
        Console.WriteLine($"     Строк получено: {returnedRowsCount:N0}");
        Console.WriteLine($"     Минимум:  {min:F2} ms | Максимум: {max:F2} ms");
        Console.WriteLine($"     Среднее:  {avg:F2} ms | Медиана:  {median:F2} ms");
        Console.WriteLine($"     Станд. отклонение (StdDev): {stdDev:F2} ms");

        // --- ШАГ 5. Сохранение метрик в CSV ---
        var result = new ExperimentResult {
            Database = provider.DbName,
            Driver = driver.Name,
            QueryName = queryName,
            DatasetSize = datasetSize,
            ReturnedRows = returnedRowsCount,
            MinMs = min,
            MaxMs = max,
            AvgMs = avg,
            MedianMs = median,
            StdDevMs = stdDev
        };
        SaveResultToCsv(result);

        // --- ШАГ 6. Сбор и сохранение плана выполнения (план без выполнения запроса) ---
        await SaveExecutionPlanAsync(provider, conn, queryName, query, datasetSize);
    }

    private void SaveResultToCsv(ExperimentResult result) {
        bool writeHeader = !File.Exists(_csvPath);

        using var writer = new StreamWriter(_csvPath, true);
        if (writeHeader) {
            writer.WriteLine("Database;Driver;QueryName;DatasetSize;ReturnedRows;MinMs;MaxMs;AvgMs;MedianMs;StdDevMs");
        }
        writer.WriteLine($"{result.Database};{result.Driver};{result.QueryName};{result.DatasetSize};{result.ReturnedRows};{result.MinMs:F4};{result.MaxMs:F4};{result.AvgMs:F4};{result.MedianMs:F4};{result.StdDevMs:F4}");
    }

    private async Task SaveExecutionPlanAsync(IConnectionProvider provider, IDbConnection conn, string queryName, string query, int datasetSize) {
        try {
            // Открываем отдельное соединение для сбора плана,
            // чтобы не влиять на состояние основного соединения бенчмарка
            using var planConn = provider.CreateConnection();
            planConn.Open();

            // Выполняем префикс плана как отдельную команду (например, SET SHOWPLAN_XML ON для MSSQL)
            // Это гарантирует, что SET SHOWPLAN будет единственной инструкцией в пакете
            string planPrefix = provider.GetPlanPrefix();
            if (!string.IsNullOrEmpty(planPrefix)) {
                using var prefixCmd = planConn.CreateCommand();
                prefixCmd.CommandText = planPrefix;
                prefixCmd.ExecuteNonQuery();
            }

            // Выполняем запрос для получения плана
            string explainQuery = provider.GetExplainQuery(query);
            using var cmd = planConn.CreateCommand();
            cmd.CommandText = explainQuery;

            using var reader = cmd.ExecuteReader();
            string planText = "";
            while (reader.Read()) {
                string? value = reader.GetValue(0)?.ToString();
                if (!string.IsNullOrEmpty(value)) {
                    planText += value + Environment.NewLine;
                }
            }

            // Если план пустой — возможно, SHOWPLAN_XML вернул NULL (неподдерживаемый запрос)
            if (string.IsNullOrWhiteSpace(planText)) {
                Console.WriteLine("  [!] План запроса пуст (возможно, запрос не поддерживает получение плана).");
                return;
            }

            // Формируем имя файла плана без пробелов и спецсимволов
            string safeDbName = provider.DbName.Replace(" ", "_");
            string fileName = $"{safeDbName}_{queryName}_{datasetSize}_plan.txt";
            string planPath = Path.Combine(_plansDir, fileName);

            await File.WriteAllTextAsync(planPath, planText);
            Console.WriteLine($"  -> [План сохранен]: results/plans/{fileName}");
        }
        catch (Exception ex) {
            Console.WriteLine($"  [!] Ошибка сбора плана запроса: {ex.Message}");
        }
    }
}