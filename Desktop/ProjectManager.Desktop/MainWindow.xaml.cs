using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ProjectManager.Desktop.Data;
using ProjectManager.Desktop.Dialogs;
using ProjectManager.Desktop.Models;
using ProjectManager.Desktop.Services;

namespace ProjectManager.Desktop;

/// <summary>Главное окно: проекты, задачи, технологии и статистика.</summary>
public partial class MainWindow : Window
{
    private List<Project> _projects = new();
    private List<TaskItem> _tasks = new();
    private List<Technology> _technologies = new();
    private bool _loading;
    private AdminWindow _adminWindow;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var user = Session.CurrentUser;

        UserNameText.Text = string.IsNullOrWhiteSpace(user.FullName) ? user.Login : user.FullName;
        UserRoleText.Text = user.IsAdmin ? "Администратор" : "Пользователь · " + user.Login;
        NavAdmin.Visibility = user.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        ShowView("projects");
        ReloadAll();
    }

    private void MainWindow_Closed(object sender, EventArgs e)
    {
        if (_adminWindow != null)
        {
            _adminWindow.Closed -= AdminWindow_Closed;
            _adminWindow.Close();
            _adminWindow = null;
        }
    }

    // ------------------------------------------------------------------ данные

    private int ScopeUserId => Session.IsAdmin ? 0 : Session.UserId;

    private void ReloadAll()
    {
        _loading = true;
        try
        {
            _projects = ProjectRepository.GetAll(ScopeUserId);
            _tasks = TaskRepository.GetAll(ScopeUserId, 0);
            _technologies = TechnologyRepository.GetAll();

            ProjectsGrid.ItemsSource = _projects;
            TechGrid.ItemsSource = _technologies;

            var filterItems = new List<Project> { new Project { Id = 0, Title = "Все проекты" } };
            filterItems.AddRange(_projects);

            var previousId = (ProjectFilterBox.SelectedItem as Project)?.Id ?? 0;
            ProjectFilterBox.ItemsSource = filterItems;
            ProjectFilterBox.SelectedItem =
                filterItems.FirstOrDefault(p => p.Id == previousId) ?? filterItems[0];

            ApplyTaskFilter();
            UpdateStats();
            UpdateSubtitles();
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyTaskFilter()
    {
        var projectId = (ProjectFilterBox.SelectedItem as Project)?.Id ?? 0;
        var onlyOpen = OnlyOpenBox.IsChecked == true;

        var filtered = _tasks
            .Where(t => projectId == 0 || t.ProjectId == projectId)
            .Where(t => !onlyOpen || !t.IsDone)
            .ToList();

        TasksGrid.ItemsSource = filtered;

        var open = filtered.Count(t => !t.IsDone);
        TasksSubtitle.Text = $"показано {filtered.Count}, из них открытых {open}";
    }

    private void UpdateSubtitles()
    {
        var work = _projects.Count(p => p.Status == Project.StatusWork || p.Status == Project.StatusTest);
        var overdue = _projects.Count(p => p.IsOverdue);
        ProjectsSubtitle.Text = $"всего {_projects.Count} · в работе {work} · просрочено {overdue}";

        StatsSubtitle.Text = $"срез на {DateTime.Now:dd.MM.yyyy HH:mm} · открытых задач: {_tasks.Count(t => !t.IsDone)}";
    }

    private void UpdateStats()
    {
        var work = _projects.Count(p => p.Status == Project.StatusWork || p.Status == Project.StatusTest);
        var overdue = _projects.Count(p => p.IsOverdue);
        var done = _tasks.Count(t => t.IsDone);

        StatProjects.Text = _projects.Count.ToString();
        StatWork.Text = work.ToString();
        StatOverdue.Text = overdue.ToString();
        StatTasks.Text = $"{done} / {_tasks.Count}";

        StatsList.ItemsSource = _projects;
    }

    // ------------------------------------------------------------------ навигация

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string view) ShowView(view);
    }

    private void ShowView(string view)
    {
        ProjectsView.Visibility = view == "projects" ? Visibility.Visible : Visibility.Collapsed;
        TasksView.Visibility = view == "tasks" ? Visibility.Visible : Visibility.Collapsed;
        TechView.Visibility = view == "tech" ? Visibility.Visible : Visibility.Collapsed;
        StatsView.Visibility = view == "stats" ? Visibility.Visible : Visibility.Collapsed;

        var activeBackground = (Brush)FindResource("AccentSoftBrush");
        var activeForeground = (Brush)FindResource("TextBrush");
        var normalForeground = (Brush)FindResource("MutedLightBrush");

        foreach (var button in new[] { NavProjects, NavTasks, NavTech, NavStats })
        {
            var isActive = (string)button.Tag == view;
            button.Background = isActive ? activeBackground : Brushes.Transparent;
            button.Foreground = isActive ? activeForeground : normalForeground;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ReloadAll();
        SetStatus("Данные обновлены.");
    }

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show("Выйти из учётной записи?", "Выход",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        Session.Clear();

        var login = new LoginWindow();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }

    private void OpenAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (!Session.IsAdmin) return;

        if (_adminWindow != null && _adminWindow.IsLoaded)
        {
            _adminWindow.Activate();
            return;
        }

        _adminWindow = new AdminWindow { Owner = this };
        _adminWindow.Closed += AdminWindow_Closed;
        _adminWindow.Show();
    }

    private void AdminWindow_Closed(object sender, EventArgs e)
    {
        if (_adminWindow != null) _adminWindow.Closed -= AdminWindow_Closed;
        _adminWindow = null;
        ReloadAll();
    }

    // ------------------------------------------------------------------ проекты

    private void ProjectsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowDetails(ProjectsGrid.SelectedItem as Project);
    }

    private void ProjectsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProjectsGrid.SelectedItem is Project) EditProject_Click(sender, e);
    }

    private void ShowDetails(Project project)
    {
        if (project == null)
        {
            DetailsTitle.Text = "Проект не выбран";
            DetailsDesc.Text = "";
            DetailsMeta.Text = "Выберите строку в таблице, чтобы увидеть подробности.";
            DetailsProgress.Value = 0;
            DetailsProgressText.Text = "0%";
            DetailsTasks.Text = "";
            return;
        }

        DetailsTitle.Text = project.Title;
        DetailsDesc.Text = string.IsNullOrWhiteSpace(project.Description)
            ? "Описание не заполнено."
            : project.Description;

        var parts = new List<string>
        {
            project.StatusText,
            "срок: " + project.DeadlineText,
            project.DaysLeftText
        };

        if (!string.IsNullOrWhiteSpace(project.RepoUrl)) parts.Add(project.RepoUrl);
        if (!string.IsNullOrWhiteSpace(project.Technologies)) parts.Add("технологии: " + project.Technologies);

        DetailsMeta.Text = string.Join("  ·  ", parts);
        DetailsProgress.Value = project.Progress;
        DetailsProgressText.Text = project.Progress + "%";
        DetailsTasks.Text = $"задачи: {project.TasksDone} из {project.TasksTotal}";
    }

    private void AddProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ProjectEditWindow(null) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ReloadAll();
            SetStatus("Проект добавлен.");
        }
    }

    private void EditProject_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectsGrid.SelectedItem is not Project project)
        {
            SetStatus("Сначала выберите проект в таблице.");
            return;
        }

        var dialog = new ProjectEditWindow(project) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ReloadAll();
            SetStatus("Изменения проекта сохранены.");
        }
    }

    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (ProjectsGrid.SelectedItem is not Project project)
        {
            SetStatus("Сначала выберите проект в таблице.");
            return;
        }

        var answer = MessageBox.Show(
            $"Удалить проект «{project.Title}» вместе со всеми задачами?",
            "Удаление проекта", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return;

        ProjectRepository.Delete(project.Id);
        ReloadAll();
        SetStatus("Проект удалён.");
    }

    // ------------------------------------------------------------------ задачи

    private void ProjectFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        ApplyTaskFilter();
    }

    private void OnlyOpen_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        ApplyTaskFilter();
    }

    private void TasksGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TasksGrid.SelectedItem is TaskItem) EditTask_Click(sender, e);
    }

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (_projects.Count == 0)
        {
            SetStatus("Сначала создайте хотя бы один проект.");
            return;
        }

        var dialog = new TaskEditWindow(null, _projects, (ProjectFilterBox.SelectedItem as Project)?.Id ?? 0)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            ReloadAll();
            SetStatus("Задача добавлена.");
        }
    }

    private void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (TasksGrid.SelectedItem is not TaskItem task)
        {
            SetStatus("Сначала выберите задачу в таблице.");
            return;
        }

        var dialog = new TaskEditWindow(task, _projects, task.ProjectId) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            ReloadAll();
            SetStatus("Изменения задачи сохранены.");
        }
    }

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (TasksGrid.SelectedItem is not TaskItem task)
        {
            SetStatus("Сначала выберите задачу в таблице.");
            return;
        }

        var answer = MessageBox.Show($"Удалить задачу «{task.Title}»?", "Удаление задачи",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        TaskRepository.Delete(task.Id);
        ReloadAll();
        SetStatus("Задача удалена.");
    }

    private void TaskDone_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (sender is not CheckBox box || box.Tag is not TaskItem task) return;

        var isDone = box.IsChecked == true;
        TaskRepository.SetDone(task.Id, isDone);

        // Перерисовываем список после завершения обработчика клика.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            ReloadAll();
            SetStatus(isDone ? "Задача отмечена выполненной." : "Задача снова открыта.");
        }), DispatcherPriority.Background);
    }

    // ------------------------------------------------------------------ технологии

    private void AddTech_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TextInputWindow("Новая технология", "Название технологии", "") { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (!TechnologyRepository.Create(dialog.Value, out var error))
        {
            MessageBox.Show(error, "Не удалось добавить", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ReloadAll();
        SetStatus("Технология добавлена в справочник.");
    }

    private void DeleteTech_Click(object sender, RoutedEventArgs e)
    {
        if (TechGrid.SelectedItem is not Technology technology)
        {
            SetStatus("Сначала выберите технологию в таблице.");
            return;
        }

        var answer = MessageBox.Show($"Удалить технологию «{technology.Name}» из справочника?",
            "Удаление технологии", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        if (!TechnologyRepository.Delete(technology.Id, out var error))
        {
            MessageBox.Show(error, "Не удалось удалить", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ReloadAll();
        SetStatus("Технология удалена.");
    }

    private void SetStatus(string message)
    {
        StatusBar.Text = $"{DateTime.Now:HH:mm:ss} · {message}";
    }
}
