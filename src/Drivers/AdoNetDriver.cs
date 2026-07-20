using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Reflection;
using System.Threading.Tasks;
using DatabaseBenchmark.Core;

namespace DatabaseBenchmark.Drivers;

public class AdoNetDriver : IDbDriver {
    public string Name => "AdoNet";

    public async Task<IEnumerable<T>> ExecuteQueryAsync<T>(IDbConnection connection, string query) {
        var results = new List<T>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;

        if (cmd is DbCommand dbCommand) {
            using var reader = await dbCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync()) {
                results.Add(MapToObject<T>(reader));
            }
        }
        else {
            // Фолбэк для синхронного чтения
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) {
                results.Add(MapToObject<T>(reader));
            }
        }

        return results;
    }

    /// <summary>
    /// Маппинг строки из IDataReader в объект типа T через рефлексию.
    /// Позволяет AdoNetDriver выполнять ту же работу по материализации данных,
    /// что и Dapper, для честного сравнения производительности.
    /// </summary>
    private static T MapToObject<T>(IDataReader reader) {
        Type type = typeof(T);

        // Случай 1: T — dynamic (object) — используем ExpandoObject
        if (type == typeof(object)) {
            var expando = new ExpandoObject();
            var dict = (IDictionary<string, object?>)expando;

            for (int i = 0; i < reader.FieldCount; i++) {
                string columnName = reader.GetName(i);
                dict[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            return (T)(object)expando;
        }

        // Случай 2: T — примитивный тип (int, string, long, etc.)
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(Guid) || type == typeof(byte[])) {
            if (reader.FieldCount >= 1) {
                object? value = reader.IsDBNull(0) ? null : reader.GetValue(0);
                if (value == null) return default!;
                return (T)Convert.ChangeType(value, type);
            }
            return default!;
        }

        // Случай 3: T — пользовательский класс (User, Role и т.д.)
        var obj = Activator.CreateInstance<T>()!;
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        for (int i = 0; i < reader.FieldCount; i++) {
            string columnName = reader.GetName(i);
            var prop = FindProperty(properties, columnName);
            if (prop == null) continue;

            object? value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            if (value == null) continue;

            // Если тип свойства не совпадает с типом значения, пытаемся преобразовать
            if (value.GetType() != prop.PropertyType) {
                value = Convert.ChangeType(value, prop.PropertyType);
            }

            prop.SetValue(obj, value);
        }

        return obj;
    }

    /// <summary>
    /// Поиск свойства по имени столбца (регистронезависимый).
    /// </summary>
    private static PropertyInfo? FindProperty(PropertyInfo[] properties, string columnName) {
        foreach (var prop in properties) {
            if (string.Equals(prop.Name, columnName, StringComparison.OrdinalIgnoreCase)) {
                return prop;
            }
        }
        return null;
    }
}