namespace TKCode.Arc.Gallery.Pages;

/// <summary>Sample option sets shared by the Selects demo pages.</summary>
internal static class SelectsDemoData
{
    public static readonly (string City, string Zone, int Offset, string Region)[] Cities =
    {
        ("Honolulu", "Hawaii", -10, "Americas"), ("Anchorage", "Alaska", -9, "Americas"),
        ("Los Angeles", "Pacific Time", -8, "Americas"), ("Denver", "Mountain Time", -7, "Americas"),
        ("Chicago", "Central Time", -6, "Americas"), ("New York", "Eastern Time", -5, "Americas"),
        ("São Paulo", "Brasília Time", -3, "Americas"), ("London", "Greenwich Mean Time", 0, "Europe"),
        ("Lisbon", "Western European Time", 0, "Europe"), ("Paris", "Central European Time", 1, "Europe"),
        ("Berlin", "Central European Time", 1, "Europe"), ("Stockholm", "Central European Time", 1, "Europe"),
        ("Athens", "Eastern European Time", 2, "Europe"), ("Istanbul", "Turkey Time", 3, "Europe"),
        ("Dubai", "Gulf Standard Time", 4, "Asia Pacific"), ("Mumbai", "India Standard Time", 5, "Asia Pacific"),
        ("Singapore", "Singapore Time", 8, "Asia Pacific"), ("Shanghai", "China Standard Time", 8, "Asia Pacific"),
        ("Tokyo", "Japan Standard Time", 9, "Asia Pacific"), ("Sydney", "Australian Eastern Time", 10, "Asia Pacific"),
        ("Auckland", "New Zealand Time", 12, "Asia Pacific")
    };

    public static string Utc(int offset) => offset == 0 ? "UTC+0" : offset > 0 ? $"UTC+{offset}" : $"UTC−{-offset}";
}
