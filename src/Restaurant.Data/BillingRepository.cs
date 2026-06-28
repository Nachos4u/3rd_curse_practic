using Dapper;
using Restaurant.Core;

namespace Restaurant.Data;

/// <summary>Доступ к счетам и чекам.</summary>
public class BillingRepository
{
    /// <summary>
    /// Формирует счёт из переданных заказов и возвращает его идентификатор.
    /// Стол, бронь и сумма счёта не хранятся, а выводятся из его заказов
    /// (представления v_bills / v_bill_totals) — без дублирования данных.
    /// </summary>
    public int CreateBill(IEnumerable<int> orderIds)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        int id = c.ExecuteScalar<int>(
            "INSERT INTO bills(status) VALUES ('OPEN') RETURNING id", transaction: tx);
        foreach (var oid in orderIds)
            c.Execute("INSERT INTO bill_orders(bill_id, order_id) VALUES (@id, @oid)", new { id, oid }, tx);
        tx.Commit();
        return id;
    }

    /// <summary>Открытые (неоплаченные) счета с выведенными столом и суммой.</summary>
    public List<Bill> GetOpenBills()
    {
        using var c = Db.Open();
        return c.Query<Bill>(@"
            SELECT id, table_id, table_number, status::text AS status,
                   total AS total_amount, created_at, paid_at
            FROM v_bills
            WHERE status = 'OPEN' ORDER BY created_at").ToList();
    }

    /// <summary>Сумма счёта (вычисляется представлением v_bill_totals).</summary>
    public decimal GetBillTotal(int billId)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<decimal>("SELECT total FROM v_bill_totals WHERE bill_id = @billId", new { billId });
    }

    /// <summary>
    /// Оплачивает счёт и формирует чек (в одной транзакции).
    /// Помечает связанные заказы как «Выдан клиенту». Возвращает чек.
    /// </summary>
    public Receipt Pay(int billId, string paymentMethod)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        decimal total = c.ExecuteScalar<decimal>("SELECT total FROM v_bill_totals WHERE bill_id = @billId", new { billId }, tx);
        c.Execute("UPDATE bills SET status = 'PAID', paid_at = now() WHERE id = @billId", new { billId }, tx);
        c.Execute(@"UPDATE orders SET status = 'SERVED'
                    WHERE id IN (SELECT order_id FROM bill_orders WHERE bill_id = @billId)", new { billId }, tx);
        int receiptId = c.ExecuteScalar<int>(@"
            INSERT INTO receipts(bill_id, total, payment_method)
            VALUES (@billId, @total, @paymentMethod) RETURNING id",
            new { billId, total, paymentMethod }, tx);
        tx.Commit();

        return new Receipt
        {
            Id = receiptId, BillId = billId,
            Total = total, PaymentMethod = paymentMethod, PaidAt = DateTime.Now
        };
    }
}
