using Restaurant.Core;
using Restaurant.Data;

namespace Restaurant.Services;

/// <summary>Регистрация и авторизация пользователей.</summary>
public class AuthService
{
    private readonly UserRepository _users = new();

    /// <summary>
    /// Выполняет вход: проверяет наличие пользователя, активность учётной записи
    /// и соответствие пароля сохранённому хешу.
    /// </summary>
    public OperationResult Login(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            return OperationResult.Fail("Введите логин и пароль.");

        var user = _users.FindByLogin(login.Trim());
        if (user is null)
            return OperationResult.Fail("Пользователь с таким логином не найден.");
        if (!user.IsActive)
            return OperationResult.Fail("Учётная запись отключена.");
        if (!PasswordHasher.Verify(password, user.PasswordHash))
            return OperationResult.Fail("Неверный пароль.");

        AppSession.Current = user;
        return OperationResult.Ok($"Добро пожаловать, {user.ShortName}!");
    }

    /// <summary>
    /// Регистрирует нового клиента (самостоятельная регистрация).
    /// Пароль сохраняется только в виде хеша.
    /// </summary>
    public OperationResult RegisterClient(string login, string password, string lastName, string firstName, string? phone)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Length < 3)
            return OperationResult.Fail("Логин должен содержать не менее 3 символов.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return OperationResult.Fail("Пароль должен содержать не менее 6 символов.");
        if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            return OperationResult.Fail("Укажите фамилию и имя.");
        if (_users.LoginExists(login.Trim()))
            return OperationResult.Fail("Логин уже занят, выберите другой.");

        var roles = _users.GetRoles();
        var clientRole = roles.First(r => r.Code == RoleCode.Client);
        _users.Create(new User
        {
            Login = login.Trim(),
            PasswordHash = PasswordHasher.Hash(password),
            RoleId = clientRole.Id,
            LastName = lastName.Trim(),
            FirstName = firstName.Trim(),
            Phone = phone
        });
        return OperationResult.Ok("Регистрация выполнена. Теперь вы можете войти.");
    }
}
