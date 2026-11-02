using System.Windows;
using ProjectManager.Desktop.Data;
using ProjectManager.Desktop.Services;

namespace ProjectManager.Desktop;

/// <summary>Форма авторизации (логин и пароль, без капчи).</summary>
public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoginBox.Focus();
    }

    private void SignIn_Click(object sender, RoutedEventArgs e)
    {
        TrySignIn();
    }

    private void DemoAdmin_Click(object sender, RoutedEventArgs e)
    {
        FillDemo("admin", "admin123");
    }

    private void DemoUser_Click(object sender, RoutedEventArgs e)
    {
        FillDemo("ivanov", "user123");
    }

    private void FillDemo(string login, string password)
    {
        LoginBox.Text = login;
        PasswordBox.Password = password;
        ErrorText.Text = "";
        PasswordBox.Focus();
    }

    private void TrySignIn()
    {
        ErrorText.Text = "";

        var login = LoginBox.Text.Trim();
        var password = PasswordBox.Password;

        if (login.Length == 0 || password.Length == 0)
        {
            ErrorText.Text = "Введите логин и пароль.";
            return;
        }

        var user = UserRepository.Login(login, password, out var error);
        if (user == null)
        {
            ErrorText.Text = error;
            PasswordBox.Clear();
            return;
        }

        Session.CurrentUser = user;

        var main = new MainWindow();
        Application.Current.MainWindow = main;
        main.Show();
        Close();
    }
}
