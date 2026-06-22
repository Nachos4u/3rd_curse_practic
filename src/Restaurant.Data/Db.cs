using Dapper;
using Npgsql;

namespace Restaurant.Data;

/// <summary>
/// Точка доступа к базе данных PostgreSQL.
/// Строка подключения берётся из переменной окружения <c>RESTAURANT_DB</c>,
/// а при её отсутствии используется значение по умолчанию (локальный сервер).
/// </summary>
public static class Db
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=restaurant_db;Username=postgres;Password=eto_CATAHA2006";

    private static string _connectionString =
        Environment.GetEnvironmentVariable("RESTAURANT_DB") ?? DefaultConnectionString;

    static Db()
    {
        // Сопоставление snake_case столбцов БД с PascalCase свойствами моделей.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Обработчики типов date/time для совместимости Dapper и Npgsql.
        SqlMapper.AddTypeHandler(new TimeOnlyHandler());
        SqlMapper.AddTypeHandler(new DateOnlyHandler());
    }

    /// <summary>Текущая строка подключения.</summary>
    public static string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value;
    }

    /// <summary>Создаёт новое открытое подключение к БД.</summary>
    public static NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>Проверяет доступность БД (для диагностики при запуске).</summary>
    public static bool TestConnection(out string message)
    {
        try
        {
            using var conn = Open();
            conn.Query<int>("SELECT 1");
            message = "Подключение к базе данных установлено.";
            return true;
        }
        catch (Exception ex)
        {
            message = "Не удалось подключиться к базе данных: " + ex.Message;
            return false;
        }
    }
}
