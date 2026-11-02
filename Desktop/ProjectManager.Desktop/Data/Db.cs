using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using ProjectManager.Desktop.Services;

namespace ProjectManager.Desktop.Data;

/// <summary>
/// Подключение к локальной базе SQLite (файл projects.db в %AppData%\ProjectManager)
/// и создание схемы при первом запуске.
/// </summary>
public static class Db
{
    public static string DbPath { get; private set; }

    public static string ConnectionString => "Data Source=" + DbPath;

    private const string Schema = @"
CREATE TABLE IF NOT EXISTS Users (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Login        TEXT    NOT NULL UNIQUE,
    PasswordHash TEXT    NOT NULL,
    FullName     TEXT    NOT NULL DEFAULT '',
    Role         TEXT    NOT NULL DEFAULT 'user',
    IsActive     INTEGER NOT NULL DEFAULT 1,
    CreatedAt    TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Technologies (
    Id   INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Projects (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId      INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Title       TEXT    NOT NULL,
    Description TEXT    NOT NULL DEFAULT '',
    StartDate   TEXT    NULL,
    Deadline    TEXT    NULL,
    Status      TEXT    NOT NULL DEFAULT 'work',
    Progress    INTEGER NOT NULL DEFAULT 0,
    RepoUrl     TEXT    NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS ProjectTechnologies (
    ProjectId    INTEGER NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
    TechnologyId INTEGER NOT NULL REFERENCES Technologies(Id) ON DELETE CASCADE,
    PRIMARY KEY (ProjectId, TechnologyId)
);

CREATE TABLE IF NOT EXISTS Tasks (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    ProjectId   INTEGER NOT NULL REFERENCES Projects(Id) ON DELETE CASCADE,
    Title       TEXT    NOT NULL,
    Description TEXT    NOT NULL DEFAULT '',
    Priority    TEXT    NOT NULL DEFAULT 'normal',
    DueDate     TEXT    NULL,
    IsDone      INTEGER NOT NULL DEFAULT 0,
    CreatedAt   TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_Tasks_Project ON Tasks(ProjectId);
CREATE INDEX IF NOT EXISTS IX_Projects_User ON Projects(UserId);
";

    public static void Initialize()
    {
        var customPath = Environment.GetEnvironmentVariable("PM_DB_PATH");

        if (!string.IsNullOrWhiteSpace(customPath))
        {
            DbPath = customPath;
        }
        else
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProjectManager");
            Directory.CreateDirectory(folder);
            DbPath = Path.Combine(folder, "projects.db");
        }

        var dir = Path.GetDirectoryName(DbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = Schema;
        command.ExecuteNonQuery();

        SeedDemoData(connection);
    }

    public static SqliteConnection Open()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    public static string ToDbDate(DateTime? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime? FromDbDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.Date
            : (DateTime?)null;
    }

    // ---------------------------------------------------------------- демо-данные

    private static void SeedDemoData(SqliteConnection connection)
    {
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM Users;";
            if (Convert.ToInt64(check.ExecuteScalar()) > 0) return;
        }

        var adminId = InsertUser(connection, "admin", "admin123", "Администратор системы", "admin");
        var ivanovId = InsertUser(connection, "ivanov", "user123", "Иван Иванов", "user");
        var petrovaId = InsertUser(connection, "petrova", "user123", "Анна Петрова", "user");

        string[] technologies =
        {
            "C#", ".NET 8", "WPF", "Python", "Flask", "SQLite",
            "HTML/CSS", "JavaScript", "Git", "REST API", "PostgreSQL", "Docker"
        };

        var techIds = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in technologies)
            techIds[name] = InsertTechnology(connection, name);

        var today = DateTime.Today;

        SeedProject(connection, ivanovId,
            "Веб-приложение для управления личными проектами",
            "Учебный проект: WPF-клиент на C# и сайт на Flask для учёта проектов, задач, сроков и технологий.",
            today.AddDays(-20), today.AddDays(12), "work",
            "https://github.com/ivanov/project-manager",
            new[] { "C#", ".NET 8", "WPF", "Python", "Flask", "SQLite" },
            new (string Title, string Priority, int DueInDays, bool Done)[]
            {
                ("Спроектировать схему базы данных", "normal", -14, true),
                ("Реализовать форму авторизации", "high", -10, true),
                ("Сделать окно администратора", "high", -4, true),
                ("Добавить расчёт прогресса по задачам", "normal", -2, false),
                ("Написать сайт на Flask", "high", 5, false),
                ("Задеплоить сайт на PythonAnywhere", "high", 9, false)
            });

        SeedProject(connection, ivanovId,
            "Парсер вакансий",
            "Сбор вакансий с публичного API, выгрузка в SQLite и отчёт по требуемым технологиям.",
            today.AddDays(-30), today.AddDays(5), "test",
            "https://github.com/ivanov/vacancy-parser",
            new[] { "Python", "REST API", "SQLite", "Git" },
            new (string Title, string Priority, int DueInDays, bool Done)[]
            {
                ("Изучить документацию API", "normal", -25, true),
                ("Реализовать сбор вакансий", "high", -18, true),
                ("Добавить сохранение в базу", "normal", -12, true),
                ("Написать отчёт по технологиям", "normal", 3, false),
                ("Проверить обработку ошибок сети", "low", 4, false)
            });

        SeedProject(connection, ivanovId,
            "Трекер привычек",
            "Личное приложение для отметок о привычках и подсчёта серий.",
            today.AddDays(-3), today.AddDays(45), "plan",
            "",
            new[] { "C#", ".NET 8", "WPF" },
            new (string Title, string Priority, int DueInDays, bool Done)[]
            {
                ("Собрать требования", "normal", 7, false),
                ("Нарисовать макеты экранов", "low", 14, false),
                ("Сверстать главное окно", "normal", 30, false)
            });

        SeedProject(connection, ivanovId,
            "Лендинг портфолио",
            "Одностраничный сайт с описанием навыков и ссылками на проекты.",
            today.AddDays(-60), today.AddDays(-10), "done",
            "https://github.com/ivanov/portfolio",
            new[] { "HTML/CSS", "JavaScript", "Git" },
            new (string Title, string Priority, int DueInDays, bool Done)[]
            {
                ("Свёрстать секции", "normal", -45, true),
                ("Адаптивность под мобильные", "low", -30, true),
                ("Опубликовать на хостинге", "normal", -12, true)
            });

        SeedProject(connection, petrovaId,
            "Telegram-бот для заметок",
            "Бот принимает текст и складывает заметки в базу, поддерживает поиск по словам.",
            today.AddDays(-7), today.AddDays(20), "work",
            "https://github.com/petrova/notes-bot",
            new[] { "Python", "REST API", "PostgreSQL" },
            new (string Title, string Priority, int DueInDays, bool Done)[]
            {
                ("Зарегистрировать бота", "high", -5, true),
                ("Реализовать сохранение заметок", "high", 2, false),
                ("Добавить поиск", "normal", 10, false),
                ("Настроить деплой", "low", 18, false)
            });
    }

    private static long InsertUser(SqliteConnection connection, string login, string password, string fullName, string role)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO Users (Login, PasswordHash, FullName, Role, IsActive, CreatedAt)
VALUES (@login, @hash, @name, @role, 1, @created);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@login", login);
        command.Parameters.AddWithValue("@hash", PasswordHasher.Hash(password));
        command.Parameters.AddWithValue("@name", fullName);
        command.Parameters.AddWithValue("@role", role);
        command.Parameters.AddWithValue("@created", ToDbDate(DateTime.Now));
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static long InsertTechnology(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO Technologies (Name) VALUES (@name);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@name", name);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void SeedProject(SqliteConnection connection, long userId, string title, string description,
        DateTime? start, DateTime? deadline, string status, string repoUrl, string[] technologies,
        (string Title, string Priority, int DueInDays, bool Done)[] tasks)
    {
        long projectId;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
INSERT INTO Projects (UserId, Title, Description, StartDate, Deadline, Status, Progress, RepoUrl)
VALUES (@user, @title, @desc, @start, @deadline, @status, 0, @repo);
SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@user", userId);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@desc", description);
            command.Parameters.AddWithValue("@start", (object)ToDbDate(start) ?? DBNull.Value);
            command.Parameters.AddWithValue("@deadline", (object)ToDbDate(deadline) ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@repo", repoUrl);
            projectId = Convert.ToInt64(command.ExecuteScalar());
        }

        foreach (var technology in technologies)
        {
            using var link = connection.CreateCommand();
            link.CommandText = @"
INSERT OR IGNORE INTO ProjectTechnologies (ProjectId, TechnologyId)
SELECT @project, Id FROM Technologies WHERE Name = @name;";
            link.Parameters.AddWithValue("@project", projectId);
            link.Parameters.AddWithValue("@name", technology);
            link.ExecuteNonQuery();
        }

        foreach (var task in tasks)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO Tasks (ProjectId, Title, Description, Priority, DueDate, IsDone, CreatedAt)
VALUES (@project, @title, '', @priority, @due, @done, @created);";
            command.Parameters.AddWithValue("@project", projectId);
            command.Parameters.AddWithValue("@title", task.Title);
            command.Parameters.AddWithValue("@priority", task.Priority);
            command.Parameters.AddWithValue("@due", ToDbDate(DateTime.Today.AddDays(task.DueInDays)));
            command.Parameters.AddWithValue("@done", task.Done ? 1 : 0);
            command.Parameters.AddWithValue("@created", ToDbDate(DateTime.Today.AddDays(task.DueInDays - 14)));
            command.ExecuteNonQuery();
        }
    }
}
