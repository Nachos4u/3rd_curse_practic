using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>
/// Смены официантов. В режиме администратора — планирование смен и прикрепление
/// столов; в режиме официанта — открытие и закрытие собственной смены.
/// </summary>
public class ShiftControl : UserControl, IReloadable
{
    private readonly bool _adminMode;
    private readonly ShiftService _shifts = new();
    private readonly UserService _users = new();
    private readonly TableService _tables = new();

    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DataGridView _grid = Ui.Grid();
    private readonly ComboBox _waiterBox = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckedListBox _tablesList = new() { Width = 220, Height = 160, CheckOnClick = true };
    private readonly Label _status = new() { AutoSize = true, Font = new Font("Segoe UI", 11F), Margin = new Padding(4, 10, 0, 0) };

    public ShiftControl(bool adminMode)
    {
        _adminMode = adminMode;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8) };
        _date.Value = DateTime.Today;
        var show = Ui.Btn("Обновить", 110); show.BackColor = Color.Gray;
        show.Click += (_, _) => Reload();
        top.Controls.Add(new Label { Text = "Дата:", AutoSize = true, Margin = new Padding(4, 10, 2, 0) });
        top.Controls.Add(_date);
        top.Controls.Add(show);

        Control side = _adminMode ? BuildAdminPanel() : BuildWaiterPanel();

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 640, FixedPanel = FixedPanel.Panel2 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(side);

        Controls.Add(split);
        Controls.Add(top);
    }

    private Control BuildAdminPanel()
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(10) };
        flow.Controls.Add(Ui.Title("Планирование смены"));
        flow.Controls.Add(new Label { Text = "Официант:", AutoSize = true });
        flow.Controls.Add(_waiterBox);
        flow.Controls.Add(new Label { Text = "Прикрепить столы:", AutoSize = true, Margin = new Padding(0, 8, 0, 2) });
        flow.Controls.Add(_tablesList);
        var plan = Ui.Btn("Запланировать смену", 220);
        plan.Click += (_, _) => Plan();
        flow.Controls.Add(plan);
        return flow;
    }

    private Control BuildWaiterPanel()
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10) };
        flow.Controls.Add(Ui.Title("Моя смена на выбранную дату"));
        flow.Controls.Add(_status);
        var open = Ui.Btn("Открыть смену", 220);
        open.Click += (_, _) => { Ui.Show(_shifts.Open(AppSession.Current!.Id, _date.Value)); Reload(); };
        var close = Ui.Btn("Закрыть смену", 220); close.BackColor = Color.Gray;
        close.Click += (_, _) => { Ui.Show(_shifts.Close(AppSession.Current!.Id, _date.Value)); Reload(); };
        flow.Controls.Add(open);
        flow.Controls.Add(close);
        return flow;
    }

    public void Reload()
    {
        try
        {
            _grid.DataSource = _shifts.GetByDate(_date.Value)
                .Select(s => new
                {
                    s.Id,
                    Официант = s.WaiterName,
                    Дата = s.WorkDate.ToString("dd.MM.yyyy"),
                    Статус = s.Status switch { ShiftStatus.PLANNED => "Запланирована", ShiftStatus.OPEN => "Открыта", _ => "Закрыта" },
                    Открыта = s.OpenedAt?.ToString("HH:mm") ?? "",
                    Закрыта = s.ClosedAt?.ToString("HH:mm") ?? "",
                    Столы = s.Tables ?? ""
                }).ToList();

            if (_adminMode)
            {
                _waiterBox.DataSource = _users.GetWaiters();
                _waiterBox.DisplayMember = nameof(User.FullName);
                _tablesList.Items.Clear();
                foreach (var t in _tables.GetAll())
                    _tablesList.Items.Add(new TableBox(t));
            }
            else
            {
                var shift = _shifts.GetForWaiter(AppSession.Current!.Id, _date.Value);
                _status.Text = shift is null
                    ? "На эту дату смена не запланирована."
                    : $"Статус: {(shift.Status == ShiftStatus.PLANNED ? "запланирована" : shift.Status == ShiftStatus.OPEN ? "открыта" : "закрыта")}\nСтолы: {shift.Tables}";
            }
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    private void Plan()
    {
        if (_waiterBox.SelectedItem is not User w) { Ui.Error("Выберите официанта."); return; }
        var tableIds = _tablesList.CheckedItems.Cast<TableBox>().Select(i => i.Table.Id).ToList();
        Ui.Show(_shifts.Plan(w.Id, _date.Value, tableIds));
        Reload();
    }

    private sealed class TableBox(RestaurantTable t)
    {
        public RestaurantTable Table { get; } = t;
        public override string ToString() => $"Стол №{Table.Number}";
    }
}
