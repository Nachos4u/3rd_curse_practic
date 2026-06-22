using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>Рабочее место кухни: очередь заказов и смена их статусов.</summary>
public class KitchenControl : UserControl, IReloadable
{
    private readonly OrderService _orders = new();
    private readonly DataGridView _grid = Ui.Grid();
    private readonly DataGridView _items = Ui.Grid();

    public KitchenControl()
    {
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
        var cook = Ui.Btn("Взять в готовку", 160);
        cook.Click += (_, _) => Act(o => _orders.StartCooking(o));
        var ready = Ui.Btn("Готов к выдаче", 160);
        ready.Click += (_, _) => Act(o => _orders.MarkReady(o));
        var refresh = Ui.Btn("Обновить", 110); refresh.BackColor = Color.Gray;
        refresh.Click += (_, _) => Reload();
        top.Controls.Add(cook);
        top.Controls.Add(ready);
        top.Controls.Add(refresh);

        _grid.SelectionChanged += (_, _) => LoadItems();

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 560 };
        split.Panel1.Controls.Add(_grid);
        split.Panel2.Controls.Add(_items);

        Controls.Add(split);
        Controls.Add(top);
    }

    public void Reload()
    {
        try
        {
            _grid.DataSource = _orders.GetKitchenQueue()
                .Select(o => new
                {
                    o.Id,
                    Стол = o.TableNumber,
                    Официант = o.WaiterName,
                    Статус = StatusNames.Order(o.Status),
                    Оформлен = o.PlacedAt?.ToString("dd.MM HH:mm") ?? "",
                    Позиций = o.ItemsCount
                }).ToList();
            LoadItems();
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    private void LoadItems()
    {
        if (_grid.CurrentRow?.Cells["Id"].Value is int id)
            _items.DataSource = _orders.GetItems(id)
                .Select(i => new { Блюдо = i.DishName, Порций = i.Quantity }).ToList();
        else
            _items.DataSource = null;
    }

    private void Act(Func<int, OperationResult> action)
    {
        if (_grid.CurrentRow?.Cells["Id"].Value is not int id) { Ui.Error("Выберите заказ."); return; }
        Ui.Show(action(id));
        Reload();
    }
}
