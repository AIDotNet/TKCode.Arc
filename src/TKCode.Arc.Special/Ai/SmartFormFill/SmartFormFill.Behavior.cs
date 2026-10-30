using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What the footer says: the words, a glyph and its tone, and whether the words shimmer (reading).</summary>
internal readonly record struct SmartFormFillStatus(string Text, Geometry Glyph, ArcDesignTokenKind Tone, bool Shimmer);

public partial class SmartFormFill
{
    private readonly List<SmartFormFillFieldView> _views = [];
    private readonly Dictionary<SmartFormFillFieldView, (TextRangeDecoration Decoration, MotionValue<double> Opacity)> _highlights = [];
    private readonly List<IDisposable> _fillShots = [];
    private SmartFormFillField[]? _contactFields;
    private IReadOnlyDictionary<string, string> _valuesSnapshot = new Dictionary<string, string>();
    private SmartFormFillPhase _phase;
    private int _reviewCount;
    private bool _canUndo;
    private Dictionary<string, string>? _before;
    private CancellationTokenSource? _readCts;
    private int _readVersion;
    private SmartFormFillPhase _phaseBeforeRead;
    private string? _readText;
    private SmartFormFillResult? _lastResult;
    private int _filledCount;
    private bool _simulatedRun;
    private int _sampleIndex;
    private bool _pastePending;
    private bool _syncingText;
    private bool _showingSource;
    private SmartFormFillFieldView? _hovered;
    private SmartFormFillFieldView? _focused;
    private SmartFormFillFieldView? _filling;
    private SmartFormFillStatus? _status;
    private string? _shownStatusKey;

    private Border? _card;
    private TextBlock? _title;
    private TextBlock? _description;
    private TextBlock? _wellHeading;
    private Border? _sourceFocus;
    private Grid? _body;
    private Border? _well;
    private Panel? _wellHost;
    private Textarea? _editor;
    private ScrollViewer? _sourceScroll;
    private DecoratedTextBlock? _source;
    private ArcGlyph? _modelMark;
    private TextBlock? _modelName;
    private AvaloniaButton? _sample;
    private AvaloniaButton? _fill;
    private AvaloniaButton? _edit;
    private SmartFormFillFormPanel? _form;
    private Grid? _footer;
    private ArcContentSwap? _statusSwap;
    private StackPanel? _actions;
    private AvaloniaButton? _undo;
    private AvaloniaButton? _submit;

    /// <summary>The timers behind the simulated read and save, the fill stagger and the autoplay (tests replace them).</summary>
    internal ISmartFormFillTimers Timers { get; set; } = SmartFormFillDispatcherTimers.Instance;

    /// <summary>The fields on screen: <see cref="Fields"/>, or the contact form when it is empty.</summary>
    internal IReadOnlyList<SmartFormFillField> EffectiveFields => _fields.Count > 0 ? _fields : (_contactFields ??= SmartFormFillField.ContactFields());

    /// <summary>The simulation being played: <see cref="Simulation"/> while the card has no <see cref="Extract"/>.</summary>
    internal SmartFormFillSimulation? PlayingSimulation => Extract is null ? Simulation : null;

    /// <summary>The samples behind Try sample: <see cref="Samples"/>, the simulation's, or none.</summary>
    internal IReadOnlyList<SmartFormFillSample> EffectiveSamples => Samples ?? PlayingSimulation?.Samples ?? [];

    /// <summary>The model named beside the paste area (empty with none).</summary>
    internal string EffectiveModelName => ModelName ?? PlayingSimulation?.ModelName ?? string.Empty;

    internal IReadOnlyList<SmartFormFillFieldView> FieldViews => _views;

    internal SmartFormFillFieldView? ViewOf(string id) => _views.FirstOrDefault(v => v.Field.Id == id);

    internal string StatusText => _status?.Text ?? string.Empty;

    /// <summary>What the polite live region says: the status, plus "Simulated" when the built-in reader ran.</summary>
    internal string Announcement { get; private set; } = string.Empty;

    internal bool ShowingSource => _showingSource;

    internal DecoratedTextBlock? SourceBlock => _source;

    internal Textarea? Editor => _editor;

    internal AvaloniaButton? SampleButton => _sample;

    internal AvaloniaButton? FillButton => _fill;

    internal AvaloniaButton? UndoButton => _undo;

    internal AvaloniaButton? SubmitButton => _submit;

    internal SmartFormFillFormPanel? FormPanel => _form;

    internal Grid? Footer => _footer;

    internal Border? WellBorder => _well;

    /// <summary>The highlight of a field's source in the pasted text, or null.</summary>
    internal TextRangeDecoration? HighlightOf(string id) =>
        ViewOf(id) is { } view && _highlights.TryGetValue(view, out var entry) ? entry.Decoration : null;

    /// <summary>The field whose source takes the strong tint: hovered, else focused, else the one filling.</summary>
    internal SmartFormFillFieldView? ActiveField => _hovered ?? _focused ?? _filling;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _card = e.NameScope.Find<Border>(CardPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _wellHeading = e.NameScope.Find<TextBlock>("PART_WellHeading");
        _sourceFocus = e.NameScope.Find<Border>("PART_SourceFocus");
        _body = e.NameScope.Find<Grid>(BodyPart);
        _well = e.NameScope.Find<Border>(WellPart);
        _wellHost = e.NameScope.Find<Panel>(WellHostPart);
        _editor = e.NameScope.Find<Textarea>(EditorPart);
        _sourceScroll = e.NameScope.Find<ScrollViewer>(SourceScrollPart);
        _source = e.NameScope.Find<DecoratedTextBlock>(SourcePart);
        _modelMark = e.NameScope.Find<ArcGlyph>(ModelMarkPart);
        _modelName = e.NameScope.Find<TextBlock>(ModelNamePart);
        _sample = e.NameScope.Find<AvaloniaButton>(SamplePart);
        _fill = e.NameScope.Find<AvaloniaButton>(FillPart);
        _edit = e.NameScope.Find<AvaloniaButton>(EditPart);
        _form = e.NameScope.Find<SmartFormFillFormPanel>(FormPart);
        _footer = e.NameScope.Find<Grid>(FooterPart);
        _statusSwap = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _actions = e.NameScope.Find<StackPanel>(ActionsPart);
        _undo = e.NameScope.Find<AvaloniaButton>(UndoPart);
        _submit = e.NameScope.Find<AvaloniaButton>(SubmitPart);

        if (_source is not null)
        {
            _source.Text = Text;
        }
        if (_editor is not null)
        {
            _editor.Text = Text;
            _editor.TextChanged += OnEditorTextChanged;
            _editor.PastingFromClipboard += OnEditorPasting;
        }
        if (_sample is not null)
        {
            _sample.Click += OnSampleClick;
        }
        if (_fill is not null)
        {
            _fill.Click += OnFillClick;
        }
        if (_edit is not null)
        {
            _edit.Click += OnEditClick;
        }
        if (_undo is not null)
        {
            _undo.Click += OnUndoClick;
        }
        if (_submit is not null)
        {
            _submit.Click += OnSubmitClick;
        }
        if (_sourceScroll is not null)
        {
            _sourceScroll.GotFocus += OnSourceFocus;
            _sourceScroll.LostFocus += OnSourceBlur;
        }
        if (_statusSwap is not null)
        {
            AutomationProperties.SetLiveSetting(_statusSwap, AutomationLiveSetting.Polite);
        }
        _highlights.Clear();
        _shownStatusKey = null;
        RebuildFields();
        _status ??= ReadyStatus();
        UpdateBreakpoints(Bounds.Width);
        RefreshChrome();
        ShowStatus();
    }

    private void Unhook()
    {
        if (_editor is not null)
        {
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.PastingFromClipboard -= OnEditorPasting;
        }
        if (_sample is not null)
        {
            _sample.Click -= OnSampleClick;
        }
        if (_fill is not null)
        {
            _fill.Click -= OnFillClick;
        }
        if (_edit is not null)
        {
            _edit.Click -= OnEditClick;
        }
        if (_undo is not null)
        {
            _undo.Click -= OnUndoClick;
        }
        if (_submit is not null)
        {
            _submit.Click -= OnSubmitClick;
        }
        if (_sourceScroll is not null)
        {
            _sourceScroll.GotFocus -= OnSourceFocus;
            _sourceScroll.LostFocus -= OnSourceBlur;
        }
    }

    /// <summary>The pasted text view is a focusable region; keyboard focus draws the ring around it, pointer focus does not.</summary>
    private void OnSourceFocus(object? sender, FocusChangedEventArgs e)
    {
        if (_sourceFocus is not null)
        {
            _sourceFocus.IsVisible = _showingSource && (e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional);
        }
    }

    private void OnSourceBlur(object? sender, RoutedEventArgs e)
    {
        if (_sourceFocus is not null)
        {
            _sourceFocus.IsVisible = false;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            OnTextPropertyChanged();
        }
        else if (change.Property == DefaultValuesProperty)
        {
            ApplyDefaults(onlyEmpty: false);
        }
        else if (change.Property == LabelsProperty)
        {
            foreach (var view in _views)
            {
                view.Refresh();
            }
            if (_status is { } status && status.Text == change.GetOldValue<SmartFormFillLabels>()?.Ready)
            {
                _status = ReadyStatus();
            }
            RefreshChrome();
            ShowStatus();
        }
        else if (change.Property == ResolveLogoProperty)
        {
            foreach (var view in _views)
            {
                view.RebuildLogo();
            }
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == ModelNameProperty
                 || change.Property == ModelMarkProperty || change.Property == SubmitLabelProperty || change.Property == SamplesProperty)
        {
            RefreshChrome();
            if (change.Property == SamplesProperty)
            {
                UpdateAutoplay();
            }
        }
        else if (change.Property == SimulationProperty)
        {
            OnSimulationChanged();
        }
        else if (change.Property == ExtractProperty || change.Property == AutoPlayProperty)
        {
            RefreshChrome();
            UpdateAutoplay();
        }
        else if (change.Property == HighlightBrushProperty || change.Property == ReviewHighlightBrushProperty
                 || change.Property == HighlightCornerRadiusProperty || change.Property == HighlightOutsetProperty
                 || change.Property == HighlightRestOpacityProperty)
        {
            RepaintHighlights();
        }
        else if (change.Property == PhaseProperty)
        {
            PseudoClasses.Set(ReadingPseudoClass, Phase == SmartFormFillPhase.Reading);
            RefreshChrome();
        }
        else if (change.Property == CanUndoProperty)
        {
            RefreshChrome();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachAutoplay();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Reads are aborted and timers cleared when the card leaves; a fill in flight lands at once.
        if (Phase == SmartFormFillPhase.Reading)
        {
            Stop();
        }
        FinishFill();
        DetachAutoplay();
    }

    // ---- Layout -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The card follows its own width, not the window's (Arc uses container queries): from 720px of content the well sits
    /// left of the form, below it the well stacks on top at a fixed height; under 440px every field takes the full row and
    /// the footer puts its status over the actions.
    /// </summary>
    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0) || _body is null)
        {
            return;
        }
        var padding = _card?.Padding ?? new Thickness(20);
        var content = width - padding.Left - padding.Right;
        var stacked = content < SideBySideWidth - 0.01;
        var narrow = content < SingleColumnWidth - 0.01;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        var gap = SectionGap;
        if (_well is not null && _form is not null)
        {
            if (stacked)
            {
                if (_body.ColumnDefinitions.Count != 1)
                {
                    _body.ColumnDefinitions = new ColumnDefinitions("*");
                }
                Grid.SetRow(_well, 0);
                Grid.SetColumn(_well, 0);
                Grid.SetRow(_form, 1);
                Grid.SetColumn(_form, 0);
                _form.Margin = new Thickness(0, gap, 0, 0);
            }
            else
            {
                if (_body.ColumnDefinitions.Count != 2)
                {
                    _body.ColumnDefinitions = new ColumnDefinitions("5*,7*");
                }
                Grid.SetRow(_well, 0);
                Grid.SetColumn(_well, 0);
                Grid.SetRow(_form, 0);
                Grid.SetColumn(_form, 1);
                _form.Margin = new Thickness(gap, 0, 0, 0);
            }
            _form.IsSingleColumn = narrow;
        }
        if (_wellHost is not null)
        {
            _wellHost.Height = stacked ? EditorHeight : double.NaN;
            _wellHost.MinHeight = stacked ? EditorHeight : EditorMinHeight;
        }
        if (_footer is not null && _statusSwap is not null && _actions is not null)
        {
            Grid.SetRow(_statusSwap, 0);
            Grid.SetColumn(_statusSwap, 0);
            Grid.SetColumnSpan(_statusSwap, narrow ? 2 : 1);
            Grid.SetRow(_actions, narrow ? 1 : 0);
            Grid.SetColumn(_actions, narrow ? 0 : 1);
            Grid.SetColumnSpan(_actions, narrow ? 2 : 1);
            _actions.HorizontalAlignment = HorizontalAlignment.Right;
            _actions.Margin = narrow ? new Thickness(0, 12, 0, 0) : new Thickness(12, 0, 0, 0);
        }
    }

    // ---- Fields and values --------------------------------------------------------------------------------------

    private void RebuildFields()
    {
        if (_form is null)
        {
            return;
        }
        FinishFill();
        var keep = _views.ToDictionary(v => v.Field.Id, v => v.Text, StringComparer.Ordinal);
        ClearHighlights();
        _views.Clear();
        _form.Children.Clear();
        _hovered = _focused = _filling = null;
        foreach (var field in EffectiveFields)
        {
            var view = new SmartFormFillFieldView(this, field);
            _views.Add(view);
            _form.Children.Add(view);
            view.SetText(keep.TryGetValue(field.Id, out var text) ? text : null);
        }
        ApplyDefaults(onlyEmpty: true);
        UpdateReviewCount();
        PublishValues(raise: false);
    }

    private void ApplyDefaults(bool onlyEmpty)
    {
        if (DefaultValues is not { } defaults)
        {
            return;
        }
        var changed = false;
        foreach (var view in _views)
        {
            if (defaults.TryGetValue(view.Field.Id, out var value) && (!onlyEmpty || view.Text.Length == 0) && view.Text != value)
            {
                view.Reset(value);
                changed = true;
            }
        }
        if (changed)
        {
            PublishValues(raise: !onlyEmpty);
        }
    }

    /// <summary>The value of one field, or an empty string.</summary>
    public string GetFieldValue(string id) => ViewOf(id)?.Text ?? (_valuesSnapshot.TryGetValue(id, out var value) ? value : string.Empty);

    /// <summary>Sets one field as if typed by the app (no confidence badge); raises <see cref="ValuesChanged"/>.</summary>
    public void SetFieldValue(string id, string? value)
    {
        if (ViewOf(id) is not { } view)
        {
            return;
        }
        view.Reset(value);
        DropHighlight(view);
        UpdateReviewCount();
        PublishValues(raise: true);
    }

    private void PublishValues(bool raise)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var view in _views)
        {
            values[view.Field.Id] = view.Text;
        }
        Values = values;
        if (raise)
        {
            RaiseEvent(new SmartFormFillValuesEventArgs(ValuesChangedEvent, values));
        }
    }

    private void UpdateReviewCount() => ReviewCount = _views.Count(v => v.State == SmartFormFillFieldState.Review);

    internal void OnFieldTyped(SmartFormFillFieldView view)
    {
        var before = ReviewCount;
        UpdateReviewCount();
        if (view.State is SmartFormFillFieldState.Edited or SmartFormFillFieldState.Manual)
        {
            DropHighlight(view);
        }
        if (Phase is SmartFormFillPhase.Saved or SmartFormFillPhase.Failed)
        {
            Phase = CanUndo ? SmartFormFillPhase.Filled : SmartFormFillPhase.Idle;
        }
        if (before > 0 && ReviewCount == 0)
        {
            SetStatus(new SmartFormFillStatus(Labels.AllChecked, ArcIcons.CircleCheck, ArcDesignTokenKind.Success, false));
        }
        PublishValues(raise: true);
    }

    internal void OnFieldHover(SmartFormFillFieldView view, bool over)
    {
        if (over)
        {
            _hovered = view;
        }
        else if (ReferenceEquals(_hovered, view))
        {
            _hovered = null;
        }
        UpdateHighlights();
    }

    internal void OnFieldFocus(SmartFormFillFieldView view, bool focused)
    {
        if (focused)
        {
            _focused = view;
        }
        else if (ReferenceEquals(_focused, view))
        {
            _focused = null;
        }
        UpdateHighlights();
    }

    /// <summary>Marks a field that asks for review as checked. Returns false when the field does not ask for review.</summary>
    public bool Check(string id)
    {
        if (ViewOf(id) is not { State: SmartFormFillFieldState.Review } view)
        {
            return false;
        }
        view.MarkChecked();
        UpdateReviewCount();
        RepaintHighlights();
        var name = view.Field.Label ?? view.Field.Id;
        SetStatus(ReviewCount == 0
            ? new SmartFormFillStatus(Labels.AllChecked, ArcIcons.CircleCheck, ArcDesignTokenKind.Success, false)
            : new SmartFormFillStatus(Labels.CheckedField(name, ReviewCount), ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning, false));
        if (Phase is SmartFormFillPhase.Saved or SmartFormFillPhase.Failed)
        {
            Phase = SmartFormFillPhase.Filled;
        }
        return true;
    }

    // ---- Reading ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Reads <paramref name="text"/> (or the pasted <see cref="Text"/>) and fills the form. A read in progress is cancelled
    /// first. Returns true once the values are on their way into the fields; false when the text is empty, the read was
    /// stopped or replaced, or it failed.
    /// </summary>
    public async Task<bool> ReadAsync(string? text = null)
    {
        text ??= _editor?.Text ?? Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || Phase == SmartFormFillPhase.Saving)
        {
            return false;
        }
        // A read started before the card is shown still needs its fields and the pasted text view.
        ApplyTemplate();
        if (Phase == SmartFormFillPhase.Reading)
        {
            CancelRead();
        }
        FinishFill();
        _readText = text;
        if (Text != text)
        {
            SetCurrentValue(TextProperty, text);
        }
        ShowSource(true);
        ClearHighlights();

        var cts = new CancellationTokenSource();
        _readCts = cts;
        var version = ++_readVersion;
        _phaseBeforeRead = CanUndo ? SmartFormFillPhase.Filled : SmartFormFillPhase.Idle;
        _simulatedRun = Extract is null;
        Phase = SmartFormFillPhase.Reading;
        SetStatus(new SmartFormFillStatus(Labels.Reading(EffectiveModelName), ModelMark ?? ArcIcons.Sparkles, ArcDesignTokenKind.Accent, true));

        SmartFormFillResult result;
        try
        {
            result = Extract is { } extract ? await extract(text, cts.Token) : await SimulateRead(text, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            if (version != _readVersion)
            {
                return false;
            }
            _readCts = null;
            cts.Dispose();
            Phase = SmartFormFillPhase.Failed;
            SetStatus(new SmartFormFillStatus(Labels.ReadFailed, ArcIcons.CircleAlert, ArcDesignTokenKind.Danger, false));
            return false;
        }
        if (version != _readVersion || cts.IsCancellationRequested)
        {
            return false;
        }
        _readCts = null;
        cts.Dispose();
        StartFill(text, result ?? new SmartFormFillResult());
        return true;
    }

    private Task<SmartFormFillResult> SimulateRead(string text, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<SmartFormFillResult>();
        var fields = EffectiveFields.ToArray();
        var samples = EffectiveSamples;
        var shot = Timers.Start(SmartFormFillReader.ReadTime, () => tcs.TrySetResult(SmartFormFillReader.Read(text, fields, samples)));
        token.Register(() =>
        {
            shot.Dispose();
            tcs.TrySetCanceled(token);
        });
        return tcs.Task;
    }

    private void CancelRead()
    {
        _readVersion++;
        var cts = _readCts;
        _readCts = null;
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <summary>
    /// Stops a read in progress (nothing changes; the extractor's token is cancelled), or lands a fill in flight at once.
    /// Returns false when there was nothing to stop.
    /// </summary>
    public bool Stop()
    {
        if (Phase == SmartFormFillPhase.Reading)
        {
            CancelRead();
            Phase = _phaseBeforeRead;
            SetStatus(new SmartFormFillStatus(Labels.Stopped, ArcIcons.CircleMinus, ArcDesignTokenKind.TextMuted, false));
            if (_phaseBeforeRead == SmartFormFillPhase.Idle)
            {
                ShowSource(false);
            }
            return true;
        }
        if (Phase == SmartFormFillPhase.Filling)
        {
            FinishFill();
            return true;
        }
        return false;
    }

    // ---- Filling ------------------------------------------------------------------------------------------------

    private List<(SmartFormFillFieldView View, SmartFormFillValue Value)> _pending = [];

    private void StartFill(string text, SmartFormFillResult result)
    {
        _lastResult = result;
        var entries = new List<(SmartFormFillFieldView, SmartFormFillValue)>();
        foreach (var view in _views)
        {
            if (result.TryGetValue(view.Field.Id, out var value) && !string.IsNullOrEmpty(value.Value))
            {
                entries.Add((view, value));
            }
        }
        if (entries.Count == 0)
        {
            Phase = _phaseBeforeRead;
            SetStatus(new SmartFormFillStatus(Labels.NothingFound, ArcIcons.Info, ArcDesignTokenKind.TextMuted, false));
            RaiseEvent(new SmartFormFillFilledEventArgs(FilledEvent, result, 0, 0));
            OnAutoplayFilled();
            return;
        }
        _before = _views.ToDictionary(v => v.Field.Id, v => v.Text, StringComparer.Ordinal);
        foreach (var (view, _) in entries)
        {
            view.AwaitFill();
        }
        CanUndo = true;
        _filledCount = entries.Count;
        CreateHighlights(text, entries);
        _pending = entries;
        Phase = SmartFormFillPhase.Filling;
        SetStatus(new SmartFormFillStatus(Labels.Filling, ModelMark ?? ArcIcons.Sparkles, ArcDesignTokenKind.Accent, false));
        if (!ArcMotion.IsFull(this))
        {
            // Reduced motion and Off: every field at once, no tint; the end state is the same.
            FinishFill();
            return;
        }
        for (var i = 0; i < entries.Count; i++)
        {
            var index = i;
            _fillShots.Add(Timers.Start(FillStagger * i, () => FillAt(index)));
        }
    }

    private void FillAt(int index)
    {
        if (Phase != SmartFormFillPhase.Filling || index >= _pending.Count)
        {
            return;
        }
        var (view, value) = _pending[index];
        if (view.Read is null)
        {
            view.Fill(value, value.Confidence < ReviewThreshold, instant: false);
            _filling = view;
            UpdateReviewCount();
            UpdateHighlights();
            PublishValues(raise: true);
        }
        if (_pending.All(p => p.View.Read is not null))
        {
            CompleteFill();
        }
    }

    /// <summary>Lands every value still on its way at once (Stop during a fill, reduced motion, leaving the tree).</summary>
    private void FinishFill()
    {
        foreach (var shot in _fillShots)
        {
            shot.Dispose();
        }
        _fillShots.Clear();
        if (Phase != SmartFormFillPhase.Filling)
        {
            return;
        }
        var any = false;
        foreach (var (view, value) in _pending)
        {
            if (view.Read is null)
            {
                view.Fill(value, value.Confidence < ReviewThreshold, instant: true);
                any = true;
            }
        }
        UpdateReviewCount();
        if (any)
        {
            PublishValues(raise: true);
        }
        CompleteFill();
    }

    private void CompleteFill()
    {
        foreach (var shot in _fillShots)
        {
            shot.Dispose();
        }
        _fillShots.Clear();
        _pending = [];
        _filling = null;
        UpdateReviewCount();
        UpdateHighlights();
        Phase = SmartFormFillPhase.Filled;
        var review = ReviewCount;
        SetStatus(review > 0
            ? new SmartFormFillStatus(Labels.Filled(_filledCount, review), ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning, false)
            : new SmartFormFillStatus(Labels.Filled(_filledCount, 0), ArcIcons.CircleCheck, ArcDesignTokenKind.Success, false));
        RaiseEvent(new SmartFormFillFilledEventArgs(FilledEvent, _lastResult ?? new SmartFormFillResult(), _filledCount, review));
        OnAutoplayFilled();
    }

    /// <summary>Puts back the values from before the last fill. Returns false when there is nothing to undo.</summary>
    public bool Undo()
    {
        if (!CanUndo || _before is null || Phase == SmartFormFillPhase.Saving)
        {
            return false;
        }
        if (Phase == SmartFormFillPhase.Reading)
        {
            CancelRead();
        }
        FinishFill();
        var restored = _before;
        _before = null;
        foreach (var view in _views)
        {
            view.Reset(restored.TryGetValue(view.Field.Id, out var value) ? value : null);
        }
        ClearHighlights();
        CanUndo = false;
        Phase = SmartFormFillPhase.Idle;
        UpdateReviewCount();
        SetStatus(new SmartFormFillStatus(Labels.Undone, ArcIcons.Undo2, ArcDesignTokenKind.TextMuted, false));
        PublishValues(raise: true);
        RaiseEvent(new SmartFormFillValuesEventArgs(UndoneEvent, Values));
        return true;
    }

    /// <summary>Pastes the next sample (cycled in order) and reads it. Returns false when there are no samples.</summary>
    public Task<bool> TrySampleAsync()
    {
        var samples = EffectiveSamples;
        if (samples.Count == 0)
        {
            return Task.FromResult(false);
        }
        var sample = samples[_sampleIndex % samples.Count];
        _sampleIndex = (_sampleIndex + 1) % samples.Count;
        RefreshChrome();
        return ReadAsync(sample.Text);
    }

    // ---- Saving -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Saves the form through <see cref="Submit"/> (or a simulated save). While fields still ask for review it saves nothing,
    /// says how many and focuses the first. Returns true when the save went through.
    /// </summary>
    public async Task<bool> SubmitAsync()
    {
        if (Phase is SmartFormFillPhase.Saving or SmartFormFillPhase.Reading)
        {
            return false;
        }
        FinishFill();
        if (ReviewCount > 0)
        {
            SetStatus(new SmartFormFillStatus(Labels.SaveBlocked(ReviewCount), ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning, false));
            _views.FirstOrDefault(v => v.State == SmartFormFillFieldState.Review)?.FocusInput();
            return false;
        }
        var values = Values;
        Phase = SmartFormFillPhase.Saving;
        SetStatus(new SmartFormFillStatus(Labels.Saving, ArcIcons.Clock, ArcDesignTokenKind.TextMuted, false));
        try
        {
            await (Submit is { } submit ? submit(values) : SimulateSave());
        }
        catch (Exception)
        {
            Phase = SmartFormFillPhase.Failed;
            SetStatus(new SmartFormFillStatus(Labels.SaveFailed, ArcIcons.CircleAlert, ArcDesignTokenKind.Danger, false));
            return false;
        }
        Phase = SmartFormFillPhase.Saved;
        SetStatus(new SmartFormFillStatus(Labels.Saved, ArcIcons.CircleCheck, ArcDesignTokenKind.Success, false));
        RaiseEvent(new SmartFormFillValuesEventArgs(SubmittedEvent, values));
        return true;
    }

    private Task SimulateSave()
    {
        var tcs = new TaskCompletionSource();
        Timers.Start(SmartFormFillReader.SaveTime, () => tcs.TrySetResult());
        return tcs.Task;
    }

    // ---- Source highlights --------------------------------------------------------------------------------------

    private void CreateHighlights(string text, List<(SmartFormFillFieldView View, SmartFormFillValue Value)> entries)
    {
        ClearHighlights();
        if (_source is null)
        {
            return;
        }
        foreach (var (view, value) in entries)
        {
            if (string.IsNullOrEmpty(value.Source))
            {
                continue;
            }
            var start = text.IndexOf(value.Source, StringComparison.Ordinal);
            if (start < 0)
            {
                start = text.IndexOf(value.Source, StringComparison.OrdinalIgnoreCase);
            }
            if (start < 0)
            {
                continue;
            }
            var decoration = new TextRangeDecoration(start, value.Source.Length) { Opacity = 0, Tag = view.Field.Id };
            var opacity = new MotionValue<double>(_source, 0, MotionVectors.Double);
            opacity.Changed += v =>
            {
                decoration.Opacity = v;
                _source?.InvalidateDecorations();
            };
            _highlights[view] = (decoration, opacity);
            _source.Decorations.Add(decoration);
        }
        RepaintHighlights();
    }

    private void ClearHighlights()
    {
        foreach (var (_, opacity) in _highlights.Values)
        {
            opacity.Jump(0);
        }
        _highlights.Clear();
        _source?.Decorations.Clear();
    }

    private void DropHighlight(SmartFormFillFieldView view)
    {
        if (_highlights.Remove(view, out var entry))
        {
            entry.Opacity.Jump(0);
            _source?.Decorations.Remove(entry.Decoration);
        }
    }

    /// <summary>The brushes, corners and outset follow the state; a checked guess turns from the warning tint to the accent.</summary>
    private void RepaintHighlights()
    {
        foreach (var (decoration, _) in _highlights.Values)
        {
            decoration.CornerRadius = HighlightCornerRadius.TopLeft;
            decoration.Outset = HighlightOutset;
        }
        UpdateHighlights();
    }

    /// <summary>
    /// A filled field's source rests at a light tint; the active field's (hovered, focused or filling) takes the full tint
    /// over 160ms. Code-driven because the highlights are ranges drawn inside one text block, not controls a ControlTheme can
    /// reach. Off: at once.
    /// </summary>
    private void UpdateHighlights()
    {
        var active = ActiveField;
        foreach (var (view, (decoration, opacity)) in _highlights)
        {
            decoration.Background = view.State == SmartFormFillFieldState.Review ? ReviewHighlightBrush : HighlightBrush;
            var target = view.Read is null ? 0 : ReferenceEquals(view, active) ? 1 : HighlightRestOpacity;
            if (Math.Abs(opacity.Value - target) < 0.001 && !opacity.IsAnimating)
            {
                continue;
            }
            if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
            {
                opacity.Jump(target);
                decoration.Opacity = target;
            }
            else
            {
                _ = opacity.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
        }
        _source?.InvalidateDecorations();
    }

    // ---- Pasted text --------------------------------------------------------------------------------------------

    private void OnTextPropertyChanged()
    {
        var text = Text ?? string.Empty;
        if (_editor is not null && (_editor.Text ?? string.Empty) != text)
        {
            _syncingText = true;
            _editor.Text = text;
            _syncingText = false;
        }
        if (_source is not null)
        {
            _source.Text = text;
        }
        if (_showingSource && text != _readText && Phase != SmartFormFillPhase.Reading)
        {
            ClearHighlights();
            ShowSource(false);
        }
        RefreshChrome();
    }

    private void OnEditorPasting(object? sender, RoutedEventArgs e) => _pastePending = true;

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingText || _editor is null)
        {
            return;
        }
        var text = _editor.Text ?? string.Empty;
        if (Text != text)
        {
            SetCurrentValue(TextProperty, text);
        }
        if (_pastePending)
        {
            // Cmd/Ctrl+V: the read starts right away.
            _pastePending = false;
            Dispatcher.UIThread.Post(() => _ = ReadAsync(text), DispatcherPriority.Background);
        }
    }

    private void ShowSource(bool show)
    {
        _showingSource = show;
        PseudoClasses.Set(SourcePseudoClass, show);
        if (_source is not null && show)
        {
            _source.Text = Text;
        }
        RefreshChrome();
    }

    // ---- Chrome and status ------------------------------------------------------------------------------------

    private void RefreshChrome()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrEmpty(Description);
        }
        // With no model to name (your form without one, or no data) the paste area names none.
        var modelName = EffectiveModelName;
        if (_modelMark is not null)
        {
            _modelMark.Data = ModelMark ?? ArcIcons.Sparkles;
            _modelMark.IsVisible = modelName.Length > 0;
        }
        if (_modelName is not null)
        {
            _modelName.Text = labels.ReadBy(modelName);
            _modelName.IsVisible = modelName.Length > 0;
        }
        if (_wellHeading is not null)
        {
            _wellHeading.Text = labels.PasteHeading;
        }
        if (_editor is not null)
        {
            _editor.Label = labels.PastedText;
            _editor.Placeholder = labels.PastePlaceholder;
        }
        if (_sourceScroll is not null)
        {
            AutomationProperties.SetName(_sourceScroll, labels.PastedText);
        }
        var reading = Phase == SmartFormFillPhase.Reading;
        var samples = EffectiveSamples;
        if (_sample is not null)
        {
            _sample.IsVisible = samples.Count > 0;
            _sample.Content = labels.TrySample;
            if (samples.Count > 0)
            {
                AutomationProperties.SetName(_sample, labels.TrySampleNamed(samples[_sampleIndex % samples.Count].Label));
            }
            _sample.IsEnabled = Phase != SmartFormFillPhase.Saving;
        }
        if (_fill is not null)
        {
            _fill.Content = reading ? labels.Stop : labels.Fill;
            _fill.IsEnabled = reading || (!string.IsNullOrWhiteSpace(Text) && Phase != SmartFormFillPhase.Saving);
        }
        if (_edit is not null)
        {
            _edit.Content = labels.EditText;
            _edit.IsVisible = _showingSource && !reading;
        }
        if (_undo is not null)
        {
            _undo.Content = labels.Undo;
            _undo.IsEnabled = CanUndo && Phase != SmartFormFillPhase.Saving;
        }
        if (_submit is not null)
        {
            _submit.Content = SubmitLabel;
            _submit.IsEnabled = Phase is not (SmartFormFillPhase.Reading or SmartFormFillPhase.Filling);
            if (_submit is Button arc)
            {
                arc.IsLoading = Phase == SmartFormFillPhase.Saving;
            }
        }
    }

    private SmartFormFillStatus ReadyStatus() =>
        new(Labels.Ready, ArcIcons.ClipboardPaste, ArcDesignTokenKind.TextMuted, false);

    private void SetStatus(SmartFormFillStatus status)
    {
        _status = status;
        ShowStatus();
    }

    /// <summary>The footer's status swaps with one crossfade (out 100ms, in 160ms rising 2px); the reading words shimmer.</summary>
    private void ShowStatus()
    {
        if (_status is not { } status)
        {
            return;
        }
        Announcement = _simulatedRun && Phase is not (SmartFormFillPhase.Idle or SmartFormFillPhase.Saving or SmartFormFillPhase.Saved)
                       && status.Text != Labels.Ready
            ? status.Text + ". " + Labels.Simulated
            : status.Text;
        if (_statusSwap is null)
        {
            return;
        }
        AutomationProperties.SetName(_statusSwap, Announcement);
        var key = status.Text + "|" + status.Tone + "|" + status.Shimmer;
        if (_shownStatusKey == key)
        {
            return;
        }
        _shownStatusKey = key;
        _statusSwap.Content = CreateStatus(status);
    }

    private static Control CreateStatus(SmartFormFillStatus status)
    {
        var glyph = new ArcGlyph { Data = status.Glyph, Size = 15, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, status.Tone);
        Control text;
        if (status.Shimmer)
        {
            var shimmer = new ToolCallText { Text = status.Text, FontSize = 13, IsShimmering = true, Swap = ToolCallTextSwap.Fade, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(shimmer, ToolCallText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            ArcTokens.Bind(shimmer, ToolCallText.MutedForegroundProperty, ArcDesignTokenKind.TextMuted);
            text = shimmer;
        }
        else
        {
            var block = new TextBlock { Text = status.Text, FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            text = block;
        }
        var row = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(glyph, Dock.Left);
        glyph.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(glyph);
        row.Children.Add(text);
        return row;
    }

    // ---- Input ---------------------------------------------------------------------------------------------------

    private void OnSampleClick(object? sender, RoutedEventArgs e) => _ = TrySampleAsync();

    private void OnFillClick(object? sender, RoutedEventArgs e)
    {
        if (Phase == SmartFormFillPhase.Reading)
        {
            Stop();
        }
        else
        {
            _ = ReadAsync();
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        ShowSource(false);
        _editor?.Focus(NavigationMethod.Tab);
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Undo();

    private void OnSubmitClick(object? sender, RoutedEventArgs e) => _ = SubmitAsync();

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e) => StopAutoplayForGood();

    private void OnAnyKeyDown(object? sender, KeyEventArgs e)
    {
        StopAutoplayForGood();
        if (e.Key == Key.Escape && Phase is SmartFormFillPhase.Reading or SmartFormFillPhase.Filling)
        {
            Stop();
            e.Handled = true;
            return;
        }
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (command && e.Key == Key.Enter && _editor is not null && e.Source is Visual source
            && (ReferenceEquals(source, _editor) || source.GetVisualAncestors().Contains(_editor)))
        {
            // Cmd/Ctrl+Enter reads the typed text instead of adding a line.
            e.Handled = true;
            _ = ReadAsync(_editor.Text);
        }
    }
}
