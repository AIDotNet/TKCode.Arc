using CommunityToolkit.Mvvm.ComponentModel;
using ArcStarterApp.Models;
using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The Settings page: the workspace's name and notifications (sample values; store them through a service) and the
/// theme, which is applied at once and saved with the other preferences (Settings/AppSettings.cs).
/// </summary>
public sealed partial class SettingsPageViewModel : ObservableObject
{
    private bool _syncing;
    private string _savedName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneralDirty))]
    private string _workspaceName;

    [ObservableProperty]
    private bool _emailDigest = true;

    [ObservableProperty]
    private bool _paymentAlerts = true;

    [ObservableProperty]
    private string? _appearance;

    [ObservableProperty]
    private string? _accent;

    public SettingsPageViewModel(IWorkspaceService workspace)
    {
        _savedName = workspace.WorkspaceName;
        _workspaceName = workspace.WorkspaceName;
        ShowTheme(ArcTheme.State);
        // The theme can also change from the top bar's switch. The page lives as long as the app (PageHost keeps it),
        // so the subscription does too.
        ArcTheme.Manager.StateChanged += (_, e) => ShowTheme(e.NewState);
    }

    /// <summary>Whether the General section has changes to save (the page's save bar shows while it does).</summary>
    public bool IsGeneralDirty => WorkspaceName != _savedName;

    public void SaveGeneral()
    {
        _savedName = WorkspaceName;
        OnPropertyChanged(nameof(IsGeneralDirty));
    }

    public void DiscardGeneral() => WorkspaceName = _savedName;

    partial void OnAppearanceChanged(string? value)
    {
        if (_syncing || !ThemeChoices.TryParseAppearance(value, out var appearance) || appearance == ArcTheme.State.Appearance)
        {
            return;
        }
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
