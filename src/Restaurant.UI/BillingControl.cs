using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>
/// Счета и оплата: формирование счёта по готовым заказам стола,
/// оплата открытых счетов и формирование чека.
/// </summary>
public class BillingControl : UserControl, IReloadable
{
    private readonly OrderService _orders = new();
    private readonly BillingService _billing = new();

    private readonly DataGridView _billable = Ui.Grid();
    private readonly DataGridView _openBills = Ui.Grid();

    public BillingControl()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
        var makeBill = Ui.Btn("Сформировать счёт по столу", 230);
        makeBill.Click += (_, _) => CreateBill();
        var pay = Ui.Btn("Оплатить счёт", 150);
        pay.Click += (_, _) => Pay();
        var refresh = Ui.Btn("Обновить", 110); refresh.BackColor = Color.Gray;
        refresh.Click += (_, _) => Reload();
        top.Controls.Add(makeBill);
        top.Controls.Add(pay);
        top.Controls.Add(refresh);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 280 };
        var p1 = new Panel { Dock = DockStyle.Fill };
        p1.Controls.Add(_billable);
        p1.Controls.Add(new Label { Text = "Готовые к выдаче заказы (выберите для формирования счёта):", Dock = DockStyle.Top, Height = 24 });
        var p2 = new Panel { Dock = DockStyle.Fill };
        p2.Controls.Add(_openBills);
        p2.Controls.Add(new Label { Text = "Открытые счета (выберите для оплаты):", Dock = DockStyle.Top, Height = 24 });
        split.Panel1.Controls.Add(p1);
        split.Panel2.Controls.Add(p2);

        Controls.Add(split);
        Controls.Add(top);
    }

    public void Reload()
    {
        try
        {
            _billable.DataSource = _orders.GetBillable()
                .Select(o => new { o.Id, Стол = o.TableNumber, Официант = o.WaiterName, Сумма = o.OrderTotal })
                .ToList();
            _openBills.DataSource = _billing.GetOpenBills()
                .Select(b => new { b.Id, Стол = b.TableNumber, Сумма = b.TotalAmount, Открыт = b.CreatedAt.ToString("dd.MM HH:mm") })
                .ToList();
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    private void CreateBill()
    {
        if (_billable.CurrentRow?.Cells["Id"].Value is not int orderId) { Ui.Error("Выберите заказ."); return; }
        var order = _orders.GetById(orderId);
        if (order is null) return;

        // В счёт включаются все готовые заказы выбранного стола.
        var sameTable = _orders.GetBillable().Where(o => o.TableNumber == order.TableNumber).Select(o => o.Id).ToList();
        var r = _billing.CreateBill(order.TableId, order.ReservationId, sameTable);
        Ui.Show(r);
        Reload();
    }

    private void Pay()
    {
        if (_openBills.CurrentRow?.Cells["Id"].Value is not int billId) { Ui.Error("Выберите счёт."); return; }
        using var dlg = new PaymentForm(_billing.GetBillTotal(billId));
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var (res, receipt) = _billing.Pay(billId, AppSession.Current!.Id, dlg.Method);
        Ui.Show(res);
        if (receipt is not null)
            new ReceiptForm(receipt).ShowDialog();
        Reload();
    }
}

/// <summary>Диалог выбора способа оплаты.</summary>
public class PaymentForm : Form
{
    public string Method { get; private set; } = "CASH";

    public PaymentForm(decimal total)
    {
        Text = "Оплата счёта";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(300, 180);
        MaximizeBox = false; MinimizeBox = false;

        var lbl = new Label { Text = $"К оплате: {total:N2} руб.", AutoSize = true, Location = new Point(20, 20), Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
        var cash = new RadioButton { Text = "Наличные", Location = new Point(20, 60), Checked = true, AutoSize = true };
        var card = new RadioButton { Text = "Банковская карта", Location = new Point(20, 86), AutoSize = true };

        var ok = Ui.Btn("Оплатить", 260);
        ok.Location = new Point(20, 130);
        ok.Click += (_, _) => { Method = card.Checked ? "CARD" : "CASH"; DialogResult = DialogResult.OK; Close(); };

        Controls.AddRange(new Control[] { lbl, cash, card, ok });
    }
}

/// <summary>Окно отображения сформированного чека.</summary>
public class ReceiptForm : Form
{
    public ReceiptForm(Receipt r)
    {
        Text = $"Чек №{r.Id}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(340, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;

        var text = new TextBox
        {
            Multiline = true, Dock = DockStyle.Fill, ReadOnly = true,
            Font = new Font("Consolas", 10F), BorderStyle = BorderStyle.None,
            BackColor = Color.White
        };
        text.Lines = new[]
        {
            "        ПРАЙМ БУРГЕР",
            "      Кассовый чек",
            "--------------------------------",
            $"Чек №:        {r.Id}",
            $"Счёт №:       {r.BillId}",
            $"Дата/время:   {r.PaidAt:dd.MM.yyyy HH:mm}",
            $"Оплата:       {(r.PaymentMethod == "CARD" ? "Банковская карта" : "Наличные")}",
            "--------------------------------",
            $"ИТОГО:        {r.Total:N2} руб.",
            "--------------------------------",
            "     Спасибо за визит!"
        };
        Controls.Add(text);
    }
}
