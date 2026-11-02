using System.Windows;
using System.Windows.Controls;
using ProjectManager.Desktop.Data;
using ProjectManager.Desktop.Dialogs;
using ProjectManager.Desktop.Models;
using ProjectManager.Desktop.Services;

namespace ProjectManager.Desktop;

/// <summary>Окно администратора: управление учётными записями.</summary>
public partial class AdminWindow : Window
{
    private List<User> _users = new();

    public AdminWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadUsers();
    }

    private void LoadUsers()
    {
        _users = UserRepository.GetAll();
        UsersGrid.ItemsSource = _users;

        SubtitleText.Text = $"всего учётных записей: {_users.Count}";
        InfoText.Text =
            $"администраторов: {_users.Count(u => u.IsAdmin)} · " +
            $"заблокировано: {_users.Count(u => !u.IsActive)} · " +
            $"проектов: {_users.Sum(u => u.ProjectsCount)}";

        UpdateBlockButton();
    }

    private void UpdateBlockButton()
    {
        var selected = UsersGrid.SelectedItem as User;
        BlockButton.Content = selected != null && selected.IsActive ? "Блокировать" : "Разблокировать";
        BlockButton.IsEnabled = selected != null;
    }

    private void UsersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateBlockButton();
    }

    private void UsersGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (UsersGrid.SelectedItem is User) Edit_Click(sender, e);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new UserEditWindow(null) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        LoadUsers();
        InfoText.Text = "Учётная запись создана. " + InfoText.Text;
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not User user)
        {
            MessageBox.Show("Выберите учётную запись в таблице.", "Администрирование",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new UserEditWindow(user) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        LoadUsers();
    }

    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not User user)
        {
            MessageBox.Show("Выберите учётную запись в таблице.", "Администрирование",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new TextInputWindow($"Новый пароль для «{user.Login}»",
            "Пароль (минимум 5 символов)", "") { Owner = this };

        if (dialog.ShowDialog() != true) return;

        if (!UserRepository.ChangePassword(user.Id, dialog.Value, out var error))
        {
            MessageBox.Show(error, "Не удалось изменить пароль", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show($"Пароль пользователя «{user.Login}» изменён.", "Готово",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Block_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not User user)
        {
            MessageBox.Show("Выберите учётную запись в таблице.", "Администрирование",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (user.Id == Session.UserId)
        {
            MessageBox.Show("Нельзя заблокировать собственную учётную запись.", "Администрирование",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (user.IsActive && user.IsAdmin && UserRepository.IsLastAdmin(user.Id))
        {
            MessageBox.Show("Нельзя заблокировать последнего администратора.", "Администрирование",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var activate = !user.IsActive;
        UserRepository.SetActive(user.Id, activate);

        LoadUsers();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (UsersGrid.SelectedItem is not User user)
        {
            MessageBox.Show("Выберите учётную запись в таблице.", "Администрирование",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (user.Id == Session.UserId)
        {
            MessageBox.Show("Нельзя удалить собственную учётную запись.", "Администрирование",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var answer = MessageBox.Show(
            $"Удалить учётную запись «{user.Login}» вместе с её проектами и задачами?",
            "Удаление учётной записи", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        if (!UserRepository.Delete(user.Id, out var error))
        {
            MessageBox.Show(error, "Не удалось удалить", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        LoadUsers();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadUsers();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
