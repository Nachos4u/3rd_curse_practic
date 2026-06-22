using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>Рабочее место официанта: ведение заказов выбранного стола.</summary>
public class WaiterOrdersControl : UserControl, IReloadable
{
    private readonly OrderService _orders = new();
    private readonly MenuService _menu = new();
    private readonly TableService _tables = new();

    private readonly DataGridView _orderGrid = Ui.Grid();
    private readonly DataGridView _itemGrid = Ui.Grid();
    private readonly ComboBox _dishBox = new() { Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _qty = new() { Minimum = 1, Maximum = 50, Value = 1, Width = 60 };
    private readonly Label _totalLbl = new() { AutoSize = true, Font = new Font("Segoe UI", 11F, FontStyle.Bold), Margin = new Padding(8, 8, 0, 0) };

    private int? _currentOrderId;

    public WaiterOrdersControl()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
        var create = Ui.Btn("Новый заказ", 130);
        create.Click += (_, _) => CreateOrder();
        var refresh = Ui.Btn("Обновить", 110); refresh.BackColor = Color.Gray;
        refresh.Click += (_, _) => Reload();
        top.Controls.Add(create);
        top.Controls.Add(refresh);

        _orderGrid.SelectionChanged += (_, _) => LoadItems();

        // Правая колонка фиксированной ширины; высота баров рассчитана на две строки
        // кнопок, чтобы они не перекрывались таблицей в узкой колонке.
        var right = new Panel { Dock = DockStyle.Fill };
        var addBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(4), WrapContents = true };
        var add = Ui.Btn("Добавить блюдо", 150);
        add.Click += (_, _) => AddDish();
        var del = Ui.Btn("Убрать блюдо", 130); del.BackColor = Color.Gray;
        del.Click += (_, _) => RemoveDish();
        addBar.Controls.Add(new Label { Text = "Блюдо:", AutoSize = true, Margin = new Padding(4, 10, 2, 0) });
        addBar.Controls.Add(_dishBox);
        addBar.Controls.Add(new Label { Text = "Порций:", AutoSize = true, Margin = new Padding(8, 10, 2, 0) });
        addBar.Controls.Add(_qty);
        addBar.SetFlowBreak(_qty, true);   // кнопки — на отдельную строку
        addBar.Controls.Add(add);
        addBar.Controls.Add(del);

        var actionBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 84, Padding = new Padding(4), WrapContents = true };
        var place = Ui.Btn("Оформить заказ", 150);
        place.Click += (_, _) => PlaceOrder();
        var cancel = Ui.Btn("Отменить заказ", 150); cancel.BackColor = Color.Gray;
        cancel.Click += (_, _) => CancelOrder();
        actionBar.Controls.Add(place);
        actionBar.Controls.Add(cancel);
        actionBar.SetFlowBreak(cancel, true);
        actionBar.Controls.Add(_totalLbl);

        // Сначала заполняющая таблица, затем док-панели сверху/снизу (порядок важен).
        right.Controls.Add(_itemGrid);
        right.Controls.Add(addBar);
        right.Controls.Add(actionBar);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 470));
        root.Controls.Add(_orderGrid, 0, 0);
        root.Controls.Add(right, 1, 0);

        Controls.Add(root);
        Controls.Add(top);
    }

    public void Reload()
    {
        try
        {
            int waiterId = AppSession.Current!.Id;
            _orderGrid.DataSource = _orders.GetActiveForWaiter(waiterId)
                .Select(o => new
                {
                    o.Id,
                    Стол = o.TableNumber,
                    Статус = StatusNames.Order(o.Status),
                    Позиций = o.ItemsCount,
                    Сумма = o.OrderTotal
                }).ToList();

            _dishBox.DataSource = _menu.GetDishes(onlyActive: true);
            _dishBox.DisplayMember = nameof(Dish.Name);
            LoadItems();
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    private void LoadItems()
    {
        if (_orderGrid.CurrentRow?.Cells["Id"].Value is int id)
        {
            _currentOrderId = id;
            var items = _orders.GetItems(id);
            _itemGrid.DataSource = items.Select(i => new
            {
                i.DishName,
                i.Quantity,
                Цена = i.UnitPrice,
                Скидка = i.DiscountPercent,
                Сумма = i.LineTotal
            }).ToList();
            if (_itemGrid.Columns["DishName"] != null) _itemGrid.Columns["DishName"]!.HeaderText = "Блюдо";
            if (_itemGrid.Columns["Quantity"] != null) _itemGrid.Columns["Quantity"]!.HeaderText = "Порций";
            _totalLbl.Text = $"Итого: {items.Sum(i => i.LineTotal):N2} руб.";
        }
        else
        {
            _currentOrderId = null;
            _itemGrid.DataSource = null;
            _totalLbl.Text = "";
        }
    }

    private void CreateOrder()
    {
        var all = _tables.GetAll();
        using var dlg = new PickTableForm(all);
        if (dlg.ShowDialog() != DialogResult.OK || dlg.SelectedTable is null) return;
        int id = _orders.Create(dlg.SelectedTable.Id, AppSession.Current!.Id, null);
        Ui.Info($"Создан заказ №{id} к столу №{dlg.SelectedTable.Number}.");
        Reload();
    }

    private void AddDish()
    {
        if (_currentOrderId is null) { Ui.Error("Выберите заказ."); return; }
        if (_dishBox.SelectedItem is not Dish dish) { Ui.Error("Выберите блюдо."); return; }
        Ui.Show(_orders.AddItem(_currentOrderId.Value, dish.Id, (int)_qty.Value));
        Reload();
    }

    private void RemoveDish()
    {
        if (_currentOrderId is null) { Ui.Error("Выберите заказ."); return; }
        if (_itemGrid.CurrentRow?.Cells["DishName"].Value is not string dishName) { Ui.Error("Выберите позицию."); return; }
        var dish = _menu.GetDishes().FirstOrDefault(d => d.Name == dishName);
        if (dish is null) return;
        Ui.Show(_orders.RemoveItem(_currentOrderId.Value, dish.Id));
        Reload();
    }

    private void PlaceOrder()
    {
        if (_currentOrderId is null) { Ui.Error("Выберите заказ."); return; }
        Ui.Show(_orders.Place(_currentOrderId.Value));
        Reload();
    }

    private void CancelOrder()
    {
        if (_currentOrderId is null) { Ui.Error("Выберите заказ."); return; }
        if (!Ui.Confirm($"Отменить заказ №{_currentOrderId}?")) return;
        Ui.Show(_orders.Cancel(_currentOrderId.Value));
        Reload();
    }
}

/// <summary>Диалог выбора стола для нового заказа.</summary>
public class PickTableForm : Form
{
    public RestaurantTable? SelectedTable { get; private set; }

    public PickTableForm(List<RestaurantTable> tables)
    {
        Text = "Выбор стола";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(300, 360);
        MaximizeBox = false; MinimizeBox = false;

        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        foreach (var t in tables) list.Items.Add(t);
        list.DisplayMember = nameof(RestaurantTable.Number);
        list.Format += (_, e) =>
        {
            if (e.ListItem is RestaurantTable t) e.Value = $"Стол №{t.Number} ({t.Seats} мест)";
        };

        var ok = Ui.Btn("Выбрать", 280);
        ok.Dock = DockStyle.Bottom;
        ok.Click += (_, _) =>
        {
            SelectedTable = list.SelectedItem as RestaurantTable;
            if (SelectedTable is null) { Ui.Error("Выберите стол."); return; }
            DialogResult = DialogResult.OK; Close();
        };

        Controls.Add(list);
        Controls.Add(ok);
    }
}
