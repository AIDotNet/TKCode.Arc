using Avalonia;
#if (UsesI18n)
using ArcStarterApp.Localization;
#endif
#if (UsesSettings)
using ArcStarterApp.Settings;
#endif
using TKCode.Arc;
using TKCode.Arc.Fonts;

namespace ArcStarterApp;

/// <summary>
/// How the app sets up Avalonia and Arc, in one place: every host (the desktop Program.cs, a browser or test host)
/// starts from <c>AppBuilder.Configure&lt;App&gt;()</c>, picks its platform and calls <see cref="UseArcApp"/>.
/// </summary>
public static class AppSetup
{
    /// <summary>Fonts, Arc's packages and the theme of the first frame.</summary>
    public static AppBuilder UseArcApp(this AppBuilder builder) =>
        builder.WithInterFont()     // Inter for body text (Avalonia.Fonts.Inter)
               .WithArcFonts()      // Geist for display text, Geist Mono for code (TKCode.Arc.Fonts)
               .UseArc(arc => arc
#if (UsesI18n)
                   // The languages and the one to start in (Localization/AppLanguages.cs); ArcLanguage.Set switches.
                   .UseLanguages(AppLanguages.Initial, AppLanguages.Supported)
#endif
#if (UsesBlocks)
                   .UseArcBlocks()   // Arc's controls and the page-level blocks (Sidebar); registers UseArcControls() too
#else
                   .UseArcControls() // Arc's controls, themes and design tokens
#endif
#if (UsesDock)
                   .UseArcDock()     // the dock layout (TKCode.Arc.Dock)
#endif
#if (UsesWorkflow)
                   .UseArcWorkflow() // the workflow canvas (TKCode.Arc.Workflow)
#endif
#if (UsesI18n)
                   // The app's own package: its text (Localization/) and any Arc controls you add (dotnet new tkcode-arc-control).
                   .UsePackage("ArcStarterApp", static p => TKCode.Arc.Generated.ArcStarterAppIdentifier.ArcGeneratedAssets.AddTo(p))
#else
                   // Your own Arc controls (dotnet new tkcode-arc-control): uncomment this line once the first one exists.
                   // .UsePackage("ArcStarterApp", static p => TKCode.Arc.Generated.ArcStarterAppIdentifier.ArcGeneratedAssets.AddTo(p))
#endif
#if (UsesSettings)
                   // The theme of the first frame: the appearance and accent saved last time (Settings/AppSettings.cs).
                   // Change it at run time with ArcTheme.Apply(t => t.Appearance(...).Accent(...)); the change is saved.
                   .WithTheme(AppSettings.Current.ApplyTheme));
#else
                   // The theme of the first frame: follow the system's light or dark setting.
                   // Change it at run time with ArcTheme.Apply(t => t.Appearance(...).Accent(...)).
                   .WithTheme(t => t.Appearance(ArcAppearance.System).Accent(ArcAccentPreset.Neutral)));
#endif
}
