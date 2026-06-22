using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>Администрирование меню и склада.</summary>
public class MenuControl : UserControl, IReloadable
{
    private readonly MenuService _menu = new();
    private readonly DataGridView _grid = Ui.Grid();

    private readonly ComboBox _categoryBox = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 200 };
    private readonly NumericUpDown _price = new() { Maximum = 100000, DecimalPlaces = 2, Width = 100 };
    private readonly NumericUpDown _stock = new() { Maximum = 100000, Value = 50, Width = 100 };

    public MenuControl()
    {
        var refresh = Ui.Btn("Обновить", 110); refresh.BackColor = Color.Gray;
        refresh.Click += (_, _) => Reload();
        var addStock = Ui.Btn("Пополнить склад (+10)", 200);
        addStock.Click += (_, _) => AddStock();
        var setPrice = Ui.Btn("Изменить цену", 150); setPrice.BackColor = Color.Gray;
        setPrice.Click += (_, _) => ChangePrice();
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
        top.Controls.Add(refresh);
        top.Controls.Add(addStock);
        top.Controls.Add(setPrice);

        var side = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10) };
        side.Controls.Add(Ui.Title("Новое блюдо"));
        side.Controls.Add(new Label { Text = "Категория:", AutoSize = true });
        side.Controls.Add(_categoryBox);
        side.Controls.Add(new Label { Text = "Название:", AutoSize = true });
        side.Controls.Add(_name);
        side.Controls.Add(new Label { Text = "Цена, руб.:", AutoSize = true });
        side.Controls.Add(_price);
        side.Controls.Add(new Label { Text = "Начальный остаток:", AutoSize = true });
        side.Controls.Add(_stock);
        var add = Ui.Btn("Добавить блюдо", 200);
        add.Click += (_, _) => AddDish();
        side.Controls.Add(add);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 720, FixedPanel = FixedPanel.Panel2 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(side);

        Controls.Add(split);
        Controls.Add(top);
    }

    public void Reload()
    {
        try
        {
            _grid.DataSource = _menu.GetDishes()
                .Select(d => new
                {
                    d.Id,
                    Категория = d.CategoryName,
                    Блюдо = d.Name,
                    Цена = d.BasePrice,
                    Остаток = d.Quantity,
                    Активно = d.IsActive ? "да" : "нет"
                }).ToList();

            _categoryBox.DataSource = _menu.GetCategories();
            _categoryBox.DisplayMember = nameof(Category.Name);
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    private void AddDish()
    {
        if (_categoryBox.SelectedItem is not Category cat) { Ui.Error("Выберите категорию."); return; }
        Ui.Show(_menu.CreateDish(cat.Id, _name.Text, _price.Value, (int)_stock.Value));
        _name.Clear();
        Reload();
    }

    private void AddStock()
    {
        if (_grid.CurrentRow?.Cells["Id"].Value is not int id) { Ui.Error("Выберите блюдо."); return; }
        Ui.Show(_menu.AddStock(id, 10));
        Reload();
    }

    private void ChangePrice()
    {
        if (_grid.CurrentRow?.Cells["Id"].Value is not int id) { Ui.Error("Выберите блюдо."); return; }
        using var dlg = new PricePromptForm();
        if (dlg.ShowDialog() != DialogResult.OK) return;
        Ui.Show(_menu.UpdatePrice(id, dlg.Price));
        Reload();
    }

    private sealed class PricePromptForm : Form
    {
        private readonly NumericUpDown _num = new() { Maximum = 100000, DecimalPlaces = 2, Width = 150, Location = new Point(20, 40) };
        public decimal Price => _num.Value;

        public PricePromptForm()
        {
            Text = "Новая цена";
            ClientSize = new Size(200, 120);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            var ok = Ui.Btn("OK", 150); ok.Location = new Point(20, 75);
            ok.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(new Label { Text = "Цена, руб.:", Location = new Point(20, 15), AutoSize = true });
            Controls.Add(_num);
            Controls.Add(ok);
        }
    }
}
