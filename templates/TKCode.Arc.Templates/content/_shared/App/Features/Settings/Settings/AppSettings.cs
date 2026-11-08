using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc;

namespace ArcStarterApp.Settings;

/// <summary>
/// The preferences kept between runs: on the desktop in <c>settings.json</c> under the user's application data folder
/// (<see cref="DefaultPath"/>), elsewhere wherever <see cref="Store"/> puts them. They are read once, the first time
/// <see cref="Current"/> is used, and written after every change. To keep something else, add a property and call
/// <see cref="Save"/> when it changes.
/// </summary>
public sealed class AppSettings
{
    private static AppSettings? s_current;
    private ISettingsStore _store = null!;

    /// <summary>The settings of this run, read from <see cref="Store"/> on first use.</summary>
    public static AppSettings Current => s_current ??= Load(Store);

    // DefaultPath comes before Store: static properties are initialized in the order they are written.
    /// <summary>The desktop settings file: <c>&lt;application data&gt;/ArcStarterApp/settings.json</c>.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        "ArcStarterApp",
        "settings.json");

    /// <summary>
    /// Where the settings are read from and written to: <see cref="DefaultPath"/> unless a host sets another store
    /// before the app is built (a browser host keeps them in the page's local storage, tests in memory).
    /// </summary>
    public static ISettingsStore Store { get; set; } = new FileSettingsStore(DefaultPath);

    /// <summary>Light, dark, or following the system.</summary>
    public ArcAppearance Appearance { get; set; } = ArcAppearance.System;

    /// <summary>The accent seed (<c>#AARRGGBB</c>), or null for the accent the app was created with.</summary>
    public string? Accent { get; set; }
#if (UsesI18n)

    /// <summary>The language chosen on the Settings page (a BCP 47 tag), or null for the one the app starts with.</summary>
    public string? Language { get; set; }
#endif
#if (UsesSidebar)

    /// <summary>Whether the sidebar is folded to its icon rail.</summary>
    public bool IsSidebarCollapsed { get; set; }
#endif
#if (UsesDock)

    /// <summary>The dock layout (panes, sizes, tabs) when the app was last closed, or null for the one in MainView.axaml.</summary>
    public string? DockLayout { get; set; }
#endif

    /// <summary>The main window's size and position when it was last closed, or null before the first close.</summary>
    public WindowPlacement? Window { get; set; }

    /// <summary>Reads the settings from <paramref name="store"/>; nothing stored, or something unreadable, gives the defaults.</summary>
    public static AppSettings Load(ISettingsStore store)
    {
        AppSettings? settings = null;
        try
        {
            if (store.Read() is { Length: > 0 } json)
            {
                settings = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
            }
        }
        catch (JsonException)
        {
            // Damaged settings must not stop the app: start from the defaults and overwrite them on the next save.
        }
        settings ??= new AppSettings();
        settings._store = store;
        return settings;
    }

    /// <summary>Writes the settings to their store. Failures are ignored: preferences are a convenience.</summary>
    public void Save()
    {
        if (Design.IsDesignMode)
        {
            return;
        }
        _store.Write(JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings));
    }

    /// <summary>The start-up theme: the saved appearance and accent (Program.cs passes this to <c>WithTheme</c>).</summary>
    public void ApplyTheme(ArcThemeBuilder theme)
    {
        theme.Appearance(Appearance);
        if (Accent is not null && Color.TryParse(Accent, out var seed))
        {
            theme.Accent(seed);
        }
        else
        {
            theme.Accent(ArcAccentPreset.Neutral);
        }
    }

    /// <summary>Saves the appearance and the accent whenever the theme changes, from anywhere in the app.</summary>
    public void TrackTheme()
    {
        ArcTheme.Manager.StateChanged += (_, e) =>
        {
            var accent = e.NewState.Accent?.ToString();
            if (e.NewState.Appearance == Appearance && accent == Accent)
            {
                return;
            }
            Appearance = e.NewState.Appearance;
            Accent = accent;
            Save();
        };
    }

    /// <summary>Puts <paramref name="window"/> where it was last closed and saves its placement when it closes again.</summary>
    public void TrackWindow(Window window)
    {
        Window?.Restore(window);
        window.Closing += (_, _) =>
        {
            Window = WindowPlacement.Capture(window, Window);
            Save();
        };
    }
}

/// <summary>A window's size, position and maximized state.</summary>
public sealed class WindowPlacement
{
    /// <summary>The left edge, in screen pixels.</summary>
    public int X { get; set; }

    /// <summary>The top edge, in screen pixels.</summary>
    public int Y { get; set; }

    /// <summary>The client width, in device-independent pixels.</summary>
    public double Width { get; set; }

    /// <summary>The client height, in device-independent pixels.</summary>
    public double Height { get; set; }

    /// <summary>Whether the window was maximized.</summary>
    public bool IsMaximized { get; set; }

    /// <summary>
    /// Reads the placement of <paramref name="window"/>. A maximized window keeps the size and position it had before
    /// (<paramref name="previous"/>), so it un-maximizes to them next time.
    /// </summary>
    public static WindowPlacement Capture(Window window, WindowPlacement? previous)
    {
        var maximized = window.WindowState == WindowState.Maximized;
        if (maximized && previous is not null)
        {
            return new WindowPlacement
            {
                X = previous.X, Y = previous.Y, Width = previous.Width, Height = previous.Height, IsMaximized = true
            };
        }
        return new WindowPlacement
        {
            X = window.Position.X,
            Y = window.Position.Y,
            Width = window.ClientSize.Width,
            Height = window.ClientSize.Height,
            IsMaximized = maximized
        };
    }

    /// <summary>Applies the placement before the window is shown; a position on a screen that is gone is skipped.</summary>
    public void Restore(Window window)
    {
        if (Width >= window.MinWidth && Height >= window.MinHeight)
        {
            window.Width = Width;
            window.Height = Height;
        }
        var position = new Avalonia.PixelPoint(X, Y);
        foreach (var screen in window.Screens.All)
        {
            if (screen.WorkingArea.Contains(position))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = position;
                break;
            }
        }
        if (IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }
}

/// <summary>The settings file's serializer, generated at build time (no reflection, trimming and AOT safe).</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext
{
}
