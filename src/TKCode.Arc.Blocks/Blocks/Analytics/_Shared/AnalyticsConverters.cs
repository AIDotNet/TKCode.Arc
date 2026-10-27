using Avalonia;
using Avalonia.Data.Converters;

namespace TKCode.Arc;

/// <summary>Small value converters for the Analytics shared themes.</summary>
internal static class AnalyticsConverters
{
    /// <summary>A gap in px to a left margin (<c>Thickness(gap, 0, 0, 0)</c>).</summary>
    public static readonly IValueConverter LeftGap = new FuncValueConverter<double, Thickness>(gap => new Thickness(gap, 0, 0, 0));
}
