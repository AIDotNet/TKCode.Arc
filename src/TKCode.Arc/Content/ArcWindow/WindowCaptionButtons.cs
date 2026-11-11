using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace TKCode.Arc;

/// <summary>
/// Minimize, maximize / restore and close for a window whose content reaches the top (an <see cref="ArcWindow"/> with
/// <c>IsTitleBarVisible="False"</c>), placed wherever the app draws its own header. The buttons drive the window they are
/// in and follow it: maximize shows restore while the window is maximized and is disabled while the window cannot grow
/// (<c>CanResize</c> or <c>CanMaximize</c> false), minimize while <c>CanMinimize</c> is false. On Windows they tell the
/// system what they are, so the maximize button opens Snap Layouts. <see cref="TitleBar"/> draws its own caption buttons
/// with this control; show one set per window.
/// </summary>
[TemplatePart(MinimizeButtonPart, typeof(AvaloniaButton))]
[TemplatePart(MaximizeButtonPart, typeof(AvaloniaButton))]
[TemplatePart(CloseButtonPart, typeof(AvaloniaButton))]
[TemplatePart(MaximizeGlyphPart, typeof(ArcGlyph))]
[PseudoClasses(MaximizedPseudoClass)]
public class WindowCaptionButtons : TemplatedControl
{
    public const string MinimizeButtonPart = "PART_MinimizeButton";
    public const string MaximizeButtonPart = "PART_MaximizeButton";
    public const string CloseButtonPart = "PART_CloseButton";
    public const string MaximizeGlyphPart = "PART_MaximizeGlyph";

    /// <summary>Set while the window is maximized: the maximize button shows restore.</summary>
    public const string MaximizedPseudoClass = ":maximized";

    /// <summary>Shows the minimize button. True by default.</summary>
    public static readonly StyledProperty<bool> ShowMinimizeButtonProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, bool>(nameof(ShowMinimizeButton), true);

    /// <summary>Shows the maximize / restore button. True by default.</summary>
    public static readonly StyledProperty<bool> ShowMaximizeButtonProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, bool>(nameof(ShowMaximizeButton), true);

    /// <summary>Shows the close button. True by default.</summary>
    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, bool>(nameof(ShowCloseButton), true);

    /// <summary>
    /// True draws the buttons without driving the window: they minimize, maximize and close nothing and the system does
    /// not treat them as caption buttons. For previews, such as a mock-up of a window inside a page. False by default.
    /// </summary>
    public static readonly StyledProperty<bool> IsPreviewProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, bool>(nameof(IsPreview));

    // Geometry comes from WindowCaptionButtonsToken; a TitleBar passes its own tokens instead (TitleBarTheme).
    internal static readonly StyledProperty<double> ButtonSizeProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, double>(nameof(ButtonSize), 30);

    internal static readonly StyledProperty<double> ButtonSpacingProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, double>(nameof(ButtonSpacing), 4);

    internal static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, double>(nameof(GlyphSize), 15);

    internal static readonly StyledProperty<double> MaximizeGlyphSizeProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, double>(nameof(MaximizeGlyphSize), 13);

    internal static readonly StyledProperty<double> RestoreGlyphSizeProperty =
        AvaloniaProperty.Register<WindowCaptionButtons, double>(nameof(RestoreGlyphSize), 14);

    private AvaloniaWindow? _window;
    private AvaloniaButton? _minimize;
    private AvaloniaButton? _maximize;
    private AvaloniaButton? _close;
    private ArcGlyph? _maximizeGlyph;

    /// <inheritdoc cref="ShowMinimizeButtonProperty"/>
    public bool ShowMinimizeButton
    {
        get => GetValue(ShowMinimizeButtonProperty);
        set => SetValue(ShowMinimizeButtonProperty, value);
    }

    /// <inheritdoc cref="ShowMaximizeButtonProperty"/>
    public bool ShowMaximizeButton
    {
        get => GetValue(ShowMaximizeButtonProperty);
        set => SetValue(ShowMaximizeButtonProperty, value);
    }

    /// <inheritdoc cref="ShowCloseButtonProperty"/>
    public bool ShowCloseButton
    {
        get => GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    /// <inheritdoc cref="IsPreviewProperty"/>
    public bool IsPreview
    {
        get => GetValue(IsPreviewProperty);
        set => SetValue(IsPreviewProperty, value);
    }

    internal double ButtonSize
    {
        get => GetValue(ButtonSizeProperty);
        set => SetValue(ButtonSizeProperty, value);
    }

    internal double ButtonSpacing
    {
        get => GetValue(ButtonSpacingProperty);
        set => SetValue(ButtonSpacingProperty, value);
    }

    internal double GlyphSize
    {
        get => GetValue(GlyphSizeProperty);
        set => SetValue(GlyphSizeProperty, value);
    }

    internal double MaximizeGlyphSize
    {
        get => GetValue(MaximizeGlyphSizeProperty);
        set => SetValue(MaximizeGlyphSizeProperty, value);
    }

    internal double RestoreGlyphSize
    {
        get => GetValue(RestoreGlyphSizeProperty);
        set => SetValue(RestoreGlyphSizeProperty, value);
    }

    /// <summary>The window the buttons drive, or null (a preview, or not in a window).</summary>
    internal AvaloniaWindow? Window => _window;

    /// <summary>
    /// The buttons of a <see cref="TitleBar"/>'s template: the bar decides which window they drive (only its own window's,
    /// and only while it draws them), so they never look for one themselves.
    /// </summary>
    private bool IsInTitleBar => TemplatedParent is TitleBar;

    /// <summary>Maximizes a normal window and restores a maximized one; does nothing when the window cannot be maximized.</summary>
    internal static void ToggleMaximize(AvaloniaWindow? window)
    {
        if (window is null || !window.CanResize || !window.CanMaximize)
        {
            return;
        }
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    /// <summary>Called by the hosting <see cref="TitleBar"/>: the window to drive, or null for none.</summary>
    internal void Drive(AvaloniaWindow? window) => Attach(IsPreview ? null : window);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook(_minimize, OnMinimizeClick);
        Unhook(_maximize, OnMaximizeClick);
        Unhook(_close, OnCloseClick);

        _minimize = e.NameScope.Find<AvaloniaButton>(MinimizeButtonPart);
        _maximize = e.NameScope.Find<AvaloniaButton>(MaximizeButtonPart);
        _close = e.NameScope.Find<AvaloniaButton>(CloseButtonPart);
        _maximizeGlyph = e.NameScope.Find<ArcGlyph>(MaximizeGlyphPart);
        Hook(_minimize, OnMinimizeClick);
        Hook(_maximize, OnMaximizeClick);
        Hook(_close, OnCloseClick);
        SyncRoles();
        SyncButtons();
        SyncState();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!IsInTitleBar)
        {
            Attach(FindWindow());
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Attach(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPreviewProperty)
        {
            if (change.GetNewValue<bool>())
            {
                Attach(null);
            }
            else if (!IsInTitleBar)
            {
                Attach(FindWindow());
            }
        }
        else if (change.Property == MaximizeGlyphSizeProperty || change.Property == RestoreGlyphSizeProperty)
        {
            SyncState();
        }
    }

    private AvaloniaWindow? FindWindow() => IsPreview ? null : TopLevel.GetTopLevel(this) as AvaloniaWindow;

    private void Attach(AvaloniaWindow? window)
    {
        if (ReferenceEquals(window, _window))
        {
            return;
        }
        if (_window is not null)
        {
            _window.PropertyChanged -= OnWindowPropertyChanged;
        }
        _window = window;
        if (window is not null)
        {
            window.PropertyChanged += OnWindowPropertyChanged;
        }
        SyncRoles();
        SyncButtons();
        SyncState();
    }

    /// <summary>
    /// While the buttons drive a window they tell the system what they are: on Windows a press on them is the system's
    /// minimize, maximize or close, and pointing at maximize opens Snap Layouts on Windows 11. Elsewhere the roles let the
    /// press through to the buttons. Buttons that drive nothing have no role, so a preview never acts as the window's.
    /// </summary>
    private void SyncRoles()
    {
        var driven = _window is not null;
        SetRole(_minimize, driven ? WindowDecorationsElementRole.MinimizeButton : WindowDecorationsElementRole.None);
        SetRole(_maximize, driven ? WindowDecorationsElementRole.MaximizeButton : WindowDecorationsElementRole.None);
        SetRole(_close, driven ? WindowDecorationsElementRole.CloseButton : WindowDecorationsElementRole.None);
    }

    private static void SetRole(AvaloniaButton? button, WindowDecorationsElementRole role)
    {
        if (button is not null)
        {
            WindowDecorationProperties.SetElementRole(button, role);
        }
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AvaloniaWindow.WindowStateProperty)
        {
            SyncState();
        }
        else if (e.Property == AvaloniaWindow.CanResizeProperty
                 || e.Property == AvaloniaWindow.CanMaximizeProperty
                 || e.Property == AvaloniaWindow.CanMinimizeProperty)
        {
            SyncButtons();
        }
    }

    /// <summary>Maximize shows restore while the window is maximized: glyph here, tooltip and name in the theme.</summary>
    private void SyncState()
    {
        var maximized = _window?.WindowState == WindowState.Maximized;
        PseudoClasses.Set(MaximizedPseudoClass, maximized);
        if (_maximizeGlyph is not null)
        {
            _maximizeGlyph.Data = maximized ? ArcIcons.Copy : ArcIcons.Square;
            _maximizeGlyph.Size = maximized ? RestoreGlyphSize : MaximizeGlyphSize;
        }
    }

    /// <summary>The buttons follow what the window allows; with no window to drive they stay enabled.</summary>
    private void SyncButtons()
    {
        var window = _window;
        if (_minimize is not null)
        {
            _minimize.IsEnabled = window is null || window.CanMinimize;
        }
        if (_maximize is not null)
        {
            _maximize.IsEnabled = window is null || (window.CanResize && window.CanMaximize);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (_window is not null)
        {
            _window.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize(_window);

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _window?.Close();

    private static void Hook(AvaloniaButton? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unhook(AvaloniaButton? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }
}
