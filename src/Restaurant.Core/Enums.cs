namespace Restaurant.Core;

/// <summary>Коды ролей пользователей (соответствуют roles.code в БД).</summary>
public static class RoleCode
{
    public const string Admin = "ADMIN";
    public const string Waiter = "WAITER";
    public const string Kitchen = "KITCHEN";
    public const string Client = "CLIENT";
}

/// <summary>Статусы заказа (соответствуют типу order_status в БД).</summary>
public enum OrderStatus
{
    /// <summary>Составление — состав заказа можно редактировать.</summary>
    COMPOSING,
    /// <summary>Оформлен — состав зафиксирован.</summary>
    PLACED,
    /// <summary>Отменён.</summary>
    CANCELLED,
    /// <summary>Готовится (статус кухни).</summary>
    COOKING,
    /// <summary>Готов к выдаче / принят на выдачу.</summary>
    READY,
    /// <summary>Выдан клиенту.</summary>
    SERVED
}

/// <summary>Статусы столика.</summary>
public enum TableStatus { FREE, RESERVED, OCCUPIED }

/// <summary>Статусы брони.</summary>
public enum ReserveStatus { ACTIVE, CANCELLED, COMPLETED }

/// <summary>Статусы смены официанта.</summary>
public enum ShiftStatus { PLANNED, OPEN, CLOSED }

/// <summary>Статусы счёта.</summary>
public enum BillStatus { OPEN, PAID, CANCELLED }

/// <summary>Человекочитаемые подписи статусов для интерфейса.</summary>
public static class StatusNames
{
    public static string Order(OrderStatus s) => s switch
    {
        OrderStatus.COMPOSING => "Составление",
        OrderStatus.PLACED    => "Оформлен",
        OrderStatus.CANCELLED => "Отменён",
        OrderStatus.COOKING   => "Готовится",
        OrderStatus.READY     => "Готов к выдаче",
        OrderStatus.SERVED    => "Выдан клиенту",
        _ => s.ToString()
    };

    public static string Reserve(ReserveStatus s) => s switch
    {
        ReserveStatus.ACTIVE    => "Активна",
        ReserveStatus.CANCELLED => "Отменена",
        ReserveStatus.COMPLETED => "Завершена",
        _ => s.ToString()
    };
}
