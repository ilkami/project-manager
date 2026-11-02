using System.Windows;

namespace ProjectManager.Desktop.Dialogs;

/// <summary>Универсальное окно ввода одной строки (технология, новый пароль и т.п.).</summary>
public partial class TextInputWindow : Window
{
    public string Value => ValueBox.Text.Trim();

    public TextInputWindow(string title, string label, string initialValue)
    {
        InitializeComponent();

        Title = title;
        HeaderText.Text = title;
        LabelText.Text = label;
        ValueBox.Text = initialValue ?? "";

        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Value.Length == 0)
        {
            ErrorText.Text = "Значение не может быть пустым.";
            return;
        }

        DialogResult = true;
    }
}
