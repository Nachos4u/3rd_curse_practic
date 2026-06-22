using Restaurant.Core;
using Restaurant.Data;
using Restaurant.Services;
using Xunit;

namespace Restaurant.Tests;

/// <summary>
/// Интеграционные тесты против базы restaurant_db с демонстрационными данными.
/// Тесты не изменяют данные (проверяют чтение и валидацию до записи),
/// поэтому безопасны для повторного запуска.
/// Требуют доступной БД; при её отсутствии тесты пропускаются.
/// </summary>
public class IntegrationTests
{
    private static bool DbAvailable() => Db.TestConnection(out _);

    [SkippableFact]
    public void Login_WithSeededAdmin_Succeeds()
    {
        Skip.IfNot(DbAvailable(), "БД недоступна");
        var auth = new AuthService();
        var r = auth.Login("admin", "admin123");
        Assert.True(r.Success, r.Message);
        Assert.Equal(RoleCode.Admin, AppSession.Current?.RoleCode);
    }

    [SkippableFact]
    public void Login_WrongPassword_Fails()
    {
        Skip.IfNot(DbAvailable(), "БД недоступна");
        var auth = new AuthService();
        var r = auth.Login("admin", "wrong-password");
        Assert.False(r.Success);
    }

    [SkippableFact]
    public void Reservation_GuestsExceedCapacity_Fails()
    {
        Skip.IfNot(DbAvailable(), "БД недоступна");
        var tables = new TableRepository().GetAll();
        var smallTable = tables.First(t => t.Seats <= 2);

        var svc = new ReservationService();
        // 10 гостей за один стол на 2 места — должно быть отклонено.
        var r = svc.Create(null, 10, DateTime.Today.AddDays(30),
            new TimeOnly(12, 0), new TimeOnly(14, 0),
            new List<int> { smallTable.Id });
        Assert.False(r.Success);
        Assert.Contains("мест", r.Message);
    }

    [SkippableFact]
    public void Reservation_OutsideWorkingHours_Fails()
    {
        Skip.IfNot(DbAvailable(), "БД недоступна");
        var tableId = new TableRepository().GetAll().First().Id;
        var svc = new ReservationService();
        var r = svc.Create(null, 2, DateTime.Today.AddDays(30),
            new TimeOnly(7, 0), new TimeOnly(8, 0),
            new List<int> { tableId });
        Assert.False(r.Success);
        Assert.Contains("9:00", r.Message);
    }

    [SkippableFact]
    public void DishSalesReport_ReturnsRows()
    {
        Skip.IfNot(DbAvailable(), "БД недоступна");
        var report = new ReportService().DishSales(2026, 5, 2026, 6);
        Assert.True(report.Rows.Count > 0, "Отчёт о продажах не должен быть пустым на демо-данных.");
    }

    [SkippableFact]
    public void FreeTablesReport_ReturnsRows()
    {
        Skip.IfNot(DbAvailable(), "БД недоступна");
        var report = new ReportService().FreeTables(DateTime.Today.AddDays(60), new TimeOnly(15, 0));
        Assert.True(report.Rows.Count > 0);
    }
}
