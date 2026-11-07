using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A feed that refreshes when it is pulled down past its top (Arc Stretch refresh, design reference). A header with the
/// title, a short status and a round refresh button sits over a well; the rows lie on a sheet that covers the well.
/// Pulling the sheet down at the top of the feed (mouse, pen, touch, or the wheel and trackpad scrolling up) moves it
/// with growing resistance while the rows spread apart, and a line in the gap grows from its centre until it reaches
/// full length exactly at <see cref="Threshold"/>. Letting go past it runs <see cref="RefreshHandler"/>: the sheet
/// rests 60 px down, the line runs back and forth and the header glyph spins; when the work ends, the result shows with
/// a check, new rows drop in from the top while the others slide down, and the sheet springs back. The refresh button
/// (and <see cref="RefreshAsync"/>) does the same without a pull.
/// <para>
/// The rows come from <see cref="ItemsSource"/>; give the new items to the collection before the handler returns (the
/// list matches rows by <see cref="ItemKeySelector"/>, by <see cref="StretchRefreshActivity.Id"/>, or by the item itself).
/// Without an <see cref="ItemTemplate"/>, <see cref="StretchRefreshActivity"/> items show as
/// <see cref="StretchRefreshActivityRow"/>s. Give the control a height (or a parent that sizes it): the feed scrolls inside.
/// </para>
/// </summary>
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(ButtonPart, typeof(AvaloniaButton))]
[TemplatePart(HeaderIconPart, typeof(Control))]
[TemplatePart(SubtitlePart, typeof(Control))]
[TemplatePart(AnchorPart, typeof(Control))]
[TemplatePart(MeterPart, typeof(Control))]
[TemplatePart(MarkHostPart, typeof(Control))]
[TemplatePart(MarkPart, typeof(StrokeDrawGlyph))]
[TemplatePart(StatusPart, typeof(Control))]
[TemplatePart(WellPart, typeof(Panel))]
[TemplatePart(SheetPart, typeof(Panel))]
[TemplatePart(SheetSurfacePart, typeof(Border))]
[TemplatePart(SheetClipPart, typeof(Border))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(ListPart, typeof(Panel))]
[TemplatePart(FeedRingPart, typeof(Border))]
[TemplatePart(AnnouncerPart, typeof(Control))]
[PseudoClasses(IdlePseudoClass, PullingPseudoClass, ArmedPseudoClass, RefreshingPseudoClass, DonePseudoClass, FailedPseudoClass,
    BusyPseudoClass, HintPseudoClass, NarrowPseudoClass)]
public partial class StretchRefresh : TemplatedControl
{
    internal const string CardPart = "PART_Card";
    internal const string ContentPart = "PART_Content";
    internal const string ButtonPart = "PART_RefreshButton";
    internal const string HeaderIconPart = "PART_HeaderIcon";
    internal const string SubtitlePart = "PART_Subtitle";
    internal const string AnchorPart = "PART_Anchor";
    internal const string MeterPart = "PART_Meter";
    internal const string MarkHostPart = "PART_MarkHost";
    internal const string MarkPart = "PART_Mark";
    internal const string StatusPart = "PART_Status";
    internal const string WellPart = "PART_Well";
    internal const string SheetPart = "PART_Sheet";
    internal const string SheetSurfacePart = "PART_SheetSurface";
    internal const string SheetClipPart = "PART_SheetClip";
    internal const string ScrollerPart = "PART_Scroller";
    internal const string ListPart = "PART_List";
    internal const string FeedRingPart = "PART_FeedRing";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string IdlePseudoClass = ":idle";
    internal const string PullingPseudoClass = ":pulling";
    internal const string ArmedPseudoClass = ":armed";
    internal const string RefreshingPseudoClass = ":refreshing";
    internal const string DonePseudoClass = ":done";
    internal const string FailedPseudoClass = ":failed";
    internal const string BusyPseudoClass = ":busy";
    internal const string HintPseudoClass = ":hint";
    internal const string NarrowPseudoClass = ":narrow";

    /// <summary>The heading above the feed; it also names the feed region for screen readers.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StretchRefresh, string?>(nameof(Title));

    /// <summary>A short status under the heading, such as "Updated 12 min ago". A change rises in.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<StretchRefresh, string?>(nameof(Subtitle));

    /// <summary>The rows, newest first. An observable collection animates its changes: new rows drop in, the others slide.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<StretchRefresh, IEnumerable?>(nameof(ItemsSource));

    /// <summary>Builds one row from an item. Null shows <see cref="StretchRefreshActivity"/> items as activity rows and other items as text.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<StretchRefresh, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>A stable key for each item, so a row keeps its place and view across refreshes. Null: <see cref="StretchRefreshActivity.Id"/>, else the item itself.</summary>
    public static readonly StyledProperty<Func<object, object>?> ItemKeySelectorProperty =
        AvaloniaProperty.Register<StretchRefresh, Func<object, object>?>(nameof(ItemKeySelector));

    /// <summary>The pull, in px after resistance, that arms a refresh: 72. The line reaches full length exactly there.</summary>
    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<StretchRefresh, double>(nameof(Threshold), 72, coerce: (_, v) => double.IsFinite(v) && v > 1 ? v : 72);

    /// <summary>
    /// The refresh work, run after a pull past the threshold or a press of the refresh button. Update
    /// <see cref="ItemsSource"/> before it returns, and return a short result such as "3 new updates" (null or empty
    /// shows <see cref="StretchRefreshLabels.Done"/>). If it throws, the result is <see cref="StretchRefreshLabels.Error"/>.
    /// The token is cancelled when the control leaves the visual tree. Keep it at least about half a second, so the
    /// running line can be read.
    /// </summary>
    public static readonly StyledProperty<Func<CancellationToken, Task<string?>>?> RefreshHandlerProperty =
        AvaloniaProperty.Register<StretchRefresh, Func<CancellationToken, Task<string?>>?>(nameof(RefreshHandler));

    /// <summary>Every word the control shows or announces (English by default).</summary>
    public static readonly DirectProperty<StretchRefresh, StretchRefreshLabels> LabelsProperty =
        AvaloniaProperty.RegisterDirect<StretchRefresh, StretchRefreshLabels>(nameof(Labels), o => o.Labels, (o, v) => o.Labels = v);

    /// <summary>Where the control is in its pull and refresh cycle.</summary>
    public static readonly DirectProperty<StretchRefresh, StretchRefreshState> StateProperty =
        AvaloniaProperty.RegisterDirect<StretchRefresh, StretchRefreshState>(nameof(State), o => o.State);

    /// <summary>True while the refresh work runs (from the start of a refresh until its result shows).</summary>
    public static readonly DirectProperty<StretchRefresh, bool> IsRefreshingProperty =
        AvaloniaProperty.RegisterDirect<StretchRefresh, bool>(nameof(IsRefreshing), o => o.IsRefreshing);

    internal static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<StretchRefresh, IBrush?>(nameof(SuccessInk));

    internal static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<StretchRefresh, IBrush?>(nameof(DangerInk));

    private StretchRefreshLabels _labels = new();
    private StretchRefreshState _state;
    private bool _isRefreshing;

    static StretchRefresh()
    {
        ClipToBoundsProperty.OverrideDefaultValue<StretchRefresh>(false);
        FocusableProperty.OverrideDefaultValue<StretchRefresh>(false);
    }

    /// <summary>Raised when a refresh starts, before <see cref="RefreshHandler"/> runs.</summary>
    public event EventHandler<StretchRefreshStartedEventArgs>? RefreshStarted;

    /// <summary>Raised when the refresh work has ended (the result is showing), with the result text or the error.</summary>
    public event EventHandler<StretchRefreshCompletedEventArgs>? RefreshCompleted;

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <inheritdoc cref="ItemTemplateProperty"/>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <inheritdoc cref="ItemKeySelectorProperty"/>
    public Func<object, object>? ItemKeySelector
    {
        get => GetValue(ItemKeySelectorProperty);
        set => SetValue(ItemKeySelectorProperty, value);
    }

    /// <inheritdoc cref="ThresholdProperty"/>
    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    /// <inheritdoc cref="RefreshHandlerProperty"/>
    public Func<CancellationToken, Task<string?>>? RefreshHandler
    {
        get => GetValue(RefreshHandlerProperty);
        set => SetValue(RefreshHandlerProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public StretchRefreshLabels Labels
    {
        get => _labels;
        set
        {
            if (SetAndRaise(LabelsProperty, ref _labels, value ?? new StretchRefreshLabels()))
            {
                RefreshTexts();
            }
        }
    }

    /// <inheritdoc cref="StateProperty"/>
    public StretchRefreshState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <inheritdoc cref="IsRefreshingProperty"/>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetAndRaise(IsRefreshingProperty, ref _isRefreshing, value);
    }

    /// <summary>How far the sheet is pushed down now, in px (0 at rest, 60 while refreshing).</summary>
    public double PullOffset => _y?.Value ?? 0;

    internal IBrush? SuccessInk
    {
        get => GetValue(SuccessInkProperty);
        set => SetValue(SuccessInkProperty, value);
    }

    internal IBrush? DangerInk
    {
        get => GetValue(DangerInkProperty);
        set => SetValue(DangerInkProperty, value);
    }

    /// <summary>The clock of the cycle's delays and the wheel's idle timer (tests replace it).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>What the polite status region said, oldest first (tests).</summary>
    internal IReadOnlyList<string> Announcements => (IReadOnlyList<string>?)_announcer?.History ?? Array.Empty<string>();

    internal StretchRefreshList? List => _list;

    internal StretchRefreshMeter? Meter => _meter;

    internal StretchRefreshSwap? StatusSwap => _status;

    internal StretchRefreshSwap? SubtitleSwap => _subtitle;

    internal StretchRefreshSwap? HeaderIconSwap => _headerIcon;

    internal AvaloniaButton? RefreshButton => _button;

    internal ScrollViewer? Scroller => _scroller;

    internal Control? Anchor => _anchor;

    internal Panel? Sheet => _sheet;

    internal Control? MarkHost => _markHost;

    /// <summary>The current row-spread tension (tests).</summary>
    internal double Tension => _tension?.Value ?? 0;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            _list?.SetItems(ItemsSource);
        }
        else if (change.Property == ItemTemplateProperty || change.Property == ItemKeySelectorProperty)
        {
            if (_list is not null)
            {
                _list.ItemTemplate = ItemTemplate;
                _list.KeySelector = ItemKeySelector;
                _list.Rebuild();
            }
        }
        else if (change.Property == SubtitleProperty)
        {
            _subtitle?.ShowText(Subtitle);
            if (_subtitle is not null)
            {
                _subtitle.IsVisible = !string.IsNullOrEmpty(Subtitle) || _subtitle.EntryCount > 1;
            }
        }
        else if (change.Property == CornerRadiusProperty || change.Property == BorderThicknessProperty)
        {
            UpdateInnerCorners();
        }
        else if (change.Property == TitleProperty)
        {
            UpdateNames();
        }
        else if (change.Property == ThresholdProperty)
        {
            _machine.Threshold = Threshold;
            Apply();
        }
        else if (change.Property == SuccessInkProperty || change.Property == DangerInkProperty)
        {
            UpdateInks();
        }
    }

    private void UpdateNames()
    {
        if (_button is not null)
        {
            AutomationProperties.SetName(_button, Labels.RefreshButton);
        }
        if (_scroller is not null)
        {
            AutomationProperties.SetName(_scroller, Title ?? string.Empty);
        }
    }

    /// <summary>Rewrites the visible words after <see cref="Labels"/> changed.</summary>
    private void RefreshTexts()
    {
        UpdateNames();
        if (_status is null)
        {
            return;
        }
        switch (_machine.State)
        {
            case StretchRefreshState.Idle:
            case StretchRefreshState.Pulling:
                _status.ShowText(Labels.PullHint, animate: false);
                break;
            case StretchRefreshState.Armed:
                _status.ShowText(Labels.ReleaseHint, animate: false);
                break;
            case StretchRefreshState.Refreshing:
                _status.ShowText(Labels.Refreshing, animate: false);
                break;
        }
    }

    private void Announce(string text) => _announcer?.Announce(text);

    private void Unhook(ScrollViewer? scroller, Panel? sheet, AvaloniaButton? button)
    {
        if (sheet is not null)
        {
            sheet.RemoveHandler(PointerPressedEvent, OnSheetPressed);
            sheet.RemoveHandler(PointerCaptureLostEvent, OnSheetCaptureLost);
        }
        RemoveHandler(PointerWheelChangedEvent, OnSheetWheel);
        if (scroller is not null)
        {
            scroller.GotFocus -= OnScrollerGotFocus;
            scroller.LostFocus -= OnScrollerLostFocus;
        }
        if (button is not null)
        {
            button.Click -= OnButtonClick;
        }
    }

    private void Hook()
    {
        if (_sheet is not null)
        {
            _sheet.AddHandler(PointerPressedEvent, OnSheetPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            _sheet.AddHandler(PointerCaptureLostEvent, OnSheetCaptureLost, RoutingStrategies.Direct | RoutingStrategies.Bubble, handledEventsToo: true);
        }
        // The wheel is watched on the whole control and accepted over the well (the gap included, once the sheet moves).
        AddHandler(PointerWheelChangedEvent, OnSheetWheel, RoutingStrategies.Tunnel, handledEventsToo: true);
        if (_scroller is not null)
        {
            _scroller.GotFocus += OnScrollerGotFocus;
            _scroller.LostFocus += OnScrollerLostFocus;
        }
        if (_button is not null)
        {
            _button.Click += OnButtonClick;
        }
    }

    /// <summary>The feed's focus ring is drawn inside the sheet (Arc's outline is clipped by the well and never shows).</summary>
    private void OnScrollerGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_feedRing is not null && ReferenceEquals(e.Source, _scroller))
        {
            _feedRing.IsVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        }
    }

    private void OnScrollerLostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_feedRing is not null && _scroller is not null && !_scroller.IsFocused)
        {
            _feedRing.IsVisible = false;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = StartRefresh(StretchRefreshTrigger.Button);
    }

    /// <summary>
    /// Runs a refresh as the refresh button does. Returns false (at once) when a refresh is already running or its
    /// result is still showing; otherwise completes with true when the work has ended (the result is showing).
    /// </summary>
    public Task<bool> RefreshAsync() => StartRefresh(StretchRefreshTrigger.Code);
}
