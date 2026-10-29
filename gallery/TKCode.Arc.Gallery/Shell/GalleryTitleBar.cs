using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The desktop window's own title bar, drawn in the client area (<see cref="GalleryWindow"/> extends it into the
/// decorations): the logo and the window title, then the pin that keeps the window on top and, on Windows and Linux, the
/// minimize, maximize / restore and close buttons. On macOS the system's traffic lights stay and the bar leaves room for
/// them on the left. Pressing the bar's empty part moves the window and a double click maximizes or restores it.
/// </summary>
public sealed class GalleryTitleBar : Border
{
    /// <summary>The bar's height; the window asks the system for a title bar of the same height (macOS centres its lights in it).</summary>
    public const double BarHeight = 40;

    /// <summary>The room the macOS traffic lights take at the left of the bar.</summary>
    private const double TrafficLightsWidth = 78;

    private const double ButtonSize = 30;

    private readonly Window _window;
    private readonly Border _leading = new();
    private readonly TextBlock _title = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _pin;
    private readonly Button? _maximize;
    private IDisposable? _pinLabel;
    private IDisposable? _maximizeLabel;
    private IDisposable? _titleColor;

    /// <param name="window">The window the bar moves, pins, minimizes, maximizes and closes.</param>
    /// <param name="drawsCaptionButtons">
    /// True on Windows and Linux: the bar draws minimize, maximize and close. False on macOS, whose traffic lights stay.
    /// </param>
    public GalleryTitleBar(Window window, bool drawsCaptionButtons)
    {
        _window = window;
        DrawsCaptionButtons = drawsCaptionButtons;
        Height = BarHeight;
        BorderThickness = new Thickness(0, 0, 0, 1);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var logo = new Image { Source = GalleryBrand.CreateLogo(), Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center };
        _title.FontSize = 13;
        ArcTokens.Bind(_title, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        _title.Bind(TextBlock.TextProperty, window.GetObservable(Window.TitleProperty));
        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { logo, _title }
        };
        Brand = brand;

        _pin = CaptionButton(GalleryIcons.Pin);
        _pin.Click += (_, _) => window.Topmost = !window.Topmost;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        buttons.Children.Add(_pin);
        if (drawsCaptionButtons)
        {
            // A hairline between the pin and the window's own buttons.
            var divider = new Border { Width = 1, Height = 16, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(divider, BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
            buttons.Children.Add(divider);

            var minimize = MinimizeButton = CaptionButton(ArcIcons.Minus);
            Label(minimize, GalleryShellLangResourceKind.WindowMinimize);
            minimize.Click += (_, _) => window.WindowState = WindowState.Minimized;

            _maximize = CaptionButton(ArcIcons.Square);
            _maximize.Click += (_, _) => ToggleMaximize();

            var close = CloseButton = CaptionButton(ArcIcons.X);
            Label(close, GalleryShellLangResourceKind.WindowClose);
            close.Click += (_, _) => window.Close();
            UseDangerHover(close);

            buttons.Children.Add(minimize);
            buttons.Children.Add(_maximize);
            buttons.Children.Add(close);
        }
        Buttons = buttons;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        brand.Margin = new Thickness(drawsCaptionButtons ? 14 : 4, 0, 12, 0);
        Grid.SetColumn(brand, 1);
        Grid.SetColumn(buttons, 3);
        row.Children.Add(_leading);
        row.Children.Add(brand);
        row.Children.Add(buttons);
        Child = row;

        window.PropertyChanged += OnWindowPropertyChanged;
        SyncPin();
        SyncState();
        SyncActive();
    }

    /// <summary>True when the bar draws minimize, maximize and close (Windows and Linux); false on macOS.</summary>
    public bool DrawsCaptionButtons { get; }

    /// <summary>The logo and the window title.</summary>
    public StackPanel Brand { get; }

    /// <summary>The buttons at the end of the bar: the pin, then minimize, maximize and close when the bar draws them.</summary>
    public StackPanel Buttons { get; }

    /// <summary>Keeps the window above other windows (<see cref="Window.Topmost"/>) or lets it go.</summary>
    public Button PinButton => _pin;

    /// <summary>Minimizes the window; null on macOS.</summary>
    public Button? MinimizeButton { get; }

    /// <summary>Maximizes or restores the window; null on macOS.</summary>
    public Button? MaximizeButton => _maximize;

    /// <summary>Closes the window; null on macOS.</summary>
    public Button? CloseButton { get; }

    /// <summary>The width kept free at the left for the macOS traffic lights (0 elsewhere and in full screen).</summary>
    public double LeadingInset => _leading.Width is var width && double.IsFinite(width) ? width : 0;

    /// <summary>Maximizes a normal window and restores a maximized one.</summary>
    public void ToggleMaximize()
    {
        if (!_window.CanResize || !_window.CanMaximize)
        {
            return;
        }
        _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    /// <summary>
    /// The bar's empty part (and the brand) is the window's handle: a press moves the window, a double click maximizes or
    /// restores it. Buttons take their own presses, so a press on one never reaches the bar.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else if (_window.WindowState != WindowState.FullScreen)
        {
            _window.BeginMoveDrag(e);
        }
        e.Handled = true;
    }

    /// <summary>A square ghost Arc button with a glyph; it takes input even where the system would see a title bar.</summary>
    private static Button CaptionButton(Geometry glyph)
    {
        var button = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            IsPressScaleEnabled = false,
            Padding = new Thickness(0),
            Width = ButtonSize,
            Height = ButtonSize,
            MinWidth = ButtonSize,
            MinHeight = ButtonSize,
            Focusable = false,
            Content = new ArcGlyph { Data = glyph, Size = 15 }
        };
        WindowDecorationProperties.SetElementRole(button, WindowDecorationsElementRole.User);
        return button;
    }

    /// <summary>Gives a button its tooltip and its accessible name, both following the language.</summary>
    private static IDisposable Label(Button button, GalleryShellLangResourceKind text)
    {
        var tip = GalleryText.Bind(button, Tooltip.TipProperty, text);
        var name = GalleryText.Bind(button, Avalonia.Automation.AutomationProperties.NameProperty, text);
        return new CompositeLabel(tip, name);
    }

    private sealed class CompositeLabel(IDisposable tip, IDisposable name) : IDisposable
    {
        public void Dispose()
        {
            tip.Dispose();
            name.Dispose();
        }
    }

    /// <summary>Close turns red under the pointer: a danger-tinted fill and a danger glyph, as Arc marks destructive actions.</summary>
    private static void UseDangerHover(Button button)
    {
        var bindings = new List<IDisposable>();
        button.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsPointerOverProperty)
            {
                return;
            }
            foreach (var binding in bindings)
            {
                binding.Dispose();
            }
            bindings.Clear();
            if (button.IsPointerOver)
            {
                bindings.Add(ArcTokens.Bind(button, Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, ArcDesignTokenKind.DangerSubtle, BindingPriority.LocalValue));
                bindings.Add(ArcTokens.Bind(button, Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty, ArcDesignTokenKind.Danger, BindingPriority.LocalValue));
            }
            else
            {
                button.ClearValue(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty);
                button.ClearValue(Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty);
            }
        };
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.TopmostProperty)
        {
            SyncPin();
        }
        else if (e.Property == Window.WindowStateProperty)
        {
            SyncState();
        }
        else if (e.Property == WindowBase.IsActiveProperty)
        {
            SyncActive();
        }
    }

    /// <summary>A pinned window shows the crossed-out pin on a secondary (filled) button; the tooltip says what a press does next.</summary>
    private void SyncPin()
    {
        var pinned = _window.Topmost;
        _pin.Content = new ArcGlyph { Data = pinned ? GalleryIcons.PinOff : GalleryIcons.Pin, Size = 15 };
        _pin.Variant = pinned ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        _pinLabel?.Dispose();
        _pinLabel = Label(_pin, pinned ? GalleryShellLangResourceKind.WindowUnpin : GalleryShellLangResourceKind.WindowPin);
    }

    private void SyncState()
    {
        var state = _window.WindowState;
        // The traffic lights hide in full screen, so their room goes too.
        _leading.Width = !DrawsCaptionButtons && state != WindowState.FullScreen ? TrafficLightsWidth : 0;
        if (_maximize is null)
        {
            return;
        }
        var maximized = state == WindowState.Maximized;
        _maximize.Content = new ArcGlyph { Data = maximized ? ArcIcons.Copy : ArcIcons.Square, Size = maximized ? 14 : 13 };
        _maximize.IsEnabled = _window.CanResize && _window.CanMaximize;
        _maximizeLabel?.Dispose();
        _maximizeLabel = Label(_maximize, maximized ? GalleryShellLangResourceKind.WindowRestore : GalleryShellLangResourceKind.WindowMaximize);
    }

    /// <summary>The title dims while another window is active, as system title bars do.</summary>
    private void SyncActive()
    {
        _titleColor?.Dispose();
        _titleColor = ArcTokens.Bind(_title, TextBlock.ForegroundProperty,
            _window.IsActive ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextMuted);
    }
}
