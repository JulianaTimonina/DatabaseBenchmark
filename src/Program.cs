using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DatabaseBenchmark.Providers;
using DatabaseBenchmark.Drivers;
using DatabaseBenchmark.Data;
using DatabaseBenchmark.Core;

namespace DatabaseBenchmark;

class Program {
    static async Task Main(string[] args) {
        Console.WriteLine("=== Инициализация системы тестирования ===");

        // 1. Загрузка конфигурации из appsettings.json
        var baseDir = AppContext.BaseDirectory;
        var config = new ConfigurationBuilder()
            .SetBasePath(baseDir)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string pgConnString = config.GetConnectionString("PostgreSQL") 
            ?? "Host=localhost;Database=pract_6;Username=postgres;Password=221131";
        string msConnString = config.GetConnectionString("MsSQL") 
            ?? "Server=localhost;Database=pract_6;Trusted_Connection=True;TrustServerCertificate=True;";

        var datasetSizes = config.GetSection("BenchmarkSettings:DatasetSizes").Get<List<int>>() 
            ?? new List<int> { 100000, 5000000 };
        var driverNames = config.GetSection("BenchmarkSettings:Drivers").Get<List<string>>() 
            ?? new List<string> { "AdoNet", "Dapper" };

        // 2. ИСПРАВЛЕНИЕ: Используем строго типизированный кортеж вместо dynamic
        var providers = new List<(string Name, IConnectionProvider Provider, string Schema)> {
            ("PostgreSQL", new PostgreSqlProvider(pgConnString), Path.Combine(baseDir, "scripts", "schema", "postgres_schema.sql")),
            ("MS SQL", new MsSqlServerProvider(msConnString), Path.Combine(baseDir, "scripts", "schema", "mssql_schema.sql"))
        };

        // 3. Определение тестовых SQL-запросов (SELECT, JOIN, GROUP BY)
        var testQueries = new Dictionary<string, string> {
            { 
                "Simple_SELECT", 
                "SELECT Id, Name FROM Users WHERE Name LIKE 'User_50%';" 
            },
            { 
                "Complex_JOIN", 
                @"SELECT u.Id, u.Name, r.Name AS RoleName 
                  FROM Users u 
                  INNER JOIN UserRoles ur ON u.Id = ur.UserId 
                  INNER JOIN Roles r ON ur.RoleId = r.Id 
                  WHERE u.Id BETWEEN 1000 AND 5000;" 
            },
            {
                "GROUP_BY_Aggregate",
                @"SELECT (u.Id / 1000) * 1000 AS RangeStart,
                        (u.Id / 1000) * 1000 + 999 AS RangeEnd,
                        COUNT(ur.RoleId) AS TotalRoles
                  FROM Users u
                  LEFT JOIN UserRoles ur ON u.Id = ur.UserId
                  GROUP BY u.Id / 1000
                  ORDER BY RangeStart;"
            }
        };

        // 4. Инициализация CSV файла результатов
        var csvPath = Path.Combine(baseDir, "results", "results.csv");
        if (File.Exists(csvPath)) {
            File.Delete(csvPath);
        }

        var runner = new BenchmarkRunner();

        // ================= ВЛОЖЕННЫЕ ЦИКЛЫ ЭКСПЕРИМЕНТОВ =================
        foreach (var item in providers) {
            Console.WriteLine($"\n================================================");
            Console.WriteLine($" СУБД: {item.Name}");
            Console.WriteLine($"================================================");

            if (!File.Exists(item.Schema)) {
                Console.WriteLine($"[ERROR] Пропущен тест СУБД {item.Name}: не найден файл схемы {item.Schema}");
                continue;
            }

            foreach (var size in datasetSizes) {
                Console.WriteLine($"\n--- [Подготовка] Объем данных: {size:N0} записей ---");

                try {
                    // А. Пересоздание чистой структуры БД
                    var initializer = new DatabaseInitializer(item.Provider);
                    initializer.RecreateDatabase(item.Schema);
                    Console.WriteLine("[OK] База очищена, структура создана.");

                    // Б. Генерация данных
                    using (var conn = item.Provider.CreateConnection()) {
                        conn.Open();
                        var generator = new DataGenerator();
                        generator.Generate(conn, size);
                    }
                    Console.WriteLine($"[OK] Генерация {size:N0} записей завершена.");

                    // В. Запуск тестов для каждого драйвера и каждого запроса
                    foreach (var driverName in driverNames) {
                        IDbDriver driver = driverName.ToLower() switch {
                            "adonet" => new AdoNetDriver(),
                            "dapper" => new DapperDriver(),
                            _ => throw new ArgumentException($"Неизвестный драйвер: {driverName}")
                        };

                        foreach (var queryEntry in testQueries) {
                            await runner.Run(item.Provider, driver, queryEntry.Key, queryEntry.Value, size);
                        }
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"[ERROR] Критическая ошибка во время теста {item.Name} на объеме {size}: {ex.Message}");
                }
            }
        }

        Console.WriteLine("\n================================================");
        Console.WriteLine("[ГОТОВО] Все эксперименты успешно завершены!");
        Console.WriteLine($"Итоговые метрики сохранены в: {csvPath}");
        Console.WriteLine($"Планы выполнения запросов сохранены в папке: {Path.Combine(baseDir, "results", "plans")}");
        Console.WriteLine("================================================");
    }
}