using System.Data;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>Вспомогательные методы оформления интерфейса.</summary>
internal static class Ui
{
    public static readonly Color Accent = Color.FromArgb(196, 84, 34);   // фирменный «бургерный» цвет
    public static readonly Color Header = Color.FromArgb(40, 40, 46);

    /// <summary>Показывает информационное сообщение.</summary>
    public static void Info(string text) =>
        MessageBox.Show(text, "Прайм Бургер", MessageBoxButtons.OK, MessageBoxIcon.Information);

    /// <summary>Показывает сообщение об ошибке.</summary>
    public static void Error(string text) =>
        MessageBox.Show(text, "Прайм Бургер", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>Запрашивает подтверждение действия.</summary>
    public static bool Confirm(string text) =>
        MessageBox.Show(text, "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    /// <summary>Показывает результат операции подходящим типом сообщения.</summary>
    public static void Show(OperationResult r)
    {
        if (r.Success) Info(r.Message); else Error(r.Message);
    }

    /// <summary>Создаёт настроенную таблицу для отображения данных только для чтения.</summary>
    public static DataGridView Grid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToOrderColumns = false
        };
    }

    /// <summary>Создаёт акцентную кнопку.</summary>
    public static Button Btn(string text, int width = 160)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.White,
            Margin = new Padding(4)
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }

    /// <summary>Создаёт заголовок раздела.</summary>
    public static Label Title(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 12F, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(4, 8, 4, 8)
    };
}
