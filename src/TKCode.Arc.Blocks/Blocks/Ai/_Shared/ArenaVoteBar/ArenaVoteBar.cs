using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The model arena's vote strip (spec 3.7): a 1px top rule and one row whose content follows <see cref="Kind"/>. Waiting:
/// a note and the count. Vote: the question and four secondary buttons (A with a left arrow first, Tie with an equals sign,
/// B with a right arrow last, Both bad with a thumbs-down). Voted: a green check and the result (the winner's mark before
/// its name in 500) and a primary Next round with a right arrow. Stopped: a note and Run again. Failed: a note and a
/// secondary Next round. Every word comes from the owner.
/// </summary>
/// <remarks>
/// <para>
/// A change of kind crossfades the rows (the old one leaves layout and fades out in 100ms, the new one fades in over 160ms,
/// no movement) while the strip's height follows the new row on the smooth spring, once the new row is measured. Reduced
/// motion keeps a 120ms crossfade and jumps the height; Off jumps both. Words change in place. The delay between the last
/// answer and the vote row is the owner's.
/// </para>
/// <para>
/// <see cref="IsNarrow"/> (an arena below 560) puts the question on its own line above the buttons; <see cref="IsCompact"/>
/// (below 480) hides the question, lays the buttons out as a stretched 2 × 2 grid and stacks the result above Next round.
/// Rows also wrap whenever they do not fit. When the focus is inside a row that is replaced, it moves to the new row's main
/// button, if it has one (with a ring only when the focus came from the keyboard).
/// </para>
/// </remarks>
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[TemplatePart(SwapPart, typeof(ArcContentSwap))]
[PseudoClasses(HiddenPseudoClass, CompactPseudoClass, NarrowPseudoClass)]
internal sealed class ArenaVoteBar : TemplatedControl
{
    public const string SizePart = "PART_Size";
    public const string SwapPart = "PART_Swap";
    public const string HiddenPseudoClass = ":hidden";
    public const string CompactPseudoClass = ":compact";
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The height of a row's line (36: the small buttons).</summary>
    private const double RowHeight = 36;

    /// <summary>The space between a waiting note and its count (Arc: margin-left 8); an en space, so the count can wrap.</summary>
    private const string CountGap = " ";

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    #region Properties

    /// <summary>What the strip shows.</summary>
    public static readonly StyledProperty<ArenaVoteKind> KindProperty =
        AvaloniaProperty.Register<ArenaVoteBar, ArenaVoteKind>(nameof(Kind));

    /// <summary>The waiting note.</summary>
    public static readonly StyledProperty<string?> PendingTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(PendingText));

    /// <summary>The waiting count ("1 of 2 finished"), after the note in TextMuted.</summary>
    public static readonly StyledProperty<string?> PendingCountTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(PendingCountText));

    /// <summary>The vote question (hidden in the compact strip).</summary>
    public static readonly StyledProperty<string?> QuestionTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(QuestionText));

    /// <summary>The "A is better" button.</summary>
    public static readonly StyledProperty<string?> VoteATextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(VoteAText));

    /// <summary>The Tie button.</summary>
    public static readonly StyledProperty<string?> TieTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(TieText));

    /// <summary>The "B is better" button.</summary>
    public static readonly StyledProperty<string?> VoteBTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(VoteBText));

    /// <summary>The "Both bad" button.</summary>
    public static readonly StyledProperty<string?> BothBadTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(BothBadText));

    /// <summary>The accessible name of the group of vote buttons; null uses <see cref="QuestionText"/>.</summary>
    public static readonly StyledProperty<string?> GroupNameProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(GroupName));

    /// <summary>The result when there is no winner to name ("Called a tie"); shown while <see cref="ResultName"/> is null.</summary>
    public static readonly StyledProperty<string?> ResultTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(ResultText));

    /// <summary>The words before the winner ("You chose").</summary>
    public static readonly StyledProperty<string?> ResultPrefixTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(ResultPrefixText));

    /// <summary>The winner's name, in 500; null shows <see cref="ResultText"/> instead.</summary>
    public static readonly StyledProperty<string?> ResultNameProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(ResultName));

    /// <summary>The winner's mark (<see cref="AiModel.Mark"/>), drawn at 16 before the name; null draws none.</summary>
    public static readonly StyledProperty<object?> ResultMarkProperty =
        AvaloniaProperty.Register<ArenaVoteBar, object?>(nameof(ResultMark));

    /// <summary>The fill of a geometry mark (<see cref="AiModel.MarkColor"/>); null uses the text color.</summary>
    public static readonly StyledProperty<IBrush?> ResultMarkColorProperty =
        AvaloniaProperty.Register<ArenaVoteBar, IBrush?>(nameof(ResultMarkColor));

    /// <summary>The Next round button (primary after a vote, secondary after a failure).</summary>
    public static readonly StyledProperty<string?> NextRoundTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(NextRoundText));

    /// <summary>The note after a stop.</summary>
    public static readonly StyledProperty<string?> StoppedTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(StoppedText));

    /// <summary>The Run again button.</summary>
    public static readonly StyledProperty<string?> RunAgainTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(RunAgainText));

    /// <summary>The note after a failure.</summary>
    public static readonly StyledProperty<string?> FailedTextProperty =
        AvaloniaProperty.Register<ArenaVoteBar, string?>(nameof(FailedText));

    /// <summary>The compact strip (an arena below 480).</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ArenaVoteBar, bool>(nameof(IsCompact));

    /// <summary>The narrow strip (an arena below 560): the question takes its own line.</summary>
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ArenaVoteBar, bool>(nameof(IsNarrow));

    #endregion

    #region Events

    /// <summary>Raised (bubbling) when a vote button is pressed.</summary>
    public static readonly RoutedEvent<ArenaVoteRequestedEventArgs> VoteRequestedEvent =
        RoutedEvent.Register<ArenaVoteBar, ArenaVoteRequestedEventArgs>(nameof(VoteRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when Next round is pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> NextRoundRequestedEvent =
        RoutedEvent.Register<ArenaVoteBar, RoutedEventArgs>(nameof(NextRoundRequested), RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when Run again is pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RunAgainRequestedEvent =
        RoutedEvent.Register<ArenaVoteBar, RoutedEventArgs>(nameof(RunAgainRequested), RoutingStrategies.Bubble);

    #endregion

    private SpringSizeDecorator? _size;
    private ArcContentSwap? _swap;
    private VoteRow? _row;

    static ArenaVoteBar()
    {
        FocusableProperty.OverrideDefaultValue<ArenaVoteBar>(false);
    }

    public ArenaVoteBar()
    {
        PseudoClasses.Set(HiddenPseudoClass, true);
    }

    /// <inheritdoc cref="KindProperty"/>
    public ArenaVoteKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="PendingTextProperty"/>
    public string? PendingText { get => GetValue(PendingTextProperty); set => SetValue(PendingTextProperty, value); }

    /// <inheritdoc cref="PendingCountTextProperty"/>
    public string? PendingCountText { get => GetValue(PendingCountTextProperty); set => SetValue(PendingCountTextProperty, value); }

    /// <inheritdoc cref="QuestionTextProperty"/>
    public string? QuestionText { get => GetValue(QuestionTextProperty); set => SetValue(QuestionTextProperty, value); }

    /// <inheritdoc cref="VoteATextProperty"/>
    public string? VoteAText { get => GetValue(VoteATextProperty); set => SetValue(VoteATextProperty, value); }

    /// <inheritdoc cref="TieTextProperty"/>
    public string? TieText { get => GetValue(TieTextProperty); set => SetValue(TieTextProperty, value); }

    /// <inheritdoc cref="VoteBTextProperty"/>
    public string? VoteBText { get => GetValue(VoteBTextProperty); set => SetValue(VoteBTextProperty, value); }

    /// <inheritdoc cref="BothBadTextProperty"/>
    public string? BothBadText { get => GetValue(BothBadTextProperty); set => SetValue(BothBadTextProperty, value); }

    /// <inheritdoc cref="GroupNameProperty"/>
    public string? GroupName { get => GetValue(GroupNameProperty); set => SetValue(GroupNameProperty, value); }

    /// <inheritdoc cref="ResultTextProperty"/>
    public string? ResultText { get => GetValue(ResultTextProperty); set => SetValue(ResultTextProperty, value); }

    /// <inheritdoc cref="ResultPrefixTextProperty"/>
    public string? ResultPrefixText { get => GetValue(ResultPrefixTextProperty); set => SetValue(ResultPrefixTextProperty, value); }

    /// <inheritdoc cref="ResultNameProperty"/>
    public string? ResultName { get => GetValue(ResultNameProperty); set => SetValue(ResultNameProperty, value); }

    /// <inheritdoc cref="ResultMarkProperty"/>
    public object? ResultMark { get => GetValue(ResultMarkProperty); set => SetValue(ResultMarkProperty, value); }

    /// <inheritdoc cref="ResultMarkColorProperty"/>
    public IBrush? ResultMarkColor { get => GetValue(ResultMarkColorProperty); set => SetValue(ResultMarkColorProperty, value); }

    /// <inheritdoc cref="NextRoundTextProperty"/>
    public string? NextRoundText { get => GetValue(NextRoundTextProperty); set => SetValue(NextRoundTextProperty, value); }

    /// <inheritdoc cref="StoppedTextProperty"/>
    public string? StoppedText { get => GetValue(StoppedTextProperty); set => SetValue(StoppedTextProperty, value); }

    /// <inheritdoc cref="RunAgainTextProperty"/>
    public string? RunAgainText { get => GetValue(RunAgainTextProperty); set => SetValue(RunAgainTextProperty, value); }

    /// <inheritdoc cref="FailedTextProperty"/>
    public string? FailedText { get => GetValue(FailedTextProperty); set => SetValue(FailedTextProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    /// <inheritdoc cref="VoteRequestedEvent"/>
    public event EventHandler<ArenaVoteRequestedEventArgs>? VoteRequested
    {
        add => AddHandler(VoteRequestedEvent, value);
        remove => RemoveHandler(VoteRequestedEvent, value);
    }

    /// <inheritdoc cref="NextRoundRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? NextRoundRequested
    {
        add => AddHandler(NextRoundRequestedEvent, value);
        remove => RemoveHandler(NextRoundRequestedEvent, value);
    }

    /// <inheritdoc cref="RunAgainRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RunAgainRequested
    {
        add => AddHandler(RunAgainRequestedEvent, value);
        remove => RemoveHandler(RunAgainRequestedEvent, value);
    }

    /// <summary>The height spring (template part).</summary>
    internal SpringSizeDecorator? SizeDecorator => _size;

    /// <summary>The row swap (template part).</summary>
    internal ArcContentSwap? RowSwap => _swap;

    /// <summary>The row on show (null while hidden).</summary>
    internal ArenaVoteRowPanel? Row => _row?.Panel;

    /// <summary>The vote buttons of the row on show, in order A, Tie, B, Both bad (empty unless voting).</summary>
    internal IReadOnlyList<Button> VoteButtons => _row?.Votes ?? [];

    /// <summary>The vote buttons' panel of the row on show (null unless voting).</summary>
    internal ArenaVoteChoicesPanel? Choices => _row?.Choices;

    /// <summary>The row's main button: Next round (voted, failed) or Run again (stopped); null otherwise.</summary>
    internal Button? ActionButton => _row?.Action;

    /// <summary>The row's note: the waiting note, the question, the stop or failure note, or the plain result.</summary>
    internal TextBlock? Note => _row?.Note;

    /// <summary>The voted row's parts: the prefix, the mark and the name (null otherwise).</summary>
    internal (TextBlock Prefix, ModelMark Mark, TextBlock Name)? NamedResult =>
        _row is { Prefix: { } prefix, Mark: { } mark, Name: { } name } ? (prefix, mark, name) : null;

    /// <summary>Puts the keyboard focus on the first vote button ("A is better").</summary>
    /// <param name="method">How the focus arrives; <see cref="NavigationMethod.Tab"/> shows the ring.</param>
    /// <returns>Whether it has the focus (false unless the strip is voting).</returns>
    public bool FocusFirstVote(NavigationMethod method = NavigationMethod.Tab) =>
        FocusPart(_row is { Kind: ArenaVoteKind.Vote, Votes.Count: > 0 } row ? row.Votes[0] : null, method);

    /// <summary>Puts the keyboard focus on Next round.</summary>
    /// <param name="method">How the focus arrives; <see cref="NavigationMethod.Tab"/> shows the ring.</param>
    /// <returns>Whether it has the focus (false unless the strip shows a result or a failure).</returns>
    public bool FocusNextRound(NavigationMethod method = NavigationMethod.Tab) =>
        FocusPart(_row is { Kind: ArenaVoteKind.Voted or ArenaVoteKind.Failed } row ? row.Action : null, method);

    private bool FocusPart(Control? part, NavigationMethod method)
    {
        if (part is null)
        {
            return false;
        }
        if (!part.IsAttachedToVisualTree())
        {
            // A row that was just put in is built by the swap's presenter at the next layout pass.
            UpdateLayout();
        }
        return part.Focus(method);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _size = e.NameScope.Find<SpringSizeDecorator>(SizePart);
        _swap = e.NameScope.Find<ArcContentSwap>(SwapPart);
        _row = BuildRow(Kind);
        if (_swap is not null)
        {
            // The first content never animates; Hidden is an empty panel so that the first row fades in.
            _swap.Content = (Control?)_row?.Panel ?? new Panel();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            PseudoClasses.Set(HiddenPseudoClass, change.GetNewValue<ArenaVoteKind>() == ArenaVoteKind.Hidden);
            ShowKind(change.GetNewValue<ArenaVoteKind>());
        }
        else if (change.Property == IsCompactProperty || change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
            if (_row is not null)
            {
                ApplyLayout(_row);
            }
        }
        else if (change.Property == PaddingProperty)
        {
            if (_row is not null)
            {
                _row.Panel.Margin = Padding;
            }
        }
        else if (change.Property.OwnerType == typeof(ArenaVoteBar) && _row is not null)
        {
            // A word, the mark or its color: change it in place.
            ApplyTexts(_row);
        }
    }

    private void ShowKind(ArenaVoteKind kind)
    {
        if (_swap is null)
        {
            return;
        }
        var outgoing = _row;
        var hadFocus = outgoing?.Panel.IsKeyboardFocusWithin == true;
        // The focus keeps its kind: a ring only if the focus inside the old row came from the keyboard.
        var method = hadFocus && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is StyledElement focused &&
                     focused.Classes.Contains(":focus-visible")
            ? NavigationMethod.Tab
            : NavigationMethod.Pointer;
        if (outgoing is not null)
        {
            // Out of reach while it fades away.
            outgoing.Panel.IsHitTestVisible = false;
            KeyboardNavigation.SetTabNavigation(outgoing.Panel, KeyboardNavigationMode.None);
        }
        _row = BuildRow(kind);
        _swap.Content = (Control?)_row?.Panel ?? new Panel();
        if (hadFocus && _row?.Main is { } main)
        {
            FocusPart(main, method);
        }
    }

    #region Rows

    /// <summary>The parts of the row on show, kept to change words and layout in place.</summary>
    private sealed class VoteRow(ArenaVoteKind kind, ArenaVoteRowPanel panel)
    {
        public ArenaVoteKind Kind { get; } = kind;

        public ArenaVoteRowPanel Panel { get; } = panel;

        public TextBlock? Note { get; set; }

        public Run? NoteRun { get; set; }

        public Run? CountRun { get; set; }

        public ArenaVoteChoicesPanel? Choices { get; set; }

        public List<Button> Votes { get; } = new(4);

        public List<TextBlock> VoteLabels { get; } = new(4);

        public Button? Action { get; set; }

        public TextBlock? ActionLabel { get; set; }

        public Control? Named { get; set; }

        public TextBlock? Prefix { get; set; }

        public ModelMark? Mark { get; set; }

        public TextBlock? Name { get; set; }

        /// <summary>Where the focus goes when it was inside the row this one replaces.</summary>
        public Control? Main => Votes.Count > 0 ? Votes[0] : Action;
    }

    private VoteRow? BuildRow(ArenaVoteKind kind)
    {
        if (kind == ArenaVoteKind.Hidden)
        {
            return null;
        }
        var row = new VoteRow(kind, new ArenaVoteRowPanel { Margin = Padding, MinHeight = RowHeight });
        switch (kind)
        {
            case ArenaVoteKind.Pending:
            {
                row.NoteRun = new Run();
                row.CountRun = new Run();
                ArcTokens.Bind(row.CountRun, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                var note = Text(ArcDesignTokenKind.TextSecondary);
                note.FontFeatures = s_tabular;
                note.Inlines = new InlineCollection { row.NoteRun, new Run(CountGap), row.CountRun };
                row.Note = note;
                row.Panel.Children.Add(note);
                break;
            }
            case ArenaVoteKind.Vote:
            {
                row.Note = Text(ArcDesignTokenKind.TextSecondary);
                row.Choices = new ArenaVoteChoicesPanel();
                AutomationProperties.SetControlTypeOverride(row.Choices, AutomationControlType.Group);
                AddVote(row, ArenaChoice.A, ArcIcons.ArrowLeft, null);
                AddVote(row, ArenaChoice.Tie, ArcIcons.Equal, null);
                AddVote(row, ArenaChoice.B, null, ArcIcons.ArrowRight);
                AddVote(row, ArenaChoice.BothBad, ArcIcons.ThumbsDown, null);
                row.Panel.Children.Add(row.Note);
                row.Panel.Children.Add(row.Choices);
                break;
            }
            case ArenaVoteKind.Voted:
            {
                var check = new ArcGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(check, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
                DockPanel.SetDock(check, Dock.Left);
                row.Note = Text(ArcDesignTokenKind.Foreground);
                row.Prefix = Text(ArcDesignTokenKind.Foreground);
                row.Prefix.TextWrapping = TextWrapping.NoWrap;
                row.Mark = new ModelMark { Size = 16, Margin = new Thickness(4, 0, 6, 0) };
                row.Name = Text(ArcDesignTokenKind.Foreground);
                row.Name.FontWeight = FontWeight.Medium;
                row.Name.TextWrapping = TextWrapping.NoWrap;
                row.Name.TextTrimming = TextTrimming.CharacterEllipsis;
                DockPanel.SetDock(row.Prefix, Dock.Left);
                DockPanel.SetDock(row.Mark, Dock.Left);
                row.Named = new DockPanel { Children = { row.Prefix, row.Mark, row.Name } };
                var lead = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
                lead.Children.Add(check);
                lead.Children.Add(new Panel { Margin = new Thickness(8, 0, 0, 0), Children = { row.Note, row.Named } });
                row.Action = MakeButton(ButtonVariant.Primary, null, ArcIcons.ArrowRight, out var label, OnNextRoundClick);
                row.ActionLabel = label;
                row.Action.HorizontalAlignment = HorizontalAlignment.Left;
                row.Panel.Children.Add(lead);
                row.Panel.Children.Add(row.Action);
                break;
            }
            case ArenaVoteKind.Stopped:
            {
                row.Note = Text(ArcDesignTokenKind.TextSecondary);
                row.Action = MakeButton(ButtonVariant.Secondary, ArcIcons.RotateCcw, null, out var label, OnRunAgainClick);
                row.ActionLabel = label;
                row.Action.HorizontalAlignment = HorizontalAlignment.Left;
                row.Panel.Children.Add(row.Note);
                row.Panel.Children.Add(row.Action);
                break;
            }
            case ArenaVoteKind.Failed:
            {
                row.Note = Text(ArcDesignTokenKind.TextSecondary);
                row.Action = MakeButton(ButtonVariant.Secondary, null, ArcIcons.ArrowRight, out var label, OnNextRoundClick);
                row.ActionLabel = label;
                row.Action.HorizontalAlignment = HorizontalAlignment.Left;
                row.Panel.Children.Add(row.Note);
                row.Panel.Children.Add(row.Action);
                break;
            }
        }
        ApplyTexts(row);
        ApplyLayout(row);
        return row;
    }

    private void AddVote(VoteRow row, ArenaChoice choice, Geometry? leading, Geometry? trailing)
    {
        var button = MakeButton(ButtonVariant.Secondary, leading, trailing, out var label, OnVoteClick);
        button.Tag = choice;
        row.Votes.Add(button);
        row.VoteLabels.Add(label);
        row.Choices!.Children.Add(button);
    }

    /// <summary>The words of a row from the current properties.</summary>
    private void ApplyTexts(VoteRow row)
    {
        switch (row.Kind)
        {
            case ArenaVoteKind.Pending:
                row.NoteRun!.Text = PendingText ?? string.Empty;
                row.CountRun!.Text = PendingCountText ?? string.Empty;
                break;
            case ArenaVoteKind.Vote:
                row.Note!.Text = QuestionText ?? string.Empty;
                AutomationProperties.SetName(row.Choices!, GroupName ?? QuestionText ?? string.Empty);
                string?[] words = [VoteAText, TieText, VoteBText, BothBadText];
                for (var i = 0; i < row.Votes.Count; i++)
                {
                    SetLabel(row.Votes[i], row.VoteLabels[i], words[i]);
                }
                break;
            case ArenaVoteKind.Voted:
                var named = ResultName is not null;
                row.Note!.Text = ResultText ?? string.Empty;
                row.Note.IsVisible = !named;
                row.Named!.IsVisible = named;
                row.Prefix!.Text = ResultPrefixText ?? string.Empty;
                row.Name!.Text = ResultName ?? string.Empty;
                row.Mark!.Mark = ResultMark;
                row.Mark.MarkColor = ResultMarkColor;
                row.Mark.IsVisible = ResultMark is not null;
                SetLabel(row.Action!, row.ActionLabel!, NextRoundText);
                break;
            case ArenaVoteKind.Stopped:
                row.Note!.Text = StoppedText ?? string.Empty;
                SetLabel(row.Action!, row.ActionLabel!, RunAgainText);
                break;
            case ArenaVoteKind.Failed:
                row.Note!.Text = FailedText ?? string.Empty;
                SetLabel(row.Action!, row.ActionLabel!, NextRoundText);
                break;
        }
    }

    /// <summary>The narrow and compact arrangements of a row.</summary>
    private void ApplyLayout(VoteRow row)
    {
        switch (row.Kind)
        {
            case ArenaVoteKind.Vote:
                row.Note!.IsVisible = !IsCompact;
                row.Choices!.IsGrid = IsCompact;
                row.Choices.HorizontalAlignment = IsCompact ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
                row.Panel.ForceWrap = IsNarrow;
                break;
            case ArenaVoteKind.Voted:
                row.Panel.ForceWrap = IsCompact;
                break;
        }
    }

    private static void SetLabel(Button button, TextBlock label, string? text)
    {
        label.Text = text ?? string.Empty;
        AutomationProperties.SetName(button, text ?? string.Empty);
    }

    /// <summary>Body text, 14 / 19.6, wrapping, in the given color.</summary>
    private static TextBlock Text(ArcDesignTokenKind color)
    {
        var text = new TextBlock
        {
            LineHeight = 19.6,
            LetterSpacing = -0.14,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, color);
        return text;
    }

    /// <summary>
    /// A small button (36 high, padding 0 12, 14 / 500) with a 14px glyph before or after its label, 8px apart. Primary is
    /// Arc's accent here, not the library's foreground fill: local values so the variant's own fill does not win (its hover
    /// and press still change the opacity and the shadow).
    /// </summary>
    private Button MakeButton(ButtonVariant variant, Geometry? leading, Geometry? trailing, out TextBlock label,
        EventHandler<RoutedEventArgs> click)
    {
        label = new TextBlock { LineHeight = 19.6, LetterSpacing = -0.14, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (leading is not null)
        {
            content.Children.Add(Glyph(leading));
        }
        content.Children.Add(label);
        if (trailing is not null)
        {
            content.Children.Add(Glyph(trailing));
        }
        var button = new Button
        {
            Variant = variant,
            Size = ControlSize.Small,
            Content = content,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (variant == ButtonVariant.Primary)
        {
            ArcTokens.Bind(button, BackgroundProperty, ArcDesignTokenKind.Accent, BindingPriority.LocalValue);
            ArcTokens.Bind(button, BorderBrushProperty, ArcDesignTokenKind.Accent, BindingPriority.LocalValue);
            ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.AccentForeground, BindingPriority.LocalValue);
        }
        button.Click += click;
        return button;
    }

    private static ArcGlyph Glyph(Geometry data) =>
        new() { Data = data, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };

    #endregion

    private void OnVoteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ArenaChoice choice })
        {
            e.Handled = true;
            RaiseEvent(new ArenaVoteRequestedEventArgs(VoteRequestedEvent, this, choice));
        }
    }

    private void OnNextRoundClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(NextRoundRequestedEvent, this));
    }

    private void OnRunAgainClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(RunAgainRequestedEvent, this));
    }
}
