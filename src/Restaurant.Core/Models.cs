namespace Restaurant.Core;

/// <summary>Роль пользователя.</summary>
public class Role
{
    public short Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>Пользователь системы (любая роль).</summary>
public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public short RoleId { get; set; }
    public string RoleCode { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? MiddleName { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }

    /// <summary>ФИО в формате «Фамилия И. О.».</summary>
    public string ShortName
    {
        get
        {
            var fi = FirstName.Length > 0 ? $" {FirstName[0]}." : "";
            var ot = !string.IsNullOrEmpty(MiddleName) ? $" {MiddleName[0]}." : "";
            return $"{LastName}{fi}{ot}";
        }
    }

    public string FullName =>
        string.Join(" ", new[] { LastName, FirstName, MiddleName }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Столик в зале.</summary>
public class RestaurantTable
{
    public int Id { get; set; }
    public int Number { get; set; }
    public short Seats { get; set; }
    public int PosX { get; set; }
    public int PosY { get; set; }
    public TableStatus Status { get; set; }
}

/// <summary>Бронь столиков.</summary>
public class Reservation
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public short GuestsCount { get; set; }
    public DateTime ReserveDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ReserveStatus Status { get; set; }
    public string? ClientName { get; set; }
    public string? Tables { get; set; }   // перечень номеров столов через запятую
}

/// <summary>Категория меню.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Блюдо меню (с остатком на складе).</summary>
public class Dish
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; }
    public int Quantity { get; set; }   // остаток на складе
}

/// <summary>Смена официанта.</summary>
public class Shift
{
    public int Id { get; set; }
    public int WaiterId { get; set; }
    public string WaiterName { get; set; } = "";
    public DateTime WorkDate { get; set; }
    public ShiftStatus Status { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Tables { get; set; }
}

/// <summary>Заказ.</summary>
public class Order
{
    public int Id { get; set; }
    public int? ReservationId { get; set; }
    public int TableId { get; set; }
    public int TableNumber { get; set; }
    public int WaiterId { get; set; }
    public string WaiterName { get; set; } = "";
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PlacedAt { get; set; }
    public decimal OrderTotal { get; set; }
    public int ItemsCount { get; set; }
}

/// <summary>Позиция заказа.</summary>
public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string DishName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>Счёт клиента (агрегирует заказы).</summary>
public class Bill
{
    public int Id { get; set; }
    public int? ReservationId { get; set; }
    public int TableId { get; set; }
    public int TableNumber { get; set; }
    public BillStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

/// <summary>Чек об оплате.</summary>
public class Receipt
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int WaiterId { get; set; }
    public string WaiterName { get; set; } = "";
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "CASH";
    public DateTime PaidAt { get; set; }
}
