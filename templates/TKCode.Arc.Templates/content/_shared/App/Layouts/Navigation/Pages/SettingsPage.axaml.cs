using Avalonia.Controls;
#if (!UsesMvvm)
using Avalonia;
using ArcStarterApp.Models;
using TKCode.Arc;
#endif

namespace ArcStarterApp.Pages;

public partial class SettingsPage : UserControl
{
#if (!UsesMvvm)
    private bool _syncing;

#endif
    public SettingsPage()
    {
        InitializeComponent();
#if (!UsesMvvm)
        AppearanceChoice.SelectionChanged += OnAppearanceChosen;
        AccentChoice.SelectionChanged += OnAccentChosen;
#endif
    }
#if (!UsesMvvm)

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The theme can also change from the top bar's switch; keep the choices in step while the page is shown.
        ArcTheme.Manager.StateChanged += OnThemeChanged;
        ShowTheme();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ArcTheme.Manager.StateChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnThemeChanged(object? sender, ArcThemeChangedEventArgs e) => ShowTheme();

    private void ShowTheme()
    {
        _syncing = true;
        AppearanceChoice.SelectedValue = ArcTheme.State.Appearance.ToString();
        AccentChoice.SelectedValue = ThemeChoices.AccentName(ArcTheme.State.Accent);
        _syncing = false;
    }

    private void OnAppearanceChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || !ThemeChoices.TryParseAppearance(AppearanceChoice.SelectedValue, out var appearance)
            || appearance == ArcTheme.State.Appearance)
        {
            return;
        }
        // Only the appearance changes; the accent and every other theme setting stay.
        ArcTheme.Apply(t => t.Appearance(appearance));
    }

    private void OnAccentChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || !ThemeChoices.TryParseAccent(AccentChoice.SelectedValue, out var accent))
        {
            return;
        }
        ArcTheme.Apply(t => t.Accent(accent));
    }
#endif
}
