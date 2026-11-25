using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace TKCode.Arc;

/// <summary>
/// A desktop window whose client area extends into the title bar, with an Arc <see cref="TitleBar"/> drawn at the top. The
/// title bar's empty part moves the window, a double click maximizes or restores it, and on Windows the system treats it as
/// the caption (system menu, Aero Snap) and its maximize button as the system's (Snap Layouts). The window keeps resizing
/// and, on macOS, the traffic lights. Its template hosts the bar and the content in a visual layer manager, so popups,
/// toasts and tooltips work in both, while in-app modals mount in the content area by default so the title bar stays
/// interactive (set <see cref="OverlayCoversTitleBar"/> to true to cover the whole window instead).
/// </summary>
/// <remarks>
/// Named <c>ArcWindow</c>, not <c>Window</c>: C# looks up types in enclosing namespaces before <c>using</c> directives, so a
/// <c>TKCode.Arc.Window</c> would silently replace Avalonia's <c>Window</c> in every file under a <c>TKCode.Arc</c> namespace.
/// </remarks>
[TemplatePart(RootPart, typeof(Border))]
[TemplatePart(TitleBarPresenterPart, typeof(ContentPresenter))]
[TemplatePart(ContentOverlayHostPart, typeof(ArcOverlayHost))]
[PseudoClasses(TopEdgePseudoClass, FrameEdgePseudoClass)]
public class ArcWindow : AvaloniaWindow, IArcContentOverlayWindow
{
    public const string RootPart = "PART_Root";
    public const string TitleBarPresenterPart = "PART_TitleBarPresenter";
    public const string ContentOverlayHostPart = "PART_ContentOverlayHost";

    /// <summary>
    /// Set on Windows while the window is in its normal state with system decorations: DWM draws the 1 px frame on the
    /// left, right and bottom edges but not on the top edge of a window whose client area extends into the title bar, so
    /// the theme draws that edge (issue #102).
    /// </summary>
    public const string TopEdgePseudoClass = ":top-edge";

    /// <summary>
    /// Set on Linux while the window is in its normal state with extended client area and <c>BorderOnly</c> decorations:
    /// X11 removes the window manager's server-side frame and Avalonia's drawn decorations are hidden, so the theme draws
    /// a 1 px frame around all four edges (issue #201).
    /// </summary>
    public const string FrameEdgePseudoClass = ":frame-edge";

    // Applied on Linux while ExtendClientAreaToDecorationsHint is true so the theme sets WindowDecorations="BorderOnly"
    // at theme priority: X11 clears _MOTIF_WM_HINTS (removing the system title bar) while keeping resize grips active,
    // and any explicit local WindowDecorations value still wins (#201).
    internal const string LinuxExtendedPseudoClass = ":linux-extended";

    /// <summary>
    /// The title bar at the top of the window. A new <see cref="Arc.TitleBar"/> by default; set your own to fill its left,
    /// center and right content, or null for none.
    /// </summary>
    public static readonly StyledProperty<TitleBar?> TitleBarProperty =
        AvaloniaProperty.Register<ArcWindow, TitleBar?>(nameof(TitleBar));

    /// <summary>
    /// Whether the title bar shows. Without it the content reaches the top of the window, which still extends into the
    /// decorations; set <c>ExtendClientAreaToDecorationsHint</c> to false as well to get the system's title bar back.
    /// </summary>
    public static readonly StyledProperty<bool> IsTitleBarVisibleProperty =
        AvaloniaProperty.Register<ArcWindow, bool>(nameof(IsTitleBarVisible), true);

    /// <summary>
    /// Whether in-app modal overlays (<see cref="Dialog"/>, <see cref="Drawer"/>, <see cref="BottomSheet"/>) cover the
    /// <see cref="TitleBar"/> as well as the content. False by default, so the title bar stays sharp, draggable and
    /// clickable (minimize, maximize, close) while a modal is open; set to true to cover the whole window (#197).
    /// </summary>
    public static readonly StyledProperty<bool> OverlayCoversTitleBarProperty =
        AvaloniaProperty.Register<ArcWindow, bool>(nameof(OverlayCoversTitleBar));

    /// <summary>
    /// What the drawn minimize and maximize buttons show while the window does not allow them (<c>CanMinimize</c>, or
    /// <c>CanResize</c> and <c>CanMaximize</c>, false): <c>Disable</c> (the default) keeps them disabled, <c>Hide</c> hides
    /// them. Inherited by the window's <see cref="Arc.TitleBar"/> and any <see cref="WindowCaptionButtons"/> in it. The
    /// macOS traffic lights are the system's and stay as they are.
    /// </summary>
    public static readonly StyledProperty<UnavailableCaptionButtonMode> UnavailableCaptionButtonsProperty =
        WindowCaptionButtons.UnavailableCaptionButtonsProperty.AddOwner<ArcWindow>();

    /// <summary>
    /// An empty theme for the decorations Avalonia draws where the system leaves them to the app (Windows, Linux): it draws
    /// no title bar of its own, so the Arc bar is the only one. The resize grips are not part of the theme and stay.
    /// </summary>
    private static readonly ControlTheme s_noDrawnDecorations = new(typeof(WindowDrawnDecorations));

    private Border? _root;
    private ArcOverlayHost? _contentOverlayHost;
    private ContentPresenter? _contentPresenter;
    private TitleBar? _observedBar;

    static ArcWindow()
    {
        ExtendClientAreaToDecorationsHintProperty.OverrideDefaultValue<ArcWindow>(true);
        WindowDecorationsThemeProperty.OverrideDefaultValue<ArcWindow>(s_noDrawnDecorations);
    }

    public ArcWindow()
    {
        SetCurrentValue(TitleBarProperty, new TitleBar());
        SyncLinuxDecorations();
    }

    /// <summary>Avalonia's <c>Window</c> styles every subclass as a <c>Window</c>; this one has its own theme.</summary>
    protected override Type StyleKeyOverride => typeof(ArcWindow);

    /// <inheritdoc cref="TitleBarProperty"/>
    public TitleBar? TitleBar
    {
        get => GetValue(TitleBarProperty);
        set => SetValue(TitleBarProperty, value);
    }

    /// <inheritdoc cref="IsTitleBarVisibleProperty"/>
    public bool IsTitleBarVisible
    {
        get => GetValue(IsTitleBarVisibleProperty);
        set => SetValue(IsTitleBarVisibleProperty, value);
    }

    /// <inheritdoc cref="OverlayCoversTitleBarProperty"/>
    public bool OverlayCoversTitleBar
    {
        get => GetValue(OverlayCoversTitleBarProperty);
        set => SetValue(OverlayCoversTitleBarProperty, value);
    }

    /// <inheritdoc cref="UnavailableCaptionButtonsProperty"/>
    public UnavailableCaptionButtonMode UnavailableCaptionButtons
    {
        get => GetValue(UnavailableCaptionButtonsProperty);
        set => SetValue(UnavailableCaptionButtonsProperty, value);
    }

    ArcOverlayHost? IArcContentOverlayWindow.ActiveContentOverlayHost =>
        ShouldHostModalsInContent ? _contentOverlayHost : null;

    private bool ShouldHostModalsInContent =>
        !OverlayCoversTitleBar && IsTitleBarVisible && TitleBar is not null;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Border>(RootPart);
        _contentOverlayHost = e.NameScope.Find<ArcOverlayHost>(ContentOverlayHostPart);
        _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
        if (_contentOverlayHost is not null)
        {
            _contentOverlayHost.BlurTarget = _contentPresenter;
        }
        SyncOverlayHost();
        SyncLinuxDecorations();
        SyncEdges();
        SyncOffScreenMargin();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleBarProperty)
        {
            ObserveBar(change.GetNewValue<TitleBar?>());
            SyncOverlayHost();
        }
        else if (change.Property == IsTitleBarVisibleProperty)
        {
            SyncTitleBarHeightHint();
            SyncOverlayHost();
        }
        else if (change.Property == OverlayCoversTitleBarProperty)
        {
            SyncOverlayHost();
        }
        else if (change.Property == ExtendClientAreaToDecorationsHintProperty)
        {
            SyncLinuxDecorations();
            SyncEdges();
        }
        else if (change.Property == WindowDecorationsProperty)
        {
            SyncEdges();
        }
        else if (change.Property == WindowStateProperty || change.Property == OffScreenMarginProperty)
        {
            SyncEdges();
            SyncOffScreenMargin();
            // The platform reports the off-screen margin of a maximized window after the state change.
            Dispatcher.UIThread.Post(SyncOffScreenMargin);
        }
    }

    private void SyncOverlayHost()
    {
        if (_contentOverlayHost is not null)
        {
            _contentOverlayHost.IsModalHostEnabled = ShouldHostModalsInContent;
        }
    }

    private void SyncLinuxDecorations() =>
        PseudoClasses.Set(LinuxExtendedPseudoClass,
            UsesLinuxBorderOnly(OperatingSystem.IsLinux(), ExtendClientAreaToDecorationsHint));

    private void SyncEdges()
    {
        PseudoClasses.Set(TopEdgePseudoClass,
            ShowsTopEdge(OperatingSystem.IsWindows(), ExtendClientAreaToDecorationsHint, WindowDecorations, WindowState));
        PseudoClasses.Set(FrameEdgePseudoClass,
            ShowsFrameEdge(OperatingSystem.IsLinux(), ExtendClientAreaToDecorationsHint, WindowDecorations, WindowState));
    }

    /// <summary>
    /// Whether the theme sets <c>WindowDecorations="BorderOnly"</c>: on Linux while the client area extends into the
    /// decorations, so X11 clears the window manager's title bar while Avalonia keeps the resize grips (#201).
    /// </summary>
    internal static bool UsesLinuxBorderOnly(bool isLinux, bool extendsIntoDecorations) =>
        isLinux && extendsIntoDecorations;

    /// <summary>
    /// Whether the theme draws the top edge: only on Windows, where the extended client area covers DWM's top frame line,
    /// and only in the normal state with system decorations (a maximized or full-screen window has no visible frame, and
    /// without decorations there is no frame on the other edges either).
    /// </summary>
    internal static bool ShowsTopEdge(bool isWindows, bool extendsIntoDecorations, WindowDecorations decorations,
        WindowState state) =>
        isWindows && extendsIntoDecorations && decorations != Avalonia.Controls.WindowDecorations.None
        && state == WindowState.Normal;

    /// <summary>
    /// Whether the theme draws a 1 px border around all four edges: on Linux in the normal state when extended client area
    /// uses <c>BorderOnly</c>, where X11 removes the server-side frame and Avalonia's drawn decorations are hidden (#201).
    /// </summary>
    internal static bool ShowsFrameEdge(bool isLinux, bool extendsIntoDecorations, WindowDecorations decorations,
        WindowState state) =>
        isLinux && extendsIntoDecorations && decorations == Avalonia.Controls.WindowDecorations.BorderOnly
        && state == WindowState.Normal;

    /// <summary>Windows keeps the frame of a maximized window off screen; the root steps in by that much.</summary>
    private void SyncOffScreenMargin()
    {
        _root?.Margin = WindowState == WindowState.Maximized ? OffScreenMargin : default;
    }

    private void ObserveBar(TitleBar? bar)
    {
        _observedBar?.PropertyChanged -= OnBarPropertyChanged;
        _observedBar = bar;
        bar?.PropertyChanged += OnBarPropertyChanged;
        SyncTitleBarHeightHint();
    }

    private void OnBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == HeightProperty || e.Property == BoundsProperty || e.Property == IsVisibleProperty)
        {
            SyncTitleBarHeightHint();
        }
    }

    /// <summary>
    /// The system's title bar is as tall as the Arc bar: macOS centers the traffic lights in it, Windows takes it as the
    /// caption band. Without a bar the system's default (-1) keeps the top edge resizable.
    /// </summary>
    private void SyncTitleBarHeightHint()
    {
        var bar = _observedBar;
        var height = -1.0;
        if (bar is not null && IsTitleBarVisible && bar.IsVisible)
        {
            height = double.IsFinite(bar.Height) && bar.Height > 0 ? bar.Height : bar.Bounds.Height;
            if (height <= 0)
            {
                // Not measured yet: keep the hint the bar's last layout gave.
                return;
            }
        }
        if (!ExtendClientAreaTitleBarHeightHint.Equals(height))
        {
            SetCurrentValue(ExtendClientAreaTitleBarHeightHintProperty, height);
        }
    }
}
