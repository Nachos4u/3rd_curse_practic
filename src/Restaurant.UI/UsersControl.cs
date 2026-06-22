using Restaurant.Core;
using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>Администрирование пользователей: список, создание, блокировка.</summary>
public class UsersControl : UserControl, IReloadable
{
    private readonly UserService _users = new();
    private readonly DataGridView _grid = Ui.Grid();

    private readonly TextBox _login = new() { Width = 180 };
    private readonly TextBox _password = new() { Width = 180, UseSystemPasswordChar = true };
    private readonly ComboBox _roleBox = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _last = new() { Width = 180 };
    private readonly TextBox _first = new() { Width = 180 };
    private readonly TextBox _middle = new() { Width = 180 };
    private readonly TextBox _phone = new() { Width = 180 };

    public UsersControl()
    {
        var refresh = Ui.Btn("Обновить", 110); refresh.BackColor = Color.Gray;
        refresh.Click += (_, _) => Reload();
        var toggle = Ui.Btn("Вкл/выкл учётку", 170);
        toggle.Click += (_, _) => Toggle();
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
        top.Controls.Add(refresh);
        top.Controls.Add(toggle);

        var side = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(10) };
        side.Controls.Add(Ui.Title("Новый пользователь"));
        void Add(string cap, Control c) { side.Controls.Add(new Label { Text = cap, AutoSize = true }); side.Controls.Add(c); }
        Add("Логин:", _login);
        Add("Пароль:", _password);
        Add("Роль:", _roleBox);
        Add("Фамилия:", _last);
        Add("Имя:", _first);
        Add("Отчество:", _middle);
        Add("Телефон:", _phone);
        var create = Ui.Btn("Создать пользователя", 200);
        create.Click += (_, _) => Create();
        side.Controls.Add(create);

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
            _grid.DataSource = _users.GetAll()
                .Select(u => new
                {
                    u.Id,
                    Логин = u.Login,
                    Роль = RoleName(u.RoleCode),
                    ФИО = u.FullName,
                    Телефон = u.Phone ?? "",
                    Активен = u.IsActive ? "да" : "нет"
                }).ToList();

            _roleBox.DataSource = _users.GetRoles();
            _roleBox.DisplayMember = nameof(Role.Name);
        }
        catch (Exception ex) { Ui.Error(ex.Message); }
    }

    private void Create()
    {
        if (_roleBox.SelectedItem is not Role role) { Ui.Error("Выберите роль."); return; }
        var r = _users.Create(_login.Text, _password.Text, role.Id, _last.Text, _first.Text, _middle.Text, _phone.Text);
        Ui.Show(r);
        if (r.Success)
        {
            _login.Clear(); _password.Clear(); _last.Clear(); _first.Clear(); _middle.Clear(); _phone.Clear();
            Reload();
        }
    }

    private void Toggle()
    {
        if (_grid.CurrentRow?.Cells["Id"].Value is not int id) { Ui.Error("Выберите пользователя."); return; }
        bool active = _grid.CurrentRow.Cells["Активен"].Value as string == "да";
        Ui.Show(_users.SetActive(id, !active));
        Reload();
    }

    private static string RoleName(string code) => code switch
    {
        RoleCode.Admin => "Администратор",
        RoleCode.Waiter => "Официант",
        RoleCode.Kitchen => "Кухня",
        RoleCode.Client => "Клиент",
        _ => code
    };
}
