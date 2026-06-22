using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>
/// Главное окно. Состав вкладок формируется по роли текущего пользователя
/// (разграничение прав доступа).
/// </summary>
public class MainForm : Form
{
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    /// <summary>Признак запроса выхода из учётной записи (для повторного показа окна входа).</summary>
    public bool LogoutRequested { get; private set; }

    public MainForm()
    {
        var user = AppSession.Current!;
        Text = $"Прайм Бургер — {RoleTitle(user.RoleCode)} ({user.ShortName})";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1100, 700);
        Font = new Font("Segoe UI", 10F);

        var top = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Ui.Header };
        var caption = new Label
        {
            Text = $"  Прайм Бургер  •  {RoleTitle(user.RoleCode)}: {user.FullName}",
            ForeColor = Color.White, Dock = DockStyle.Left, AutoSize = false, Width = 800,
            TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };
        var logout = new Button
        {
            Text = "Выход", Dock = DockStyle.Right, Width = 120, FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White, BackColor = Ui.Accent
        };
        logout.FlatAppearance.BorderSize = 0;
        logout.Click += (_, _) => { LogoutRequested = true; AppSession.Clear(); Close(); };
        top.Controls.Add(caption);
        top.Controls.Add(logout);

        _tabs.SelectedIndexChanged += (_, _) => RefreshCurrentTab();
        BuildTabs(user.RoleCode);

        Controls.Add(_tabs);
        Controls.Add(top);
    }

    private void BuildTabs(string role)
    {
        switch (role)
        {
            case RoleCode.Admin:
                AddTab("Схема зала", new HallSchemeControl());
                AddTab("Брони", new ReservationsControl(clientMode: false));
                AddTab("Меню и склад", new MenuControl());
                AddTab("Смены", new ShiftControl(adminMode: true));
                AddTab("Пользователи", new UsersControl());
                AddTab("Отчёты", new ReportsControl());
                break;

            case RoleCode.Waiter:
                AddTab("Схема зала", new HallSchemeControl());
                AddTab("Брони", new ReservationsControl(clientMode: false));
                AddTab("Заказы", new WaiterOrdersControl());
                AddTab("Счета и оплата", new BillingControl());
                AddTab("Моя смена", new ShiftControl(adminMode: false));
                break;

            case RoleCode.Kitchen:
                AddTab("Кухня", new KitchenControl());
                break;

            case RoleCode.Client:
                AddTab("Схема зала", new HallSchemeControl());
                AddTab("Мои брони", new ReservationsControl(clientMode: true));
                break;
        }
        RefreshCurrentTab();
    }

    private void AddTab(string title, Control content)
    {
        content.Dock = DockStyle.Fill;
        var page = new TabPage(title);
        page.Controls.Add(content);
        _tabs.TabPages.Add(page);
    }

    private void RefreshCurrentTab()
    {
        if (_tabs.SelectedTab?.Controls.Count > 0 &&
            _tabs.SelectedTab.Controls[0] is IReloadable r)
            r.Reload();
    }

    private static string RoleTitle(string code) => code switch
    {
        RoleCode.Admin => "Администратор",
        RoleCode.Waiter => "Официант",
        RoleCode.Kitchen => "Кухня",
        RoleCode.Client => "Клиент",
        _ => code
    };
}

/// <summary>Контракт страниц, обновляющих данные при открытии вкладки.</summary>
public interface IReloadable
{
    void Reload();
}
