using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// Settings → Preferences, the demo's own panel: appearance, accent color, compact sidebar and the demo session. The
/// appearance and accent choices are shown here only (the gallery's own appearance menu sets the app theme); the compact
/// sidebar switch folds the demo's sidebar, and Sign out leaves for the sign-in page.
/// </summary>
internal sealed class DemoSettingsPreferences : Border
{
    private static readonly (string Value, string Label, string Hint)[] s_appearances =
    [
        ("light", "Light", "Always light."),
        ("dark", "Dark", "Always dark."),
        ("system", "System", "Follows your device's light or dark setting.")
    ];

    /// <summary>uiarc.dev's accent swatches; the check is dark on the light Amber and Green.</summary>
    private static readonly (string Name, string Hex, bool DarkCheck)[] s_accents =
    [
        ("Gray", "#747474", false), ("Violet", "#7747ff", false), ("Blue", "#0562ef", false), ("Green", "#0db879", true),
        ("Amber", "#f3ad20", true), ("Orange", "#f48120", false), ("Coral", "#f15f55", false), ("Rose", "#ed4e9d", false)
    ];

    private readonly IDemoHost _host;
    private readonly List<Border> _rows = [];
    private readonly Border _head;
    private readonly Border _note;
    private readonly List<(ToggleButton Button, PresenceHost Check, Border Ring)> _swatches = [];
    private readonly Switch _compact;

    public DemoSettingsPreferences(IDemoHost host)
    {
        _host = host;
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);

        var head = new StackPanel { Spacing = 4 };
        head.Children.Add(DemoUi.Text("Preferences", ArcDesignTokenKind.Foreground, 15, FontWeight.Medium));
        head.Children.Add(DemoUi.Text("How Northstar looks and behaves on this device.", ArcDesignTokenKind.TextSecondary));
        _head = new Border { Padding = new Thickness(20, 16), Child = head };

        // Appearance: the hint morphs with the choice.
        var hint = new TextMorph { FontSize = DemoUi.BodySize, Text = s_appearances[2].Hint };
        ArcTokens.Bind(hint, TextMorph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var appearance = new SegmentedControl();
        AutomationProperties.SetName(appearance, "Appearance");
        foreach (var (value, label, _) in s_appearances)
        {
            appearance.Items.Add(new SegmentedOption(value, label));
        }
        appearance.SelectedValue = "system";
        appearance.SelectionChanged += (_, _) =>
            hint.Text = s_appearances.FirstOrDefault(item => item.Value == appearance.SelectedValue as string).Hint ?? hint.Text;

        // Compact sidebar: the demo shell's folded sidebar.
        _compact = new Switch { VerticalAlignment = VerticalAlignment.Center };
        _compact.IsCheckedChanged += (_, _) =>
        {
            if (_host.IsSidebarCollapsed != (_compact.IsChecked == true))
            {
                _host.ToggleSidebar();
            }
        };

        // Demo session: Sign out shows loading, then leaves for the sign-in page.
        var signOut = DemoUi.Button("Sign out", ButtonVariant.Secondary);
        signOut.Click += async (_, _) =>
        {
            signOut.IsLoading = true;
            signOut.Content = "Signing out";
            await Task.Delay(ArcMotion.IsFull(this) ? 600 : 150);
            await _host.SignOutAsync();
            signOut.IsLoading = false;
            signOut.Content = "Sign out";
        };
        var email = string.IsNullOrEmpty(host.Account.Email) ? "the demo account" : host.Account.Email;

        var noteText = DemoUi.Text("Appearance is saved in this browser and shared with the Arc library.", ArcDesignTokenKind.TextMuted, 12);
        _note = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 12, 20, 16), Child = noteText };
        ArcTokens.Bind(_note, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var column = new StackPanel();
        column.Children.Add(_head);
        column.Children.Add(Row("Appearance", hint, appearance));
        column.Children.Add(Row("Accent color", Hint("Used for progress, charts, and a few selected controls."), Swatches()));
        column.Children.Add(Row("Compact sidebar", Hint("Folds the sidebar into icons on wide screens. Press [ anywhere to toggle it."), _compact));
        column.Children.Add(Row("Demo session", Hint($"Signed in as {email}. Signing out returns you to the sign in screen."), signOut));
        column.Children.Add(_note);
        Child = column;

        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
        AttachedToVisualTree += (_, _) => _compact.IsChecked = _host.IsSidebarCollapsed;
    }

    private static TextBlock Hint(string text) => DemoUi.Text(text, ArcDesignTokenKind.TextSecondary);

    /// <summary>A row: the title and hint on the left, the control on the right (under them at 560 or narrower).</summary>
    private Border Row(string title, Control hint, Control control)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(DemoUi.Text(title, ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium));
        text.Children.Add(hint);
        control.VerticalAlignment = VerticalAlignment.Center;
        control.HorizontalAlignment = HorizontalAlignment.Left;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 32, RowSpacing = 12 };
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        var row = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 16), Child = grid };
        ArcTokens.Bind(row, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        _rows.Add(row);
        return row;
    }

    private void ApplyWidth(double width)
    {
        var narrow = width <= 560;
        var pad = narrow ? new Thickness(16, 14) : new Thickness(20, 16);
        _head.Padding = pad;
        foreach (var row in _rows)
        {
            row.Padding = pad;
            var grid = (Grid)row.Child!;
            grid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : "*,Auto");
            grid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "Auto");
            Grid.SetColumn(grid.Children[1], narrow ? 0 : 1);
            Grid.SetRow(grid.Children[1], narrow ? 1 : 0);
        }
        _note.Padding = narrow ? new Thickness(16, 12, 16, 16) : new Thickness(20, 12, 20, 16);
    }

    /// <summary>
    /// Eight 28px swatches 10 apart. The pressed one has a ring (2px surface gap, 1px strong border) and a check that pops
    /// in. They show the choice only: the app accent is the gallery's appearance setting.
    /// </summary>
    private Control Swatches()
    {
        var panel = new WrapPanel { ItemSpacing = 10, LineSpacing = 10 };
        AutomationProperties.SetName(panel, "Accent color");
        foreach (var (name, hex, darkCheck) in s_accents)
        {
            var color = Color.Parse(hex);
            var check = new ArcGlyph { Data = ArcIcons.Check, Size = 14, StrokeThickness = 2.4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            check.Foreground = darkCheck ? new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16)) : Brushes.White;
            var checkHost = new PresenceHost { Mode = PresenceMode.Pop, EnterScale = 0.5, IsPresent = false, Child = check };
            var dot = new Panel();
            dot.Children.Add(new Ellipse { Fill = new SolidColorBrush(color) });
            dot.Children.Add(checkHost);
            // The ring sits 3px outside the dot: 2px of surface, then a 1px strong border.
            var ring = new Border { Margin = new Thickness(-3), BorderThickness = new Thickness(1), IsVisible = false, IsHitTestVisible = false }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(ring, Border.BorderBrushProperty, ArcDesignTokenKind.BorderStrong);
            var face = new Panel { Width = 28, Height = 28 };
            face.Children.Add(ring);
            face.Children.Add(dot);
            var button = new ToggleButton
            {
                Content = face,
                Padding = default,
                Background = Brushes.Transparent,
                BorderThickness = default,
                Width = 28,
                Height = 28,
                RenderTransformOrigin = RelativePoint.Center,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                Template = new Avalonia.Controls.Templates.FuncControlTemplate<ToggleButton>((owner, _) => new ContentPresenter { [!ContentPresenter.ContentProperty] = owner[!ContentControl.ContentProperty] })
            };
            AutomationProperties.SetName(button, name);
            button.PointerEntered += (_, _) => button.RenderTransform = new ScaleTransform(1.1, 1.1);
            button.PointerExited += (_, _) => button.RenderTransform = null;
            button.PointerPressed += (_, _) => button.RenderTransform = new ScaleTransform(0.92, 0.92);
            button.PointerReleased += (_, _) => button.RenderTransform = button.IsPointerOver ? new ScaleTransform(1.1, 1.1) : null;
            button.Click += (_, _) => Select(button);
            _swatches.Add((button, checkHost, ring));
            panel.Children.Add(button);
        }
        Select(_swatches[0].Button);
        return panel;
    }

    private void Select(ToggleButton selected)
    {
        foreach (var (button, check, ring) in _swatches)
        {
            var on = ReferenceEquals(button, selected);
            button.IsChecked = on;
            check.IsPresent = on;
            ring.IsVisible = on;
        }
    }
}
