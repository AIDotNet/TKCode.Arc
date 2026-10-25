using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// A phone on the gallery's stage, for the demos of TKCode.Arc.Mobile: a bezel around a screen of a <see cref="PhoneDevice"/>'s
/// size, a simulated status bar (time, signal, battery), a simulated safe area, a home indicator and an optional soft
/// keyboard that shrinks the content area. A toolbar above it switches the device, the orientation and the keyboard.
/// It is a desktop simulation: it does not run the platform's insets or keyboard, and what it draws is an approximation.
/// </summary>
/// <remarks>
/// The safe area (<see cref="SafeArea"/>) is the device's insets for the orientation, with no bottom inset while the keyboard
/// is up. A <see cref="Screen"/> that holds a mobile <c>Page</c> gets it through <c>Page.SafeArea</c> (the frame turns the
/// page's <c>UsePlatformSafeArea</c> off, so the page's background runs under the status bar, as on a device); any other
/// screen is padded by it. Chrome colors, radii and shadows are ArcDesign tokens, so the frame follows light and dark, the
/// accent and the Pixel style.
/// </remarks>
public sealed class PhoneFrame : UserControl
{
    /// <summary>The simulated device.</summary>
    public static readonly StyledProperty<PhoneDevice> DeviceProperty =
        AvaloniaProperty.Register<PhoneFrame, PhoneDevice>(nameof(Device), PhoneDevice.IPhone15);

    /// <summary>Whether the screen is turned sideways (width and height swap).</summary>
    public static readonly StyledProperty<bool> IsLandscapeProperty =
        AvaloniaProperty.Register<PhoneFrame, bool>(nameof(IsLandscape));

    /// <summary>Whether the soft keyboard placeholder is up: a panel at the bottom that shrinks the content area.</summary>
    public static readonly StyledProperty<bool> IsKeyboardVisibleProperty =
        AvaloniaProperty.Register<PhoneFrame, bool>(nameof(IsKeyboardVisible));

    /// <summary>Whether the toolbar (device, orientation, keyboard) shows above the phone.</summary>
    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<PhoneFrame, bool>(nameof(ShowToolbar), true);

    /// <summary>The time the status bar shows.</summary>
    public static readonly StyledProperty<string> StatusTimeProperty =
        AvaloniaProperty.Register<PhoneFrame, string>(nameof(StatusTime), "9:41");

    /// <summary>What the screen shows; a mobile <c>Page</c> in it receives the safe area.</summary>
    public static readonly StyledProperty<Control?> ScreenProperty =
        AvaloniaProperty.Register<PhoneFrame, Control?>(nameof(Screen));

    private readonly GallerySegmented _devices;
    private readonly GallerySegmented _orientation;
    private readonly Switch _keyboardSwitch;
    private readonly WrapPanel _toolbar;
    private readonly Border _bezel;
    private readonly Border _screen;
    private readonly Grid _screenGrid;
    private readonly Border _host;
    private readonly Grid _statusBar;
    private readonly TextBlock _time;
    private readonly Border _cutout;
    private readonly Border _homeIndicator;
    private readonly Border _keyboard;
    private bool _syncing;

    public PhoneFrame()
    {
        _devices = new GallerySegmented(GalleryShellLangResourceKind.PhoneDeviceIPhone, GalleryShellLangResourceKind.PhoneDevicePixel,
            GalleryShellLangResourceKind.PhoneDeviceSmall, GalleryShellLangResourceKind.PhoneDeviceTablet);
        _devices.SelectionChanged += index => Device = PhoneDevice.Presets[index];
        GalleryText.Bind(_devices, AutomationProperties.NameProperty, GalleryShellLangResourceKind.PhoneDevice);
        _orientation = new GallerySegmented(GalleryShellLangResourceKind.PhonePortrait, GalleryShellLangResourceKind.PhoneLandscape);
        _orientation.SelectionChanged += index => IsLandscape = index == 1;
        _keyboardSwitch = new Switch { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(_keyboardSwitch, ContentControl.ContentProperty, GalleryShellLangResourceKind.PhoneKeyboard);
        _keyboardSwitch.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing)
            {
                IsKeyboardVisible = _keyboardSwitch.IsChecked == true;
            }
        };
        _toolbar = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 16, LineSpacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        _toolbar.Children.Add(_devices);
        _toolbar.Children.Add(_orientation);
        _toolbar.Children.Add(_keyboardSwitch);

        _host = new Border();
        _time = new TextBlock { FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_time, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _statusBar = BuildStatusBar(_time);
        // The camera cutout is hardware, not theme: black in light and dark.
        _cutout = new Border { Background = Brushes.Black, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = false };
        _homeIndicator = new Border
        {
            Width = 134,
            Height = 5,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            Opacity = 0.85,
            IsHitTestVisible = false
        };
        ArcTokens.Bind(_homeIndicator, Border.BackgroundProperty, ArcDesignTokenKind.Foreground);

        var content = new Panel();
        content.Children.Add(_host);
        content.Children.Add(_statusBar);
        content.Children.Add(_cutout);
        content.Children.Add(_homeIndicator);

        _keyboard = BuildKeyboard();
        _screenGrid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        _screenGrid.Children.Add(content);
        Grid.SetRow(_keyboard, 1);
        _screenGrid.Children.Add(_keyboard);

        _screen = new Border { ClipToBounds = true, Child = _screenGrid };
        ArcTokens.Bind(_screen, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(_screen, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusPanel);

        _bezel = new Border { Padding = new Thickness(8), Child = _screen, HorizontalAlignment = HorizontalAlignment.Center };
        ArcTokens.Bind(_bezel, Border.BackgroundProperty, ArcDesignTokenKind.BorderStrong);
        ArcTokens.Bind(_bezel, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusSurface);
        ArcTokens.Bind(_bezel, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowRaised);

        // A wide device (a tablet in landscape) shrinks to the width the page gives it instead of being cut off.
        var scaled = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = _bezel };
        var note = new TextBlock { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 12, MaxWidth = 440, HorizontalAlignment = HorizontalAlignment.Center };
        ArcTokens.Bind(note, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        GalleryText.Bind(note, TextBlock.TextProperty, GalleryShellLangResourceKind.PhoneSimulationNote);

        var root = new StackPanel { Spacing = 16 };
        root.Children.Add(_toolbar);
        root.Children.Add(scaled);
        root.Children.Add(note);
        Content = root;
        Refresh();
    }

    /// <inheritdoc cref="DeviceProperty"/>
    public PhoneDevice Device
    {
        get => GetValue(DeviceProperty);
        set => SetValue(DeviceProperty, value);
    }

    /// <inheritdoc cref="IsLandscapeProperty"/>
    public bool IsLandscape
    {
        get => GetValue(IsLandscapeProperty);
        set => SetValue(IsLandscapeProperty, value);
    }

    /// <inheritdoc cref="IsKeyboardVisibleProperty"/>
    public bool IsKeyboardVisible
    {
        get => GetValue(IsKeyboardVisibleProperty);
        set => SetValue(IsKeyboardVisibleProperty, value);
    }

    /// <inheritdoc cref="ShowToolbarProperty"/>
    public bool ShowToolbar
    {
        get => GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    /// <inheritdoc cref="StatusTimeProperty"/>
    public string StatusTime
    {
        get => GetValue(StatusTimeProperty);
        set => SetValue(StatusTimeProperty, value);
    }

    /// <inheritdoc cref="ScreenProperty"/>
    public Control? Screen
    {
        get => GetValue(ScreenProperty);
        set => SetValue(ScreenProperty, value);
    }

    /// <summary>The screen's size in the current orientation.</summary>
    public Size ScreenSize => Device.ScreenSize(IsLandscape);

    /// <summary>
    /// The insets the screen keeps free: the device's safe area for the orientation, without the bottom inset while the
    /// keyboard is up (the keyboard covers the home indicator's room).
    /// </summary>
    public Thickness SafeArea
    {
        get
        {
            var area = Device.SafeArea(IsLandscape);
            return IsKeyboardVisible ? new Thickness(area.Left, area.Top, area.Right, 0) : area;
        }
    }

    /// <summary>The size of the area the content gets: the screen, less the keyboard when it is up.</summary>
    public Size ContentSize
    {
        get
        {
            var size = ScreenSize;
            return IsKeyboardVisible ? new Size(size.Width, Math.Max(0, size.Height - Device.KeyboardHeight(IsLandscape))) : size;
        }
    }

    /// <summary>The mobile page the frame passes <see cref="SafeArea"/> to, or null when the screen holds none.</summary>
    public global::TKCode.Arc.Mobile.Page? ScreenPage
        => Screen switch
        {
            null => null,
            global::TKCode.Arc.Mobile.Page page => page,
            var screen => screen.GetLogicalDescendants().OfType<global::TKCode.Arc.Mobile.Page>().FirstOrDefault()
        };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DeviceProperty || change.Property == IsLandscapeProperty || change.Property == IsKeyboardVisibleProperty
            || change.Property == ShowToolbarProperty || change.Property == StatusTimeProperty || change.Property == ScreenProperty)
        {
            Refresh();
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // A demo's view may fill in its page after it was handed over.
        ApplySafeArea();
    }

    /// <summary>Brings every part of the frame in line with the properties.</summary>
    private void Refresh()
    {
        var device = Device;
        var landscape = IsLandscape;
        var size = ScreenSize;
        _screen.Width = size.Width;
        _screen.Height = size.Height;
        _toolbar.IsVisible = ShowToolbar;
        _time.Text = StatusTime;
        AutomationProperties.SetName(this, device.Name);

        var area = SafeArea;
        _statusBar.Height = area.Top;
        _statusBar.IsVisible = area.Top > 0;
        _statusBar.Margin = new Thickness(area.Left, 0, area.Right, 0);
        var hasCutout = !landscape && device.CutoutWidth > 0;
        _cutout.IsVisible = hasCutout;
        _cutout.Width = device.CutoutWidth;
        _cutout.Height = device.CutoutHeight;
        _cutout.Margin = new Thickness(0, device.CutoutTop, 0, 0);
        _cutout.CornerRadius = new CornerRadius(device.CutoutHeight / 2);
        // The status bar leaves the cutout's room free between its time and its icons.
        _statusBar.ColumnDefinitions[1].Width = new GridLength(hasCutout ? device.CutoutWidth : 0);
        _homeIndicator.IsVisible = !IsKeyboardVisible && area.Bottom >= 20;

        _keyboard.IsVisible = IsKeyboardVisible;
        _keyboard.Height = device.KeyboardHeight(landscape);

        ApplySafeArea();
        SyncToolbar();
    }

    private void ApplySafeArea()
    {
        var page = ScreenPage;
        if (_host.Child != Screen)
        {
            _host.Child = Screen;
        }
        if (page is null)
        {
            _host.Padding = SafeArea;
            return;
        }
        // The page keeps the space itself and paints its background under the status bar.
        _host.Padding = default;
        page.UsePlatformSafeArea = false;
        page.SafeArea = SafeArea;
    }

    private void SyncToolbar()
    {
        _syncing = true;
        try
        {
            var preset = PhoneDevice.Presets.ToList().IndexOf(Device);
            if (preset >= 0)
            {
                _devices.SelectedIndex = preset;
            }
            _orientation.SelectedIndex = IsLandscape ? 1 : 0;
            _keyboardSwitch.IsChecked = IsKeyboardVisible;
        }
        finally
        {
            _syncing = false;
        }
    }

    private static Grid BuildStatusBar(TextBlock time)
    {
        var bar = new Grid { VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, ColumnDefinitions = new ColumnDefinitions("*,0,*") };
        // The time and the icons are centered in the inset's height, left and right of the cutout.
        bar.Children.Add(time);
        var right = BuildIndicators();
        Grid.SetColumn(right, 2);
        bar.Children.Add(right);
        return bar;
    }

    /// <summary>Four signal bars and a battery, drawn from rectangles in the foreground color.</summary>
    private static StackPanel BuildIndicators()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var bars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 4; i++)
        {
            var bar = new Border { Width = 3, Height = 4 + i * 2.5, CornerRadius = new CornerRadius(1), VerticalAlignment = VerticalAlignment.Bottom };
            ArcTokens.Bind(bar, Border.BackgroundProperty, ArcDesignTokenKind.Foreground);
            bars.Children.Add(bar);
        }
        var battery = new Border { Width = 24, Height = 12, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3.5), Padding = new Thickness(1.5), VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(battery, Border.BorderBrushProperty, ArcDesignTokenKind.TextMuted);
        var charge = new Border { Width = 15, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
        ArcTokens.Bind(charge, Border.BackgroundProperty, ArcDesignTokenKind.Foreground);
        battery.Child = charge;
        panel.Children.Add(bars);
        panel.Children.Add(battery);
        return panel;
    }

    /// <summary>The soft keyboard placeholder: a muted panel with three rows of keys.</summary>
    private static Border BuildKeyboard()
    {
        var rows = new Grid { RowDefinitions = new RowDefinitions("*,*,*,*"), Margin = new Thickness(6, 10, 6, 28) };
        for (var r = 0; r < 4; r++)
        {
            var keys = new UniformGrid { Rows = 1, Columns = r == 3 ? 3 : 10, Margin = new Thickness(0, 3) };
            for (var k = 0; k < (r == 3 ? 3 : 10); k++)
            {
                var key = new Border { Margin = new Thickness(2.5, 0), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1) };
                ArcTokens.Bind(key, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
                ArcTokens.Bind(key, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
                keys.Children.Add(key);
            }
            Grid.SetRow(keys, r);
            rows.Children.Add(keys);
        }
        var panel = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Child = rows, IsVisible = false };
        ArcTokens.Bind(panel, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(panel, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return panel;
    }
}
