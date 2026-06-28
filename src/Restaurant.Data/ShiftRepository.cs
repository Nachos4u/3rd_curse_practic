using Dapper;
using Restaurant.Core;

namespace Restaurant.Data;

/// <summary>Доступ к сменам официантов и прикреплению столов.</summary>
public class ShiftRepository
{
    private const string Select = @"
        SELECT s.id, s.waiter_id, s.work_date::timestamp AS work_date, s.status::text AS status,
               s.opened_at, s.closed_at,
               u.last_name || ' ' || u.first_name AS waiter_name,
               (SELECT string_agg(t.number::text, ', ' ORDER BY t.number)
                FROM shift_tables st JOIN restaurant_tables t ON t.id = st.table_id
                WHERE st.shift_id = s.id) AS tables
        FROM shifts s JOIN users u ON u.id = s.waiter_id ";

    /// <summary>Смены на указанную дату.</summary>
    public List<Shift> GetByDate(DateTime date)
    {
        using var c = Db.Open();
        return c.Query<Shift>(Select + "WHERE s.work_date = @date ORDER BY u.last_name",
            new { date = date.Date }).ToList();
    }

    /// <summary>Столы, закреплённые за официантом в его смене на дату.</summary>
    public List<RestaurantTable> GetAssignedTables(int waiterId, DateTime date)
    {
        using var c = Db.Open();
        return c.Query<RestaurantTable>(@"
            SELECT t.id, t.number, t.seats, t.pos_x, t.pos_y, t.status::text AS status
            FROM shift_tables st
            JOIN shifts s ON s.id = st.shift_id
            JOIN restaurant_tables t ON t.id = st.table_id
            WHERE s.waiter_id = @waiterId AND s.work_date = @date
            ORDER BY t.number", new { waiterId, date = date.Date }).ToList();
    }

    /// <summary>Смена конкретного официанта на дату (или null).</summary>
    public Shift? GetForWaiter(int waiterId, DateTime date)
    {
        using var c = Db.Open();
        return c.QueryFirstOrDefault<Shift>(Select +
            "WHERE s.waiter_id = @waiterId AND s.work_date = @date",
            new { waiterId, date = date.Date });
    }

    /// <summary>Администратор планирует смену и прикрепляет столы.</summary>
    public int Plan(int waiterId, DateTime date, IEnumerable<int> tableIds)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        int id = c.ExecuteScalar<int>(@"
            INSERT INTO shifts(waiter_id, work_date, status) VALUES (@waiterId, @date, 'PLANNED')
            ON CONFLICT (waiter_id, work_date) DO UPDATE SET status = shifts.status
            RETURNING id", new { waiterId, date = date.Date }, tx);
        c.Execute("DELETE FROM shift_tables WHERE shift_id = @id", new { id }, tx);
        foreach (var tid in tableIds)
            c.Execute("INSERT INTO shift_tables(shift_id, table_id) VALUES (@id, @tid)", new { id, tid }, tx);
        tx.Commit();
        return id;
    }

    /// <summary>Официант открывает смену.</summary>
    public void Open(int shiftId)
    {
        using var c = Db.Open();
        c.Execute("UPDATE shifts SET status = 'OPEN', opened_at = now() WHERE id = @shiftId AND status = 'PLANNED'",
            new { shiftId });
    }

    /// <summary>Официант закрывает смену.</summary>
    public void Close(int shiftId)
    {
        using var c = Db.Open();
        c.Execute("UPDATE shifts SET status = 'CLOSED', closed_at = now() WHERE id = @shiftId AND status = 'OPEN'",
            new { shiftId });
    }
}
