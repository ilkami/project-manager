using ProjectManager.Desktop.Models;

namespace ProjectManager.Desktop.Services;

/// <summary>Текущая сессия: кто вошёл в приложение.</summary>
public static class Session
{
    public static User CurrentUser { get; set; }

    public static bool IsAuthenticated => CurrentUser != null;

    public static bool IsAdmin => CurrentUser != null && CurrentUser.IsAdmin;

    public static int UserId => CurrentUser?.Id ?? 0;

    public static void Clear()
    {
        CurrentUser = null;
    }
}
