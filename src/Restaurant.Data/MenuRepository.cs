using Dapper;
using Restaurant.Core;

namespace Restaurant.Data;

/// <summary>Доступ к меню: категории, блюда, склад, акции.</summary>
public class MenuRepository
{
    /// <summary>Список категорий.</summary>
    public List<Category> GetCategories()
    {
        using var c = Db.Open();
        return c.Query<Category>("SELECT id, name FROM categories ORDER BY name").ToList();
    }

    /// <summary>Список блюд с остатком на складе.</summary>
    public List<Dish> GetDishes(bool onlyActive = false)
    {
        using var c = Db.Open();
        var where = onlyActive ? "WHERE d.is_active " : "";
        return c.Query<Dish>($@"
            SELECT d.id, d.category_id, c.name AS category_name, d.name,
                   d.base_price, d.is_active, COALESCE(s.quantity, 0) AS quantity
            FROM dishes d
            JOIN categories c ON c.id = d.category_id
            LEFT JOIN stock s ON s.dish_id = d.id
            {where}
            ORDER BY c.name, d.name").ToList();
    }

    /// <summary>Создаёт блюдо и заводит складскую позицию.</summary>
    public int CreateDish(Dish d, int initialStock)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        int id = c.ExecuteScalar<int>(@"
            INSERT INTO dishes(category_id, name, base_price, is_active)
            VALUES (@CategoryId, @Name, @BasePrice, TRUE) RETURNING id", d, tx);
        c.Execute("INSERT INTO stock(dish_id, quantity) VALUES (@id, @q)",
            new { id, q = initialStock }, tx);
        tx.Commit();
        return id;
    }

    /// <summary>Пополняет склад на указанное количество порций.</summary>
    public void AddStock(int dishId, int quantity)
    {
        using var c = Db.Open();
        using var tx = c.BeginTransaction();
        c.Execute("UPDATE stock SET quantity = quantity + @quantity WHERE dish_id = @dishId",
            new { dishId, quantity }, tx);
        c.Execute("INSERT INTO stock_movements(dish_id, change, reason) VALUES (@dishId, @quantity, 'Пополнение склада')",
            new { dishId, quantity }, tx);
        tx.Commit();
    }

    /// <summary>Изменяет цену блюда.</summary>
    public void UpdatePrice(int dishId, decimal price)
    {
        using var c = Db.Open();
        c.Execute("UPDATE dishes SET base_price = @price WHERE id = @dishId", new { dishId, price });
    }
}
