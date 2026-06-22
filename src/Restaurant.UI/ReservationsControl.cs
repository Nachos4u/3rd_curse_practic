using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>
/// Управление бронями. В режиме клиента бронь создаётся от его имени;
/// в режиме персонала отображаются все брони на выбранную дату.
/// </summary>
public class ReservationsControl : UserControl, IReloadable
{
    private readonly bool _clientMode;
    private readonly ReservationService _service = new();
    private readonly TableService _tableSvc = new();

    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DataGridView _grid = Ui.Grid();

    // Панель создания брони
    private readonly DateTimePicker _resvDate = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _start = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };
    private readonly DateTimePicker _end = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };
    private readonly NumericUpDown _guests = new() { Minimum = 1, Maximum = 100, Value = 2, Width = 60 };
    private readonly CheckedListBox _tablesList = new() { Width = 220, Height = 150, CheckOnClick = true };

    public ReservationsControl(bool clientMode)
    {
        _clientMode = clientMode;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8) };
        _date.Value = DateTime.Today;
        var show = Ui.Btn("Обновить", 110);
        show.Click += (_, _) => Reload();
        var cancel = Ui.Btn("Отменить бронь", 150);
        cancel.BackColor = Color.Gray;
        cancel.Click += (_, _) => CancelSelected();
        top.Controls.Add(new Label { Text = "Дата:", AutoSize = true, Margin = new Padding(4, 10, 2, 0) });
        top.Controls.Add(_date);
        top.Controls.Add(show);
        top.Controls.Add(cancel);

        var right = BuildCreatePanel();

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 640, FixedPanel = FixedPanel.Panel2 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(right);

        Controls.Add(split);
        Controls.Add(top);
    }

    private Control BuildCreatePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        _resvDate.Value = DateTime.Today;
        _start.Value = DateTime.Today.AddHours(12);
        _end.Value = DateTime.Today.AddHours(14);

        flow.Controls.Add(Ui.Title("Новая бронь"));
        flow.Controls.Add(Field("Дата:", _resvDate));
        flow.Controls.Add(Field("Начало:", _start));
        flow.Controls.Add(Field("Окончание:", _end));
        flow.Controls.Add(Field("Гостей:", _guests));
        flow.Controls.Add(new Label { Text = "Столы (≤4 мест каждый):", AutoSize = true, Margin = new Padding(4, 8, 0, 2) });
        flow.Controls.Add(_tablesList);

        var create = Ui.Btn("Создать бронь", 220);
        create.Click += (_, _) => Create();
        flow.Controls.Add(create);

        panel.Controls.Add(flow);
        return panel;
    }

    private static Control Field(string caption, Control input)
    {
        var fl = new FlowLayoutPanel { AutoSize = true };
        fl.Controls.Add(new Label { Text = caption, AutoSize = true, Width = 90, Margin = new Padding(4, 8, 4, 0) });
        fl.Controls.Add(input);
        return fl;
    }

    public void Reload()
    {
        try
        {
            _grid.DataSource = _service.GetByDate(_date.Value)
                .Select(r => new
                {
                    r.Id,
                    Клиент = r.ClientName ?? "—",
                    Гостей = r.GuestsCount,
                    Дата = r.ReserveDate.ToString("dd.MM.yyyy"),
                    Начало = r.StartTime.ToString("HH:mm"),
                    Окончание = r.EndTime.ToString("HH:mm"),
                    Столы = r.Tables ?? "",
                    Статус = StatusNames.Reserve(r.Status)
                }).ToList();

            _tablesList.Items.Clear();
            foreach (var t in _tableSvc.GetAll())
                _tablesList.Items.Add(new TableItem(t));
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    private void Create()
    {
        var tableIds = _tablesList.CheckedItems.Cast<TableItem>().Select(i => i.Table.Id).ToList();
        int? clientId = _clientMode ? AppSession.Current!.Id : null;
        var r = _service.Create(clientId, (int)_guests.Value, _resvDate.Value,
            TimeOnly.FromDateTime(_start.Value), TimeOnly.FromDateTime(_end.Value), tableIds);
        Ui.Show(r);
        if (r.Success) { _date.Value = _resvDate.Value; Reload(); }
    }

    private void CancelSelected()
    {
        if (_grid.CurrentRow?.Cells["Id"].Value is not int id) { Ui.Error("Выберите бронь."); return; }
        if (!Ui.Confirm($"Отменить бронь №{id}?")) return;
        Ui.Show(_service.Cancel(id));
        Reload();
    }

    private sealed class TableItem(RestaurantTable t)
    {
        public RestaurantTable Table { get; } = t;
        public override string ToString() => $"Стол №{Table.Number} ({Table.Seats} мест)";
    }
}
