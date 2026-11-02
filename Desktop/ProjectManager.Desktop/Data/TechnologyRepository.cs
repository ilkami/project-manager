using Microsoft.Data.Sqlite;
using ProjectManager.Desktop.Models;

namespace ProjectManager.Desktop.Data;

/// <summary>Справочник технологий и связь "проект — технологии".</summary>
public static class TechnologyRepository
{
    public static List<Technology> GetAll()
    {
        var result = new List<Technology>();

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT t.Id, t.Name, (SELECT COUNT(*) FROM ProjectTechnologies pt WHERE pt.TechnologyId = t.Id)
FROM Technologies t
ORDER BY t.Name COLLATE NOCASE;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Technology
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                ProjectsCount = reader.GetInt32(2)
            });
        }

        return result;
    }

    /// <summary>Возвращает Id технологии, создавая её при необходимости.</summary>
    public static long Ensure(SqliteConnection connection, string name)
    {
        using (var find = connection.CreateCommand())
        {
            find.CommandText = "SELECT Id FROM Technologies WHERE Name = @name COLLATE NOCASE;";
            find.Parameters.AddWithValue("@name", name);
            var found = find.ExecuteScalar();
            if (found != null) return Convert.ToInt64(found);
        }

        using var insert = connection.CreateCommand();
        insert.CommandText = @"
INSERT INTO Technologies (Name) VALUES (@name);
SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("@name", name);
        return Convert.ToInt64(insert.ExecuteScalar());
    }

    /// <summary>Заменяет набор технологий проекта.</summary>
    public static void SetForProject(SqliteConnection connection, long projectId, IEnumerable<string> names)
    {
        using (var clear = connection.CreateCommand())
        {
            clear.CommandText = "DELETE FROM ProjectTechnologies WHERE ProjectId = @id;";
            clear.Parameters.AddWithValue("@id", projectId);
            clear.ExecuteNonQuery();
        }

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            var technologyId = Ensure(connection, name.Trim());

            using var link = connection.CreateCommand();
            link.CommandText = "INSERT OR IGNORE INTO ProjectTechnologies (ProjectId, TechnologyId) VALUES (@p, @t);";
            link.Parameters.AddWithValue("@p", projectId);
            link.Parameters.AddWithValue("@t", technologyId);
            link.ExecuteNonQuery();
        }
    }

    public static List<string> GetNames(int projectId)
    {
        var result = new List<string>();

        using var connection = Db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT t.Name
FROM ProjectTechnologies pt
JOIN Technologies t ON t.Id = pt.TechnologyId
WHERE pt.ProjectId = @id
ORDER BY t.Name COLLATE NOCASE;";
        command.Parameters.AddWithValue("@id", projectId);

        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));

        return result;
    }

    public static bool Create(string name, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Название технологии не может быть пустым.";
            return false;
        }

        using var connection = Db.Open();

        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM Technologies WHERE Name = @name COLLATE NOCASE;";
            check.Parameters.AddWithValue("@name", name.Trim());
            if (Convert.ToInt32(check.ExecuteScalar()) > 0)
            {
                error = "Такая технология уже есть в справочнике.";
                return false;
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Technologies (Name) VALUES (@name);";
        command.Parameters.AddWithValue("@name", name.Trim());
        command.ExecuteNonQuery();

        return true;
    }

    public static bool Delete(int technologyId, out string error)
    {
        error = null;

        using (var connection = Db.Open())
        {
            using var links = connection.CreateCommand();
            links.CommandText = "SELECT COUNT(*) FROM ProjectTechnologies WHERE TechnologyId = @id;";
            links.Parameters.AddWithValue("@id", technologyId);
            if (Convert.ToInt32(links.ExecuteScalar()) > 0)
            {
                error = "Технология используется в проектах, сначала уберите её из проектов.";
                return false;
            }
        }

        using (var connection = Db.Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Technologies WHERE Id = @id;";
            command.Parameters.AddWithValue("@id", technologyId);
            command.ExecuteNonQuery();
        }

        return true;
    }
}
