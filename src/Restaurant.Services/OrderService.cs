using Restaurant.Core;
using Restaurant.Data;

namespace Restaurant.Services;

/// <summary>
/// Бизнес-логика заказов. Контроль склада и статусов реализован функциями БД,
/// сервис добавляет проверки уровня приложения и переходы статусов кухни.
/// </summary>
public class OrderService
{
    private readonly OrderRepository _repo = new();

    public List<Order> GetActiveForWaiter(int waiterId) => _repo.GetActiveForWaiter(waiterId);
    public List<Order> GetBillable() => _repo.GetBillable();
    public List<Order> GetKitchenQueue() => _repo.GetKitchenQueue();
    public Order? GetById(int id) => _repo.GetById(id);
    public List<OrderItem> GetItems(int orderId) => _repo.GetItems(orderId);

    /// <summary>Создаёт новый заказ для стола.</summary>
    public int Create(int tableId, int waiterId, int? reservationId) =>
        _repo.Create(tableId, waiterId, reservationId);

    /// <summary>Добавляет блюдо в заказ (контроль склада — в функции БД).</summary>
    public OperationResult AddItem(int orderId, int dishId, int qty)
    {
        string msg = _repo.AddItem(orderId, dishId, qty);
        return msg.StartsWith("Блюдо") ? OperationResult.Ok(msg) : OperationResult.Fail(msg);
    }

    /// <summary>Удаляет блюдо из заказа (возврат порций на склад — в функции БД).</summary>
    public OperationResult RemoveItem(int orderId, int dishId)
    {
        string msg = _repo.RemoveItem(orderId, dishId);
        return msg.StartsWith("Блюдо") ? OperationResult.Ok(msg) : OperationResult.Fail(msg);
    }

    /// <summary>Оформляет заказ (фиксирует дату и время).</summary>
    public OperationResult Place(int orderId)
    {
        string msg = _repo.Place(orderId);
        return msg.Contains("успешно") ? OperationResult.Ok(msg) : OperationResult.Fail(msg);
    }

    /// <summary>Отменяет заказ, если кухня ещё не приступила к готовке.</summary>
    public OperationResult Cancel(int orderId)
    {
        return _repo.TryCancel(orderId)
            ? OperationResult.Ok($"Заказ №{orderId} отменён.")
            : OperationResult.Fail("Отмена невозможна: заказ уже взят в готовку.");
    }

    /// <summary>Кухня берёт заказ в готовку.</summary>
    public OperationResult StartCooking(int orderId)
    {
        _repo.SetStatus(orderId, OrderStatus.COOKING);
        return OperationResult.Ok($"Заказ №{orderId} взят в готовку.");
    }

    /// <summary>Кухня отмечает заказ готовым к выдаче.</summary>
    public OperationResult MarkReady(int orderId)
    {
        _repo.SetStatus(orderId, OrderStatus.READY);
        return OperationResult.Ok($"Заказ №{orderId} готов к выдаче.");
    }
}
