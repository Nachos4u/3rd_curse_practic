using Restaurant.Core;
using Restaurant.Data;

namespace Restaurant.Services;

/// <summary>Бизнес-логика бронирования столиков.</summary>
public class ReservationService
{
    private readonly ReservationRepository _repo = new();

    /// <summary>Время открытия ресторана.</summary>
    public static readonly TimeOnly OpenTime = new(9, 0);
    /// <summary>Время закрытия ресторана.</summary>
    public static readonly TimeOnly CloseTime = new(23, 0);

    public List<Reservation> GetByDate(DateTime date) => _repo.GetByDate(date);
    public List<Reservation> GetUpcoming() => _repo.GetUpcoming();

    /// <summary>
    /// Создаёт бронь с контролем правил:
    /// рабочее время 9:00–23:00, число гостей &gt; 0, достаточная вместимость столов,
    /// отсутствие пересечений с другими активными бронями.
    /// </summary>
    public OperationResult Create(int? clientId, int guests, DateTime date, TimeOnly start, TimeOnly end, List<int> tableIds)
    {
        if (tableIds.Count == 0)
            return OperationResult.Fail("Выберите хотя бы один столик.");
        if (guests <= 0)
            return OperationResult.Fail("Число гостей должно быть больше нуля.");
        if (date.Date < DateTime.Today)
            return OperationResult.Fail("Нельзя бронировать на прошедшую дату.");
        if (end <= start)
            return OperationResult.Fail("Время окончания должно быть позже времени начала.");
        if (start < OpenTime || end > CloseTime)
            return OperationResult.Fail("Ресторан работает с 9:00 до 23:00.");

        int capacity = _repo.SeatsCapacity(tableIds);
        if (guests > capacity)
            return OperationResult.Fail(
                $"Недостаточно мест: выбрано столов на {capacity} мест, а гостей {guests}. Добавьте столы.");

        if (!_repo.AreTablesFree(date, start, end, tableIds))
            return OperationResult.Fail("Один или несколько выбранных столов уже заняты на это время.");

        var r = new Reservation
        {
            ClientId = clientId, GuestsCount = (short)guests,
            ReserveDate = date.Date, StartTime = start, EndTime = end
        };
        int id = _repo.Create(r, tableIds);
        return OperationResult.Ok($"Бронь №{id} успешно создана.");
    }

    /// <summary>Отменяет бронь.</summary>
    public OperationResult Cancel(int reservationId)
    {
        _repo.Cancel(reservationId);
        return OperationResult.Ok($"Бронь №{reservationId} отменена.");
    }
}
