using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>Раздел «Статистика»: шесть аналитических отчётов.</summary>
public class ReportsControl : UserControl, IReloadable
{
    private readonly ReportService _reports = new();

    public ReportsControl()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(MakeTwoMonthReport("Продажи блюд",
            (y1, m1, y2, m2) => _reports.DishSales(y1, m1, y2, m2)));
        tabs.TabPages.Add(MakeMonthReport("Брони столов",
            (y, m) => _reports.TableBookings(y, m)));
        tabs.TabPages.Add(MakeTwoMonthReport("Работа официантов",
            (y1, m1, y2, m2) => _reports.WaiterStats(y1, m1, y2, m2)));
        tabs.TabPages.Add(MakeDateTimeReport("Свободные столы",
            (d, t) => _reports.FreeTables(d, t)));
        tabs.TabPages.Add(MakeDateReport("Занятость столов",
            d => _reports.TablesOccupancy(d)));
        tabs.TabPages.Add(MakeDateReport("Почасовая занятость",
            d => _reports.HourlyOccupancy(d)));
        Controls.Add(tabs);
    }

    public void Reload() { }

    private static (FlowLayoutPanel bar, DataGridView grid, TabPage page) Scaffold(string title)
    {
        var page = new TabPage(title);
        var grid = Ui.Grid();
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6) };
        page.Controls.Add(grid);
        page.Controls.Add(bar);
        return (bar, grid, page);
    }

    private static NumericUpDown Year(int v) => new() { Minimum = 2020, Maximum = 2100, Value = v, Width = 70 };
    private static NumericUpDown Month(int v) => new() { Minimum = 1, Maximum = 12, Value = v, Width = 50 };

    private TabPage MakeTwoMonthReport(string title, Func<int, int, int, int, System.Data.DataTable> q)
    {
        var (bar, grid, page) = Scaffold(title);
        var y1 = Year(DateTime.Today.Year); var m1 = Month(Math.Max(1, DateTime.Today.Month - 1));
        var y2 = Year(DateTime.Today.Year); var m2 = Month(DateTime.Today.Month);
        var go = Ui.Btn("Сформировать", 140);
        go.Click += (_, _) => Run(grid, () => q((int)y1.Value, (int)m1.Value, (int)y2.Value, (int)m2.Value));
        bar.Controls.AddRange(new Control[]
        {
            new Label { Text = "Период 1 — год:", AutoSize = true, Margin = new Padding(4,12,2,0) }, y1,
            new Label { Text = "мес:", AutoSize = true, Margin = new Padding(4,12,2,0) }, m1,
            new Label { Text = "Период 2 — год:", AutoSize = true, Margin = new Padding(12,12,2,0) }, y2,
            new Label { Text = "мес:", AutoSize = true, Margin = new Padding(4,12,2,0) }, m2, go
        });
        return page;
    }

    private TabPage MakeMonthReport(string title, Func<int, int, System.Data.DataTable> q)
    {
        var (bar, grid, page) = Scaffold(title);
        var y = Year(DateTime.Today.Year); var m = Month(DateTime.Today.Month);
        var go = Ui.Btn("Сформировать", 140);
        go.Click += (_, _) => Run(grid, () => q((int)y.Value, (int)m.Value));
        bar.Controls.AddRange(new Control[]
        {
            new Label { Text = "Год:", AutoSize = true, Margin = new Padding(4,12,2,0) }, y,
            new Label { Text = "Месяц:", AutoSize = true, Margin = new Padding(8,12,2,0) }, m, go
        });
        return page;
    }

    private TabPage MakeDateReport(string title, Func<DateTime, System.Data.DataTable> q)
    {
        var (bar, grid, page) = Scaffold(title);
        var d = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 120, Value = DateTime.Today };
        var go = Ui.Btn("Сформировать", 140);
        go.Click += (_, _) => Run(grid, () => q(d.Value));
        bar.Controls.AddRange(new Control[]
        {
            new Label { Text = "Дата:", AutoSize = true, Margin = new Padding(4,12,2,0) }, d, go
        });
        return page;
    }

    private TabPage MakeDateTimeReport(string title, Func<DateTime, TimeOnly, System.Data.DataTable> q)
    {
        var (bar, grid, page) = Scaffold(title);
        var d = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 120, Value = DateTime.Today };
        var t = new DateTimePicker { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90, Value = DateTime.Now };
        var go = Ui.Btn("Сформировать", 140);
        go.Click += (_, _) => Run(grid, () => q(d.Value, TimeOnly.FromDateTime(t.Value)));
        bar.Controls.AddRange(new Control[]
        {
            new Label { Text = "Дата:", AutoSize = true, Margin = new Padding(4,12,2,0) }, d,
            new Label { Text = "Время:", AutoSize = true, Margin = new Padding(8,12,2,0) }, t, go
        });
        return page;
    }

    private static void Run(DataGridView grid, Func<System.Data.DataTable> query)
    {
        try { grid.DataSource = query(); }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }
}
