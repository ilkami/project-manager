using Microsoft.Data.Sqlite;
using ProjectManager.Desktop.Models;

namespace ProjectManager.Desktop.Data;

/// <summary>Работа с таблицей Tasks.</summary>
public static class TaskRepository
{
    private const string SelectFields = @"
SELECT t.Id, t.ProjectId, t.Title, t.Description, t.Priority, t.DueDate, t.IsDone, p.Title
FROM Tasks t
JOIN Projects p ON p.Id = t.ProjectId";

    /// <summary>
    /// Задачи пользователя. userId = 0 — все пользователи (режим администратора),
    /// projectId = 0 — все проекты.
    /// </summary>
    public static List<TaskItem> GetAll(int userId, int projectId)
    {
        var result = new List<TaskItem>();

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SelectFields + @"
WHERE (@uid = 0 OR p.UserId = @uid)
  AND (@pid = 0 OR t.ProjectId = @pid)
ORDER BY t.IsDone,
         CASE t.Priority WHEN 'high' THEN 0 WHEN 'normal' THEN 1 ELSE 2 END,
         t.DueDate IS NULL, t.DueDate, t.Id;";
        command.Parameters.AddWithValue("@uid", userId);
        command.Parameters.AddWithValue("@pid", projectId);

        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(Read(reader));

        return result;
    }

    public static List<TaskItem> GetByProject(int projectId)
        => GetAll(0, projectId);

    public static bool Insert(TaskItem task, int userId, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(task.Title))
        {
            error = "Название задачи не может быть пустым.";
            return false;
        }

        if (task.ProjectId <= 0)
        {
            error = "Выберите проект.";
            return false;
        }

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO Tasks (ProjectId, Title, Description, Priority, DueDate, IsDone, CreatedAt)
SELECT @pid, @title, @desc, @priority, @due, @done, @created
WHERE (@uid = 0 OR EXISTS (SELECT 1 FROM Projects WHERE Id = @pid AND UserId = @uid));";
        command.Parameters.AddWithValue("@pid", task.ProjectId);
        command.Parameters.AddWithValue("@title", task.Title.Trim());
        command.Parameters.AddWithValue("@desc", task.Description ?? "");
        command.Parameters.AddWithValue("@priority", task.Priority ?? TaskItem.PriorityNormal);
        command.Parameters.AddWithValue("@due", (object)Db.ToDbDate(task.DueDate) ?? DBNull.Value);
        command.Parameters.AddWithValue("@done", task.IsDone ? 1 : 0);
        command.Parameters.AddWithValue("@created", Db.ToDbDate(DateTime.Now));
        command.Parameters.AddWithValue("@uid", userId);

        if (command.ExecuteNonQuery() == 0)
        {
            error = "Нет доступа к выбранному проекту.";
            return false;
        }

        return true;
    }

    public static bool Update(TaskItem task, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(task.Title))
        {
            error = "Название задачи не может быть пустым.";
            return false;
        }

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE Tasks
SET Title = @title, Description = @desc, Priority = @priority, DueDate = @due, IsDone = @done
WHERE Id = @id;";
        command.Parameters.AddWithValue("@title", task.Title.Trim());
        command.Parameters.AddWithValue("@desc", task.Description ?? "");
        command.Parameters.AddWithValue("@priority", task.Priority ?? TaskItem.PriorityNormal);
        command.Parameters.AddWithValue("@due", (object)Db.ToDbDate(task.DueDate) ?? DBNull.Value);
        command.Parameters.AddWithValue("@done", task.IsDone ? 1 : 0);
        command.Parameters.AddWithValue("@id", task.Id);
        command.ExecuteNonQuery();

        return true;
    }

    public static void SetDone(int taskId, bool done)
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tasks SET IsDone = @done WHERE Id = @id;";
        command.Parameters.AddWithValue("@done", done ? 1 : 0);
        command.Parameters.AddWithValue("@id", taskId);
        command.ExecuteNonQuery();
    }

    public static void Delete(int taskId)
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Tasks WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", taskId);
        command.ExecuteNonQuery();
    }

    private static TaskItem Read(SqliteDataReader reader)
    {
        return new TaskItem
        {
            Id = reader.GetInt32(0),
            ProjectId = reader.GetInt32(1),
            Title = reader.GetString(2),
            Description = reader.GetString(3),
            Priority = reader.GetString(4),
            DueDate = reader.IsDBNull(5) ? null : Db.FromDbDate(reader.GetString(5)),
            IsDone = reader.GetInt32(6) == 1,
            ProjectTitle = reader.GetString(7)
        };
    }
}
