using System.Data;
using Npgsql;

namespace Restaurant.Data;

/// <summary>
/// Доступ к отчётам. Результаты возвращаются как <see cref="DataTable"/>
/// для прямого связывания с таблицами интерфейса.
/// </summary>
public class ReportRepository
{
    private static DataTable Fill(string sql, params NpgsqlParameter[] ps)
    {
        using var c = Db.Open();
        using var cmd = new NpgsqlCommand(sql, c);
        cmd.Parameters.AddRange(ps);
        using var da = new NpgsqlDataAdapter(cmd);
        var dt = new DataTable();
        da.Fill(dt);
        return dt;
    }

    /// <summary>Отчёт 1: продажи блюд за два месяца с динамикой.</summary>
    public DataTable DishSales(int y1, int m1, int y2, int m2) => Fill(
        @"SELECT category AS ""Категория"", dish AS ""Блюдо"",
                 qty_period1 AS ""Кол-во (период 1)"", qty_period2 AS ""Кол-во (период 2)"",
                 dynamics AS ""Динамика""
          FROM rpt_dish_sales(@y1,@m1,@y2,@m2)",
        new NpgsqlParameter("y1", y1), new NpgsqlParameter("m1", m1),
        new NpgsqlParameter("y2", y2), new NpgsqlParameter("m2", m2));

    /// <summary>Отчёт 2: количество броней по столам за месяц.</summary>
    public DataTable TableBookings(int year, int month) => Fill(
        @"SELECT year_no AS ""Год"", month_no AS ""Месяц"", table_no AS ""№ стола"",
                 bookings AS ""Кол-во броней""
          FROM rpt_table_bookings(@year,@month)",
        new NpgsqlParameter("year", year), new NpgsqlParameter("month", month));

    /// <summary>Отчёт 3: работа официантов за два месяца с динамикой.</summary>
    public DataTable WaiterStats(int y1, int m1, int y2, int m2) => Fill(
        @"SELECT last_name AS ""Фамилия"", first_name AS ""Имя"", middle_name AS ""Отчество"",
                 orders_p1 AS ""Заказов (1)"", receipts_p1 AS ""Чеков (1)"", sum_p1 AS ""Сумма (1)"",
                 orders_p2 AS ""Заказов (2)"", receipts_p2 AS ""Чеков (2)"", sum_p2 AS ""Сумма (2)"",
                 d_orders AS ""Δ заказов"", d_receipts AS ""Δ чеков"", d_sum AS ""Δ сумма""
          FROM rpt_waiter_stats(@y1,@m1,@y2,@m2)",
        new NpgsqlParameter("y1", y1), new NpgsqlParameter("m1", m1),
        new NpgsqlParameter("y2", y2), new NpgsqlParameter("m2", m2));

    /// <summary>Отчёт 4: свободные столы на дату и время.</summary>
    public DataTable FreeTables(DateTime date, TimeOnly time) => Fill(
        @"SELECT table_no AS ""№ стола"", seats AS ""Мест"" FROM rpt_free_tables(@d::date,@t)",
        new NpgsqlParameter("d", date.Date), new NpgsqlParameter("t", time));

    /// <summary>Отчёт 5: список занятости столов на дату.</summary>
    public DataTable TablesOccupancy(DateTime date) => Fill(
        @"SELECT on_date AS ""Дата"", start_time AS ""Начало"", end_time AS ""Окончание"",
                 table_no AS ""№ стола"", reservation_no AS ""№ брони""
          FROM rpt_tables_occupancy(@d::date)",
        new NpgsqlParameter("d", date.Date));

    /// <summary>
    /// Отчёт 6: почасовая занятость столов на дату.
    /// Данные сводятся в таблицу «столы × часы»: «Бронь» либо пусто.
    /// </summary>
    public DataTable HourlyOccupancy(DateTime date)
    {
        var raw = Fill(@"SELECT table_no, hour_no, busy FROM rpt_hourly_occupancy(@d::date)",
            new NpgsqlParameter("d", date.Date));

        var result = new DataTable();
        result.Columns.Add("№ стола", typeof(int));
        for (int h = 9; h <= 22; h++)
            result.Columns.Add($"{h}:00", typeof(string));

        var byTable = new Dictionary<int, DataRow>();
        foreach (DataRow r in raw.Rows)
        {
            int t = Convert.ToInt32(r["table_no"]);
            int h = Convert.ToInt32(r["hour_no"]);
            bool busy = Convert.ToBoolean(r["busy"]);
            if (!byTable.TryGetValue(t, out var row))
            {
                row = result.NewRow();
                row["№ стола"] = t;
                result.Rows.Add(row);
                byTable[t] = row;
            }
            row[$"{h}:00"] = busy ? "Бронь" : "";
        }
        return result;
    }
}
