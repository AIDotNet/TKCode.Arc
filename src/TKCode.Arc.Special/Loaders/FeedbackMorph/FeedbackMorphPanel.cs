using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>What the status line at the bottom left of the panel shows.</summary>
internal enum FeedbackStatusKind
{
    /// <summary>Nothing (an empty note, no message).</summary>
    None,

    /// <summary>How many characters are left.</summary>
    Counter,

    /// <summary>The hint after an empty send.</summary>
    Hint,

    /// <summary>The error after a failed send.</summary>
    Error
}

/// <summary>
/// The panel face of a <see cref="FeedbackMorph"/>: the question with a close button, the faces with their caption, the
/// note with its per-face prompt, and the status line beside the send button. Its rows come in one after another when it
/// opens, the faces from the middle outward.
/// </summary>
[TemplatePart(HeaderPart, typeof(Control))]
[TemplatePart(ClosePart, typeof(FeedbackMorphIconButton))]
[TemplatePart(MoodBlockPart, typeof(Control))]
[TemplatePart(MoodsPart, typeof(FeedbackMoodPicker))]
[TemplatePart(CaptionPart, typeof(ArcContentSwap))]
[TemplatePart(FieldPart, typeof(Control))]
[TemplatePart(NotePart, typeof(FeedbackMorphNote))]
[TemplatePart(PromptPart, typeof(ArcContentSwap))]
[TemplatePart(ActionsPart, typeof(Control))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(SendPart, typeof(FeedbackMorphSend))]
[PseudoClasses(BusyPseudoClass)]
internal sealed class FeedbackMorphPanel : TemplatedControl, IFeedbackMorphFace
{
    internal const string HeaderPart = "PART_Header";
    internal const string ClosePart = "PART_Close";
    internal const string MoodBlockPart = "PART_MoodBlock";
    internal const string MoodsPart = "PART_Moods";
    internal const string CaptionPart = "PART_Caption";
    internal const string FieldPart = "PART_Field";
    internal const string NotePart = "PART_Note";
    internal const string PromptPart = "PART_Prompt";
    internal const string ActionsPart = "PART_Actions";
    internal const string StatusPart = "PART_Status";
    internal const string SendPart = "PART_Send";
    internal const string BusyPseudoClass = ":busy";

    /// <summary>The question at the top.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, string?>(nameof(Title));

    /// <summary>The close button's accessible name.</summary>
    public static readonly StyledProperty<string?> CloseLabelProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, string?>(nameof(CloseLabel));

    /// <summary>The note's character limit.</summary>
    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, int>(nameof(MaxLength), 500);

    /// <summary>The send button's text.</summary>
    public static readonly StyledProperty<string?> SendLabelProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, string?>(nameof(SendLabel));

    /// <summary>The send button's text while sending.</summary>
    public static readonly StyledProperty<string?> SendingLabelProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, string?>(nameof(SendingLabel));

    /// <summary>The ink of the error line.</summary>
    public static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, IBrush?>(nameof(DangerBrush));

    /// <summary>The ink of the hint after an empty send.</summary>
    public static readonly StyledProperty<IBrush?> HintBrushProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, IBrush?>(nameof(HintBrush));

    /// <summary>The ink of the counter and of the idle caption and prompt.</summary>
    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, IBrush?>(nameof(MutedBrush));

    /// <summary>The ink of a face's name under the row.</summary>
    public static readonly StyledProperty<IBrush?> CaptionBrushProperty =
        AvaloniaProperty.Register<FeedbackMorphPanel, IBrush?>(nameof(CaptionBrush));

    // Arc Feedback morph (design reference): rows start 80 ms after the click, 50 ms apart (title, faces, note, actions),
    // each fading in over 170 ms while it settles from 10 px on the shape's spring and clears a 2 px blur; the faces start
    // with the note (180 ms), from the middle outward 35 ms apart, growing from 0.7. An empty send kicks the faces left at
    // about 300 px/s on a stiff, critically damped spring (≈ 3.5 px at most, back by 350 ms).
    private static readonly TimeSpan RowDelay = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan RowStagger = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MoodDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan MoodStagger = TimeSpan.FromMilliseconds(35);
    private const double RowOffset = 10;
    private const double MoodScale = 0.7;
    private static readonly SpringSpec NudgeSpring = SpringSpec.FromVisual(0.2, 0);
    private const double NudgeVelocity = -300;

    private readonly FeedbackMorph _owner;
    private readonly Dictionary<Control, ItemMotion> _motions = new();
    private Control? _header;
    private FeedbackMorphIconButton? _close;
    private Control? _moodBlock;
    private FeedbackMoodPicker? _moods;
    private ArcContentSwap? _caption;
    private Control? _field;
    private FeedbackMorphNote? _note;
    private ArcContentSwap? _prompt;
    private Control? _actions;
    private ArcContentSwap? _status;
    private FeedbackMorphSend? _send;
    private RollingDigits? _digits;
    private Control? _counter;
    private TextBlock? _counterLabel;
    private TextBlock? _hint;
    private Control? _error;
    private TextBlock? _errorText;
    private TextBlock? _captionText;
    private TextBlock? _promptText;
    private TranslateTransform? _nudge;
    private MotionValue<double>? _nudgeValue;
    private FeedbackStatusKind _statusKind;
    private bool _syncingText;
    private bool? _pendingEntrance;

    public FeedbackMorphPanel(FeedbackMorph owner)
    {
        _owner = owner;
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? CloseLabel { get => GetValue(CloseLabelProperty); set => SetValue(CloseLabelProperty, value); }

    public int MaxLength { get => GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }

    public string? SendLabel { get => GetValue(SendLabelProperty); set => SetValue(SendLabelProperty, value); }

    public string? SendingLabel { get => GetValue(SendingLabelProperty); set => SetValue(SendingLabelProperty, value); }

    public IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }

    public IBrush? HintBrush { get => GetValue(HintBrushProperty); set => SetValue(HintBrushProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public IBrush? CaptionBrush { get => GetValue(CaptionBrushProperty); set => SetValue(CaptionBrushProperty, value); }

    internal FeedbackMoodPicker? Moods => _moods;

    internal FeedbackMorphNote? Note => _note;

    internal FeedbackMorphSend? Send => _send;

    internal FeedbackMorphIconButton? Close => _close;

    internal FeedbackStatusKind StatusKind => _statusKind;

    internal string? CaptionText => _captionText?.Text;

    internal string? PromptText => _promptText?.Text;

    internal bool IsPromptVisible => _prompt?.IsVisible == true;

    internal Control? MoodBlock => _moodBlock;

    /// <summary>The remaining-characters counter (tests).</summary>
    internal RollingDigits? Digits => _digits;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_moods is not null)
        {
            _moods.Picked -= OnMoodPicked;
            _moods.HoverChanged -= UpdateCaption;
        }
        if (_note is not null)
        {
            _note.TextChanged -= OnNoteTextChanged;
            _note.RemoveHandler(KeyDownEvent, OnNoteKeyDown);
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        _motions.Clear();
        _header = e.NameScope.Find<Control>(HeaderPart);
        _close = e.NameScope.Find<FeedbackMorphIconButton>(ClosePart);
        _moodBlock = e.NameScope.Find<Control>(MoodBlockPart);
        _moods = e.NameScope.Find<FeedbackMoodPicker>(MoodsPart);
        _caption = e.NameScope.Find<ArcContentSwap>(CaptionPart);
        _field = e.NameScope.Find<Control>(FieldPart);
        _note = e.NameScope.Find<FeedbackMorphNote>(NotePart);
        _prompt = e.NameScope.Find<ArcContentSwap>(PromptPart);
        _actions = e.NameScope.Find<Control>(ActionsPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _send = e.NameScope.Find<FeedbackMorphSend>(SendPart);
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_moods is not null)
        {
            _moods.Picked += OnMoodPicked;
            _moods.HoverChanged += UpdateCaption;
            AutomationProperties.SetName(_moods, _owner.MoodsLabel);
        }
        if (_note is not null)
        {
            _note.TextChanged += OnNoteTextChanged;
            // Tunnel: Ctrl/Cmd+Enter sends before the text box turns it into a new line.
            _note.AddHandler(KeyDownEvent, OnNoteKeyDown, RoutingStrategies.Tunnel);
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        if (_moodBlock is not null)
        {
            _nudge = new TranslateTransform();
            _moodBlock.RenderTransform = _nudge;
            _nudgeValue?.Dispose();
            _nudgeValue = null;
        }
        _captionText = null;
        _promptText = null;
        _counter = null;
        _hint = null;
        _error = null;
        Refresh();
        if (_pendingEntrance is { } animate)
        {
            _pendingEntrance = null;
            OnEntering(animate);
        }
    }

    /// <summary>Takes every value from the owner (moods, pick, note, words).</summary>
    internal void Refresh()
    {
        _moods?.SetMoods(_owner.EffectiveMoods, _owner.SelectedMood);
        // Forget the entrance motions of faces that were replaced.
        if (_moods is not null)
        {
            foreach (var stale in _motions.Keys.OfType<FeedbackMoodButton>().Where(b => !_moods.Buttons.Contains(b)).ToList())
            {
                _motions.Remove(stale);
            }
        }
        SyncNote();
        UpdateCaption();
        UpdatePrompt();
        UpdateSendState();
        UpdateStatus();
    }

    /// <summary>Shows the owner's pick (property changed from code or by the person).</summary>
    internal void SyncSelection(bool animate)
    {
        _moods?.Select(_owner.SelectedMood, animate);
        UpdateCaption();
        UpdatePrompt();
        UpdateSendState();
    }

    /// <summary>Copies the owner's message into the note (when it was set from code).</summary>
    internal void SyncNote()
    {
        if (_note is null)
        {
            return;
        }
        var text = _owner.Message ?? string.Empty;
        if (!string.Equals(_note.Text ?? string.Empty, text, StringComparison.Ordinal))
        {
            _syncingText = true;
            try
            {
                _note.Text = text;
            }
            finally
            {
                _syncingText = false;
            }
        }
        UpdatePrompt();
        UpdateSendState();
        UpdateStatus();
    }

    /// <summary>Moves focus to the picked face, else the first face.</summary>
    internal bool FocusFirst(NavigationMethod method)
    {
        if (_moods?.FocusTarget is { } target)
        {
            return target.Focus(method);
        }
        return _note?.Focus(method) == true;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.CloseFromPanel(returnFocus: true);
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _owner.Submit();
    }

    private void OnNoteKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            e.Handled = true;
            _owner.Submit();
        }
    }

    private void OnMoodPicked(FeedbackMood mood)
    {
        _owner.PickFromPanel(mood);
    }

    private void OnNoteTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingText)
        {
            return;
        }
        _owner.MessageFromPanel(_note?.Text ?? string.Empty);
        UpdatePrompt();
        UpdateSendState();
        if (_statusKind is FeedbackStatusKind.Hint or FeedbackStatusKind.Error)
        {
            _statusKind = FeedbackStatusKind.None;
        }
        UpdateStatus();
    }

    /// <summary>The caption: the face under the mouse, else the picked face, else the hint.</summary>
    private void UpdateCaption()
    {
        if (_caption is null || _moods is null)
        {
            return;
        }
        var text = FeedbackMoodCaption.Text(_moods, _owner.CaptionHint);
        var named = (_moods.Hovered ?? _moods.Selected) is not null;
        if (_captionText is not null && string.Equals(_captionText.Text, text, StringComparison.Ordinal))
        {
            return;
        }
        _captionText = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis };
        _captionText.Bind(TextBlock.ForegroundProperty, this.GetObservable(named ? CaptionBrushProperty : MutedBrushProperty));
        _caption.Content = _captionText;
    }

    /// <summary>The prompt over the empty note: the picked face's question, else the placeholder; hidden once there is text.</summary>
    private void UpdatePrompt()
    {
        if (_prompt is null)
        {
            return;
        }
        var empty = string.IsNullOrEmpty(_note?.Text);
        _prompt.IsVisible = empty;
        var text = _owner.SelectedMood?.Prompt ?? _owner.Placeholder ?? string.Empty;
        if (_promptText is not null && string.Equals(_promptText.Text, text, StringComparison.Ordinal))
        {
            return;
        }
        _promptText = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        _promptText.Bind(TextBlock.ForegroundProperty, this.GetObservable(MutedBrushProperty));
        _prompt.Content = _promptText;
        if (_note is not null)
        {
            AutomationProperties.SetHelpText(_note, text);
        }
    }

    private void UpdateSendState()
    {
        if (_send is null)
        {
            return;
        }
        _send.IsEmpty = !_owner.HasContent;
    }

    /// <summary>Shows the hint after an empty send and nudges the faces.</summary>
    internal void ShowEmptyHint()
    {
        _statusKind = FeedbackStatusKind.Hint;
        UpdateStatus();
        Nudge();
    }

    /// <summary>Shows the error after a failed send.</summary>
    internal void ShowError()
    {
        _statusKind = FeedbackStatusKind.Error;
        UpdateStatus();
    }

    /// <summary>Back to the counter (a fresh form, or after the error was read).</summary>
    internal void ClearMessage()
    {
        _statusKind = FeedbackStatusKind.None;
        UpdateStatus();
    }

    /// <summary>Marks the panel busy while the note is sent (read-only note, dimmed faces, spinner).</summary>
    internal void SetBusy(bool busy)
    {
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_note is not null)
        {
            _note.IsReadOnly = busy;
        }
        if (_send is not null)
        {
            _send.IsBusy = busy;
        }
        if (_moods is not null)
        {
            _moods.IsHitTestVisible = !busy;
        }
        if (busy)
        {
            _statusKind = FeedbackStatusKind.None;
            UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        if (_status is null)
        {
            return;
        }
        var length = _note?.Text?.Length ?? 0;
        var kind = _statusKind;
        if (kind == FeedbackStatusKind.None && length > 0)
        {
            kind = FeedbackStatusKind.Counter;
        }
        object? content = kind switch
        {
            FeedbackStatusKind.Counter => EnsureCounter(),
            FeedbackStatusKind.Hint => EnsureHint(),
            FeedbackStatusKind.Error => EnsureError(),
            _ => null
        };
        if (_digits is not null)
        {
            _digits.Value = Math.Max(0, MaxLength - length);
        }
        if (!ReferenceEquals(_status.Content, content))
        {
            _status.Content = content;
        }
    }

    private Control EnsureCounter()
    {
        if (_counter is null)
        {
            _digits = new RollingDigits
            {
                UseGrouping = false,
                FontSize = 12,
                Falloff = RollingDigitsFalloff.Linear,
                GlyphPitch = 0.85,
                WheelMotion = MotionSpec.Spring(FeedbackMorphSurface.ShapeSpring),
                VerticalAlignment = VerticalAlignment.Center,
                Value = Math.Max(0, MaxLength - (_note?.Text?.Length ?? 0))
            };
            _digits.Bind(RollingDigits.ForegroundProperty, this.GetObservable(MutedBrushProperty));
            _counterLabel = new TextBlock { Text = _owner.RemainingLabel, VerticalAlignment = VerticalAlignment.Center };
            _counterLabel.Bind(TextBlock.ForegroundProperty, this.GetObservable(MutedBrushProperty));
            _counter = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 3,
                Children = { _digits, _counterLabel }
            };
        }
        if (_counterLabel is not null)
        {
            _counterLabel.Text = _owner.RemainingLabel;
        }
        return _counter;
    }

    private Control EnsureHint()
    {
        if (_hint is null)
        {
            _hint = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            _hint.Bind(TextBlock.ForegroundProperty, this.GetObservable(HintBrushProperty));
        }
        _hint.Text = _owner.EmptyHint;
        return _hint;
    }

    private Control EnsureError()
    {
        if (_error is null)
        {
            _errorText = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var glyph = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 16, VerticalAlignment = VerticalAlignment.Center };
            glyph.Bind(ArcGlyph.ForegroundProperty, this.GetObservable(DangerBrushProperty));
            _errorText.Bind(TextBlock.ForegroundProperty, this.GetObservable(DangerBrushProperty));
            _error = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { glyph, _errorText } };
        }
        if (_errorText is not null)
        {
            _errorText.Text = _owner.ErrorMessage;
        }
        return _error;
    }

    /// <summary>The kick that tells the person to pick a face first (full motion only).</summary>
    private void Nudge()
    {
        if (_nudge is null || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            return;
        }
        if (_nudgeValue is null)
        {
            _nudgeValue = new MotionValue<double>(this, 0, MotionVectors.Double);
            _nudgeValue.Changed += x => _nudge.X = x;
        }
        _ = _nudgeValue.AnimateTo(0, MotionSpec.Spring(NudgeSpring), NudgeVelocity);
    }

    /// <summary>The rows come in one after another and the faces from the middle outward (full motion only).</summary>
    public void OnEntering(bool animate)
    {
        if (_header is null && _actions is null)
        {
            // Shown before its template was applied (first open): play once the parts exist.
            _pendingEntrance = animate;
            return;
        }
        var rows = new[] { _header, _moodBlock, _field, _actions };
        var sign = _owner.Side == FeedbackMorphSide.Top ? 1 : -1;
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i] is not { } row)
            {
                continue;
            }
            var motion = MotionOf(row, isRow: true);
            if (animate)
            {
                motion.Play(RowDelay + RowStagger * i, sign * RowOffset, 1);
            }
            else
            {
                motion.Rest();
            }
        }
        if (_moods is null)
        {
            return;
        }
        var buttons = _moods.Buttons;
        var middle = (buttons.Count - 1) / 2.0;
        for (var i = 0; i < buttons.Count; i++)
        {
            var motion = MotionOf(buttons[i], isRow: false);
            if (animate)
            {
                motion.Play(MoodDelay + MoodStagger * Math.Floor(Math.Abs(i - middle)), 0, MoodScale);
            }
            else
            {
                motion.Rest();
            }
        }
    }

    /// <summary>The rows drift back by 10 px while the surface fades the panel out (full motion only).</summary>
    internal void OnLeaving(bool animate)
    {
        if (!animate)
        {
            return;
        }
        var sign = _owner.Side == FeedbackMorphSide.Top ? 1 : -1;
        foreach (var row in new[] { _header, _moodBlock, _field, _actions })
        {
            if (row is not null)
            {
                MotionOf(row, isRow: true).Leave(sign * RowOffset);
            }
        }
    }

    private ItemMotion MotionOf(Control control, bool isRow)
    {
        if (!_motions.TryGetValue(control, out var motion))
        {
            motion = new ItemMotion(this, control, isRow);
            _motions.Add(control, motion);
        }
        return motion;
    }

    /// <summary>One row's or face's entrance: fade, offset or scale, and (rows) a soft blur.</summary>
    private sealed class ItemMotion
    {
        private readonly Control _target;
        private readonly bool _isRow;
        private readonly TranslateTransform _translate = new();
        private readonly ScaleTransform _scale = new();
        private readonly MotionValue<double> _fade;
        private readonly MotionValue<double> _move;

        public ItemMotion(Visual owner, Control target, bool isRow)
        {
            _target = target;
            _isRow = isRow;
            if (isRow)
            {
                // The faces row already carries the nudge's translation: move a wrapper transform instead.
                target.RenderTransform = target.RenderTransform is TranslateTransform nudge
                    ? new TransformGroup { Children = { nudge, _translate } }
                    : _translate;
            }
            else
            {
                target.RenderTransformOrigin = RelativePoint.Center;
                target.RenderTransform = _scale;
            }
            _fade = new MotionValue<double>(owner, 1, MotionVectors.Double);
            _fade.Changed += p =>
            {
                _target.Opacity = p;
                if (_isRow)
                {
                    _target.Effect = ArcBlur.Get(2 * (1 - p));
                }
            };
            _move = new MotionValue<double>(owner, isRow ? 0 : 1, MotionVectors.Double);
            _move.Changed += v =>
            {
                if (_isRow)
                {
                    _translate.Y = v;
                }
                else
                {
                    _scale.ScaleX = v;
                    _scale.ScaleY = v;
                }
            };
        }

        public void Play(TimeSpan delay, double offset, double scale)
        {
            _fade.Jump(0);
            _move.Jump(_isRow ? offset : scale);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(FeedbackMorphSurface.Fade, ArcEasings.Standard, delay));
            _ = _move.AnimateTo(_isRow ? 0 : 1, MotionSpec.Spring(FeedbackMorphSurface.ShapeSpring, delay));
        }

        public void Leave(double offset)
        {
            _ = _move.AnimateTo(offset, MotionSpec.Tween(FeedbackMorphSurface.QuickFade, ArcEasings.Standard));
        }

        public void Rest()
        {
            _fade.Jump(1);
            _move.Jump(_isRow ? 0 : 1);
            _target.Effect = null;
        }
    }
}
