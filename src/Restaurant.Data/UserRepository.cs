using Dapper;
using Restaurant.Core;

namespace Restaurant.Data;

/// <summary>Доступ к пользователям и ролям.</summary>
public class UserRepository
{
    private const string Select = @"
        SELECT u.id, u.login, u.password_hash, u.role_id, r.code AS role_code,
               u.last_name, u.first_name, u.middle_name, u.phone, u.is_active
        FROM users u JOIN roles r ON r.id = u.role_id ";

    /// <summary>Возвращает пользователя по логину или null.</summary>
    public User? FindByLogin(string login)
    {
        using var c = Db.Open();
        return c.QueryFirstOrDefault<User>(Select + "WHERE u.login = @login", new { login });
    }

    /// <summary>Список всех пользователей.</summary>
    public List<User> GetAll()
    {
        using var c = Db.Open();
        return c.Query<User>(Select + "ORDER BY r.code, u.last_name").ToList();
    }

    /// <summary>Список официантов.</summary>
    public List<User> GetWaiters()
    {
        using var c = Db.Open();
        return c.Query<User>(Select + "WHERE r.code = 'WAITER' ORDER BY u.last_name").ToList();
    }

    /// <summary>Список ролей.</summary>
    public List<Role> GetRoles()
    {
        using var c = Db.Open();
        return c.Query<Role>("SELECT id, code, name FROM roles ORDER BY id").ToList();
    }

    /// <summary>Создаёт пользователя, возвращает его идентификатор.</summary>
    public int Create(User u)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<int>(@"
            INSERT INTO users(login, password_hash, role_id, last_name, first_name, middle_name, phone)
            VALUES (@Login, @PasswordHash, @RoleId, @LastName, @FirstName, @MiddleName, @Phone)
            RETURNING id", u);
    }

    /// <summary>Включает/отключает учётную запись.</summary>
    public void SetActive(int id, bool active)
    {
        using var c = Db.Open();
        c.Execute("UPDATE users SET is_active = @active WHERE id = @id", new { id, active });
    }

    /// <summary>Проверяет уникальность логина.</summary>
    public bool LoginExists(string login)
    {
        using var c = Db.Open();
        return c.ExecuteScalar<bool>("SELECT EXISTS(SELECT 1 FROM users WHERE login = @login)", new { login });
    }
}
