using Restaurant.Core;
using Restaurant.Data;

namespace Restaurant.Services;

/// <summary>Бизнес-логика смен официантов.</summary>
public class ShiftService
{
    private readonly ShiftRepository _repo = new();

    public List<Shift> GetByDate(DateTime date) => _repo.GetByDate(date);
    public Shift? GetForWaiter(int waiterId, DateTime date) => _repo.GetForWaiter(waiterId, date);

    /// <summary>Администратор планирует смену официанта и прикрепляет столы.</summary>
    public OperationResult Plan(int waiterId, DateTime date, List<int> tableIds)
    {
        if (tableIds.Count == 0)
            return OperationResult.Fail("Прикрепите к смене хотя бы один стол.");
        _repo.Plan(waiterId, date, tableIds);
        return OperationResult.Ok("Смена запланирована.");
    }

    /// <summary>Официант открывает смену.</summary>
    public OperationResult Open(int waiterId, DateTime date)
    {
        var shift = _repo.GetForWaiter(waiterId, date);
        if (shift is null)
            return OperationResult.Fail("На сегодня смена не запланирована администратором.");
        if (shift.Status == ShiftStatus.OPEN)
            return OperationResult.Fail("Смена уже открыта.");
        if (shift.Status == ShiftStatus.CLOSED)
            return OperationResult.Fail("Смена уже закрыта сегодня.");
        _repo.Open(shift.Id);
        return OperationResult.Ok("Смена открыта.");
    }

    /// <summary>Официант закрывает смену.</summary>
    public OperationResult Close(int waiterId, DateTime date)
    {
        var shift = _repo.GetForWaiter(waiterId, date);
        if (shift is null || shift.Status != ShiftStatus.OPEN)
            return OperationResult.Fail("Нет открытой смены для закрытия.");
        _repo.Close(shift.Id);
        return OperationResult.Ok("Смена закрыта.");
    }
}
