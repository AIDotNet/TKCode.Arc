using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The form inside the feedback panel: a title with a close button, the reason chips in a labelled group, the optional
/// note, and a footer with the privacy note (or the error line) and the two actions. After a thumbs down it reads "What went
/// wrong?" with Cancel and Submit; after a thumbs up it is the lighter "What was good?" row with Skip and Send. The owner
/// (<see cref="FeedbackActions"/>) holds the state and listens to <see cref="PrimaryRequested"/>,
/// <see cref="SecondaryRequested"/> and <see cref="CloseRequested"/>.
/// </summary>
[TemplatePart(ClosePart, typeof(AvaloniaButton))]
[TemplatePart(ChipsPart, typeof(WrapPanel))]
[TemplatePart(NotePart, typeof(Textarea))]
[TemplatePart(FootnotePart, typeof(TextBlock))]
[TemplatePart(ErrorPart, typeof(Control))]
[TemplatePart(ErrorTextPart, typeof(TextBlock))]
[TemplatePart(SecondaryPart, typeof(Button))]
[TemplatePart(PrimaryPart, typeof(Button))]
[PseudoClasses(NarrowPseudoClass)]
internal sealed class FeedbackForm : TemplatedControl
{
    internal const string ClosePart = "PART_Close";
    internal const string ChipsPart = "PART_Chips";
    internal const string NotePart = "PART_Note";
    internal const string FootnotePart = "PART_Footnote";
    internal const string ErrorPart = "PART_Error";
    internal const string ErrorTextPart = "PART_ErrorText";
    internal const string SecondaryPart = "PART_Secondary";
    internal const string PrimaryPart = "PART_Primary";
    internal const string NarrowPseudoClass = ":narrow";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FeedbackForm, string?>(nameof(Title));

    public static readonly StyledProperty<IReadOnlyList<string>?> ReasonsProperty =
        AvaloniaProperty.Register<FeedbackForm, IReadOnlyList<string>?>(nameof(Reasons));

    public static readonly StyledProperty<bool> ShowsNoteProperty =
        AvaloniaProperty.Register<FeedbackForm, bool>(nameof(ShowsNote));

    public static readonly StyledProperty<string?> NotePlaceholderProperty =
        AvaloniaProperty.Register<FeedbackForm, string?>(nameof(NotePlaceholder));

    public static readonly StyledProperty<int> NoteMaxLengthProperty =
        AvaloniaProperty.Register<FeedbackForm, int>(nameof(NoteMaxLength), 1000);

    public static readonly StyledProperty<string?> FootnoteProperty =
        AvaloniaProperty.Register<FeedbackForm, string?>(nameof(Footnote));

    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<FeedbackForm, string?>(nameof(ErrorText));

    public static readonly StyledProperty<string?> PrimaryLabelProperty =
        AvaloniaProperty.Register<FeedbackForm, string?>(nameof(PrimaryLabel));

    public static readonly StyledProperty<string?> SecondaryLabelProperty =
        AvaloniaProperty.Register<FeedbackForm, string?>(nameof(SecondaryLabel));

    public static readonly StyledProperty<bool> IsSendingProperty =
        AvaloniaProperty.Register<FeedbackForm, bool>(nameof(IsSending));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<FeedbackForm, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<bool> IsCoarseProperty =
        AvaloniaProperty.Register<FeedbackForm, bool>(nameof(IsCoarse));

    /// <summary>Whether the secondary action gives way below the narrow width (Cancel does; Skip does not).</summary>
    public static readonly StyledProperty<bool> SecondaryCollapsesProperty =
        AvaloniaProperty.Register<FeedbackForm, bool>(nameof(SecondaryCollapses));

    public static readonly StyledProperty<ResponseFeedbackLabels> LabelsProperty =
        AvaloniaProperty.Register<FeedbackForm, ResponseFeedbackLabels>(nameof(Labels), ResponseFeedbackLabels.Default);

    private readonly List<FeedbackChip> _chips = [];
    private AvaloniaButton? _close;
    private WrapPanel? _chipsPanel;
    private Textarea? _note;
    private TextBlock? _footnote;
    private Control? _error;
    private TextBlock? _errorText;
    private Button? _secondary;
    private Button? _primary;
    private string _noteText = string.Empty;
    private bool _resetting;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public IReadOnlyList<string>? Reasons { get => GetValue(ReasonsProperty); set => SetValue(ReasonsProperty, value); }

    public bool ShowsNote { get => GetValue(ShowsNoteProperty); set => SetValue(ShowsNoteProperty, value); }

    public string? NotePlaceholder { get => GetValue(NotePlaceholderProperty); set => SetValue(NotePlaceholderProperty, value); }

    public int NoteMaxLength { get => GetValue(NoteMaxLengthProperty); set => SetValue(NoteMaxLengthProperty, value); }

    public string? Footnote { get => GetValue(FootnoteProperty); set => SetValue(FootnoteProperty, value); }

    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    public string? PrimaryLabel { get => GetValue(PrimaryLabelProperty); set => SetValue(PrimaryLabelProperty, value); }

    public string? SecondaryLabel { get => GetValue(SecondaryLabelProperty); set => SetValue(SecondaryLabelProperty, value); }

    public bool IsSending { get => GetValue(IsSendingProperty); set => SetValue(IsSendingProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public bool IsCoarse { get => GetValue(IsCoarseProperty); set => SetValue(IsCoarseProperty, value); }

    public bool SecondaryCollapses { get => GetValue(SecondaryCollapsesProperty); set => SetValue(SecondaryCollapsesProperty, value); }

    public ResponseFeedbackLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Submit, Send or Try again was pressed, or Cmd / Ctrl + Enter in the note.</summary>
    public event EventHandler? PrimaryRequested;

    /// <summary>Cancel or Skip was pressed.</summary>
    public event EventHandler? SecondaryRequested;

    /// <summary>The close button was pressed.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>The reasons the person selected, in the order they are offered.</summary>
    public IReadOnlyList<string> SelectedReasons =>
        _chips.Where(chip => chip.IsChecked == true).Select(chip => (string)chip.Content!).ToList();

    /// <summary>The note, trimmed, or null.</summary>
    public string? Note => ShowsNote && !string.IsNullOrWhiteSpace(_noteText) ? _noteText.Trim() : null;

    /// <summary>Whether there is something to send.</summary>
    public bool HasContent => _chips.Any(chip => chip.IsChecked == true) || Note is not null;

    /// <summary>Whether the primary action can be pressed now.</summary>
    internal bool CanSubmit => !IsSending && (HasContent || ErrorText is not null);

    internal IReadOnlyList<FeedbackChip> Chips => _chips;

    internal Textarea? NoteBox => _note;

    internal Button? Primary => _primary;

    internal Button? Secondary => _secondary;

    internal AvaloniaButton? CloseButton => _close;

    internal TextBlock? FootnoteBlock => _footnote;

    internal TextBlock? ErrorBlock => _errorText;

    /// <summary>Clears the chips, the note and the error.</summary>
    public void Reset()
    {
        _resetting = true;
        foreach (var chip in _chips)
        {
            chip.IsChecked = false;
        }
        _noteText = string.Empty;
        if (_note is not null)
        {
            _note.Text = string.Empty;
        }
        _resetting = false;
        SetCurrentValue(ErrorTextProperty, null);
        SetCurrentValue(IsSendingProperty, false);
        Refresh();
    }

    /// <summary>Moves keyboard focus to the first reason (or the note when there are none).</summary>
    public void FocusFirst()
    {
        if (_chips.Count > 0)
        {
            _chips[0].Focus(NavigationMethod.Tab);
        }
        else if (ShowsNote)
        {
            _note?.Focus(NavigationMethod.Tab);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_close is not null)
        {
            _close.Click -= OnClose;
        }
        if (_primary is not null)
        {
            _primary.Click -= OnPrimary;
        }
        if (_secondary is not null)
        {
            _secondary.Click -= OnSecondary;
        }
        if (_note is not null)
        {
            _note.RemoveHandler(KeyDownEvent, OnNoteKeyDown);
            _note.PropertyChanged -= OnNoteChanged;
        }
        _close = e.NameScope.Find<AvaloniaButton>(ClosePart);
        _chipsPanel = e.NameScope.Find<WrapPanel>(ChipsPart);
        _note = e.NameScope.Find<Textarea>(NotePart);
        _footnote = e.NameScope.Find<TextBlock>(FootnotePart);
        _error = e.NameScope.Find<Control>(ErrorPart);
        _errorText = e.NameScope.Find<TextBlock>(ErrorTextPart);
        _secondary = e.NameScope.Find<Button>(SecondaryPart);
        _primary = e.NameScope.Find<Button>(PrimaryPart);
        if (_close is not null)
        {
            _close.Click += OnClose;
        }
        if (_primary is not null)
        {
            _primary.Click += OnPrimary;
        }
        if (_secondary is not null)
        {
            _secondary.Click += OnSecondary;
        }
        if (_note is not null)
        {
            // Tunnel: the note accepts Return, so a plain bubbling handler would never see Cmd / Ctrl + Enter.
            _note.AddHandler(KeyDownEvent, OnNoteKeyDown, RoutingStrategies.Tunnel);
            _note.PropertyChanged += OnNoteChanged;
            _note.Text = _noteText;
        }
        var selected = SelectedReasons;
        BuildChips(selected);
        PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ReasonsProperty)
        {
            BuildChips([]);
            Refresh();
        }
        else if (change.Property == IsCoarseProperty)
        {
            foreach (var chip in _chips)
            {
                chip.SetCoarse(IsCoarse);
            }
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
            Refresh();
        }
        else if (change.Property == ErrorTextProperty || change.Property == IsSendingProperty
                 || change.Property == FootnoteProperty || change.Property == PrimaryLabelProperty
                 || change.Property == SecondaryLabelProperty || change.Property == SecondaryCollapsesProperty
                 || change.Property == NotePlaceholderProperty || change.Property == LabelsProperty
                 || change.Property == ShowsNoteProperty || change.Property == TitleProperty)
        {
            Refresh();
        }
    }

    private void BuildChips(IReadOnlyList<string> keep)
    {
        foreach (var chip in _chips)
        {
            chip.IsCheckedChanged -= OnChipChanged;
        }
        _chips.Clear();
        _chipsPanel?.Children.Clear();
        foreach (var reason in Reasons ?? [])
        {
            var chip = new FeedbackChip { Content = reason, IsChecked = keep.Contains(reason) };
            AutomationProperties.SetName(chip, reason);
            chip.SetCoarse(IsCoarse);
            chip.IsCheckedChanged += OnChipChanged;
            _chips.Add(chip);
            _chipsPanel?.Children.Add(chip);
        }
    }

    private bool IsOther(FeedbackChip chip) =>
        string.Equals(chip.Content as string, Labels.Other, StringComparison.OrdinalIgnoreCase);

    private void OnChipChanged(object? sender, RoutedEventArgs e)
    {
        if (_resetting)
        {
            return;
        }
        if (sender is FeedbackChip chip && chip.IsChecked == true && IsOther(chip) && ShowsNote)
        {
            _note?.Focus(chip.IsKeyboardFocusWithin ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
        Refresh();
    }

    private void OnNoteChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty && _note is not null && !_resetting)
        {
            _noteText = _note.Text ?? string.Empty;
            Refresh();
        }
    }

    private void OnNoteKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            e.Handled = true;
            if (CanSubmit)
            {
                PrimaryRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnPrimary(object? sender, RoutedEventArgs e)
    {
        if (CanSubmit)
        {
            PrimaryRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSecondary(object? sender, RoutedEventArgs e) => SecondaryRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Brings the parts in line with the state.</summary>
    private void Refresh()
    {
        var labels = Labels;
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, labels.Close);
            Tooltip.SetTip(_close, labels.Close);
        }
        if (_chipsPanel is not null)
        {
            AutomationProperties.SetName(_chipsPanel, Title);
        }
        if (_note is not null)
        {
            _note.IsVisible = ShowsNote;
            var other = _chips.Any(chip => chip.IsChecked == true && IsOther(chip));
            _note.Placeholder = other ? labels.OtherPlaceholder : NotePlaceholder;
            _note.MaxLength = Math.Max(0, NoteMaxLength);
            AutomationProperties.SetName(_note, labels.Note);
        }
        var error = ErrorText;
        if (_error is not null)
        {
            _error.IsVisible = error is not null;
        }
        if (_errorText is not null)
        {
            _errorText.Text = error;
        }
        if (_footnote is not null)
        {
            _footnote.Text = Footnote;
            _footnote.IsVisible = error is null && !IsNarrow && !string.IsNullOrEmpty(Footnote);
        }
        if (_secondary is not null)
        {
            _secondary.Content = SecondaryLabel;
            _secondary.IsVisible = !(IsNarrow && SecondaryCollapses);
        }
        if (_primary is not null)
        {
            _primary.Content = error is not null ? labels.TryAgain : PrimaryLabel;
            _primary.IsLoading = IsSending;
            _primary.IsEnabled = IsSending || HasContent || error is not null;
        }
    }
}

/// <summary>
/// The thank-you state of the feedback panel: a success tick that draws once in 280ms, "Thanks for your feedback" and a
/// second line. It takes keyboard focus after a keyboard submit, so the announcement and the focus land in the same place.
/// </summary>
[TemplatePart(TickPart, typeof(Primitives.StrokeDrawGlyph))]
internal sealed class FeedbackThanks : TemplatedControl
{
    internal const string TickPart = "PART_Tick";

    /// <summary>How long the tick takes to draw.</summary>
    internal static readonly TimeSpan TickDuration = TimeSpan.FromMilliseconds(280);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FeedbackThanks, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<FeedbackThanks, string?>(nameof(Message));

    private Primitives.StrokeDrawGlyph? _tick;
    private bool _playPending;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    internal double TickProgress => _tick?.Progress ?? 0;

    /// <summary>Draws the tick once (at once under reduced motion or off).</summary>
    public void PlayTick()
    {
        if (_tick is null)
        {
            _playPending = true;
            return;
        }
        if (ArcMotion.IsFull(this) && IsLoaded)
        {
            _tick.Progress = 0;
            _ = _tick.Play(TickDuration, ArcEasings.Standard);
        }
        else
        {
            _tick.Progress = 1;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _tick = e.NameScope.Find<Primitives.StrokeDrawGlyph>(TickPart);
        if (_playPending)
        {
            _playPending = false;
            if (_tick is not null)
            {
                _tick.Progress = 0;
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(PlayTick, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == MessageProperty)
        {
            AutomationProperties.SetName(this, Title + ". " + Message);
        }
    }
}
