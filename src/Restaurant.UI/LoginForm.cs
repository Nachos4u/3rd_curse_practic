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
        ClientSize = new Size(420, 270);
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

        // Явное позиционирование: каждая подпись слева от своего поля ввода.
        var loginLbl = new Label { Text = "Логин:", AutoSize = true, Location = new Point(40, 100) };
        _login.Location = new Point(140, 97);
        var passLbl = new Label { Text = "Пароль:", AutoSize = true, Location = new Point(40, 140) };
        _password.Location = new Point(140, 137);

        var loginBtn = Ui.Btn("Войти", 150);
        loginBtn.Location = new Point(40, 195);
        loginBtn.Click += (_, _) => DoLogin();

        var registerBtn = Ui.Btn("Регистрация", 150);
        registerBtn.BackColor = Color.Gray;
        registerBtn.Location = new Point(230, 195);
        registerBtn.Click += (_, _) => new RegisterForm().ShowDialog();

        Controls.AddRange(new Control[]
        {
            loginLbl, _login, passLbl, _password, loginBtn, registerBtn, header
        });
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

        var login = new TextBox { Width = 200, Location = new Point(120, 20) };
        var pass = new TextBox { Width = 200, UseSystemPasswordChar = true, Location = new Point(120, 55) };
        var last = new TextBox { Width = 200, Location = new Point(120, 90) };
        var first = new TextBox { Width = 200, Location = new Point(120, 125) };
        var phone = new TextBox { Width = 200, Location = new Point(120, 160) };

        var labels = new[]
        {
            new Label { Text = "Логин:", AutoSize = true, Location = new Point(20, 23) },
            new Label { Text = "Пароль:", AutoSize = true, Location = new Point(20, 58) },
            new Label { Text = "Фамилия:", AutoSize = true, Location = new Point(20, 93) },
            new Label { Text = "Имя:", AutoSize = true, Location = new Point(20, 128) },
            new Label { Text = "Телефон:", AutoSize = true, Location = new Point(20, 163) },
        };

        var ok = Ui.Btn("Зарегистрироваться", 200);
        ok.Location = new Point(20, 210);
        ok.Click += (_, _) =>
        {
            var r = _auth.RegisterClient(login.Text, pass.Text, last.Text, first.Text, phone.Text);
            Ui.Show(r);
            if (r.Success) Close();
        };

        Controls.AddRange(new Control[] { login, pass, last, first, phone, ok });
        Controls.AddRange(labels);
    }
}
