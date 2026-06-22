using Dapper;
using Restaurant.Core;

namespace Restaurant.Data;

/// <summary>Доступ к счетам и чекам.</summary>
public class BillingRepository
{
    /// <summary>
    /// Формирует счёт по выданным/готовым заказам стола и возвращает его идентификатор.
    /// Итоговая сумма пересчитывается триггером БД при добавлении заказов в счёт.
    /// </summary>
    public int CreateBill(int tableId, int? reservationId, IEnumerable<int> orderIds)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        int id = c.ExecuteScalar<int>(@"
            INSERT INTO bills(reservation_id, table_id, status) VALUES (@reservationId, @tableId, 'OPEN')
            RETURNING id", new { reservationId, tableId }, tx);
        foreach (var oid in orderIds)
            c.Execute("INSERT INTO bill_orders(bill_id, order_id) VALUES (@id, @oid)", new { id, oid }, tx);
        tx.Commit();
        return id;
    }

    /// <summary>Открытые (неоплаченные) счета.</summary>
    public List<Bill> GetOpenBills()
    {
        using var c = Db.Open();
        return c.Query<Bill>(@"
            SELECT b.id, b.reservation_id, b.table_id, t.number AS table_number,
                   b.status::text AS status, b.total_amount, b.created_at, b.paid_at
            FROM bills b JOIN restaurant_tables t ON t.id = b.table_id
            WHERE b.status = 'OPEN' ORDER BY b.created_at").ToList();
    }

    /// <summary>Сумма счёта.</summary>
    public decimal GetBillTotal(int billId)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<decimal>("SELECT total_amount FROM bills WHERE id = @billId", new { billId });
    }

    /// <summary>
    /// Оплачивает счёт и формирует чек (в одной транзакции).
    /// Помечает связанные заказы как «Выдан клиенту». Возвращает чек.
    /// </summary>
    public Receipt Pay(int billId, int waiterId, string paymentMethod)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        decimal total = c.ExecuteScalar<decimal>("SELECT total_amount FROM bills WHERE id = @billId", new { billId }, tx);
        c.Execute("UPDATE bills SET status = 'PAID', paid_at = now() WHERE id = @billId", new { billId }, tx);
        c.Execute(@"UPDATE orders SET status = 'SERVED'
                    WHERE id IN (SELECT order_id FROM bill_orders WHERE bill_id = @billId)", new { billId }, tx);
        int receiptId = c.ExecuteScalar<int>(@"
            INSERT INTO receipts(bill_id, waiter_id, total, payment_method)
            VALUES (@billId, @waiterId, @total, @paymentMethod) RETURNING id",
            new { billId, waiterId, total, paymentMethod }, tx);
        tx.Commit();

        return new Receipt
        {
            Id = receiptId, BillId = billId, WaiterId = waiterId,
            Total = total, PaymentMethod = paymentMethod, PaidAt = DateTime.Now
        };
    }
}
