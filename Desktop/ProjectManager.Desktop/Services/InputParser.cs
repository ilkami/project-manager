using System.Globalization;

namespace ProjectManager.Desktop.Services;

/// <summary>Разбор пользовательского ввода: даты и списки через запятую.</summary>
public static class InputParser
{
    private static readonly string[] DateFormats =
    {
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy",
        "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"
    };

    /// <summary>Пустая строка трактуется как "дата не задана".</summary>
    public static bool TryParseDate(string text, out DateTime? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;

        if (DateTime.TryParseExact(text.Trim(), DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            value = date.Date;
            return true;
        }

        return false;
    }

    /// <summary>"C#, Python , Flask" -> ["C#", "Python", "Flask"]</summary>
    public static string[] SplitList(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

        return text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
