using Restaurant.Data;

namespace Restaurant.UI;

/// <summary>Точка входа приложения «Прайм Бургер».</summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Проверка доступности БД перед запуском.
        if (!Db.TestConnection(out string message))
        {
            MessageBox.Show(
                message + "\n\nПроверьте, что сервер PostgreSQL запущен и база restaurant_db создана.\n" +
                "Строку подключения можно задать переменной окружения RESTAURANT_DB.",
                "Прайм Бургер — ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Цикл «вход → главное окно»: после выхода снова показывается окно входа.
        while (true)
        {
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK)
                break;

            using var main = new MainForm();
            Application.Run(main);

            if (!main.LogoutRequested)
                break;
        }
    }
}
