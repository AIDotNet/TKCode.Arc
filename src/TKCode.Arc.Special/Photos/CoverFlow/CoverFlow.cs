using System.Collections;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A row of covers in perspective: the centred cover faces forward with its title underneath, and the covers on either
/// side turn toward it, stacked into the distance with their reflections on the floor. Drag the row and it follows the
/// hand; let go and it coasts with the flick's momentum, then snaps a cover to the centre. Clicking a side cover brings
/// it to the centre and clicking the centred one opens it (<see cref="ItemInvoked"/>).
/// </summary>
/// <remarks>
/// Covers come from <see cref="Items"/> (written inline) or <see cref="ItemsSource"/>, whose elements may be
/// <see cref="CoverFlowItem"/>s, <see cref="ImageSource"/>s or addresses. <see cref="SelectedIndex"/> is the centred cover
/// and can be bound both ways. Each cover is a light control of its own, so the row suits up to a few hundred covers.
/// Under reduced motion the row moves to the chosen cover at once and only the caption cross-fades.
/// </remarks>
[TemplatePart(StagePart, typeof(CoverFlowStage))]
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(CaptionPart, typeof(ArcContentSwap))]
[TemplatePart(StatusPart, typeof(TextBlock))]
public class CoverFlow : TemplatedControl
{
    internal const string StagePart = "PART_Stage";
    internal const string RootPart = "PART_Root";
    internal const string CaptionPart = "PART_Caption";
    internal const string StatusPart = "PART_Status";

    /// <summary>The covers written inline, used while <see cref="ItemsSource"/> is null.</summary>
    public static readonly DirectProperty<CoverFlow, AvaloniaList<CoverFlowItem>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<CoverFlow, AvaloniaList<CoverFlowItem>>(nameof(Items), o => o.Items);

    /// <summary>
    /// The covers to show: <see cref="CoverFlowItem"/>s, <see cref="ImageSource"/>s or addresses. Wins over
    /// <see cref="Items"/>; a collection that reports changes is followed.
    /// </summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<CoverFlow, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The index of the centred cover; -1 when there are none. Two-way.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<CoverFlow, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The width of a cover in px (default 180).</summary>
    public static readonly StyledProperty<double> CoverWidthProperty =
        AvaloniaProperty.Register<CoverFlow, double>(nameof(CoverWidth), 180,
            coerce: static (_, value) => double.IsFinite(value) ? Math.Clamp(value, 40, 800) : 180);

    /// <summary>A cover's width divided by its height (default 1, square like an album; 2:3 is a book).</summary>
    public static readonly StyledProperty<double> CoverAspectRatioProperty =
        AvaloniaProperty.Register<CoverFlow, double>(nameof(CoverAspectRatio), 1,
            coerce: static (_, value) => double.IsFinite(value) && value > 0 ? value : 1);

    /// <summary>How far the side covers turn, in degrees (default 55; 0 lays them flat).</summary>
    public static readonly StyledProperty<double> TiltAngleProperty =
        AvaloniaProperty.Register<CoverFlow, double>(nameof(TiltAngle), 55,
            coerce: static (_, value) => double.IsFinite(value) ? Math.Clamp(value, 0, 80) : 55);

    /// <summary>Whether the covers stand on a glossy floor with their reflections under them (default true).</summary>
    public static readonly StyledProperty<bool> ShowsReflectionProperty =
        AvaloniaProperty.Register<CoverFlow, bool>(nameof(ShowsReflection), true);

    /// <summary>The row's accessible name (default "Covers").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CoverFlow, string>(nameof(Label), "Covers");

    /// <summary>How a cover is announced; {0} is its position and {1} the number of covers (default "{0} of {1}").</summary>
    public static readonly StyledProperty<string> ItemNameFormatProperty =
        AvaloniaProperty.Register<CoverFlow, string>(nameof(ItemNameFormat), "{0} of {1}");

    /// <summary>The centred cover, or null.</summary>
    public static readonly DirectProperty<CoverFlow, CoverFlowItem?> SelectedItemProperty =
        AvaloniaProperty.RegisterDirect<CoverFlow, CoverFlowItem?>(nameof(SelectedItem), o => o.SelectedItem);

    /// <summary>Raised when the centred cover is clicked or tapped, or Enter or Space is pressed.</summary>
    public static readonly RoutedEvent<CoverFlowItemInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<CoverFlow, CoverFlowItemInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    private readonly PhotosItemFeed<CoverFlowItem> _feed;
    private CoverFlowStage? _stage;
    private ArcContentSwap? _caption;
    private TextBlock? _status;
    private LazyFocusRing? _focusRing;
    private CoverFlowItem? _selectedItem;
    private bool _syncing;
    private double? _gestureVelocity;

    static CoverFlow()
    {
        FocusableProperty.OverrideDefaultValue<CoverFlow>(true);
        ClipToBoundsProperty.OverrideDefaultValue<CoverFlow>(false);
        AffectsMeasure<CoverFlow>(CoverWidthProperty, CoverAspectRatioProperty);
    }

    public CoverFlow()
    {
        _feed = new PhotosItemFeed<CoverFlowItem>(Convert, OnItemsChanged);
    }

    /// <inheritdoc cref="ItemsProperty"/>
    [Content]
    public AvaloniaList<CoverFlowItem> Items => _feed.Items;

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="CoverWidthProperty"/>
    public double CoverWidth
    {
        get => GetValue(CoverWidthProperty);
        set => SetValue(CoverWidthProperty, value);
    }

    /// <inheritdoc cref="CoverAspectRatioProperty"/>
    public double CoverAspectRatio
    {
        get => GetValue(CoverAspectRatioProperty);
        set => SetValue(CoverAspectRatioProperty, value);
    }

    /// <inheritdoc cref="TiltAngleProperty"/>
    public double TiltAngle
    {
        get => GetValue(TiltAngleProperty);
        set => SetValue(TiltAngleProperty, value);
    }

    /// <inheritdoc cref="ShowsReflectionProperty"/>
    public bool ShowsReflection
    {
        get => GetValue(ShowsReflectionProperty);
        set => SetValue(ShowsReflectionProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="ItemNameFormatProperty"/>
    public string ItemNameFormat
    {
        get => GetValue(ItemNameFormatProperty);
        set => SetValue(ItemNameFormatProperty, value);
    }

    /// <inheritdoc cref="SelectedItemProperty"/>
    public CoverFlowItem? SelectedItem
    {
        get => _selectedItem;
        private set => SetAndRaise(SelectedItemProperty, ref _selectedItem, value);
    }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<CoverFlowItemInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <summary>The number of covers.</summary>
    public int Count => _feed.Count;

    internal CoverFlowStage? Stage => _stage;

    /// <summary>Brings the next cover to the centre; returns false at the last one.</summary>
    public bool SelectNext() => Select(SelectedIndex + 1);

    /// <summary>Brings the previous cover to the centre; returns false at the first one.</summary>
    public bool SelectPrevious() => Select(SelectedIndex - 1);

    internal bool Select(int index)
    {
        if (index < 0 || index >= Count || index == SelectedIndex)
        {
            return false;
        }
        SetCurrentValue(SelectedIndexProperty, index);
        return true;
    }

    internal void Invoke(int index)
    {
        if (index >= 0 && index < Count)
        {
            RaiseEvent(new CoverFlowItemInvokedEventArgs(index, _feed[index]));
        }
    }

    /// <summary>A drag, a flick or a trackpad scroll chose <paramref name="index"/>; the row settles there keeping its speed.</summary>
    internal void CommitFromGesture(int index, double velocity)
    {
        index = Math.Clamp(index, 0, Math.Max(0, Count - 1));
        if (index == SelectedIndex)
        {
            _stage?.GoTo(index, animate: true, velocity);
            return;
        }
        _gestureVelocity = velocity;
        SetCurrentValue(SelectedIndexProperty, index);
        _gestureVelocity = null;
    }

    private static CoverFlowItem? Convert(object? element) => element switch
    {
        CoverFlowItem item => item,
        _ when PhotosItemFeed<CoverFlowItem>.SourceOf(element) is { } source => new CoverFlowItem { Source = source },
        _ => null
    };

    /// <summary>"Night Harbour, Ola Brandt, 3 of 12".</summary>
    internal string NameOf(int index)
    {
        string position;
        try
        {
            position = string.Format(CultureInfo.CurrentCulture, ItemNameFormat, index + 1, Count);
        }
        catch (FormatException)
        {
            position = string.Format(CultureInfo.CurrentCulture, "{0} of {1}", index + 1, Count);
        }
        var item = _feed[index];
        var parts = new List<string>(3);
        if (!string.IsNullOrEmpty(item.Title))
        {
            parts.Add(item.Title);
        }
        if (!string.IsNullOrEmpty(item.Subtitle))
        {
            parts.Add(item.Subtitle);
        }
        parts.Add(position);
        return string.Join(", ", parts);
    }

    // Items and selection ------------------------------------------------------------------------------------------------

    private void OnItemsChanged()
    {
        var count = Count;
        var index = SelectedIndex;
        var clamped = count == 0 ? -1 : Math.Clamp(index < 0 ? 0 : index, 0, count - 1);
        if (clamped != index)
        {
            _syncing = true;
            SetCurrentValue(SelectedIndexProperty, clamped);
            _syncing = false;
        }
        if (_stage is not null)
        {
            _stage.SetCovers(_feed.Current);
            _stage.GoTo(Math.Max(0, clamped), animate: false);
        }
        UpdateState(Math.Max(-1, clamped));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _feed.ItemsSource = change.GetNewValue<IEnumerable?>();
        }
        else if (change.Property == SelectedIndexProperty && !_syncing)
        {
            var index = change.GetNewValue<int>();
            var count = Count;
            if (count > 0 && (index < 0 || index >= count))
            {
                _syncing = true;
                SetCurrentValue(SelectedIndexProperty, Math.Clamp(index, 0, count - 1));
                _syncing = false;
                index = SelectedIndex;
            }
            if (index >= 0)
            {
                _stage?.GoTo(index, animate: true, _gestureVelocity ?? 0);
            }
            UpdateState(index);
        }
        else if (change.Property == TiltAngleProperty || change.Property == ShowsReflectionProperty)
        {
            _stage?.Apply();
        }
        else if (change.Property == LabelProperty || change.Property == ItemNameFormatProperty)
        {
            UpdateState(SelectedIndex);
        }
    }

    private void UpdateState(int index)
    {
        var valid = index >= 0 && index < Count;
        SelectedItem = valid ? _feed[index] : null;
        var spoken = valid ? NameOf(index) : string.Empty;
        if (_status is not null)
        {
            _status.Text = spoken;
        }
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetHelpText(this, spoken);
        if (!valid && _caption is not null)
        {
            _caption.Content = null;
        }
    }

    /// <summary>The cover at the centre changed while moving: the caption follows it at once.</summary>
    internal void OnCentreChanged(int index)
    {
        if (_caption is not null)
        {
            _caption.Direction = index >= (_caption.Content is CoverFlowItem shown ? _feed.IndexOf(shown) : 0) ? 1 : -1;
            _caption.Content = index >= 0 && index < Count ? _feed[index] : null;
        }
    }

    // Template -----------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_stage is not null)
        {
            _stage.Owner = null;
        }
        _stage = e.NameScope.Find<CoverFlowStage>(StagePart);
        _caption = e.NameScope.Find<ArcContentSwap>(CaptionPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_stage is not null)
        {
            _stage.Owner = this;
            _stage.SetCovers(_feed.Current);
            _stage.GoTo(Math.Max(0, SelectedIndex), animate: false);
        }
        _focusRing ??= new LazyFocusRing(this, CoverFlowTokens.Key(CoverFlowTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPanel));
        _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
        if (_caption is not null)
        {
            _caption.Content = SelectedItem;
        }
        UpdateState(SelectedIndex);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _feed.Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _feed.Detach();
    }

    // Keyboard -----------------------------------------------------------------------------------------------------------

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || Count == 0)
        {
            return;
        }
        var index = SelectedIndex;
        switch (e.Key)
        {
            case Key.Left:
                Select(index - 1);
                break;
            case Key.Right:
                Select(index + 1);
                break;
            case Key.PageUp:
                Select(Math.Max(0, index - 5));
                break;
            case Key.PageDown:
                Select(Math.Min(Count - 1, index + 5));
                break;
            case Key.Home:
                Select(0);
                break;
            case Key.End:
                Select(Count - 1);
                break;
            case Key.Enter or Key.Space:
                Invoke(index);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CoverFlowAutomationPeer(this);

    private sealed class CoverFlowAutomationPeer(CoverFlow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetNameCore() => owner.Label;
    }
}
