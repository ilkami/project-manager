using System.Windows;
using ProjectManager.Desktop.Data;
using ProjectManager.Desktop.Models;
using ProjectManager.Desktop.Services;

namespace ProjectManager.Desktop.Dialogs;

/// <summary>Диалог создания и редактирования задачи.</summary>
public partial class TaskEditWindow : Window
{
    private static readonly string[] PriorityNames = { "Низкий", "Обычный", "Высокий" };

    private static readonly string[] PriorityCodes =
    {
        TaskItem.PriorityLow, TaskItem.PriorityNormal, TaskItem.PriorityHigh
    };

    private readonly TaskItem _task;

    public TaskEditWindow(TaskItem task, List<Project> projects, int defaultProjectId)
    {
        InitializeComponent();

        _task = task;
        ProjectBox.ItemsSource = projects;
        PriorityBox.ItemsSource = PriorityNames;

        if (task == null)
        {
            HeaderText.Text = "Новая задача";
            PriorityBox.SelectedIndex = 1;
            DueDateBox.Text = DateTime.Today.AddDays(7).ToString("dd.MM.yyyy");
            ProjectBox.SelectedItem = projects.FirstOrDefault(p => p.Id == defaultProjectId) ?? projects.FirstOrDefault();
        }
        else
        {
            HeaderText.Text = "Редактирование задачи";
            TitleBox.Text = task.Title;
            DescriptionBox.Text = task.Description;
            DueDateBox.Text = task.DueDate?.ToString("dd.MM.yyyy") ?? "";
            DoneBox.IsChecked = task.IsDone;
            ProjectBox.SelectedItem = projects.FirstOrDefault(p => p.Id == task.ProjectId) ?? projects.FirstOrDefault();

            var index = Array.IndexOf(PriorityCodes, task.Priority);
            PriorityBox.SelectedIndex = index >= 0 ? index : 1;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        var title = TitleBox.Text.Trim();
        if (title.Length == 0)
        {
            ErrorText.Text = "Укажите название задачи.";
            TitleBox.Focus();
            return;
        }

        if (ProjectBox.SelectedItem is not Project project)
        {
            ErrorText.Text = "Выберите проект.";
            return;
        }

        if (!InputParser.TryParseDate(DueDateBox.Text, out var dueDate))
        {
            ErrorText.Text = "Срок должен быть в формате дд.мм.гггг.";
            DueDateBox.Focus();
            return;
        }

        var task = _task ?? new TaskItem();
        task.ProjectId = project.Id;
        task.Title = title;
        task.Description = DescriptionBox.Text.Trim();
        task.Priority = PriorityCodes[Math.Max(0, PriorityBox.SelectedIndex)];
        task.DueDate = dueDate;
        task.IsDone = DoneBox.IsChecked == true;

        bool saved;
        string error;

        if (_task == null)
            saved = TaskRepository.Insert(task, Session.IsAdmin ? 0 : Session.UserId, out error);
        else
            saved = TaskRepository.Update(task, out error);

        if (!saved)
        {
            ErrorText.Text = error;
            return;
        }

        DialogResult = true;
    }
}
