using Avalonia;
using Avalonia.Controls;
using TKCode.Arc;

namespace ArcStarterApp.Pages;

public partial class SettingsPage : UserControl
{
    private bool _syncing;

    public SettingsPage()
    {
        InitializeComponent();
        AppearanceChoice.SelectionChanged += OnAppearanceChosen;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The theme can also change from the top bar's switch; keep the choice in step while the page is shown.
        ArcTheme.Manager.StateChanged += OnThemeChanged;
        ShowAppearance();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ArcTheme.Manager.StateChanged -= OnThemeChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnThemeChanged(object? sender, ArcThemeChangedEventArgs e) => ShowAppearance();

    private void ShowAppearance()
    {
        _syncing = true;
        AppearanceChoice.SelectedValue = ArcTheme.State.Appearance.ToString();
        _syncing = false;
    }

    private void OnAppearanceChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || AppearanceChoice.SelectedValue is not string value
            || !Enum.TryParse<ArcAppearance>(value, out var appearance) || appearance == ArcTheme.State.Appearance)
        {
            return;
        }
        // Only the appearance changes; the accent and every other theme setting stay.
        ArcTheme.Apply(t => t.Appearance(appearance));
    }
}
