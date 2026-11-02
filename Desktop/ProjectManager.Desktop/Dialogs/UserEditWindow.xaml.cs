using System.Windows;
using ProjectManager.Desktop.Data;
using ProjectManager.Desktop.Models;

namespace ProjectManager.Desktop.Dialogs;

/// <summary>Диалог создания и редактирования учётной записи (только для администратора).</summary>
public partial class UserEditWindow : Window
{
    private readonly User _user;

    public UserEditWindow(User user)
    {
        InitializeComponent();

        _user = user;
        RoleBox.ItemsSource = new[] { "Пользователь", "Администратор" };

        if (user == null)
        {
            HeaderText.Text = "Новая учётная запись";
            RoleBox.SelectedIndex = 0;
            ActiveBox.IsChecked = true;
        }
        else
        {
            HeaderText.Text = "Редактирование учётной записи";
            LoginBox.Text = user.Login;
            FullNameBox.Text = user.FullName;
            RoleBox.SelectedIndex = user.IsAdmin ? 1 : 0;
            ActiveBox.IsChecked = user.IsActive;

            // Пароль меняется отдельной командой "Сбросить пароль".
            PasswordPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        var login = LoginBox.Text.Trim();
        if (login.Length == 0)
        {
            ErrorText.Text = "Укажите логин.";
            LoginBox.Focus();
            return;
        }

        var role = RoleBox.SelectedIndex == 1 ? "admin" : "user";

        if (_user == null)
        {
            var password = PasswordBox.Password;
            if (password.Length < 5)
            {
                ErrorText.Text = "Пароль должен содержать минимум 5 символов.";
                PasswordBox.Focus();
                return;
            }

            if (!UserRepository.Create(login, password, FullNameBox.Text, role, out var createError))
            {
                ErrorText.Text = createError;
                return;
            }
        }
        else
        {
            _user.Login = login;
            _user.FullName = FullNameBox.Text.Trim();
            _user.Role = role;
            _user.IsActive = ActiveBox.IsChecked == true;

            if (!UserRepository.Update(_user, out var updateError))
            {
                ErrorText.Text = updateError;
                return;
            }
        }

        DialogResult = true;
    }
}
