using Dapper;
using Restaurant.Core;

namespace Restaurant.Data;

/// <summary>Доступ к броням столиков.</summary>
public class ReservationRepository
{
    private const string Select = @"
        SELECT r.id, r.client_id, r.guests_count, r.reserve_date::timestamp AS reserve_date,
               r.start_time, r.end_time, r.status::text AS status,
               u.last_name || ' ' || u.first_name AS client_name,
               (SELECT string_agg(t.number::text, ', ' ORDER BY t.number)
                FROM reservation_tables rt JOIN restaurant_tables t ON t.id = rt.table_id
                WHERE rt.reservation_id = r.id) AS tables
        FROM reservations r
        LEFT JOIN users u ON u.id = r.client_id ";

    /// <summary>Брони на указанную дату.</summary>
    public List<Reservation> GetByDate(DateTime date)
    {
        using var c = Db.Open();
        return c.Query<Reservation>(Select + "WHERE r.reserve_date = @date ORDER BY r.start_time",
            new { date = date.Date }).ToList();
    }

    /// <summary>Все активные брони начиная с сегодняшней даты.</summary>
    public List<Reservation> GetUpcoming()
    {
        using var c = Db.Open();
        return c.Query<Reservation>(Select +
            "WHERE r.status = 'ACTIVE' AND r.reserve_date >= CURRENT_DATE ORDER BY r.reserve_date, r.start_time")
            .ToList();
    }

    /// <summary>
    /// Создаёт бронь с привязкой столов. Возвращает идентификатор брони.
    /// Проверка вместимости и пересечений выполняется в сервисном слое.
    /// </summary>
    public int Create(Reservation r, IEnumerable<int> tableIds)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        int id = c.ExecuteScalar<int>(@"
            INSERT INTO reservations(client_id, guests_count, reserve_date, start_time, end_time, status)
            VALUES (@ClientId, @GuestsCount, @ReserveDate, @StartTime, @EndTime, 'ACTIVE')
            RETURNING id", r, tx);
        foreach (var tid in tableIds)
            c.Execute("INSERT INTO reservation_tables(reservation_id, table_id) VALUES (@id, @tid)",
                new { id, tid }, tx);
        tx.Commit();
        return id;
    }

    /// <summary>Отменяет бронь.</summary>
    public void Cancel(int reservationId)
    {
        using var c = Db.Open();
        c.Execute("UPDATE reservations SET status = 'CANCELLED' WHERE id = @reservationId", new { reservationId });
    }

    /// <summary>Суммарная вместимость указанных столов (мест).</summary>
    public int SeatsCapacity(IEnumerable<int> tableIds)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<int>(
            "SELECT COALESCE(SUM(seats),0) FROM restaurant_tables WHERE id = ANY(@ids)",
            new { ids = tableIds.ToArray() });
    }

    /// <summary>
    /// Проверяет, свободны ли столы на дату/интервал (нет пересечений с активными бронями).
    /// </summary>
    public bool AreTablesFree(DateTime date, TimeOnly start, TimeOnly end, IEnumerable<int> tableIds, int? exceptReservation = null)
    {
        using var c = Db.Open();
        int conflicts = c.ExecuteScalar<int>(@"
            SELECT COUNT(*)
            FROM reservation_tables rt
            JOIN reservations r ON r.id = rt.reservation_id
            WHERE rt.table_id = ANY(@ids)
              AND r.status = 'ACTIVE'
              AND r.reserve_date = @date
              AND r.id <> COALESCE(@except, -1)
              AND (@start < r.end_time AND @end > r.start_time)",
            new { ids = tableIds.ToArray(), date = date.Date, start, end, except = exceptReservation });
        return conflicts == 0;
    }
}
