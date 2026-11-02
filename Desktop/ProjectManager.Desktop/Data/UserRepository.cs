using Microsoft.Data.Sqlite;
using ProjectManager.Desktop.Models;
using ProjectManager.Desktop.Services;

namespace ProjectManager.Desktop.Data;

/// <summary>Работа с таблицей Users: авторизация и управление учётными записями.</summary>
public static class UserRepository
{
    private const string SelectFields = @"
SELECT u.Id, u.Login, u.PasswordHash, u.FullName, u.Role, u.IsActive, u.CreatedAt,
       (SELECT COUNT(*) FROM Projects p WHERE p.UserId = u.Id)
FROM Users u";

    /// <summary>Проверка логина и пароля. Возвращает null и текст ошибки при неудаче.</summary>
    public static User Login(string login, string password, out string error)
    {
        error = null;

        var user = FindByLogin(login);
        if (user == null)
        {
            error = "Пользователь с таким логином не найден.";
            return null;
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            error = "Неверный пароль.";
            return null;
        }

        if (!user.IsActive)
        {
            error = "Учётная запись заблокирована администратором.";
            return null;
        }

        return user;
    }

    public static User FindByLogin(string login)
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SelectFields + " WHERE u.Login = @login COLLATE NOCASE;";
        command.Parameters.AddWithValue("@login", login);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public static List<User> GetAll()
    {
        var result = new List<User>();

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SelectFields + " ORDER BY u.Id;";

        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(Read(reader));

        return result;
    }

    public static bool Create(string login, string password, string fullName, string role, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(login))
        {
            error = "Логин не может быть пустым.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 5)
        {
            error = "Пароль должен содержать минимум 5 символов.";
            return false;
        }

        if (FindByLogin(login) != null)
        {
            error = "Пользователь с таким логином уже существует.";
            return false;
        }

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO Users (Login, PasswordHash, FullName, Role, IsActive, CreatedAt)
VALUES (@login, @hash, @name, @role, 1, @created);";
        command.Parameters.AddWithValue("@login", login.Trim());
        command.Parameters.AddWithValue("@hash", PasswordHasher.Hash(password));
        command.Parameters.AddWithValue("@name", (fullName ?? "").Trim());
        command.Parameters.AddWithValue("@role", role == "admin" ? "admin" : "user");
        command.Parameters.AddWithValue("@created", Db.ToDbDate(DateTime.Now));
        command.ExecuteNonQuery();

        return true;
    }

    public static bool Update(User user, out string error)
    {
        error = null;

        if (user == null)
        {
            error = "Пользователь не выбран.";
            return false;
        }

        var existing = FindByLogin(user.Login);
        if (existing != null && existing.Id != user.Id)
        {
            error = "Этот логин уже занят другим пользователем.";
            return false;
        }

        if (user.Role != "admin" && IsLastAdmin(user.Id))
        {
            error = "Нельзя снять права с последнего администратора.";
            return false;
        }

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE Users
SET Login = @login, FullName = @name, Role = @role, IsActive = @active
WHERE Id = @id;";
        command.Parameters.AddWithValue("@login", user.Login.Trim());
        command.Parameters.AddWithValue("@name", (user.FullName ?? "").Trim());
        command.Parameters.AddWithValue("@role", user.Role == "admin" ? "admin" : "user");
        command.Parameters.AddWithValue("@active", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@id", user.Id);
        command.ExecuteNonQuery();

        return true;
    }

    public static bool ChangePassword(int userId, string newPassword, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 5)
        {
            error = "Пароль должен содержать минимум 5 символов.";
            return false;
        }

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET PasswordHash = @hash WHERE Id = @id;";
        command.Parameters.AddWithValue("@hash", PasswordHasher.Hash(newPassword));
        command.Parameters.AddWithValue("@id", userId);
        command.ExecuteNonQuery();

        return true;
    }

    public static void SetActive(int userId, bool active)
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Users SET IsActive = @active WHERE Id = @id;";
        command.Parameters.AddWithValue("@active", active ? 1 : 0);
        command.Parameters.AddWithValue("@id", userId);
        command.ExecuteNonQuery();
    }

    public static bool Delete(int userId, out string error)
    {
        error = null;

        if (IsLastAdmin(userId))
        {
            error = "Нельзя удалить последнего администратора.";
            return false;
        }

        using var connection = Db.Open();
        using (var projects = connection.CreateCommand())
        {
            projects.CommandText = "DELETE FROM Tasks WHERE ProjectId IN (SELECT Id FROM Projects WHERE UserId = @id);";
            projects.Parameters.AddWithValue("@id", userId);
            projects.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Projects WHERE UserId = @id;";
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Users WHERE Id = @id;";
            command.Parameters.AddWithValue("@id", userId);
            command.ExecuteNonQuery();
        }

        return true;
    }

    /// <summary>Является ли пользователь единственным активным администратором.</summary>
    public static bool IsLastAdmin(int userId)
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT CASE WHEN (SELECT COUNT(*) FROM Users WHERE Role = 'admin' AND IsActive = 1) <= 1
             AND EXISTS (SELECT 1 FROM Users WHERE Id = @id AND Role = 'admin')
            THEN 1 ELSE 0 END;";
        command.Parameters.AddWithValue("@id", userId);
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    public static int Count()
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static User Read(SqliteDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt32(0),
            Login = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            FullName = reader.GetString(3),
            Role = reader.GetString(4),
            IsActive = reader.GetInt32(5) == 1,
            CreatedAt = Db.FromDbDate(reader.GetString(6)) ?? DateTime.MinValue,
            ProjectsCount = reader.FieldCount > 7 ? reader.GetInt32(7) : 0
        };
    }
}
