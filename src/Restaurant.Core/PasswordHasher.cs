using System.Security.Cryptography;

namespace Restaurant.Core;

/// <summary>
/// Хеширование и проверка паролей по алгоритму PBKDF2-SHA256.
/// Формат строки: <c>PBKDF2$&lt;итерации&gt;$&lt;соль_base64&gt;$&lt;хеш_base64&gt;</c>.
/// Соль генерируется случайно для каждого пароля, что исключает хранение
/// паролей в открытом виде и совпадение хешей у одинаковых паролей.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;   // байт
    private const int KeySize = 32;    // байт

    /// <summary>Вычисляет хеш для нового пароля.</summary>
    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Pbkdf2(password, salt, Iterations);
        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    /// <summary>Проверяет соответствие пароля сохранённому хешу.</summary>
    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2")
            return false;
        if (!int.TryParse(parts[1], out int iterations))
            return false;

        byte[] salt = Convert.FromBase64String(parts[2]);
        byte[] expected = Convert.FromBase64String(parts[3]);
        byte[] actual = Pbkdf2(password, salt, iterations);

        // Сравнение за постоянное время — защита от атак по времени.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);
}
