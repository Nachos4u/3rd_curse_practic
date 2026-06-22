using Restaurant.Core;
using Restaurant.Data;

namespace Restaurant.Services;

/// <summary>Бизнес-логика счетов и оплаты.</summary>
public class BillingService
{
    private readonly BillingRepository _repo = new();

    public List<Bill> GetOpenBills() => _repo.GetOpenBills();
    public decimal GetBillTotal(int billId) => _repo.GetBillTotal(billId);

    /// <summary>Формирует счёт из готовых/выданных заказов стола.</summary>
    public OperationResult CreateBill(int tableId, int? reservationId, List<int> orderIds)
    {
        if (orderIds.Count == 0)
            return OperationResult.Fail("Нет заказов для формирования счёта.");
        int id = _repo.CreateBill(tableId, reservationId, orderIds);
        decimal total = _repo.GetBillTotal(id);
        return OperationResult.Ok($"Счёт №{id} сформирован на сумму {total:N2} руб.");
    }

    /// <summary>
    /// Оплачивает счёт и формирует чек. Возвращает текст чека для печати/отображения.
    /// </summary>
    public (OperationResult result, Receipt? receipt) Pay(int billId, int waiterId, string method)
    {
        var receipt = _repo.Pay(billId, waiterId, method);
        var ok = OperationResult.Ok($"Счёт №{billId} оплачен. Сформирован чек №{receipt.Id}.");
        return (ok, receipt);
    }
}
