using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace DatabaseBenchmark.Data;

public class DataGenerator {
    private const int BatchSize = 100_000; // Оптимальный размер пакета для экономии ОЗУ

    public void Generate(IDbConnection conn, int count) {
        Console.WriteLine($"[Генератор] Начало генерации данных. Цель: {count} пользователей...");

        // 1. Создаем 5 базовых ролей
        using (var cmd = conn.CreateCommand()) {
            cmd.CommandText = "INSERT INTO Roles (Name) VALUES ('Admin'), ('User'), ('Editor'), ('Viewer'), ('Guest');";
            cmd.ExecuteNonQuery();
        }
        Console.WriteLine("[Генератор] Роли успешно добавлены.");

        // 2. Генерация и вставка
        if (conn is NpgsqlConnection pgConn) {
            GeneratePostgreSql(pgConn, count);
        } 
        else if (conn is SqlConnection msConn) {
            GenerateMsSqlServer(msConn, count);
        }
        
        Console.WriteLine("[Генератор] Генерация успешно завершена.");
    }

    private void GeneratePostgreSql(NpgsqlConnection conn, int count) {
        Console.WriteLine("[Генератор] Запуск бинарного импорта в PostgreSQL...");

        // Шаг A: Заливаем пользователей
        using (var writer = conn.BeginBinaryImport("COPY Users (Name) FROM STDIN (FORMAT BINARY)")) {
            for (int i = 1; i <= count; i++) {
                writer.StartRow();
                writer.Write($"User_{i}");
            }
            writer.Complete();
        }
        Console.WriteLine("[Генератор] Пользователи импортированы в PostgreSQL.");

        // Шаг B: Заливаем связи UserRoles (каждому пользователю присваиваем все 5 ролей)
        // В PostgreSQL ID автоинкремента стартует с 1 и идет последовательно (1, 2, ... count)
        using (var writer = conn.BeginBinaryImport("COPY UserRoles (UserId, RoleId) FROM STDIN (FORMAT BINARY)")) {
            for (int userId = 1; userId <= count; userId++) {
                for (int roleId = 1; roleId <= 5; roleId++) {
                    writer.StartRow();
                    writer.Write(userId);
                    writer.Write(roleId);
                }
            }
            writer.Complete();
        }
        Console.WriteLine("[Генератор] Связи UserRoles импортированы в PostgreSQL.");
    }

    private void GenerateMsSqlServer(SqlConnection conn, int count) {
        Console.WriteLine("[Генератор] Запуск SqlBulkCopy для MS SQL Server...");

        // Шаг A: Заливаем пользователей порциями (чтобы не держать миллионы строк в памяти)
        int usersInserted = 0;
        while (usersInserted < count) {
            int currentBatchSize = Math.Min(BatchSize, count - usersInserted);
            
            using var table = new DataTable();
            table.Columns.Add("Name", typeof(string));

            for (int i = 1; i <= currentBatchSize; i++) {
                table.Rows.Add($"User_{usersInserted + i}");
            }

            using var bulk = new SqlBulkCopy(conn) {
                DestinationTableName = "Users",
                BatchSize = currentBatchSize
            };
            bulk.ColumnMappings.Add("Name", "Name");
            bulk.WriteToServer(table);
            
            usersInserted += currentBatchSize;
        }
        Console.WriteLine("[Генератор] Пользователи импортированы в MS SQL Server.");

        // Шаг B: Заливаем связи UserRoles порциями (по 5 ролей на каждого пользователя)
        int rolesInsertedUsers = 0;
        while (rolesInsertedUsers < count) {
            int currentBatchSize = Math.Min(BatchSize, count - rolesInsertedUsers);

            using var table = new DataTable();
            table.Columns.Add("UserId", typeof(int));
            table.Columns.Add("RoleId", typeof(int));

            for (int i = 1; i <= currentBatchSize; i++) {
                int userId = rolesInsertedUsers + i;
                for (int roleId = 1; roleId <= 5; roleId++) {
                    table.Rows.Add(userId, roleId);
                }
            }

            using var bulk = new SqlBulkCopy(conn) {
                DestinationTableName = "UserRoles",
                BatchSize = currentBatchSize * 5
            };
            bulk.ColumnMappings.Add("UserId", "UserId");
            bulk.ColumnMappings.Add("RoleId", "RoleId");
            bulk.WriteToServer(table);

            rolesInsertedUsers += currentBatchSize;
        }
        Console.WriteLine("[Генератор] Связи UserRoles импортированы в MS SQL Server.");
    }
}