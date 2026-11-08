using CommunityToolkit.Mvvm.ComponentModel;
using ArcStarterApp.Models;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>The Settings page: the theme's appearance and accent, kept in step with changes made elsewhere.</summary>
public sealed partial class SettingsPageViewModel : ObservableObject
{
    private bool _syncing;

    [ObservableProperty]
    private string? _appearance;

    [ObservableProperty]
    private string? _accent;

    public SettingsPageViewModel()
    {
        ShowTheme(ArcTheme.State);
        // The theme can also change from the top bar's switch. The page lives as long as the app (PageHost keeps it),
        // so the subscription does too; unsubscribe if you make pages that come and go.
        ArcTheme.Manager.StateChanged += (_, e) => ShowTheme(e.NewState);
    }

    partial void OnAppearanceChanged(string? value)
    {
        if (_syncing || !ThemeChoices.TryParseAppearance(value, out var appearance) || appearance == ArcTheme.State.Appearance)
        {
            return;
        }
        // Only the appearance changes; the accent and every other theme setting stay.
        ArcTheme.Apply(t => t.Appearance(appearance));
    }

    partial void OnAccentChanged(string? value)
    {
        if (_syncing || !ThemeChoices.TryParseAccent(value, out var accent))
        {
            return;
        }
        ArcTheme.Apply(t => t.Accent(accent));
    }

    private void ShowTheme(ArcThemeState state)
    {
        _syncing = true;
        Appearance = state.Appearance.ToString();
        Accent = ThemeChoices.AccentName(state.Accent);
        _syncing = false;
    }
}
