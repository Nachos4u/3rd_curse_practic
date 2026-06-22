using Restaurant.Core;

namespace Restaurant.Services;

/// <summary>Сведения о текущем сеансе работы (авторизованный пользователь).</summary>
public static class AppSession
{
    /// <summary>Текущий авторизованный пользователь.</summary>
    public static User? Current { get; set; }

    public static bool IsInRole(string roleCode) => Current?.RoleCode == roleCode;

    /// <summary>Завершает сеанс.</summary>
    public static void Clear() => Current = null;
}
