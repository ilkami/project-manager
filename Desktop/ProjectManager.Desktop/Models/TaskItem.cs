namespace ProjectManager.Desktop.Models;

/// <summary>Задача внутри проекта. Класс назван TaskItem, чтобы не конфликтовать с System.Threading.Tasks.Task.</summary>
public class TaskItem
{
    public const string PriorityLow = "low";
    public const string PriorityNormal = "normal";
    public const string PriorityHigh = "high";

    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = PriorityNormal;
    public DateTime? DueDate { get; set; }
    public bool IsDone { get; set; }

    // Вычисляемые поля
    public string ProjectTitle { get; set; } = "";

    public string PriorityText => Priority switch
    {
        PriorityLow => "Низкий",
        PriorityHigh => "Высокий",
        _ => "Обычный"
    };

    public string StatusText => IsDone ? "Выполнена" : "Открыта";
    public string DueDateText => DueDate?.ToString("dd.MM.yyyy") ?? "—";

    public bool IsOverdue => !IsDone && DueDate.HasValue && DueDate.Value.Date < DateTime.Today;

    public override string ToString() => Title;
}
