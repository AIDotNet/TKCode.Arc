using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace TKCode.Arc;

/// <summary>Who draws minimize, maximize and close (see <see cref="TitleBar.CaptionButtons"/>).</summary>
public enum TitleBarCaptionButtons
{
    /// <summary>The system's choice: <see cref="System"/> on macOS, <see cref="Drawn"/> elsewhere.</summary>
    Auto,

    /// <summary>The title bar draws minimize, maximize / restore and close at its right end.</summary>
    Drawn,

    /// <summary>The system draws them (the macOS traffic lights); the bar leaves room for them at its left.</summary>
    System,

    /// <summary>Neither draws them.</summary>
    None
}

/// <summary>
/// The title bar of an <see cref="ArcWindow"/>: an icon and the window title, then three areas for your own controls (left,
/// center and right) and the caption buttons. Its empty part moves the window and a double click maximizes or restores it;
/// controls placed in the three areas take their own input. On Windows the bar is the system caption (system menu, Aero
/// Snap) and the maximize button is the system's (Snap Layouts). The center content is centered on the whole bar while it
/// fits between the sides. Only the window's own bar drives the window; one placed anywhere else is a preview.
/// </summary>
[TemplatePart(RootPart, typeof(Border))]
[TemplatePart(LeftPresenterPart, typeof(ContentPresenter))]
[TemplatePart(CenterPresenterPart, typeof(ContentPresenter))]
[TemplatePart(RightPresenterPart, typeof(ContentPresenter))]
[TemplatePart(MinimizeButtonPart, typeof(AvaloniaButton))]
[TemplatePart(MaximizeButtonPart, typeof(AvaloniaButton))]
[TemplatePart(CloseButtonPart, typeof(AvaloniaButton))]
[PseudoClasses(MaximizedPseudoClass, FullScreenPseudoClass, InactivePseudoClass, DrawnButtonsPseudoClass,
    SystemButtonsPseudoClass, HasRightPseudoClass)]
public class TitleBar : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string LeftPresenterPart = "PART_LeftPresenter";
    public const string CenterPresenterPart = "PART_CenterPresenter";
    public const string RightPresenterPart = "PART_RightPresenter";
    public const string MinimizeButtonPart = "PART_MinimizeButton";
    public const string MaximizeButtonPart = "PART_MaximizeButton";
    public const string CloseButtonPart = "PART_CloseButton";

    public const string MaximizedPseudoClass = ":maximized";
    public const string FullScreenPseudoClass = ":fullscreen";
    public const string InactivePseudoClass = ":inactive";
    public const string DrawnButtonsPseudoClass = ":drawn-buttons";
    public const string SystemButtonsPseudoClass = ":system-buttons";
    public const string HasRightPseudoClass = ":has-right";

    /// <summary>The icon before the title, such as an <c>Image</c> or an <c>ArcGlyph</c>; none by default.</summary>
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<TitleBar, object?>(nameof(Icon));

    /// <summary>The title. Follows the window's <c>Title</c> unless set.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TitleBar, string?>(nameof(Title));

    /// <summary>Content after the title, at the left of the bar (a menu, navigation).</summary>
    public static readonly StyledProperty<object?> LeftContentProperty =
        AvaloniaProperty.Register<TitleBar, object?>(nameof(LeftContent));

    /// <summary>
    /// Content in the middle of the bar (a search field, tabs). It is centered on the whole bar while it fits between the
    /// sides; give it a <c>Width</c> or <c>MaxWidth</c>.
    /// </summary>
    public static readonly StyledProperty<object?> CenterContentProperty =
        AvaloniaProperty.Register<TitleBar, object?>(nameof(CenterContent));

    /// <summary>Content before the caption buttons, at the right of the bar (tool buttons, a theme switcher).</summary>
    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<TitleBar, object?>(nameof(RightContent));

    /// <summary>
    /// Who draws minimize, maximize and close: the bar (<c>Drawn</c>), the system (<c>System</c>, the macOS traffic lights,
    /// with room left for them) or neither (<c>None</c>). <c>Auto</c> picks <c>System</c> on macOS and <c>Drawn</c> elsewhere.
    /// </summary>
    public static readonly StyledProperty<TitleBarCaptionButtons> CaptionButtonsProperty =
        AvaloniaProperty.Register<TitleBar, TitleBarCaptionButtons>(nameof(CaptionButtons));

    private AvaloniaWindow? _window;
    private IDisposable? _titleBinding;
    private AvaloniaButton? _minimize;
    private AvaloniaButton? _maximize;
    private AvaloniaButton? _close;

    public TitleBar()
    {
        SyncCaptionButtons();
    }

    /// <inheritdoc cref="IconProperty"/>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="LeftContentProperty"/>
    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    /// <inheritdoc cref="CenterContentProperty"/>
    public object? CenterContent
    {
        get => GetValue(CenterContentProperty);
        set => SetValue(CenterContentProperty, value);
    }

    /// <inheritdoc cref="RightContentProperty"/>
    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    /// <inheritdoc cref="CaptionButtonsProperty"/>
    public TitleBarCaptionButtons CaptionButtons
    {
        get => GetValue(CaptionButtonsProperty);
        set => SetValue(CaptionButtonsProperty, value);
    }

    /// <summary>What <see cref="CaptionButtons"/> means on this system: never <see cref="TitleBarCaptionButtons.Auto"/>.</summary>
    public TitleBarCaptionButtons EffectiveCaptionButtons => CaptionButtons == TitleBarCaptionButtons.Auto
        ? OperatingSystem.IsMacOS() ? TitleBarCaptionButtons.System : TitleBarCaptionButtons.Drawn
        : CaptionButtons;

    /// <summary>Maximizes a normal window and restores a maximized one; does nothing when the window cannot be maximized.</summary>
    public void ToggleMaximize()
    {
        var window = _window;
        if (window is null || !window.CanResize || !window.CanMaximize)
        {
            return;
        }
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook(_minimize, OnMinimizeClick);
        Unhook(_maximize, OnMaximizeClick);
        Unhook(_close, OnCloseClick);

        _minimize = e.NameScope.Find<AvaloniaButton>(MinimizeButtonPart);
        _maximize = e.NameScope.Find<AvaloniaButton>(MaximizeButtonPart);
        _close = e.NameScope.Find<AvaloniaButton>(CloseButtonPart);
        Hook(_minimize, OnMinimizeClick);
        Hook(_maximize, OnMaximizeClick);
        Hook(_close, OnCloseClick);

        // Controls placed in the three areas take their own input where the system would see a title bar; the areas'
        // empty parts are not hit, so they stay the bar.
        ContentPresenter?[] presenters =
        [
            e.NameScope.Find<ContentPresenter>(LeftPresenterPart),
            e.NameScope.Find<ContentPresenter>(CenterPresenterPart),
            e.NameScope.Find<ContentPresenter>(RightPresenterPart)
        ];
        foreach (var presenter in presenters)
        {
            if (presenter is not null)
            {
                WindowDecorationProperties.SetElementRole(presenter, WindowDecorationsElementRole.User);
            }
        }
        SyncRole();
        SyncButtons();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Only the window's own bar drives it: a bar shown elsewhere in the window (a preview) moves and closes nothing.
        Attach(TopLevel.GetTopLevel(this) is ArcWindow window && ReferenceEquals(window.TitleBar, this) ? window : null);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Attach(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CaptionButtonsProperty)
        {
            SyncCaptionButtons();
        }
        else if (change.Property == RightContentProperty)
        {
            PseudoClasses.Set(HasRightPseudoClass, change.NewValue is not null);
        }
    }

    /// <summary>
    /// A press that no control took moves the window, and a double click maximizes or restores it. On Windows the bar's
    /// empty part is the system caption and its presses never get here; what does is content in the three areas that does
    /// not handle the press itself, such as a label, which then behaves like the empty part.
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var window = _window;
        if (e.Handled || window is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else if (window.WindowState != WindowState.FullScreen)
        {
            window.BeginMoveDrag(e);
        }
        e.Handled = true;
    }

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
        _titleBinding?.Dispose();
        _titleBinding = null;
        _window = window;
        if (window is not null)
        {
            window.PropertyChanged += OnWindowPropertyChanged;
            // Template priority: a title set on the bar wins over the window's.
            _titleBinding = this.Bind(TitleProperty, window.GetObservable(AvaloniaWindow.TitleProperty), BindingPriority.Template);
        }
        SyncRole();
        SyncState();
        SyncActive();
        SyncButtons();
    }

    /// <summary>
    /// On Windows the bar is the system caption: the system moves the window, maximizes on a double click, shows its menu on
    /// a right click and snaps. On macOS the system would act only on the very element hit and still pass the press on, and
    /// on Linux it would swallow the press and with it the double click, so there the bar moves the window itself. The caption
    /// buttons tell the system what they are (the maximize button opens Snap Layouts on Windows 11) while the bar is the
    /// window's.
    /// </summary>
    private void SyncRole()
    {
        var attached = _window is not null;
        WindowDecorationProperties.SetElementRole(this, attached && OperatingSystem.IsWindows()
            ? WindowDecorationsElementRole.TitleBar
            : WindowDecorationsElementRole.None);
        SetRole(_minimize, attached ? WindowDecorationsElementRole.MinimizeButton : WindowDecorationsElementRole.None);
        SetRole(_maximize, attached ? WindowDecorationsElementRole.MaximizeButton : WindowDecorationsElementRole.None);
        SetRole(_close, attached ? WindowDecorationsElementRole.CloseButton : WindowDecorationsElementRole.None);
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
        else if (e.Property == WindowBase.IsActiveProperty)
        {
            SyncActive();
        }
        else if (e.Property == AvaloniaWindow.CanResizeProperty
                 || e.Property == AvaloniaWindow.CanMaximizeProperty
                 || e.Property == AvaloniaWindow.CanMinimizeProperty)
        {
            SyncButtons();
        }
    }

    private void SyncState()
    {
        var state = _window?.WindowState ?? WindowState.Normal;
        PseudoClasses.Set(MaximizedPseudoClass, state == WindowState.Maximized);
        PseudoClasses.Set(FullScreenPseudoClass, state == WindowState.FullScreen);
    }

    /// <summary>The title dims while another window is active, as system title bars do.</summary>
    private void SyncActive() => PseudoClasses.Set(InactivePseudoClass, _window is { IsActive: false });

    private void SyncCaptionButtons()
    {
        var effective = EffectiveCaptionButtons;
        PseudoClasses.Set(DrawnButtonsPseudoClass, effective == TitleBarCaptionButtons.Drawn);
        PseudoClasses.Set(SystemButtonsPseudoClass, effective == TitleBarCaptionButtons.System);
    }

    private void SyncButtons()
    {
        SyncCaptionButtons();
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

    private void OnMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximize();

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
