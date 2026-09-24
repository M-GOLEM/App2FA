using Secure2FA.UI;

namespace Secure2FA;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.EnableVisualStyles();
        using var login = new LoginForm();
        if (login.ShowDialog() != DialogResult.OK) return;
        string pin = (string)login.Tag!;
        Application.Run(new MainForm(pin));
    }
}
