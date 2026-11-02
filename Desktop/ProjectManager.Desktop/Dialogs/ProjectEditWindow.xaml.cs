using System.Windows;
using ProjectManager.Desktop.Data;
using ProjectManager.Desktop.Models;
using ProjectManager.Desktop.Services;

namespace ProjectManager.Desktop.Dialogs;

/// <summary>Диалог создания и редактирования проекта.</summary>
public partial class ProjectEditWindow : Window
{
    private static readonly string[] StatusNames = { "Планирование", "В работе", "Тестирование", "Завершён" };

    private static readonly string[] StatusCodes =
    {
        Project.StatusPlan, Project.StatusWork, Project.StatusTest, Project.StatusDone
    };

    private readonly Project _project;

    public ProjectEditWindow(Project project)
    {
        InitializeComponent();

        _project = project;
        StatusBox.ItemsSource = StatusNames;

        if (project == null)
        {
            HeaderText.Text = "Новый проект";
            StatusBox.SelectedIndex = 1;
            StartDateBox.Text = DateTime.Today.ToString("dd.MM.yyyy");
            DeadlineBox.Text = DateTime.Today.AddDays(30).ToString("dd.MM.yyyy");
        }
        else
        {
            HeaderText.Text = "Редактирование проекта";
            TitleBox.Text = project.Title;
            DescriptionBox.Text = project.Description;
            StartDateBox.Text = project.StartDate?.ToString("dd.MM.yyyy") ?? "";
            DeadlineBox.Text = project.Deadline?.ToString("dd.MM.yyyy") ?? "";
            RepoBox.Text = project.RepoUrl;
            TechBox.Text = string.Join(", ", TechnologyRepository.GetNames(project.Id));

            var index = Array.IndexOf(StatusCodes, project.Status);
            StatusBox.SelectedIndex = index >= 0 ? index : 1;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        var title = TitleBox.Text.Trim();
        if (title.Length == 0)
        {
            ErrorText.Text = "Укажите название проекта.";
            TitleBox.Focus();
            return;
        }

        if (!InputParser.TryParseDate(StartDateBox.Text, out var startDate))
        {
            ErrorText.Text = "Дата начала должна быть в формате дд.мм.гггг.";
            StartDateBox.Focus();
            return;
        }

        if (!InputParser.TryParseDate(DeadlineBox.Text, out var deadline))
        {
            ErrorText.Text = "Срок сдачи должен быть в формате дд.мм.гггг.";
            DeadlineBox.Focus();
            return;
        }

        var project = _project ?? new Project();
        project.Title = title;
        project.Description = DescriptionBox.Text.Trim();
        project.StartDate = startDate;
        project.Deadline = deadline;
        project.Status = StatusCodes[Math.Max(0, StatusBox.SelectedIndex)];
        project.RepoUrl = RepoBox.Text.Trim();

        var technologies = InputParser.SplitList(TechBox.Text);

        bool saved;
        string error;

        if (_project == null)
            saved = ProjectRepository.Insert(project, Session.UserId, technologies, out error);
        else
            saved = ProjectRepository.Update(project, technologies, out error);

        if (!saved)
        {
            ErrorText.Text = error;
            return;
        }

        DialogResult = true;
    }
}
