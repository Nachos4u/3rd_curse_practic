using Restaurant.Core;
using Xunit;

namespace Restaurant.Tests;

/// <summary>Модульные тесты хеширования паролей (не требуют БД).</summary>
public class PasswordHasherTests
{
    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        string hash = PasswordHasher.Hash("Secret123");
        Assert.True(PasswordHasher.Verify("Secret123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        string hash = PasswordHasher.Hash("Secret123");
        Assert.False(PasswordHasher.Verify("secret123", hash));
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        // Разные соли => разные хеши, но оба проверяются успешно.
        string h1 = PasswordHasher.Hash("Pass");
        string h2 = PasswordHasher.Hash("Pass");
        Assert.NotEqual(h1, h2);
        Assert.True(PasswordHasher.Verify("Pass", h1));
        Assert.True(PasswordHasher.Verify("Pass", h2));
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse()
    {
        Assert.False(PasswordHasher.Verify("Pass", "not-a-valid-hash"));
    }

    [Fact]
    public void Hash_HasExpectedFormat()
    {
        string hash = PasswordHasher.Hash("abc");
        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2", parts[0]);
        Assert.Equal("100000", parts[1]);
    }
}

/// <summary>Модульные тесты подписей статусов.</summary>
public class StatusNamesTests
{
    [Theory]
    [InlineData(OrderStatus.COMPOSING, "Составление")]
    [InlineData(OrderStatus.PLACED, "Оформлен")]
    [InlineData(OrderStatus.SERVED, "Выдан клиенту")]
    public void Order_ReturnsRussianCaption(OrderStatus s, string expected)
    {
        Assert.Equal(expected, StatusNames.Order(s));
    }

    [Fact]
    public void User_ShortName_FormatsInitials()
    {
        var u = new User { LastName = "Иванов", FirstName = "Иван", MiddleName = "Иванович" };
        Assert.Equal("Иванов И. И.", u.ShortName);
    }
}
