namespace ProjectManager.Desktop.Models;

/// <summary>Технология (C#, Python, WPF, Flask ...).</summary>
public class Technology
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int ProjectsCount { get; set; }

    public string ProjectsCountText => ProjectsCount == 0 ? "не используется" : $"{ProjectsCount} проект(ов)";

    public override string ToString() => Name;
}
