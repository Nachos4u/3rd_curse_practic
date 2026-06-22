using System.Data;
using Restaurant.Core;
using Restaurant.Data;

namespace Restaurant.Services;

/// <summary>Сервис столиков и схемы зала.</summary>
public class TableService
{
    private readonly TableRepository _repo = new();

    public List<RestaurantTable> GetAll() => _repo.GetAll();

    /// <summary>Номера столов, занятых бронью на указанный момент времени.</summary>
    public HashSet<int> GetBusyTableNumbers(DateTime date, TimeOnly time) =>
        _repo.GetBusyTableNumbers(date, time);
}

/// <summary>Сервис меню и склада.</summary>
public class MenuService
{
    private readonly MenuRepository _repo = new();

    public List<Category> GetCategories() => _repo.GetCategories();
    public List<Dish> GetDishes(bool onlyActive = false) => _repo.GetDishes(onlyActive);

    public OperationResult CreateDish(int categoryId, string name, decimal price, int stock)
    {
        if (string.IsNullOrWhiteSpace(name))
            return OperationResult.Fail("Укажите название блюда.");
        if (price < 0)
            return OperationResult.Fail("Цена не может быть отрицательной.");
        if (stock < 0)
            return OperationResult.Fail("Количество на складе не может быть отрицательным.");
        _repo.CreateDish(new Dish { CategoryId = categoryId, Name = name.Trim(), BasePrice = price }, stock);
        return OperationResult.Ok($"Блюдо «{name}» добавлено в меню.");
    }

    public OperationResult AddStock(int dishId, int quantity)
    {
        if (quantity <= 0)
            return OperationResult.Fail("Количество пополнения должно быть больше нуля.");
        _repo.AddStock(dishId, quantity);
        return OperationResult.Ok("Склад пополнен.");
    }

    public OperationResult UpdatePrice(int dishId, decimal price)
    {
        if (price < 0)
            return OperationResult.Fail("Цена не может быть отрицательной.");
        _repo.UpdatePrice(dishId, price);
        return OperationResult.Ok("Цена обновлена.");
    }
}

/// <summary>Сервис отчётов «Статистика».</summary>
public class ReportService
{
    private readonly ReportRepository _repo = new();

    public DataTable DishSales(int y1, int m1, int y2, int m2) => _repo.DishSales(y1, m1, y2, m2);
    public DataTable TableBookings(int year, int month) => _repo.TableBookings(year, month);
    public DataTable WaiterStats(int y1, int m1, int y2, int m2) => _repo.WaiterStats(y1, m1, y2, m2);
    public DataTable FreeTables(DateTime date, TimeOnly time) => _repo.FreeTables(date, time);
    public DataTable TablesOccupancy(DateTime date) => _repo.TablesOccupancy(date);
    public DataTable HourlyOccupancy(DateTime date) => _repo.HourlyOccupancy(date);
}

/// <summary>Сервис администрирования пользователей.</summary>
public class UserService
{
    private readonly UserRepository _repo = new();

    public List<User> GetAll() => _repo.GetAll();
    public List<User> GetWaiters() => _repo.GetWaiters();
    public List<Role> GetRoles() => _repo.GetRoles();

    /// <summary>Создаёт пользователя с указанной ролью (действие администратора).</summary>
    public OperationResult Create(string login, string password, short roleId,
        string lastName, string firstName, string? middleName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Length < 3)
            return OperationResult.Fail("Логин должен содержать не менее 3 символов.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return OperationResult.Fail("Пароль должен содержать не менее 6 символов.");
        if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            return OperationResult.Fail("Укажите фамилию и имя.");
        if (_repo.LoginExists(login.Trim()))
            return OperationResult.Fail("Логин уже занят.");

        _repo.Create(new User
        {
            Login = login.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            RoleId = roleId,
            LastName = lastName.Trim(),
            FirstName = firstName.Trim(),
            MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim(),
            Phone = phone
        });
        return OperationResult.Ok($"Пользователь «{login}» создан.");
    }

    public OperationResult SetActive(int id, bool active)
    {
        _repo.SetActive(id, active);
        return OperationResult.Ok(active ? "Учётная запись включена." : "Учётная запись отключена.");
    }
}
