using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Data of <see cref="ToolCall.ApproveRequested"/>, <see cref="ToolCall.DenyRequested"/> and
/// <see cref="ToolCall.RetryRequested"/>: the call the person acted on. Set <c>Handled</c> when you update the call
/// yourself.</summary>
public sealed class ToolCallActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public ToolCallActionEventArgs(RoutedEvent routedEvent, ToolCallItem item) : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The call acted on.</summary>
    public ToolCallItem Item { get; }
}

/// <summary>Data of <see cref="ToolCall.ExpandedChanged"/> and <see cref="ToolCallGroup.ExpandedChanged"/>.</summary>
public sealed class ToolCallExpandedChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public ToolCallExpandedChangedEventArgs(RoutedEvent routedEvent, ToolCallItem? item, bool isExpanded, bool isAutomatic)
        : base(routedEvent)
    {
        Item = item;
        IsExpanded = isExpanded;
        IsAutomatic = isAutomatic;
    }

    /// <summary>The row's call; null for a group.</summary>
    public ToolCallItem? Item { get; }

    /// <summary>Whether it is open now.</summary>
    public bool IsExpanded { get; }

    /// <summary>True when the control changed it itself (a group folding away after its calls finished, or opening for a
    /// call that needs a decision); false when the person or the app did.</summary>
    public bool IsAutomatic { get; }
}

/// <summary>
/// One tool call of an agent transcript (Arc Tool call, design reference): the tool's mark, the call as a verb phrase,
/// quiet meta ("4 results · 1.2s") and a status icon. The whole row is a button: it springs open into Input (the
/// arguments as a mono key / value list) and Output (the result as a list, a table, tinted JSON or text, folding long ones
/// behind "Show N more lines"), each with a copy button. A running call shimmers its title next to a spinner; a failed one
/// shows its reason and Retry; one that waits for approval sits on an accent outline with Deny and Allow; a denied one is
/// muted.
/// <para>
/// Bind <see cref="Item"/> and change the item in place as the call moves on. Allow, Deny and Retry raise
/// <see cref="ApproveRequested"/>, <see cref="DenyRequested"/> and <see cref="RetryRequested"/> (they bubble, so a
/// <see cref="ToolCallGroup"/> or any parent can handle them): update the call's status yourself and set <c>Handled</c>.
/// When nothing handles them, Deny marks the call denied here; Allow and Retry go to the <see cref="ToolCallSimulation"/>
/// that appended the call, and change nothing otherwise. With no item the row is empty: a wrench and "No tool call"
/// (<see cref="ToolCallLabels.EmptyTitle"/>) without a status, and it does not open.
/// On its own the row sits in a 1px bordered Surface frame (radius 16, 4px in); inside a group it is flush.
/// </para>
/// </summary>
[TemplatePart(TogglePart, typeof(ToolCallHeadButton))]
[TemplatePart(HeadPart, typeof(ToolCallHeadPanel))]
[TemplatePart(BodyPart, typeof(AiHeightBox))]
[PseudoClasses(RunningPseudoClass, SuccessPseudoClass, ErrorPseudoClass, ApprovalPseudoClass, DeniedPseudoClass,
    ExpandedPseudoClass, CompactPseudoClass, TightPseudoClass, FlushPseudoClass, HeadHoverPseudoClass, EmptyPseudoClass)]
public class ToolCall : TemplatedControl
{
    internal const string TogglePart = "PART_Toggle";
    internal const string HeadPart = "PART_Head";
    internal const string BodyPart = "PART_Body";

    internal const string RunningPseudoClass = ":running";
    internal const string SuccessPseudoClass = ":success";
    internal const string ErrorPseudoClass = ":error";
    internal const string ApprovalPseudoClass = ":approval";
    internal const string DeniedPseudoClass = ":denied";
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string CompactPseudoClass = ":compact";
    internal const string TightPseudoClass = ":tight";
    internal const string FlushPseudoClass = ":flush";
    internal const string HeadHoverPseudoClass = ":head-hover";

    /// <summary>Below this width a row is compact: the meta keeps only the duration, the reason wraps and the buttons move
    /// under it, and the panel drops its title indent.</summary>
    internal const double CompactWidth = 440;

    /// <summary>Below this width the meta is hidden.</summary>
    internal const double TightWidth = 370;

    internal const string EmptyPseudoClass = ":empty";

    /// <summary>The call shown. Change it in place; the row follows every change. Null shows the empty row.</summary>
    public static readonly StyledProperty<ToolCallItem?> ItemProperty =
        AvaloniaProperty.Register<ToolCall, ToolCallItem?>(nameof(Item));

    /// <summary>Whether Input and Output are open (two-way).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ToolCall, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Drops the row's own frame (a group sets it on its rows).</summary>
    public static readonly StyledProperty<bool> IsFlushProperty =
        AvaloniaProperty.Register<ToolCall, bool>(nameof(IsFlush));

    /// <summary>Every word the row shows or says.</summary>
    public static readonly StyledProperty<ToolCallLabels> LabelsProperty =
        AvaloniaProperty.Register<ToolCall, ToolCallLabels>(nameof(Labels), ToolCallLabels.Default);

    /// <summary>Draws a successful call's result in the Output well (a chart, a file preview); a template that does not
    /// match the item, or builds nothing, falls back to the built-in view. It receives the <see cref="ToolCallItem"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> OutputTemplateProperty =
        AvaloniaProperty.Register<ToolCall, IDataTemplate?>(nameof(OutputTemplate));

    /// <summary>The call on screen: <see cref="Item"/>, or an empty call when it is null.</summary>
    public static readonly DirectProperty<ToolCall, ToolCallItem> DisplayedItemProperty =
        AvaloniaProperty.RegisterDirect<ToolCall, ToolCallItem>(nameof(DisplayedItem), o => o.DisplayedItem);

    /// <summary>Raised when the person presses Allow on a call waiting for approval. Bubbles.</summary>
    public static readonly RoutedEvent<ToolCallActionEventArgs> ApproveRequestedEvent =
        RoutedEvent.Register<ToolCall, ToolCallActionEventArgs>(nameof(ApproveRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Deny on a call waiting for approval. Bubbles.</summary>
    public static readonly RoutedEvent<ToolCallActionEventArgs> DenyRequestedEvent =
        RoutedEvent.Register<ToolCall, ToolCallActionEventArgs>(nameof(DenyRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Retry on a failed call. Bubbles.</summary>
    public static readonly RoutedEvent<ToolCallActionEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<ToolCall, ToolCallActionEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Input and Output open or close.</summary>
    public static readonly RoutedEvent<ToolCallExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ToolCall, ToolCallExpandedChangedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Direct);

    private ToolCallItem? _observed;
    private ToolCallItem? _empty;
    private ToolCallItem? _item;
    private ToolCallHeadButton? _toggle;
    private ToolCallHeadPanel? _head;
    private AiHeightBox? _body;
    private ToolCallText? _title;
    private ArcGlyph? _mark;
    private TextBlock? _note;
    private TextBlock? _summary;
    private TextBlock? _dot;
    private TextBlock? _duration;
    private AvaloniaButton? _retry;
    private AvaloniaButton? _deny;
    private AvaloniaButton? _allow;
    private TextBlock? _retryLabel;
    private ToolCallStatusGlyph? _status;
    private ExplainerAnnouncer? _announcer;
    private ToolCallStatus _shownStatus;
    private bool _compact;
    private bool _tight;
    private string? _pendingNote;

    static ToolCall()
    {
        SpecialFocusModality.Watch();
        AffectsMeasure<ToolCall>(IsFlushProperty);
    }

    /// <summary>Creates a row (empty until <see cref="Item"/> is set).</summary>
    public ToolCall()
    {
        _shownStatus = ToolCallStatus.Success;
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    /// <inheritdoc cref="ItemProperty"/>
    [Content]
    public ToolCallItem? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="IsFlushProperty"/>
    public bool IsFlush { get => GetValue(IsFlushProperty); set => SetValue(IsFlushProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ToolCallLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="OutputTemplateProperty"/>
    public IDataTemplate? OutputTemplate { get => GetValue(OutputTemplateProperty); set => SetValue(OutputTemplateProperty, value); }

    /// <inheritdoc cref="DisplayedItemProperty"/>
    public ToolCallItem DisplayedItem => _item ?? EmptyItem;

    /// <inheritdoc cref="ApproveRequestedEvent"/>
    public event EventHandler<ToolCallActionEventArgs>? ApproveRequested
    {
        add => AddHandler(ApproveRequestedEvent, value);
        remove => RemoveHandler(ApproveRequestedEvent, value);
    }

    /// <inheritdoc cref="DenyRequestedEvent"/>
    public event EventHandler<ToolCallActionEventArgs>? DenyRequested
    {
        add => AddHandler(DenyRequestedEvent, value);
        remove => RemoveHandler(DenyRequestedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<ToolCallActionEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <inheritdoc cref="ExpandedChangedEvent"/>
    public event EventHandler<ToolCallExpandedChangedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    /// <summary>Raised when the person opened or closed the row (not the app).</summary>
    internal event EventHandler? UserToggled;

    /// <summary>The live region (tests read it).</summary>
    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>The row head's button.</summary>
    internal ToolCallHeadButton? Toggle => _toggle;

    /// <summary>The head's layout.</summary>
    internal ToolCallHeadPanel? Head => _head;

    /// <summary>The Input / Output box.</summary>
    internal AiHeightBox? Body => _body;

    /// <summary>The title.</summary>
    internal ToolCallText? TitleText => _title;

    /// <summary>The status icon.</summary>
    internal ToolCallStatusGlyph? StatusGlyph => _status;

    /// <summary>The action buttons.</summary>
    internal AvaloniaButton? RetryButton => _retry;

    internal AvaloniaButton? DenyButton => _deny;

    internal AvaloniaButton? AllowButton => _allow;

    /// <summary>Whether the compact layout applies.</summary>
    internal bool IsCompact => _compact;

    /// <summary>Whether the meta is hidden.</summary>
    internal bool IsTight => _tight;

    /// <summary>The meta as shown ("4 results · 1.2s"), or null.</summary>
    internal string? MetaText => ToolCallFormat.MetaText(DisplayedItem, Labels, _compact, _tight);

    /// <summary>Whether the row has no call (<see cref="Item"/> is null).</summary>
    internal bool IsEmpty => _item is null;

    private ToolCallItem EmptyItem => _empty ??= new ToolCallItem();

    /// <summary>Opens or closes the row as the person would (counts as their toggle); an empty row does not open.</summary>
    internal void ToggleByUser()
    {
        if (IsEmpty && !IsExpanded)
        {
            return;
        }
        SetCurrentValue(IsExpandedProperty, !IsExpanded);
        UserToggled?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
            _toggle.PropertyChanged -= OnTogglePropertyChanged;
        }
        foreach (var button in new[] { _retry, _deny, _allow })
        {
            if (button is not null)
            {
                button.Click -= OnActionClick;
            }
        }
        if (_body is not null)
        {
            _body.ContentUnmounted -= OnBodyUnmounted;
        }
        var scope = e.NameScope;
        _toggle = scope.Find<ToolCallHeadButton>(TogglePart);
        _head = scope.Find<ToolCallHeadPanel>(HeadPart);
        _body = scope.Find<AiHeightBox>(BodyPart);
        _mark = scope.Find<ArcGlyph>("PART_Mark");
        _title = scope.Find<ToolCallText>("PART_Title");
        _note = scope.Find<TextBlock>("PART_Note");
        _summary = scope.Find<TextBlock>("PART_Summary");
        _dot = scope.Find<TextBlock>("PART_MetaDot");
        _duration = scope.Find<TextBlock>("PART_Duration");
        _retry = scope.Find<AvaloniaButton>("PART_Retry");
        _deny = scope.Find<AvaloniaButton>("PART_Deny");
        _allow = scope.Find<AvaloniaButton>("PART_Allow");
        _retryLabel = scope.Find<TextBlock>("PART_RetryLabel");
        _status = scope.Find<ToolCallStatusGlyph>("PART_Status");
        _announcer = scope.Find<ExplainerAnnouncer>("PART_Announcer");
        if (_head is not null)
        {
            _head.MarkPart = _mark;
            _head.TitlePart = _title;
            _head.StatusPart = _status;
            _head.ChevronPart = scope.Find<Control>("PART_Chevron");
            _head.NotePart = _note;
            _head.MetaPart = scope.Find<Control>("PART_Meta");
            _head.ActionsPart = scope.Find<Control>("PART_Actions");
            _head.IsCompact = _compact;
            _head.IsTight = _tight;
        }
        foreach (var block in new[] { _summary, _dot, _duration })
        {
            if (block is not null)
            {
                block.FontFeatures = ToolCallBodyBuilder.TabularNumbers;
            }
        }
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
            _toggle.PropertyChanged += OnTogglePropertyChanged;
            _toggle.IsExpanded = IsExpanded;
        }
        foreach (var button in new[] { _retry, _deny, _allow })
        {
            if (button is not null)
            {
                button.Click += OnActionClick;
            }
        }
        if (_body is not null)
        {
            _body.ContentUnmounted += OnBodyUnmounted;
            if (IsExpanded)
            {
                _body.Content = BuildBody();
                _body.OpenInstantly();
            }
        }
        _shownStatus = DisplayedItem.Status;
        Refresh(animate: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemProperty)
        {
            var old = DisplayedItem;
            _item = change.GetNewValue<ToolCallItem?>();
            var item = DisplayedItem;
            RaisePropertyChanged(DisplayedItemProperty, old, item);
            if (this.IsAttachedToVisualTree())
            {
                Observe(item);
            }
            PseudoClasses.Set(EmptyPseudoClass, _item is null);
            _shownStatus = item.Status;
            Refresh(animate: false);
            RebuildBody();
        }
        else if (change.Property == IsExpandedProperty)
        {
            var open = change.GetNewValue<bool>();
            PseudoClasses.Set(ExpandedPseudoClass, open);
            if (_toggle is not null)
            {
                _toggle.IsExpanded = open;
            }
            if (_body is not null)
            {
                if (open)
                {
                    _body.Content ??= BuildBody();
                }
                _body.IsExpanded = open;
            }
            RaiseEvent(new ToolCallExpandedChangedEventArgs(ExpandedChangedEvent, DisplayedItem, open, isAutomatic: false));
        }
        else if (change.Property == IsFlushProperty)
        {
            PseudoClasses.Set(FlushPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == LabelsProperty)
        {
            Refresh(animate: false);
            RebuildBody();
        }
        else if (change.Property == OutputTemplateProperty)
        {
            RebuildBody();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(DisplayedItem);
        if (_shownStatus != DisplayedItem.Status)
        {
            _shownStatus = DisplayedItem.Status;
            Refresh(animate: false);
            RebuildBody();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
    }

    /// <summary>The item is followed only while the row is in a window, so a long-lived item does not hold the row.</summary>
    private void Observe(ToolCallItem? item)
    {
        if (ReferenceEquals(_observed, item))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnItemPropertyChanged;
            _observed.Input.CollectionChanged -= OnInputChanged;
        }
        _observed = item;
        if (item is not null)
        {
            item.PropertyChanged += OnItemPropertyChanged;
            item.Input.CollectionChanged += OnInputChanged;
        }
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ToolCallItem.StatusProperty)
        {
            var status = DisplayedItem.Status;
            var changed = status != _shownStatus;
            _shownStatus = status;
            Refresh(animate: true);
            if (changed)
            {
                Announce(status);
                RebuildBody();
            }
            return;
        }
        Refresh(animate: true);
        if (e.Property == ToolCallItem.OutputProperty || e.Property == ToolCallItem.MessageProperty)
        {
            RebuildBody();
        }
    }

    private void OnInputChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildBody();

    private void Announce(ToolCallStatus status)
    {
        var text = ToolCallFormat.Announcement(DisplayedItem, Labels);
        if (_pendingNote is { } note && status == ToolCallStatus.Running)
        {
            text += ". " + note;
            _pendingNote = null;
        }
        _announcer?.Announce(text);
    }

    /// <summary>Says <paramref name="text"/> through the row's live region.</summary>
    internal void Say(string text) => _announcer?.Announce(text);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width))
        {
            UpdateBreakpoints(availableSize.Width);
        }
        return base.MeasureOverride(availableSize);
    }

    private void UpdateBreakpoints(double width)
    {
        var compact = width < CompactWidth;
        var tight = width < TightWidth;
        if (compact == _compact && tight == _tight)
        {
            return;
        }
        _compact = compact;
        _tight = tight;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(TightPseudoClass, tight);
        if (_head is not null)
        {
            _head.IsCompact = compact;
            _head.IsTight = tight;
        }
        if (_body?.Content is Control body)
        {
            body.Margin = BodyMargin;
        }
        // A width change cuts straight to the new layout (no fades).
        Refresh(animate: false);
    }

    private Thickness BodyMargin => _compact ? new Thickness(12, 0, 12, 12) : new Thickness(ToolCallHeadPanel.TitleLeft, 0, 12, 12);

    /// <summary>Brings every part in line with the call.</summary>
    private void Refresh(bool animate)
    {
        var item = DisplayedItem;
        var labels = Labels;
        var status = item.Status;
        var empty = IsEmpty;
        UpdateStatusClasses(status);
        if (_mark is not null)
        {
            _mark.Data = item.ToolMark ?? ArcIcons.Wrench;
            if (item.ToolMarkBrush is { } brush)
            {
                _mark.Foreground = brush;
            }
            else
            {
                _mark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
        if (_title is not null)
        {
            _title.Text = empty ? labels.EmptyTitle : item.TitleFor(status);
            _title.IsShimmering = !empty && status == ToolCallStatus.Running;
        }
        if (_status is not null)
        {
            _status.Status = status;
            // An empty row has no status to show.
            _status.IsVisible = !empty;
        }
        if (_head is not null)
        {
            if (_note is not null)
            {
                var shows = ToolCallFormat.ShowsNote(status, item.Message);
                if (shows)
                {
                    _note.Text = item.Message;
                }
                _head.SetShown(ToolCallHeadSlot.Note, shows, animate);
            }
            var (summary, duration) = ToolCallFormat.Meta(item, labels, _compact, _tight);
            var hasMeta = summary is not null || duration is not null;
            if (hasMeta)
            {
                SetText(_summary, summary);
                SetText(_duration, duration);
                if (_dot is not null)
                {
                    _dot.IsVisible = summary is not null && duration is not null;
                }
            }
            _head.SetShown(ToolCallHeadSlot.Meta, hasMeta, animate);
            if (ToolCallFormat.ShowsActions(status))
            {
                var title = item.TitleFor(status);
                SetAction(_retry, status == ToolCallStatus.Error, labels.Retry, title, labels);
                SetAction(_deny, status == ToolCallStatus.Approval, labels.Deny, title, labels);
                SetAction(_allow, status == ToolCallStatus.Approval, labels.Allow, title, labels);
                if (_retryLabel is not null)
                {
                    _retryLabel.Text = labels.Retry;
                }
                if (_deny is not null && !Equals(_deny.Content, labels.Deny))
                {
                    _deny.Content = labels.Deny;
                }
                if (_allow is not null && !Equals(_allow.Content, labels.Allow))
                {
                    _allow.Content = labels.Allow;
                }
                _head.SetShown(ToolCallHeadSlot.Actions, true, animate);
            }
            else
            {
                // Focus on a button that is going away moves to the row, instead of dropping to the page.
                if (_toggle is not null && _head.ActionsPart is { } actions && actions.IsKeyboardFocusWithin)
                {
                    // Allow or Deny clicked with the mouse: no keyboard ring on the row.
                    _toggle.Focus(SpecialFocusModality.For(this));
                }
                _head.SetShown(ToolCallHeadSlot.Actions, false, animate);
            }
        }
        if (_toggle is not null)
        {
            AutomationProperties.SetName(_toggle, empty ? labels.EmptyTitle : ToolCallFormat.RowName(item, labels));
        }
    }

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is null)
        {
            return;
        }
        block.Text = text;
        block.IsVisible = text is not null;
    }

    private static void SetAction(AvaloniaButton? button, bool visible, string action, string title, ToolCallLabels labels)
    {
        if (button is null)
        {
            return;
        }
        button.IsVisible = visible;
        AutomationProperties.SetName(button, labels.ActionName(action, title));
    }

    private void UpdateStatusClasses(ToolCallStatus status)
    {
        PseudoClasses.Set(RunningPseudoClass, status == ToolCallStatus.Running);
        PseudoClasses.Set(SuccessPseudoClass, status == ToolCallStatus.Success);
        PseudoClasses.Set(ErrorPseudoClass, status == ToolCallStatus.Error);
        PseudoClasses.Set(ApprovalPseudoClass, status == ToolCallStatus.Approval);
        PseudoClasses.Set(DeniedPseudoClass, status == ToolCallStatus.Denied);
    }

    private Control BuildBody()
    {
        var body = ToolCallBodyBuilder.Build(new ToolCallBodyBuilder.Context(DisplayedItem, Labels, OutputTemplate, Say));
        body.Margin = BodyMargin;
        return body;
    }

    /// <summary>Replaces an open panel's content (a status or output change); a closed panel builds when it opens.</summary>
    private void RebuildBody()
    {
        if (_body is null)
        {
            return;
        }
        if (IsExpanded)
        {
            // Focus inside the old panel moves to the row before the panel is replaced.
            if (_body.Content is { IsKeyboardFocusWithin: true })
            {
                _toggle?.Focus(SpecialFocusModality.For(this));
            }
            _body.Content = BuildBody();
        }
        else if (!_body.IsMounted)
        {
            _body.Content = null;
        }
    }

    private void OnBodyUnmounted(object? sender, EventArgs e)
    {
        if (!IsExpanded && _body is not null)
        {
            // Closed: the panel is built again the next time it opens.
            _body.Content = null;
        }
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleByUser();
    }

    private void OnTogglePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty)
        {
            PseudoClasses.Set(HeadHoverPseudoClass, e.GetNewValue<bool>());
        }
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (ReferenceEquals(sender, _retry))
        {
            Act(RetryRequestedEvent);
        }
        else if (ReferenceEquals(sender, _deny))
        {
            Act(DenyRequestedEvent);
        }
        else if (ReferenceEquals(sender, _allow))
        {
            Act(ApproveRequestedEvent);
        }
    }

    /// <summary>
    /// Raises an action; when nothing handled it, the simulation that drives the call takes it, or else Deny marks the call
    /// denied (Allow and Retry change nothing).
    /// </summary>
    internal void Act(RoutedEvent<ToolCallActionEventArgs> action)
    {
        var item = DisplayedItem;
        var args = new ToolCallActionEventArgs(action, item);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        if (item.Simulation is { } simulation)
        {
            if (action != DenyRequestedEvent)
            {
                _pendingNote = Labels.SimulatedNote;
            }
            if (action == RetryRequestedEvent)
            {
                simulation.Retry(item);
            }
            else if (action == ApproveRequestedEvent)
            {
                simulation.Approve(item);
            }
            else
            {
                simulation.Deny(item);
            }
            return;
        }
        if (action == DenyRequestedEvent && item.Status == ToolCallStatus.Approval)
        {
            item.Status = ToolCallStatus.Denied;
        }
    }
}
