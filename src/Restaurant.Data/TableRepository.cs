using Dapper;
using Restaurant.Core;

namespace Restaurant.Data;

/// <summary>Доступ к столикам и их доступности.</summary>
public class TableRepository
{
    /// <summary>Все столики зала.</summary>
    public List<RestaurantTable> GetAll()
    {
        using var c = Db.Open();
        return c.Query<RestaurantTable>(
            "SELECT id, number, seats, pos_x, pos_y, status::text AS status FROM restaurant_tables ORDER BY number")
            .ToList();
    }

    /// <summary>
    /// Возвращает номера столов, занятых бронью на указанные дату и время.
    /// Используется для подсветки схемы зала.
    /// </summary>
    public HashSet<int> GetBusyTableNumbers(DateTime date, TimeOnly time)
    {
        using var c = Db.Open();
        var ids = c.Query<int>(@"
            SELECT DISTINCT t.number
            FROM restaurant_tables t
            JOIN reservation_tables rt ON rt.table_id = t.id
            JOIN reservations r ON r.id = rt.reservation_id
            WHERE r.status = 'ACTIVE' AND r.reserve_date = @date
              AND @time >= r.start_time AND @time < r.end_time",
            new { date = date.Date, time });
        return ids.ToHashSet();
    }
}
