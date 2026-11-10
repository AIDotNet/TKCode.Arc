using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A segmented control on frosted glass: a row of two to five options on a translucent track that blurs what lies behind
/// it (see <see cref="GlassBackdrop"/>). The selection is a lens of brighter glass that glides to the chosen segment,
/// its leading edge running ahead so it stretches on the way and draws together as it lands. Press the selected segment
/// and the lens swells; drag it and it follows the finger or pointer, choosing the segment it is released over. Arrows,
/// Home and End move the selection (wrapping, skipping disabled segments); only the selected segment is in the Tab order.
/// </summary>
/// <remarks>
/// Options are <see cref="GlassSegment"/>s written inline or any items from <c>ItemsSource</c> (shown as their text).
/// One option is always selected. Under the ArcDesign <c>ReduceTransparency</c> token the track and the lens are opaque.
/// Reduced motion fades the lens to its new place instead of gliding; with motion off it moves at once.
/// </remarks>
[TemplatePart(LensPart, typeof(GlassLens))]
[TemplatePart(PanePart, typeof(GlassPane))]
public class GlassSegmented : SelectingItemsControl
{
    public const string LensPart = "PART_Lens";
    public const string PanePart = "PART_Pane";

    /// <summary>How far the lens swells on every side while it is pressed or dragged, in px (set by the theme).</summary>
    internal static readonly StyledProperty<double> LensSwellProperty =
        AvaloniaProperty.Register<GlassSegmented, double>(nameof(LensSwell), 2);

    private readonly GlassLensTrack _track;
    private GlassLens? _lens;
    private GlassPane? _pane;

    static GlassSegmented()
    {
        SelectionModeProperty.OverrideDefaultValue<GlassSegmented>(SelectionMode.Single | SelectionMode.AlwaysSelected);
        FocusableProperty.OverrideDefaultValue<GlassSegmented>(false);
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<GlassSegmented>(KeyboardNavigationMode.Once);
    }

    public GlassSegmented()
    {
        _track = new GlassLensTrack(this);
        SelectionChanged += OnSelectionChanged;
    }

    internal double LensSwell
    {
        get => GetValue(LensSwellProperty);
        set => SetValue(LensSwellProperty, value);
    }

    internal GlassLens? Lens => _lens;

    internal GlassPane? Pane => _pane;

    internal GlassLensTrack Track => _track;

    protected override Type StyleKeyOverride => typeof(GlassSegmented);

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        => NeedsContainer<GlassSegment>(item, out recycleKey);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) => new GlassSegment();

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
        if (change.Property == LensSwellProperty && _lens is not null)
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

    protected override AutomationPeer OnCreateAutomationPeer() => new GlassSegmentedAutomationPeer(this);

    private sealed class GlassSegmentedAutomationPeer : SelectingItemsControlAutomationPeer
    {
        public GlassSegmentedAutomationPeer(GlassSegmented owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;
    }
}
