using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a row came and goes.</summary>
internal enum AgentPlanRowPresence
{
    Present,
    Entering,
    Leaving
}

/// <summary>
/// One step of the plan (notes 3.1): padding 8 12 around a grid of a fixed 20px lead column, the body and a fixed 48px
/// trail column, 12 apart, radius 14, clipping what overflows. In a draft the lead is the drag handle, the body the title
/// field and the trail the remove button; once approved the lead is the status mark, the body the drawn title (the same
/// box, to the pixel) with the note, the activity lines and the failure block, and the trail the time or "Skipped".
/// The row's own height springs when it enters or leaves (the layout spring, with a fade); inside it, activity lines and
/// the failure block spring on their own, so the rows below follow every change. Lifted while dragged (Surface with the
/// floating shadow), and washed with 8% Accent when the agent inserts it mid-run.
/// </summary>
internal sealed class AgentPlanRow : Control
{
    public const double LeadWidth = 20;
    public const double TrailWidth = 48;
    public const double Gap = 12;
    public static readonly Thickness Inner = new(12, 8);

    private readonly AgentPlan _owner;
    private readonly RowContent _content;
    private readonly AgentPlanSwap _lead = new() { Width = LeadWidth, Height = 20 };
    private readonly Panel _removeHost = new();
    private readonly AgentPlanSwap _trail = new() { Width = TrailWidth, Height = 20 };
    private readonly StackPanel _body = new() { Orientation = Orientation.Vertical };
    private readonly Panel _titleCell = new();
    private readonly StackPanel _noteRow = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 0), Height = 16 };
    private readonly ArcGlyph _noteMark = new() { Size = 12, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _noteText = new() { FontSize = 12, LineHeight = 16, LetterSpacing = -0.14, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _lines = new() { Orientation = Orientation.Vertical };
    private readonly AgentPlanCollapse _activityBlock;
    private readonly AgentPlanCollapse _failBlock;
    private readonly TextBlock _failText = new() { FontSize = 12, LineHeight = 16, LetterSpacing = -0.14, TextWrapping = TextWrapping.Wrap };
    private readonly Run _failWord = new() { FontWeight = FontWeight.Medium };
    private readonly Run _failReason = new();
    private readonly TextBlock _time = new() { FontSize = 12, LineHeight = 20, LetterSpacing = -0.14, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _skipped = new() { FontSize = 12, LineHeight = 20, LetterSpacing = -0.14, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
    private readonly AgentPlanHeight _height;
    private readonly MotionValue<double> _fade;
    private readonly MotionValue<double> _lift;
    private readonly MotionValue<double> _wash;
    private readonly MotionValue<double> _offset;
    private readonly TranslateTransform _shift = new();
    private readonly RectangleGeometry _clip = new();
    private string _escapeBase = string.Empty;
    private bool _committing;
    private bool _streaming;
    private bool _revealed;
    private bool _touch;
    private double _contentHeight;
    private IImmutableSolidColorBrush? _washBrush;
    private Color _washColor;

    public AgentPlanRow(AgentPlan owner, AgentPlanStep step)
    {
        _owner = owner;
        Step = step;
        Grip = new AgentPlanGrip { Width = 24, Height = 28, Margin = new Thickness(-2, -4) };
        Glyph = new AgentPlanGlyph();
        TitleBox = new AgentPlanTitleBox
        {
            Margin = new Thickness(-8, -4),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            LineHeight = 20,
            LetterSpacing = -0.14
        };
        Title = new AgentPlanText { LineHeight = 20, LetterSpacing = -0.14, IsVisible = false };
        Remove = new AgentPlanButton { Classes = { "remove" }, Content = new ArcGlyph { Data = ArcIcons.X, Size = 14 } };
        Retry = new AgentPlanButton { Classes = { "retry" }, Content = Face(ArcIcons.RotateCcw, _retryLabel) };
        Skip = new AgentPlanButton { Classes = { "skip" }, Content = Face(ExplainerIcons.SkipForward, _skipLabel) };

        _lead.Children.Add(Grip);
        _lead.Children.Add(Glyph);
        // The swap owns its layers' opacity; the remove button's own reveal fade lives on the button inside a host.
        _removeHost.Children.Add(Remove);
        _trail.Children.Add(_removeHost);
        _trail.Children.Add(_time);
        _trail.Children.Add(_skipped);
        _titleCell.Children.Add(TitleBox);
        _titleCell.Children.Add(Title);
        _noteRow.Children.Add(_noteMark);
        _noteRow.Children.Add(_noteText);
        _activityBlock = new AgentPlanCollapse { Child = _lines, IsOpen = false, ExitFade = AgentPlanMotion.BlockExit };
        _failText.Inlines!.Add(_failWord);
        _failText.Inlines.Add(new Run(" "));
        _failText.Inlines.Add(_failReason);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(Retry);
        actions.Children.Add(Skip);
        var fail = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8, Margin = new Thickness(0, 6, 0, 2) };
        fail.Children.Add(_failText);
        fail.Children.Add(actions);
        _failBlock = new AgentPlanCollapse { Child = fail, IsOpen = false, EnterFade = AgentPlanMotion.FailEnter, ExitFade = TimeSpan.FromMilliseconds(50) };
        _body.Children.Add(_titleCell);
        _body.Children.Add(_noteRow);
        _body.Children.Add(_activityBlock);
        _body.Children.Add(_failBlock);
        _content = new RowContent(this, _lead, _body, _trail);
        VisualChildren.Add(_content);
        LogicalChildren.Add(_content);

        AutomationProperties.SetName(_lines, owner.Labels.ActivityName);
        AutomationProperties.SetControlTypeOverride(_lines, AutomationControlType.List);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
        AutomationProperties.SetAccessibilityView(_noteMark, AccessibilityView.Raw);

        ArcTokens.Bind(_noteText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_failWord, Run.ForegroundProperty, ArcDesignTokenKind.Danger);
        ArcTokens.Bind(_failReason, Run.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_time, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_skipped, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(Title, AgentPlanText.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(this, SurfaceInkProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, WashInkProperty, ArcDesignTokenKind.Accent);
        ArcTokens.Bind(this, ShadowProperty, ArcDesignTokenKind.ShadowFloating);

        _height = new AgentPlanHeight(this);
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += v => Opacity = v;
        _lift = new MotionValue<double>(this, 0, MotionVectors.Double);
        _lift.Changed += _ => InvalidateVisual();
        _wash = new MotionValue<double>(this, 0, MotionVectors.Double);
        _wash.Changed += _ => InvalidateVisual();
        _offset = new MotionValue<double>(this, 0, MotionVectors.Double);
        _offset.Changed += v => _shift.Y = v;
        RenderTransform = _shift;
        ClipToBounds = false;

        TitleBox.AddHandler(KeyDownEvent, OnTitleKeyDown, RoutingStrategies.Tunnel);
        TitleBox.GotFocus += OnTitleGotFocus;
        TitleBox.LostFocus += OnTitleLostFocus;
        Grip.AddHandler(KeyDownEvent, OnGripKeyDown, RoutingStrategies.Bubble);
        Grip.AddHandler(PointerPressedEvent, OnGripPressed, RoutingStrategies.Tunnel);
        Grip.AddHandler(PointerMovedEvent, OnGripMoved, RoutingStrategies.Tunnel);
        Grip.AddHandler(PointerReleasedEvent, OnGripReleased, RoutingStrategies.Tunnel);
        Grip.AddHandler(PointerCaptureLostEvent, OnGripCaptureLost, RoutingStrategies.Bubble);
        Remove.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.RemoveStep(this, AgentPlanRemoveSource.Button);
        };
        Retry.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.RequestStepAction(AgentPlanStepActionKind.Retry, Step);
        };
        Skip.Click += (_, e) =>
        {
            e.Handled = true;
            _owner.RequestStepAction(AgentPlanStepActionKind.Skip, Step);
        };
        Title.StreamCompleted += OnStreamCompleted;
        Grip.Cursor = new Cursor(StandardCursorType.Hand);
    }

    internal static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<AgentPlanRow, IBrush?>("SurfaceInk");

    internal static readonly StyledProperty<IBrush?> WashInkProperty =
        AvaloniaProperty.Register<AgentPlanRow, IBrush?>("WashInk");

    internal static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<AgentPlanRow, BoxShadows>("Shadow");

    static AgentPlanRow()
    {
        AffectsRender<AgentPlanRow>(SurfaceInkProperty, WashInkProperty, ShadowProperty);
    }

    public AgentPlanStep Step { get; }
    public AgentPlanGrip Grip { get; }
    public AgentPlanGlyph Glyph { get; }
    public AgentPlanTitleBox TitleBox { get; }
    public AgentPlanText Title { get; }
    public AgentPlanButton Remove { get; }
    public AgentPlanButton Retry { get; }
    public AgentPlanButton Skip { get; }
    public AgentPlanCollapse ActivityBlock => _activityBlock;
    public AgentPlanCollapse FailBlock => _failBlock;
    public StackPanel Lines => _lines;
    public TextBlock Time => _time;
    public TextBlock SkippedTag => _skipped;
    public StackPanel NoteRow => _noteRow;
    public TextBlock NoteText => _noteText;
    public TextBlock FailText => _failText;
    public AgentPlanSwap Lead => _lead;
    public AgentPlanSwap Trail => _trail;

    public AgentPlanRowPresence Presence { get; private set; } = AgentPlanRowPresence.Present;

    /// <summary>The row's vertical offset from its layout slot (FLIP and dragging).</summary>
    public MotionValue<double> Offset => _offset;

    /// <summary>Where the list last laid the row out (list coordinates), or NaN before.</summary>
    public double LayoutTop { get; set; } = double.NaN;

    /// <summary>The wash's strength now (0-1 of the 8%).</summary>
    public double WashAmount => _wash.Value;

    /// <summary>How lifted the row is (0-1).</summary>
    public double LiftAmount => _lift.Value;

    /// <summary>True while the title streams in.</summary>
    public bool IsStreaming => _streaming;

    /// <summary>The content's height (what the row settles to).</summary>
    public double ContentHeight => _contentHeight;

    // Presence -----------------------------------------------------------------------------------------------------------

    /// <summary>Grows the row in from nothing and fades it in (160ms).</summary>
    public void Enter(bool animate)
    {
        if (!animate)
        {
            Presence = AgentPlanRowPresence.Present;
            _fade.Jump(1);
            return;
        }
        Presence = AgentPlanRowPresence.Entering;
        _height.Jump(0);
        if (AgentPlanMotion.Fades(_owner))
        {
            _fade.Jump(0);
            _ = _fade.AnimateTo(1, AgentPlanMotion.Enter(AgentPlanMotion.RowEnter));
        }
        InvalidateMeasure();
    }

    private Action? _left;

    /// <summary>Folds the row away (height to nothing, fading out in 80ms); <paramref name="done"/> runs once it is gone.</summary>
    public void Leave(bool animate, Action done)
    {
        Presence = AgentPlanRowPresence.Leaving;
        IsHitTestVisible = false;
        Grip.IsTabStop = false;
        TitleBox.IsTabStop = false;
        Remove.IsTabStop = false;
        if (!animate || !IsLoaded || !AgentPlanMotion.Fades(_owner))
        {
            done();
            return;
        }
        _left = done;
        _ = _fade.AnimateTo(0, AgentPlanMotion.Enter(AgentPlanMotion.RowExit)).ContinueWith(
            _ => Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateMeasure), TaskScheduler.Default);
        InvalidateMeasure();
    }

    /// <summary>Washes the row with 8% Accent: held 640ms, then faded out over 1175ms (full motion only).</summary>
    public void Wash()
    {
        if (!AgentPlanMotion.IsFull(_owner))
        {
            return;
        }
        _wash.Jump(1);
        _ = _wash.AnimateTo(0, AgentPlanMotion.Standard(AgentPlanMotion.WashFade, AgentPlanMotion.WashHold));
    }

    /// <summary>Lifts the row while it is dragged (Surface and the floating shadow, 160ms; no scale).</summary>
    public void SetLifted(bool lifted)
    {
        Grip.IsDragging = lifted;
        ZIndex = lifted ? 1 : 0;
        if (IsLoaded && AgentPlanMotion.Fades(_owner))
        {
            _ = _lift.AnimateTo(lifted ? 1 : 0, AgentPlanMotion.Standard(AgentPlanMotion.Color));
        }
        else
        {
            _lift.Jump(lifted ? 1 : 0);
        }
    }

    // Content ------------------------------------------------------------------------------------------------------------

    /// <summary>Shows the step as the plan's status and the step's data say.</summary>
    public void Refresh(bool animate)
    {
        var labels = _owner.Labels;
        var status = _owner.CurrentStatus;
        var draft = status == AgentPlanStatus.Draft;
        var state = Step.State;

        _lead.Show(draft ? Grip : Glyph, animate);
        Glyph.Kind = draft ? AgentPlanGlyphKind.Pending : state switch
        {
            AgentPlanStepState.Active => status == AgentPlanStatus.Paused ? AgentPlanGlyphKind.Paused : AgentPlanGlyphKind.Active,
            AgentPlanStepState.Done => AgentPlanGlyphKind.Done,
            AgentPlanStepState.Failed => AgentPlanGlyphKind.Failed,
            AgentPlanStepState.Skipped => AgentPlanGlyphKind.Skipped,
            _ => AgentPlanGlyphKind.Pending
        };

        // The field while it is a draft (unless a suggestion is streaming in); the drawn title otherwise.
        var showBox = draft && !_streaming;
        if (!showBox && TitleBox.IsKeyboardFocusWithin)
        {
            Commit(focusLeaving: false);
        }
        TitleBox.IsVisible = showBox;
        Title.IsVisible = !showBox;
        if (!TitleBox.IsKeyboardFocusWithin && TitleBox.Text != Step.Title)
        {
            TitleBox.Text = Step.Title;
        }
        Title.Text = Step.Title;
        Title.Foreground = draft ? _owner.ForegroundInk : state switch
        {
            AgentPlanStepState.Done => _owner.SecondaryInk,
            AgentPlanStepState.Skipped => _owner.MutedInk,
            _ => _owner.ForegroundInk
        };
        Title.IsShimmering = !draft && state == AgentPlanStepState.Active && status == AgentPlanStatus.Running;
        TitleBox.PlaceholderText = labels.TitlePlaceholder;

        // Note.
        var noteText = Step.Note ?? Step.NoteKind switch
        {
            AgentPlanNoteKind.Suggested => labels.Suggested,
            AgentPlanNoteKind.AddedByAgent => labels.AddedByAgent,
            _ => null
        };
        _noteRow.IsVisible = !string.IsNullOrEmpty(noteText);
        _noteText.Text = noteText;
        var model = _owner.EffectiveModel;
        _noteMark.IsVisible = Step.NoteKind != AgentPlanNoteKind.None && model is not null;
        if (model is not null)
        {
            _noteMark.Data = model.Mark ?? ArcIcons.Sparkles;
            if (model.MarkBrush is { } markBrush)
            {
                _noteMark.Foreground = markBrush;
            }
            else
            {
                _noteMark.Foreground = _owner.ForegroundInk;
            }
        }

        // Activity lines: under the active, paused or failed step; folded away once it is done or skipped.
        SyncLines(animate);
        _activityBlock.IsOpen = !draft && (state is AgentPlanStepState.Active or AgentPlanStepState.Failed) && _lines.Children.Count > 0;

        // Failure.
        _failWord.Text = labels.FailedPrefix;
        _failReason.Text = Step.Error ?? string.Empty;
        _failBlock.IsOpen = !draft && state == AgentPlanStepState.Failed;
        _retryLabel.Text = labels.Retry;
        _skipLabel.Text = labels.Skip;
        AutomationProperties.SetName(Retry, labels.Retry + ": " + Step.Title);
        AutomationProperties.SetName(Skip, labels.Skip + ": " + Step.Title);

        // Trail.
        _time.Text = Step.Duration is { } duration ? labels.Duration(duration) : string.Empty;
        _skipped.Text = labels.SkippedTag;
        _trail.Show(draft ? _removeHost : state switch
        {
            AgentPlanStepState.Done when Step.Duration is not null => _time,
            AgentPlanStepState.Skipped => _skipped,
            _ => null
        }, animate);

        Grip.IsTabStop = draft && Presence != AgentPlanRowPresence.Leaving;
        Remove.IsTabStop = draft;
        UpdateNames();
        UpdateRemoveReveal();
    }

    private readonly TextBlock _retryLabel = new() { LineHeight = 16, LetterSpacing = -0.14, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _skipLabel = new() { LineHeight = 16, LetterSpacing = -0.14, VerticalAlignment = VerticalAlignment.Center };

    private static StackPanel Face(Geometry icon, TextBlock label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new ArcGlyph { Data = icon, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(label);
        return row;
    }

    private void SyncLines(bool animate)
    {
        var activities = Step.Activities;
        var children = _lines.Children;
        // Lines only grow while a step runs (a retry adds under the ones shown); anything else rebuilds them.
        var prefix = 0;
        while (prefix < children.Count && prefix < activities.Count && children[prefix] is AgentPlanActivityLine line && line.Text == activities[prefix])
        {
            prefix++;
        }
        if (prefix < children.Count)
        {
            if (activities.Count == 0 && _activityBlock.IsOpen)
            {
                // Folding away: keep the lines on screen while the block closes; they go once it is closed.
                return;
            }
            children.RemoveRange(prefix, children.Count - prefix);
        }
        var grow = animate && _activityBlock.IsOpen;
        for (var i = prefix; i < activities.Count; i++)
        {
            var line = new AgentPlanActivityLine(activities[i], enter: grow);
            children.Add(line);
            if (grow)
            {
                line.Open();
            }
        }
    }

    /// <summary>Re-reads accessible names (positions and titles).</summary>
    public void UpdateNames()
    {
        var labels = _owner.Labels;
        var position = _owner.PositionOf(this);
        var count = _owner.Steps.Count;
        var title = string.IsNullOrWhiteSpace(Step.Title) ? labels.TitlePlaceholder : Step.Title;
        AutomationProperties.SetName(TitleBox, labels.TitleName(position));
        AutomationProperties.SetName(Grip, labels.MoveName(position, title));
        AutomationProperties.SetName(Remove, labels.RemoveName(position, title));
        AutomationProperties.SetHelpText(TitleBox, labels.EditHint);
        AutomationProperties.SetHelpText(Grip, labels.EditHint);
        AutomationProperties.SetHelpText(Remove, labels.EditHint);
        AutomationProperties.SetPositionInSet(this, position);
        AutomationProperties.SetSizeOfSet(this, count);
        var draft = _owner.CurrentStatus == AgentPlanStatus.Draft;
        AutomationProperties.SetName(this, draft ? title : labels.StateWord(Step.State) + ": " + Step.Title);
        AutomationProperties.SetName(_lines, labels.ActivityName);
    }

    // Streaming ----------------------------------------------------------------------------------------------------------

    /// <summary>Streams the title in (a suggestion); the field takes over when it is done.</summary>
    public void StreamTitle()
    {
        _streaming = true;
        Refresh(animate: false);
        Title.Stream();
    }

    private void OnStreamCompleted(object? sender, EventArgs e)
    {
        if (!_streaming)
        {
            return;
        }
        _streaming = false;
        Refresh(animate: false);
        _owner.OnSuggestionStreamed(this);
    }

    // Editing ------------------------------------------------------------------------------------------------------------

    private void OnTitleGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _escapeBase = Step.Title;
        UpdateRemoveReveal();
    }

    private void OnTitleLostFocus(object? sender, FocusChangedEventArgs e)
    {
        UpdateRemoveReveal();
        if (Presence == AgentPlanRowPresence.Leaving || _owner.CurrentStatus != AgentPlanStatus.Draft)
        {
            return;
        }
        Commit(focusLeaving: true);
    }

    /// <summary>Saves the field's text; an emptied title that loses the focus removes the step.</summary>
    public void Commit(bool focusLeaving)
    {
        if (_committing)
        {
            return;
        }
        _committing = true;
        try
        {
            var text = TitleBox.Text ?? string.Empty;
            if (focusLeaving && string.IsNullOrWhiteSpace(text))
            {
                _owner.RemoveStep(this, AgentPlanRemoveSource.EmptyBlur);
                return;
            }
            if (text != Step.Title)
            {
                _owner.RenameStep(this, text, announce: !focusLeaving);
            }
            _escapeBase = text;
        }
        finally
        {
            _committing = false;
        }
    }

    private void OnTitleKeyDown(object? sender, KeyEventArgs e)
    {
        if (_owner.CurrentStatus != AgentPlanStatus.Draft)
        {
            return;
        }
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                var text = TitleBox.Text ?? string.Empty;
                if (text != Step.Title)
                {
                    _owner.RenameStep(this, text, announce: true);
                }
                _escapeBase = text;
                break;
            case Key.Escape:
                e.Handled = true;
                TitleBox.Text = _escapeBase;
                TitleBox.CaretIndex = _escapeBase.Length;
                TitleBox.ClearSelection();
                break;
            case Key.Back when string.IsNullOrEmpty(TitleBox.Text):
                e.Handled = true;
                _owner.RemoveStep(this, AgentPlanRemoveSource.Backspace);
                break;
            case Key.Up when alt:
                e.Handled = true;
                _owner.MoveStep(this, -1, fromKeyboard: true);
                break;
            case Key.Down when alt:
                e.Handled = true;
                _owner.MoveStep(this, 1, fromKeyboard: true);
                break;
        }
    }

    private void OnGripKeyDown(object? sender, KeyEventArgs e)
    {
        if (_owner.CurrentStatus != AgentPlanStatus.Draft || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (e.Key == Key.Up)
        {
            e.Handled = true;
            _owner.MoveStep(this, -1, fromKeyboard: true);
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            _owner.MoveStep(this, 1, fromKeyboard: true);
        }
    }

    // Dragging (the list does the work) ------------------------------------------------------------------------------------

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touch = true;
            UpdateRemoveReveal();
        }
        if (_owner.CurrentStatus != AgentPlanStatus.Draft || !e.GetCurrentPoint(Grip).Properties.IsLeftButtonPressed)
        {
            return;
        }
        Grip.Focus(NavigationMethod.Pointer);
        e.Pointer.Capture(Grip);
        _owner.List?.BeginDrag(this, e.GetPosition(_owner.List).Y);
        e.Handled = true;
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_owner.List is { } list && list.Dragged == this)
        {
            list.DragTo(e.GetPosition(list).Y);
            e.Handled = true;
        }
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_owner.List is { } list && list.Dragged == this)
        {
            e.Pointer.Capture(null);
            list.EndDrag();
            e.Handled = true;
        }
    }

    private void OnGripCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_owner.List is { } list && list.Dragged == this)
        {
            list.EndDrag();
        }
    }

    // Hover and focus: the remove button shows while the row is hovered or holds the focus (always on touch) ----------------

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _touch = true;
        }
        UpdateRemoveReveal();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateRemoveReveal();
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateRemoveReveal();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateRemoveReveal();
    }

    /// <summary>True while the remove button shows.</summary>
    public bool IsRemoveRevealed => _revealed;

    private void UpdateRemoveReveal()
    {
        _revealed = _touch || IsPointerOver || IsKeyboardFocusWithin || Grip.IsDragging;
        Remove.Classes.Set("revealed", _revealed);
    }

    // Layout ---------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        _content.Measure(availableSize.WithHeight(double.PositiveInfinity));
        _contentHeight = _content.DesiredSize.Height;
        switch (Presence)
        {
            case AgentPlanRowPresence.Entering:
                _height.To(_contentHeight);
                if (!_height.IsMoving && Math.Abs(_height.Value - _contentHeight) < 0.5)
                {
                    Presence = AgentPlanRowPresence.Present;
                }
                break;
            case AgentPlanRowPresence.Leaving:
                _height.To(0);
                if (_left is { } left && !_height.IsMoving && _height.Value < 0.5 && !_fade.IsAnimating)
                {
                    // Gone: the list removes it after this layout pass.
                    _left = null;
                    Avalonia.Threading.Dispatcher.UIThread.Post(left);
                }
                break;
            default:
                _height.Jump(_contentHeight);
                break;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : _content.DesiredSize.Width;
        return new Size(width, _height.Value);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _content.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_contentHeight, finalSize.Height)));
        // Only while the row is shorter than its content (entering or leaving). A clip's geometry changes reach the
        // compositor only after InvalidateVisual (design guide, pitfalls).
        if (_contentHeight > finalSize.Height + 0.5)
        {
            _clip.Rect = new Rect(0, 0, finalSize.Width, finalSize.Height);
            _content.Clip = _clip;
            _content.InvalidateVisual();
        }
        else if (_content.Clip is not null)
        {
            _content.Clip = null;
            _content.InvalidateVisual();
        }
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new RoundedRect(new Rect(Bounds.Size), 14);
        var lift = _lift.Value;
        if (lift > 0.001 && SurfaceInk is { } surface)
        {
            using (context.PushOpacity(lift))
            {
                context.DrawRectangle(surface, null, bounds, GetValue(ShadowProperty));
            }
        }
        var wash = _wash.Value;
        if (wash > 0.001 && WashInk is ISolidColorBrush accent)
        {
            if (_washBrush is null || _washColor != accent.Color)
            {
                _washColor = accent.Color;
                _washBrush = new ImmutableSolidColorBrush(accent.Color, AgentPlanMotion.WashAlpha);
            }
            using (context.PushOpacity(wash))
            {
                context.DrawRectangle(_washBrush, null, bounds);
            }
        }
    }

    private IBrush? SurfaceInk => GetValue(SurfaceInkProperty);

    private IBrush? WashInk => GetValue(WashInkProperty);

    /// <summary>Lays out the lead, body and trail columns (the CSS grid 20px | 1fr | 48px, gap 12, padding 8 12).</summary>
    private sealed class RowContent : Panel
    {
        private readonly AgentPlanRow _row;
        private readonly Control _lead;
        private readonly Control _body;
        private readonly Control _trail;

        public RowContent(AgentPlanRow row, Control lead, Control body, Control trail)
        {
            _row = row;
            _lead = lead;
            _body = body;
            _trail = trail;
            Children.Add(lead);
            Children.Add(body);
            Children.Add(trail);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 560;
            var bodyWidth = Math.Max(0, width - Inner.Left - Inner.Right - LeadWidth - TrailWidth - 2 * Gap);
            _lead.Measure(new Size(LeadWidth, 20));
            _trail.Measure(new Size(TrailWidth, 20));
            _body.Measure(new Size(bodyWidth, double.PositiveInfinity));
            var height = Inner.Top + Math.Max(20, _body.DesiredSize.Height) + Inner.Bottom;
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var bodyWidth = Math.Max(0, finalSize.Width - Inner.Left - Inner.Right - LeadWidth - TrailWidth - 2 * Gap);
            _lead.Arrange(new Rect(Inner.Left, Inner.Top, LeadWidth, 20));
            _body.Arrange(new Rect(Inner.Left + LeadWidth + Gap, Inner.Top, bodyWidth, _body.DesiredSize.Height));
            _trail.Arrange(new Rect(finalSize.Width - Inner.Right - TrailWidth, Inner.Top, TrailWidth, 20));
            return finalSize;
        }
    }
}

/// <summary>What removed a step (it decides the announcement and where the focus goes).</summary>
internal enum AgentPlanRemoveSource
{
    Button,
    Backspace,
    EmptyBlur
}
