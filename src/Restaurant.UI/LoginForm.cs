using Restaurant.Services;

namespace Restaurant.UI;

/// <summary>Окно входа в систему с возможностью регистрации клиента.</summary>
public class LoginForm : Form
{
    private readonly AuthService _auth = new();
    private readonly TextBox _login = new() { Width = 220 };
    private readonly TextBox _password = new() { Width = 220, UseSystemPasswordChar = true };

    public LoginForm()
    {
        Text = "Прайм Бургер — вход";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 320);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var header = new Label
        {
            Text = "Прайм Бургер",
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Ui.Header,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold)
        };

        var layout = new TableLayoutPanel
        {
            Location = new Point(40, 90),
            Size = new Size(340, 110),
            ColumnCount = 2
        };
        layout.Controls.Add(new Label { Text = "Логин:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        layout.Controls.Add(_login, 1, 0);
        layout.Controls.Add(new Label { Text = "Пароль:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        layout.Controls.Add(_password, 1, 1);

        var loginBtn = Ui.Btn("Войти", 150);
        loginBtn.Location = new Point(40, 215);
        loginBtn.Click += (_, _) => DoLogin();

        var registerBtn = Ui.Btn("Регистрация", 150);
        registerBtn.BackColor = Color.Gray;
        registerBtn.Location = new Point(230, 215);
        registerBtn.Click += (_, _) => new RegisterForm().ShowDialog();

        var hint = new Label
        {
            Text = "Демо-доступ: admin / ivanov / kitchen / client (пароль *123)",
            Location = new Point(40, 265),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        Controls.AddRange(new Control[] { layout, loginBtn, registerBtn, hint, header });
        AcceptButton = loginBtn;
    }

    private void DoLogin()
    {
        var r = _auth.Login(_login.Text, _password.Text);
        if (!r.Success)
        {
            Ui.Error(r.Message);
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>Окно самостоятельной регистрации клиента.</summary>
public class RegisterForm : Form
{
    private readonly AuthService _auth = new();

    public RegisterForm()
    {
        Text = "Регистрация клиента";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(380, 280);
        Font = new Font("Segoe UI", 10F);

        var login = new TextBox { Width = 200 };
        var pass = new TextBox { Width = 200, UseSystemPasswordChar = true };
        var last = new TextBox { Width = 200 };
        var first = new TextBox { Width = 200 };
        var phone = new TextBox { Width = 200 };

        var grid = new TableLayoutPanel { Location = new Point(20, 20), Size = new Size(340, 180), ColumnCount = 2 };
        grid.Controls.Add(new Label { Text = "Логин:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); grid.Controls.Add(login, 1, 0);
        grid.Controls.Add(new Label { Text = "Пароль:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); grid.Controls.Add(pass, 1, 1);
        grid.Controls.Add(new Label { Text = "Фамилия:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); grid.Controls.Add(last, 1, 2);
        grid.Controls.Add(new Label { Text = "Имя:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3); grid.Controls.Add(first, 1, 3);
        grid.Controls.Add(new Label { Text = "Телефон:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4); grid.Controls.Add(phone, 1, 4);

        var ok = Ui.Btn("Зарегистрироваться", 200);
        ok.Location = new Point(20, 220);
        ok.Click += (_, _) =>
        {
            var r = _auth.RegisterClient(login.Text, pass.Text, last.Text, first.Text, phone.Text);
            Ui.Show(r);
            if (r.Success) Close();
        };

        Controls.AddRange(new Control[] { grid, ok });
    }
}
