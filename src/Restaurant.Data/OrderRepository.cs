using Dapper;
using Restaurant.Core;

namespace Restaurant.Data;

/// <summary>
/// Доступ к заказам. Операции, требующие контроля склада и статусов,
/// выполняются через функции БД (fn_add_order_item, fn_remove_order_item, fn_place_order).
/// </summary>
public class OrderRepository
{
    // Заказы читаются через представление v_orders: стол, официант (по закреплению
    // в смене) и сумма выводятся в нём, в таблице orders этих данных нет.
    private const string Select = @"
        SELECT id, table_id, table_number, waiter_name,
               status::text AS status, created_at, placed_at,
               order_total, items_count
        FROM v_orders ";

    /// <summary>Заказы официанта (активные — не выданные/не отменённые).</summary>
    public List<Order> GetActiveForWaiter(int waiterId)
    {
        using var c = Db.Open();
        return c.Query<Order>(Select +
            "WHERE waiter_id = @waiterId AND status NOT IN ('SERVED','CANCELLED') ORDER BY created_at",
            new { waiterId }).ToList();
    }

    /// <summary>Очередь кухни (оформленные и готовящиеся заказы).</summary>
    public List<Order> GetKitchenQueue()
    {
        using var c = Db.Open();
        return c.Query<Order>(Select +
            "WHERE status IN ('PLACED','COOKING') ORDER BY placed_at").ToList();
    }

    /// <summary>
    /// Заказы, готовые к включению в счёт: статус «Готов к выдаче»,
    /// ещё не привязанные ни к одному счёту.
    /// </summary>
    public List<Order> GetBillable()
    {
        using var c = Db.Open();
        return c.Query<Order>(Select +
            "WHERE status = 'READY' AND id NOT IN (SELECT order_id FROM bill_orders) " +
            "ORDER BY table_number").ToList();
    }

    /// <summary>Один заказ по идентификатору.</summary>
    public Order? GetById(int orderId)
    {
        using var c = Db.Open();
        return c.QueryFirstOrDefault<Order>(Select + "WHERE id = @orderId", new { orderId });
    }

    /// <summary>Позиции заказа с суммами.</summary>
    public List<OrderItem> GetItems(int orderId)
    {
        using var c = Db.Open();
        return c.Query<OrderItem>(
            "SELECT id, order_id, dish_name, category_name, quantity, unit_price, discount_percent, line_total " +
            "FROM v_order_items WHERE order_id = @orderId ORDER BY dish_name", new { orderId }).ToList();
    }

    /// <summary>Создаёт новый пустой заказ для стола в статусе «Составление».</summary>
    public int Create(int tableId)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<int>(
            "INSERT INTO orders(table_id, status) VALUES (@tableId, 'COMPOSING') RETURNING id",
            new { tableId });
    }

    /// <summary>Добавляет блюдо в заказ через функцию БД. Возвращает текст-сообщение.</summary>
    public string AddItem(int orderId, int dishId, int qty)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<string>("SELECT fn_add_order_item(@orderId, @dishId, @qty)",
            new { orderId, dishId, qty }) ?? "";
    }

    /// <summary>Удаляет блюдо из заказа через функцию БД. Возвращает текст-сообщение.</summary>
    public string RemoveItem(int orderId, int dishId)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<string>("SELECT fn_remove_order_item(@orderId, @dishId)",
            new { orderId, dishId }) ?? "";
    }

    /// <summary>Оформляет заказ через функцию БД. Возвращает текст-сообщение.</summary>
    public string Place(int orderId)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<string>("SELECT fn_place_order(@orderId)", new { orderId }) ?? "";
    }

    /// <summary>Меняет статус заказа (используется официантом и кухней).</summary>
    public void SetStatus(int orderId, OrderStatus status)
    {
        using var c = Db.Open();
        c.Execute("UPDATE orders SET status = @status::order_status WHERE id = @orderId",
            new { orderId, status = status.ToString() });
    }

    /// <summary>
    /// Отменяет заказ, если кухня ещё не приступила к готовке.
    /// Возвращает false, если отмена уже невозможна.
    /// </summary>
    public bool TryCancel(int orderId)
    {
        using var c = Db.Open();
        int rows = c.Execute(
            "UPDATE orders SET status = 'CANCELLED' WHERE id = @orderId AND status IN ('COMPOSING','PLACED')",
            new { orderId });
        return rows > 0;
    }

    /// <summary>Идентификатор «дишей» по блюду в составляемом заказе (для удаления по строке).</summary>
    public int GetDishIdByItem(int orderItemId)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<int>("SELECT dish_id FROM order_items WHERE id = @orderItemId", new { orderItemId });
    }
}
