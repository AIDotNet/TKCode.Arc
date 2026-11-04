using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace TKCode.Arc;

/// <summary>
/// A desktop window whose client area extends into the title bar, with an Arc <see cref="TitleBar"/> drawn at the top. The
/// title bar's empty part moves the window, a double click maximizes or restores it, and on Windows the system treats it as
/// the caption (system menu, Aero Snap) and its maximize button as the system's (Snap Layouts). The window keeps resizing
/// and, on macOS, the traffic lights. Its template hosts the bar and the content in a visual layer manager, so popups,
/// toasts and tooltips work in both.
/// </summary>
/// <remarks>
/// Named <c>ArcWindow</c>, not <c>Window</c>: C# looks up types in enclosing namespaces before <c>using</c> directives, so a
/// <c>TKCode.Arc.Window</c> would silently replace Avalonia's <c>Window</c> in every file under a <c>TKCode.Arc</c> namespace.
/// </remarks>
[TemplatePart(RootPart, typeof(Border))]
[TemplatePart(TitleBarPresenterPart, typeof(ContentPresenter))]
public class ArcWindow : AvaloniaWindow
{
    public const string RootPart = "PART_Root";
    public const string TitleBarPresenterPart = "PART_TitleBarPresenter";

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
    /// An empty theme for the decorations Avalonia draws where the system leaves them to the app (Windows, Linux): it draws
    /// no title bar of its own, so the Arc bar is the only one. The resize grips are not part of the theme and stay.
    /// </summary>
    private static readonly ControlTheme s_noDrawnDecorations = new(typeof(WindowDrawnDecorations));

    private Border? _root;
    private TitleBar? _observedBar;

    static ArcWindow()
    {
        ExtendClientAreaToDecorationsHintProperty.OverrideDefaultValue<ArcWindow>(true);
        WindowDecorationsThemeProperty.OverrideDefaultValue<ArcWindow>(s_noDrawnDecorations);
    }

    public ArcWindow()
    {
        SetCurrentValue(TitleBarProperty, new TitleBar());
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

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Border>(RootPart);
        SyncOffScreenMargin();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleBarProperty)
        {
            ObserveBar(change.GetNewValue<TitleBar?>());
        }
        else if (change.Property == IsTitleBarVisibleProperty)
        {
            SyncTitleBarHeightHint();
        }
        else if (change.Property == WindowStateProperty || change.Property == OffScreenMarginProperty)
        {
            SyncOffScreenMargin();
            // The platform reports the off-screen margin of a maximized window after the state change.
            Dispatcher.UIThread.Post(SyncOffScreenMargin);
        }
    }

    /// <summary>Windows keeps the frame of a maximized window off screen; the root steps in by that much.</summary>
    private void SyncOffScreenMargin()
    {
        if (_root is not null)
        {
            _root.Margin = WindowState == WindowState.Maximized ? OffScreenMargin : default;
        }
    }

    private void ObserveBar(TitleBar? bar)
    {
        if (_observedBar is not null)
        {
            _observedBar.PropertyChanged -= OnBarPropertyChanged;
        }
        _observedBar = bar;
        if (bar is not null)
        {
            bar.PropertyChanged += OnBarPropertyChanged;
        }
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
