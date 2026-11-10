using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A floating bottom tab bar of frosted glass for an app shell: a pill that hovers over the content near the bottom edge,
/// blurring what scrolls under it (see <see cref="GlassBackdrop"/>), with three to five destinations. The selected tab
/// sits in a lens of brighter glass that glides and stretches to the next tab; press and drag the lens to scrub across the
/// tabs. Arrows, Home and End move between tabs (wrapping); only the selected tab is in the Tab order.
/// </summary>
/// <remarks>
/// Tabs are <see cref="GlassTab"/>s written inline or any items from <c>ItemsSource</c>. One tab is always selected; react
/// to <c>SelectionChanged</c> or bind <c>SelectedIndex</c> to switch the page. Under the ArcDesign
/// <c>ReduceTransparency</c> token the bar and the lens are opaque. Reduced motion fades the lens to the new tab; with
/// motion off it moves at once.
/// </remarks>
[TemplatePart(LensPart, typeof(GlassLens))]
[TemplatePart(PanePart, typeof(GlassPane))]
[PseudoClasses(CompactPseudoClass)]
public class GlassTabBar : SelectingItemsControl
{
    public const string LensPart = "PART_Lens";
    public const string PanePart = "PART_Pane";
    public const string CompactPseudoClass = ":compact";

    /// <summary>
    /// Whether each tab shows its label under the icon (true, the default). False shows icons only, in a lower bar; the
    /// labels remain the tabs' accessible names.
    /// </summary>
    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<GlassTabBar, bool>(nameof(ShowLabels), true);

    internal static readonly StyledProperty<double> LensSwellProperty =
        AvaloniaProperty.Register<GlassTabBar, double>(nameof(LensSwell), 3);

    private readonly GlassLensTrack _track;
    private GlassLens? _lens;
    private GlassPane? _pane;

    static GlassTabBar()
    {
        SelectionModeProperty.OverrideDefaultValue<GlassTabBar>(SelectionMode.Single | SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<GlassTabBar>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<GlassTabBar>(KeyboardNavigationMode.Once);
    }

    public GlassTabBar()
    {
        _track = new GlassLensTrack(this);
        SelectionChanged += OnSelectionChanged;
    }

    /// <inheritdoc cref="ShowLabelsProperty"/>
    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    internal double LensSwell
    {
        get => GetValue(LensSwellProperty);
        set => SetValue(LensSwellProperty, value);
    }

    internal GlassLens? Lens => _lens;

    internal GlassPane? Pane => _pane;

    internal GlassLensTrack Track => _track;

    protected override Type StyleKeyOverride => typeof(GlassTabBar);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<GlassTab>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new GlassTab();

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is GlassTab tab)
        {
            tab.SetCompact(!ShowLabels);
        }
    }

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        _track.UpdateTabStop();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _pane = e.NameScope.Find<GlassPane>(PanePart);
        _lens = e.NameScope.Find<GlassLens>(LensPart);
        if (_lens is not null)
        {
            _lens.Swell = LensSwell;
        }
        _track.Attach(_lens);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ShowLabelsProperty)
        {
            var compact = !ShowLabels;
            PseudoClasses.Set(CompactPseudoClass, compact);
            for (var i = 0; i < ItemCount; i++)
            {
                if (ContainerFromIndex(i) is GlassTab tab)
                {
                    tab.SetCompact(compact);
                }
            }
        }
        else if (change.Property == LensSwellProperty && _lens is not null)
        {
            _lens.Swell = LensSwell;
        }
        else if (change.Property == IsEnabledProperty)
        {
            _track.UpdateTabStop();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        LayoutUpdated += OnLayoutUpdated;
        _track.Sync(false);
        _track.UpdateTabStop();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        LayoutUpdated -= OnLayoutUpdated;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => _track.OnLayoutUpdated();

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _track.Sync(true);
        _track.UpdateTabStop();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && _track.OnPointerPressed(e))
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _track.OnPointerMoved(e);
        _pane?.PointAt(e.GetPosition(_pane));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pane?.PointAt(null);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_track.OnPointerReleased(e))
        {
            e.Handled = true;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _track.OnPointerCaptureLost(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_track.HandleKey(e))
        {
            return;
        }
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GlassTabBarAutomationPeer(this);

    private sealed class GlassTabBarAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public GlassTabBarAutomationPeer(GlassTabBar owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
