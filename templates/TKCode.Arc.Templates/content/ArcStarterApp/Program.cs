using Avalonia;
using TKCode.Arc;
using TKCode.Arc.Fonts;

namespace ArcStarterApp;

internal static class Program
{
    // Avalonia is not ready until Main runs: do not use any Avalonia or Arc API before BuildAvaloniaApp().
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the XAML previewer of your IDE.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
                  .UsePlatformDetect()
                  .WithInterFont()      // Inter for body text (Avalonia.Fonts.Inter)
                  .WithArcFonts()       // Geist for display text, Geist Mono for code (TKCode.Arc.Fonts)
                  .UseArc(arc => arc
#if (UsesBlocks)
                      .UseArcBlocks()   // Arc's controls and the page-level blocks (Sidebar); registers UseArcControls() too
#else
                      .UseArcControls() // Arc's controls, themes and design tokens
#endif
                      // The theme of the first frame: follow the system's light or dark setting.
                      // Change it at run time with ArcTheme.Apply(t => t.Appearance(...).Accent(...)).
                      .WithTheme(t => t.Appearance(ArcAppearance.System).Accent(ArcAccentPreset.Neutral)))
                  .LogToTrace();
}
