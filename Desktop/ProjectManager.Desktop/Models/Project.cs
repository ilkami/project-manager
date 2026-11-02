namespace ProjectManager.Desktop.Models;

/// <summary>Личный проект программиста.</summary>
public class Project
{
    public const string StatusPlan = "plan";
    public const string StatusWork = "work";
    public const string StatusTest = "test";
    public const string StatusDone = "done";

    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; } = StatusWork;
    public int Progress { get; set; }
    public string RepoUrl { get; set; } = "";

    // Вычисляемые поля (заполняются при чтении из БД)
    public string Technologies { get; set; } = "";
    public int TasksTotal { get; set; }
    public int TasksDone { get; set; }
    public string OwnerLogin { get; set; } = "";

    public string StatusText => Status switch
    {
        StatusPlan => "Планирование",
        StatusWork => "В работе",
        StatusTest => "Тестирование",
        StatusDone => "Завершён",
        _ => Status
    };

    public string DeadlineText => Deadline?.ToString("dd.MM.yyyy") ?? "—";
    public string StartDateText => StartDate?.ToString("dd.MM.yyyy") ?? "—";
    public string TasksText => $"{TasksDone} / {TasksTotal}";

    public bool IsOverdue => Deadline.HasValue
                             && Deadline.Value.Date < DateTime.Today
                             && Status != StatusDone;

    public int DaysLeft => Deadline.HasValue ? (Deadline.Value.Date - DateTime.Today).Days : 0;

    public string DaysLeftText
    {
        get
        {
            if (!Deadline.HasValue) return "—";
            if (Status == StatusDone) return "завершён";
            var days = DaysLeft;
            if (days < 0) return $"просрочен на {-days} дн.";
            if (days == 0) return "сегодня";
            return $"{days} дн.";
        }
    }

    public string TechnologiesText => string.IsNullOrWhiteSpace(Technologies) ? "—" : Technologies;

    public override string ToString() => Title;
}
