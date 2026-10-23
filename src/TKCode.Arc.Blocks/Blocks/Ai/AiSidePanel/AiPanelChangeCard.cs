using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Carries the change a card asks to apply or discard.</summary>
internal sealed class AiPanelChangeRequestEventArgs(RoutedEvent routedEvent, object source, AiPanelChange change)
    : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The change.</summary>
    public AiPanelChange Change { get; } = change;
}

/// <summary>
/// The card of a proposed change (spec 3.6, 4.3): a 1px Border box, radius 14, on Surface. The head: a 14px PenLine in
/// TextMuted, 7px, the block's label (13 / 18.2, 500, Foreground) and at the right the summary (12 / 16.8, TextMuted:
/// "28 words (was 45)", "New column: Sync"). The body (padding 8 12 12) is the word diff of a heading or paragraph
/// (<see cref="AiDiffText"/>, card look) or the whole table redrawn small (<see cref="AiDiffTable"/>); a discarded card
/// shows it at 50% (240ms standard). The footer (43 high under a 1px BorderSubtle line, padding 0 7 0 12) has the state
/// at the left and the buttons at the right; both swap in the same fixed slot (out 100ms, in 160ms):
/// <list type="bullet">
/// <item>Proposed: Discard (ghost) and Apply (primary, on the accent), 4px apart.</item>
/// <item>Applied: "✓ Applied to page" (Check 14 in Success, Foreground text).</item>
/// <item>Undone: "↺ Undone" (TextMuted) and Apply.</item>
/// <item>Discarded: "× Discarded" (TextMuted).</item>
/// </list>
/// The card follows <see cref="Change"/> (its state and blocks); its buttons raise <see cref="ApplyRequestedEvent"/> and
/// <see cref="DiscardRequestedEvent"/>, the owner changes the state. Words come from <see cref="Labels"/>.
/// </summary>
[TemplatePart(LabelPart, typeof(TextBlock))]
[TemplatePart(SummaryPart, typeof(TextBlock))]
[TemplatePart(BodyPart, typeof(Border))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(ActionsPart, typeof(ArcContentSwap))]
[PseudoClasses(ProposedPseudoClass, AppliedPseudoClass, UndonePseudoClass, DiscardedPseudoClass)]
internal sealed class AiPanelChangeCard : TemplatedControl
{
    public const string LabelPart = "PART_Label";
    public const string SummaryPart = "PART_Summary";
    public const string BodyPart = "PART_Body";
    public const string StatusPart = "PART_Status";
    public const string ActionsPart = "PART_Actions";

    public const string ProposedPseudoClass = ":proposed";
    public const string AppliedPseudoClass = ":applied";
    public const string UndonePseudoClass = ":undone";
    public const string DiscardedPseudoClass = ":discarded";

    /// <summary>Raised (bubbling) when Apply is pressed.</summary>
    public static readonly RoutedEvent<AiPanelChangeRequestEventArgs> ApplyRequestedEvent =
        RoutedEvent.Register<AiPanelChangeCard, AiPanelChangeRequestEventArgs>("ApplyRequested", RoutingStrategies.Bubble);

    /// <summary>Raised (bubbling) when Discard is pressed.</summary>
    public static readonly RoutedEvent<AiPanelChangeRequestEventArgs> DiscardRequestedEvent =
        RoutedEvent.Register<AiPanelChangeCard, AiPanelChangeRequestEventArgs>("DiscardRequested", RoutingStrategies.Bubble);

    /// <summary>The height of the footer's buttons (set by the theme: 30).</summary>
    internal static readonly StyledProperty<double> ButtonHeightProperty =
        AvaloniaProperty.Register<AiPanelChangeCard, double>(nameof(ButtonHeight), 30);

    /// <summary>The padding of the footer's buttons (set by the theme: 0 12).</summary>
    internal static readonly StyledProperty<Thickness> ButtonPaddingProperty =
        AvaloniaProperty.Register<AiPanelChangeCard, Thickness>(nameof(ButtonPadding), new Thickness(12, 0));

    private readonly AiPanelChange _change;
    private AiSidePanelLabels _labels;
    private TextBlock? _label;
    private TextBlock? _summary;
    private Border? _body;
    private ArcContentSwap? _status;
    private ArcContentSwap? _actions;
    private AiDiffText? _diffText;
    private AiDiffTable? _diffTable;
    private Button? _apply;
    private Button? _discard;
    private AiPanelChangeState? _shownState;
    private bool _watching;

    static AiPanelChangeCard()
    {
        FocusableProperty.OverrideDefaultValue<AiPanelChangeCard>(false);
    }

    public AiPanelChangeCard(AiPanelChange change, AiSidePanelLabels labels)
    {
        _change = change;
        _labels = labels;
    }

    /// <summary>The change shown.</summary>
    public AiPanelChange Change => _change;

    /// <inheritdoc cref="ButtonHeightProperty"/>
    internal double ButtonHeight { get => GetValue(ButtonHeightProperty); set => SetValue(ButtonHeightProperty, value); }

    /// <inheritdoc cref="ButtonPaddingProperty"/>
    internal Thickness ButtonPadding { get => GetValue(ButtonPaddingProperty); set => SetValue(ButtonPaddingProperty, value); }

    /// <summary>The words.</summary>
    public AiSidePanelLabels Labels
    {
        get => _labels;
        set
        {
            _labels = value;
            _shownState = null;
            Refresh();
        }
    }

    /// <summary>The label of the changed block (its label; a heading without one is named by its text).</summary>
    public string BlockLabel => LabelOf(_change.Before ?? _change.After);

    /// <summary>The summary on the right of the head.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>The Apply button on show (Proposed and Undone), or null.</summary>
    internal Button? ApplyButton => _apply;

    /// <summary>The Discard button on show (Proposed), or null.</summary>
    internal Button? DiscardButton => _discard;

    /// <summary>The diff of a heading or paragraph, or null.</summary>
    internal AiDiffText? DiffText => _diffText;

    /// <summary>The diff of a table, or null.</summary>
    internal AiDiffTable? DiffTable => _diffTable;

    /// <summary>The label of <paramref name="block"/> as the panel names it: its label, a heading's text, or nothing.</summary>
    internal static string LabelOf(AiPanelBlock? block) => block switch
    {
        null => string.Empty,
        { Label: { Length: > 0 } label } => label,
        AiPanelHeading heading => heading.Text,
        _ => string.Empty
    };

    /// <summary>The number of words in <paramref name="text"/> (runs of non-whitespace).</summary>
    internal static int CountWords(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return count;
    }

    private static string? TextOf(AiPanelBlock? block) => block switch
    {
        AiPanelParagraph paragraph => paragraph.Text,
        AiPanelHeading heading => heading.Text,
        _ => null
    };

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<TextBlock>(LabelPart);
        _summary = e.NameScope.Find<TextBlock>(SummaryPart);
        _body = e.NameScope.Find<Border>(BodyPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _actions = e.NameScope.Find<ArcContentSwap>(ActionsPart);
        _diffText = null;
        _diffTable = null;
        _shownState = null;
        RefreshBody();
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_watching)
        {
            _watching = true;
            _change.PropertyChanged += OnChangePropertyChanged;
        }
        RefreshBody();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_watching)
        {
            _watching = false;
            _change.PropertyChanged -= OnChangePropertyChanged;
        }
    }

    private void OnChangePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiPanelChange.BeforeProperty || e.Property == AiPanelChange.AfterProperty)
        {
            RefreshBody();
            Refresh();
        }
        else if (e.Property == AiPanelChange.StateProperty)
        {
            Refresh();
        }
    }

    /// <summary>The diff: text for headings and paragraphs, the small table for tables.</summary>
    private void RefreshBody()
    {
        if (_body is null)
        {
            return;
        }
        if (_change.After is AiPanelTable afterTable)
        {
            _diffText = null;
            _diffTable ??= new AiDiffTable();
            _diffTable.Before = _change.Before as AiPanelTable;
            _diffTable.After = afterTable;
            _diffTable.AddsColumnFormat = _labels.AddsColumn;
            _diffTable.ChangesText = _labels.ChangesTable;
            _diffTable.Refresh();
            _body.Child = _diffTable;
        }
        else
        {
            _diffTable = null;
            _diffText ??= new AiDiffText();
            _diffText.Before = TextOf(_change.Before) ?? TextOf(_change.After) ?? string.Empty;
            _diffText.After = TextOf(_change.After) ?? string.Empty;
            _body.Child = _diffText;
        }
    }

    private void Refresh()
    {
        var label = BlockLabel;
        if (_label is not null)
        {
            _label.Text = label;
        }
        Summary = _diffTable is { } table
            ? table.Summary
            : _labels.WordsWas(CountWords(TextOf(_change.After)), CountWords(TextOf(_change.Before) ?? TextOf(_change.After)));
        if (_summary is not null)
        {
            _summary.Text = Summary;
        }
        if (_diffText is not null)
        {
            _diffText.SummaryName = Summary;
        }
        AutomationProperties.SetName(this, _labels.ChangeCard(label));
        var state = _change.State;
        PseudoClasses.Set(ProposedPseudoClass, state == AiPanelChangeState.Proposed);
        PseudoClasses.Set(AppliedPseudoClass, state == AiPanelChangeState.Applied);
        PseudoClasses.Set(UndonePseudoClass, state == AiPanelChangeState.Undone);
        PseudoClasses.Set(DiscardedPseudoClass, state == AiPanelChangeState.Discarded);
        if (_status is null || _actions is null || _shownState == state)
        {
            return;
        }
        _shownState = state;
        // New footer parts for every state: the old ones fade out in the slot while these fade in.
        DetachButtons();
        switch (state)
        {
            case AiPanelChangeState.Proposed:
                _status.Content = null;
                _discard = MakeButton(ButtonVariant.Ghost, _labels.Discard, OnDiscardClick);
                _apply = MakeButton(ButtonVariant.Primary, _labels.Apply, OnApplyClick);
                _actions.Content = Row(4, _discard, _apply);
                break;
            case AiPanelChangeState.Applied:
                _status.Content = StatusLine(ArcIcons.Check, 14, ArcDesignTokenKind.Success, _labels.AppliedToThePage, ArcDesignTokenKind.Foreground);
                _actions.Content = null;
                break;
            case AiPanelChangeState.Undone:
                _status.Content = StatusLine(ArcIcons.RotateCcw, 13, ArcDesignTokenKind.TextMuted, _labels.Undone, ArcDesignTokenKind.TextMuted);
                _apply = MakeButton(ButtonVariant.Primary, _labels.Apply, OnApplyClick);
                _actions.Content = Row(4, _apply);
                break;
            default:
                _status.Content = StatusLine(ArcIcons.X, 14, ArcDesignTokenKind.TextMuted, _labels.Discarded, ArcDesignTokenKind.TextMuted);
                _actions.Content = null;
                break;
        }
    }

    private void DetachButtons()
    {
        if (_apply is not null)
        {
            _apply.Click -= OnApplyClick;
            _apply = null;
        }
        if (_discard is not null)
        {
            _discard.Click -= OnDiscardClick;
            _discard = null;
        }
    }

    /// <summary>A small card button (Arc's sm: 30 high, padding 0 12, 13 / 500); made in code, so sized here.</summary>
    private Button MakeButton(ButtonVariant variant, string text, EventHandler<RoutedEventArgs> click)
    {
        var button = new Button
        {
            Variant = variant,
            Content = text,
            MinHeight = ButtonHeight,
            Height = ButtonHeight,
            Padding = ButtonPadding,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (variant == ButtonVariant.Primary)
        {
            // Arc's Apply is the accent (the send button's color), not the library's foreground-filled primary; local
            // values so the variant's own fill does not win (its hover and press still change the opacity).
            ArcTokens.Bind(button, BackgroundProperty, ArcDesignTokenKind.Accent, BindingPriority.LocalValue);
            ArcTokens.Bind(button, BorderBrushProperty, ArcDesignTokenKind.Accent, BindingPriority.LocalValue);
            ArcTokens.Bind(button, ForegroundProperty, ArcDesignTokenKind.AccentForeground, BindingPriority.LocalValue);
        }
        AutomationProperties.SetName(button, text);
        button.Click += click;
        return button;
    }

    private static StackPanel Row(double spacing, params Control[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };
        row.Children.AddRange(children);
        return row;
    }

    /// <summary>The footer's state: a glyph and 12 / 16.8 text, 6px apart, in the given token colors.</summary>
    private static Control StatusLine(Geometry glyph, double size, ArcDesignTokenKind glyphColor, string text, ArcDesignTokenKind textColor)
    {
        var icon = new ArcGlyph { Data = glyph, Size = size, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, glyphColor);
        var label = new TextBlock { Text = text, LineHeight = 16.8, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, textColor);
        ArcTokens.Bind(label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        var row = Row(6, icon, label);
        AutomationProperties.SetName(row, text);
        return row;
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new AiPanelChangeRequestEventArgs(ApplyRequestedEvent, this, _change));
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new AiPanelChangeRequestEventArgs(DiscardRequestedEvent, this, _change));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);
}
