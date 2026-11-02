using Microsoft.Data.Sqlite;
using ProjectManager.Desktop.Models;

namespace ProjectManager.Desktop.Data;

/// <summary>Работа с таблицей Projects. Прогресс считается по выполненным задачам.</summary>
public static class ProjectRepository
{
    private const string SelectFields = @"
SELECT p.Id, p.UserId, p.Title, p.Description, p.StartDate, p.Deadline, p.Status, p.Progress, p.RepoUrl,
       u.Login,
       (SELECT COUNT(*) FROM Tasks t WHERE t.ProjectId = p.Id),
       (SELECT COUNT(*) FROM Tasks t WHERE t.ProjectId = p.Id AND t.IsDone = 1),
       (SELECT GROUP_CONCAT(tc.Name, ', ')
          FROM ProjectTechnologies pt
          JOIN Technologies tc ON tc.Id = pt.TechnologyId
         WHERE pt.ProjectId = p.Id)
FROM Projects p
JOIN Users u ON u.Id = p.UserId";

    /// <summary>userId = 0 — вернуть проекты всех пользователей (для администратора).</summary>
    public static List<Project> GetAll(int userId)
    {
        var result = new List<Project>();

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SelectFields + @"
WHERE (@uid = 0 OR p.UserId = @uid)
ORDER BY CASE p.Status WHEN 'done' THEN 1 ELSE 0 END,
         p.Deadline IS NULL, p.Deadline, p.Id;";
        command.Parameters.AddWithValue("@uid", userId);

        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(Read(reader));

        return result;
    }

    public static Project GetById(int id)
    {
        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = SelectFields + " WHERE p.Id = @id;";
        command.Parameters.AddWithValue("@id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public static bool Insert(Project project, int ownerUserId, IEnumerable<string> technologies, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(project.Title))
        {
            error = "Название проекта не может быть пустым.";
            return false;
        }

        if (project.Deadline.HasValue && project.StartDate.HasValue &&
            project.Deadline.Value.Date < project.StartDate.Value.Date)
        {
            error = "Срок сдачи не может быть раньше даты начала.";
            return false;
        }

        using var connection = Db.Open();
        long projectId;

        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
INSERT INTO Projects (UserId, Title, Description, StartDate, Deadline, Status, Progress, RepoUrl)
VALUES (@user, @title, @desc, @start, @deadline, @status, 0, @repo);
SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@user", ownerUserId);
            command.Parameters.AddWithValue("@title", project.Title.Trim());
            command.Parameters.AddWithValue("@desc", project.Description ?? "");
            command.Parameters.AddWithValue("@start", (object)Db.ToDbDate(project.StartDate) ?? DBNull.Value);
            command.Parameters.AddWithValue("@deadline", (object)Db.ToDbDate(project.Deadline) ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", project.Status ?? Project.StatusWork);
            command.Parameters.AddWithValue("@repo", project.RepoUrl ?? "");
            projectId = Convert.ToInt64(command.ExecuteScalar());
        }

        TechnologyRepository.SetForProject(connection, projectId, technologies);
        project.Id = (int)projectId;

        return true;
    }

    public static bool Update(Project project, IEnumerable<string> technologies, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(project.Title))
        {
            error = "Название проекта не может быть пустым.";
            return false;
        }

        if (project.Deadline.HasValue && project.StartDate.HasValue &&
            project.Deadline.Value.Date < project.StartDate.Value.Date)
        {
            error = "Срок сдачи не может быть раньше даты начала.";
            return false;
        }

        using var connection = Db.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
UPDATE Projects
SET Title = @title, Description = @desc, StartDate = @start, Deadline = @deadline,
    Status = @status, RepoUrl = @repo
WHERE Id = @id;";
            command.Parameters.AddWithValue("@title", project.Title.Trim());
            command.Parameters.AddWithValue("@desc", project.Description ?? "");
            command.Parameters.AddWithValue("@start", (object)Db.ToDbDate(project.StartDate) ?? DBNull.Value);
            command.Parameters.AddWithValue("@deadline", (object)Db.ToDbDate(project.Deadline) ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", project.Status ?? Project.StatusWork);
            command.Parameters.AddWithValue("@repo", project.RepoUrl ?? "");
            command.Parameters.AddWithValue("@id", project.Id);
            command.ExecuteNonQuery();
        }

        TechnologyRepository.SetForProject(connection, project.Id, technologies);

        return true;
    }

    public static void Delete(int projectId)
    {
        using var connection = Db.Open();

        using (var tasks = connection.CreateCommand())
        {
            tasks.CommandText = "DELETE FROM Tasks WHERE ProjectId = @id;";
            tasks.Parameters.AddWithValue("@id", projectId);
            tasks.ExecuteNonQuery();
        }

        using (var links = connection.CreateCommand())
        {
            links.CommandText = "DELETE FROM ProjectTechnologies WHERE ProjectId = @id;";
            links.Parameters.AddWithValue("@id", projectId);
            links.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM Projects WHERE Id = @id;";
            command.Parameters.AddWithValue("@id", projectId);
            command.ExecuteNonQuery();
        }
    }

    private static Project Read(SqliteDataReader reader)
    {
        var project = new Project
        {
            Id = reader.GetInt32(0),
            UserId = reader.GetInt32(1),
            Title = reader.GetString(2),
            Description = reader.GetString(3),
            StartDate = reader.IsDBNull(4) ? null : Db.FromDbDate(reader.GetString(4)),
            Deadline = reader.IsDBNull(5) ? null : Db.FromDbDate(reader.GetString(5)),
            Status = reader.GetString(6),
            RepoUrl = reader.GetString(8),
            OwnerLogin = reader.GetString(9),
            TasksTotal = reader.GetInt32(10),
            TasksDone = reader.GetInt32(11),
            Technologies = reader.IsDBNull(12) ? "" : reader.GetString(12)
        };

        var storedProgress = reader.GetInt32(7);
        project.Progress = project.TasksTotal > 0
            ? (int)Math.Round(project.TasksDone * 100.0 / project.TasksTotal)
            : storedProgress;

        return project;
    }
}
