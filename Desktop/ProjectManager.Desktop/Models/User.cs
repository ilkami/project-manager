namespace ProjectManager.Desktop.Models;

/// <summary>Пользователь системы (администратор или обычный пользователь).</summary>
public class User
{
    public int Id { get; set; }
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "user";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int ProjectsCount { get; set; }

    public bool IsAdmin => Role == "admin";
    public string RoleText => IsAdmin ? "Администратор" : "Пользователь";
    public string StatusText => IsActive ? "Активен" : "Заблокирован";
    public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy");

    public override string ToString() => $"{FullName} ({Login})";
}
