using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>
/// Схема расположения столиков с подсветкой доступности на выбранный момент времени
/// (по умолчанию — текущий момент).
/// </summary>
public class HallSchemeControl : UserControl, IReloadable
{
    private readonly TableService _tables = new();
    private readonly DateTimePicker _date = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly DateTimePicker _time = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };
    private readonly Panel _canvas = new() { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };

    private List<RestaurantTable> _all = new();
    private HashSet<int> _busy = new();

    public HallSchemeControl()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(8) };
        _date.Value = DateTime.Today;
        _time.Value = DateTime.Now;
        var apply = Ui.Btn("Показать", 120);
        apply.Click += (_, _) => Reload();

        bar.Controls.Add(new Label { Text = "Дата:", AutoSize = true, Margin = new Padding(4, 10, 2, 0) });
        bar.Controls.Add(_date);
        bar.Controls.Add(new Label { Text = "Время:", AutoSize = true, Margin = new Padding(12, 10, 2, 0) });
        bar.Controls.Add(_time);
        bar.Controls.Add(apply);
        bar.Controls.Add(MakeLegend());

        _canvas.Paint += DrawScheme;

        Controls.Add(_canvas);
        Controls.Add(bar);
    }

    private static Control MakeLegend()
    {
        var p = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(24, 6, 0, 0) };
        p.Controls.Add(Swatch(Color.FromArgb(120, 190, 120), "свободен"));
        p.Controls.Add(Swatch(Color.FromArgb(220, 120, 120), "забронирован"));
        return p;
    }

    private static Control Swatch(Color c, string text)
    {
        var fl = new FlowLayoutPanel { AutoSize = true };
        fl.Controls.Add(new Panel { BackColor = c, Width = 18, Height = 18, Margin = new Padding(4, 4, 2, 0) });
        fl.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(0, 6, 8, 0) });
        return fl;
    }

    public void Reload()
    {
        try
        {
            _all = _tables.GetAll();
            _busy = _tables.GetBusyTableNumbers(_date.Value, TimeOnly.FromDateTime(_time.Value));
            _canvas.Invalidate();
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    private void DrawScheme(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        const int size = 110;
        using var freeBrush = new SolidBrush(Color.FromArgb(120, 190, 120));
        using var busyBrush = new SolidBrush(Color.FromArgb(220, 120, 120));
        using var pen = new Pen(Color.FromArgb(70, 70, 70), 2);
        using var font = new Font("Segoe UI", 10F, FontStyle.Bold);
        using var small = new Font("Segoe UI", 8F);
        var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        foreach (var t in _all)
        {
            var rect = new Rectangle(t.PosX + 10, t.PosY + 10, size, size - 20);
            bool busy = _busy.Contains(t.Number);
            g.FillRectangle(busy ? busyBrush : freeBrush, rect);
            g.DrawRectangle(pen, rect);
            g.DrawString($"Стол №{t.Number}", font, Brushes.Black, rect, fmt);
            g.DrawString($"{t.Seats} мест • {(busy ? "бронь" : "свободен")}", small, Brushes.Black,
                new RectangleF(rect.X, rect.Bottom - 22, rect.Width, 18), fmt);
        }
    }
}
