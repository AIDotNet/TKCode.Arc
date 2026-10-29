namespace TKCode.Arc;

/// <summary>Shortcuts to the theme manager of the current application.</summary>
public static class ArcTheme
{
    /// <summary>The theme manager of the current application. Throws before <c>UseArc(...)</c> set it up.</summary>
    public static ArcThemeManager Manager
        => ArcThemeManager.Current
           ?? throw new InvalidOperationException("Arc is not set up for the current application: call AppBuilder.UseArc(...).");

    /// <summary>The applied state.</summary>
    public static ArcThemeState State => Manager.State;

    /// <summary>
    /// Applies a state changed from the current one; what <paramref name="configure"/> does not set is kept, for example
    /// <c>ArcTheme.Apply(t => t.Appearance(ArcAppearance.Dark))</c>.
    /// </summary>
    /// <param name="configure">Changes to make.</param>
    public static void Apply(Action<ArcThemeBuilder> configure) => Manager.Apply(configure);
}
