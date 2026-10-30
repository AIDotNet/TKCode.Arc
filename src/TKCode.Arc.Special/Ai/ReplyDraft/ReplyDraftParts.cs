using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaToggleButton = Avalonia.Controls.Primitives.ToggleButton;

namespace TKCode.Arc;

/// <summary>A quick reply chip: a toggle button (pressed while it steers the draft) the card checks itself.</summary>
[PseudoClasses(CoarsePseudoClass)]
internal sealed class ReplyDraftChip : AvaloniaToggleButton
{
    /// <summary>Set on touch: the chip grows to 36px.</summary>
    internal const string CoarsePseudoClass = ":coarse";

    /// <summary>The suggestion the chip stands for.</summary>
    internal ReplyDraftSuggestion? Suggestion { get; init; }

    internal void SetCoarse(bool coarse) => PseudoClasses.Set(CoarsePseudoClass, coarse);

    protected override Type StyleKeyOverride => typeof(ReplyDraftChip);

    protected override void Toggle()
    {
        // The card owns the pressed state: choosing the pressed chip again writes a fresh draft, it does not unpress.
    }
}

/// <summary>
/// The finished draft: a borderless plain-text editor with the draft's type, so the reader can fix a word before Insert.
/// It derives from the Arc field base for caret, selection, undo and IME. While a rewrite runs it dims and is read-only.
/// </summary>
[PseudoClasses(DimmedPseudoClass)]
internal sealed class ReplyDraftEditor : FieldTextBox
{
    /// <summary>Set while a rewrite runs.</summary>
    internal const string DimmedPseudoClass = ":dimmed";

    /// <summary>Dims the text while a rewrite runs.</summary>
    public static readonly StyledProperty<bool> IsDimmedProperty =
        AvaloniaProperty.Register<ReplyDraftEditor, bool>(nameof(IsDimmed));

    /// <inheritdoc cref="IsDimmedProperty"/>
    public bool IsDimmed { get => GetValue(IsDimmedProperty); set => SetValue(IsDimmedProperty, value); }

    protected override Type StyleKeyOverride => typeof(ReplyDraftEditor);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsDimmedProperty)
        {
            PseudoClasses.Set(DimmedPseudoClass, IsDimmed);
        }
    }
}

/// <summary>Which state the status line shows; it picks the glyph and its color.</summary>
internal enum ReplyDraftStatusKind
{
    Idle,
    Working,
    Ready,
    Done,
    Failed
}

/// <summary>The status line: an icon and a short word; working words shimmer. A new status crossfades in.</summary>
[PseudoClasses(":working", ":ready", ":done", ":failed")]
internal sealed class ReplyDraftStatusView : TemplatedControl
{
    /// <summary>The word.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ReplyDraftStatusView, string?>(nameof(Text));

    /// <summary>The glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ReplyDraftStatusView, Geometry?>(nameof(Glyph));

    /// <summary>True while the word shimmers.</summary>
    public static readonly StyledProperty<bool> IsWorkingProperty =
        AvaloniaProperty.Register<ReplyDraftStatusView, bool>(nameof(IsWorking));

    public ReplyDraftStatusView()
    {
    }

    public ReplyDraftStatusView(ReplyDraftStatusKind kind, string text)
    {
        Kind = kind;
        Text = text;
        IsWorking = kind == ReplyDraftStatusKind.Working;
        Glyph = kind switch
        {
            ReplyDraftStatusKind.Working => ArcIcons.Sparkles,
            ReplyDraftStatusKind.Ready => ArcIcons.CircleCheck,
            ReplyDraftStatusKind.Done => ArcIcons.CheckCheck,
            ReplyDraftStatusKind.Failed => ArcIcons.CircleAlert,
            _ => ArcIcons.PenLine,
        };
        PseudoClasses.Set(":working", kind == ReplyDraftStatusKind.Working);
        PseudoClasses.Set(":ready", kind == ReplyDraftStatusKind.Ready);
        PseudoClasses.Set(":done", kind == ReplyDraftStatusKind.Done);
        PseudoClasses.Set(":failed", kind == ReplyDraftStatusKind.Failed);
    }

    /// <summary>The state shown.</summary>
    public ReplyDraftStatusKind Kind { get; }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="IsWorkingProperty"/>
    public bool IsWorking { get => GetValue(IsWorkingProperty); set => SetValue(IsWorkingProperty, value); }
}

/// <summary>A one-line note in the draft box with a shimmer while the model reads the email.</summary>
internal sealed class ReplyDraftPending : TemplatedControl
{
    /// <summary>The line.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ReplyDraftPending, string?>(nameof(Text));

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}

/// <summary>The draft box before the first draft when the card does not start by itself: a line and Draft reply.</summary>
[TemplatePart(ButtonPart, typeof(Button))]
internal sealed class ReplyDraftEmptyView : TemplatedControl
{
    internal const string ButtonPart = "PART_Draft";

    /// <summary>The line.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ReplyDraftEmptyView, string?>(nameof(Text));

    /// <summary>The button's word.</summary>
    public static readonly StyledProperty<string?> ButtonTextProperty =
        AvaloniaProperty.Register<ReplyDraftEmptyView, string?>(nameof(ButtonText));

    private Button? _button;
    private bool _canDraft = true;

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ButtonTextProperty"/>
    public string? ButtonText { get => GetValue(ButtonTextProperty); set => SetValue(ButtonTextProperty, value); }

    /// <summary>Whether Draft reply can be pressed (off when there is nothing to draft from).</summary>
    public bool CanDraft
    {
        get => _canDraft;
        set
        {
            _canDraft = value;
            if (_button is not null)
            {
                _button.IsEnabled = value;
            }
        }
    }

    /// <summary>Raised when Draft reply is pressed.</summary>
    public event EventHandler? DraftRequested;

    internal Button? DraftButton => _button;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_button is not null)
        {
            _button.Click -= OnClick;
        }
        _button = e.NameScope.Find<Button>(ButtonPart);
        if (_button is not null)
        {
            _button.Click += OnClick;
            _button.IsEnabled = _canDraft;
        }
    }

    private void OnClick(object? sender, RoutedEventArgs e) => DraftRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>A failed draft: an alert icon, the message and Try again.</summary>
[TemplatePart(RetryPart, typeof(Button))]
internal sealed class ReplyDraftAlert : TemplatedControl
{
    internal const string RetryPart = "PART_Retry";

    /// <summary>The message.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ReplyDraftAlert, string?>(nameof(Message));

    /// <summary>The retry button's word.</summary>
    public static readonly StyledProperty<string?> RetryTextProperty =
        AvaloniaProperty.Register<ReplyDraftAlert, string?>(nameof(RetryText));

    private Button? _retry;

    /// <inheritdoc cref="MessageProperty"/>
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    /// <inheritdoc cref="RetryTextProperty"/>
    public string? RetryText { get => GetValue(RetryTextProperty); set => SetValue(RetryTextProperty, value); }

    /// <summary>Raised when Try again is pressed.</summary>
    public event EventHandler? RetryRequested;

    internal Button? RetryButton => _retry;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_retry is not null)
        {
            _retry.Click -= OnClick;
        }
        _retry = e.NameScope.Find<Button>(RetryPart);
        if (_retry is not null)
        {
            _retry.Click += OnClick;
        }
    }

    private void OnClick(object? sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>One paragraph of the received email: selectable body text.</summary>
internal sealed class ReplyDraftParagraph : SelectableTextBlock
{
    protected override Type StyleKeyOverride => typeof(ReplyDraftParagraph);
}

/// <summary>
/// The tone and length row: the tone group, a hairline and the length group on one line. When they do not fit, or
/// while <see cref="IsStacked"/> (below 460px), the hairline hides and Length moves to its own row.
/// Children: the tone block, the hairline, the length block.
/// </summary>
internal sealed class ReplyDraftControlsPanel : Panel
{
    /// <summary>Puts Length on its own row and hides the hairline.</summary>
    public static readonly StyledProperty<bool> IsStackedProperty =
        AvaloniaProperty.Register<ReplyDraftControlsPanel, bool>(nameof(IsStacked));

    /// <summary>The space between the groups (and around the hairline).</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ReplyDraftControlsPanel, double>(nameof(Gap), 16);

    /// <summary>The space between rows when Length has its own row.</summary>
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<ReplyDraftControlsPanel, double>(nameof(RowGap), 10);

    static ReplyDraftControlsPanel()
    {
        AffectsMeasure<ReplyDraftControlsPanel>(IsStackedProperty, GapProperty, RowGapProperty);
    }

    /// <inheritdoc cref="IsStackedProperty"/>
    public bool IsStacked { get => GetValue(IsStackedProperty); set => SetValue(IsStackedProperty, value); }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <inheritdoc cref="RowGapProperty"/>
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>Whether the last layout put Length on its own row.</summary>
    internal bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 3)
        {
            return default;
        }
        var loose = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(loose);
        }
        var tone = Children[0].DesiredSize;
        var line = Children[1].DesiredSize;
        var length = Children[2].DesiredSize;
        var inline = tone.Width + Gap + line.Width + Gap + length.Width;
        IsWrapped = IsStacked || inline > availableSize.Width;
        if (IsWrapped)
        {
            return new Size(Math.Max(tone.Width, length.Width), tone.Height + RowGap + length.Height);
        }
        return new Size(inline, Math.Max(tone.Height, Math.Max(line.Height, length.Height)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 3)
        {
            return finalSize;
        }
        var tone = Children[0];
        var line = Children[1];
        var length = Children[2];
        if (IsWrapped)
        {
            tone.Arrange(new Rect(0, 0, tone.DesiredSize.Width, tone.DesiredSize.Height));
            line.Arrange(default);
            line.Opacity = 0;
            length.Arrange(new Rect(0, tone.DesiredSize.Height + RowGap, length.DesiredSize.Width, length.DesiredSize.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        var x = 0.0;
        tone.Arrange(new Rect(x, (height - tone.DesiredSize.Height) / 2, tone.DesiredSize.Width, tone.DesiredSize.Height));
        x += tone.DesiredSize.Width + Gap;
        line.Opacity = 1;
        line.Arrange(new Rect(x, (height - line.DesiredSize.Height) / 2, line.DesiredSize.Width, line.DesiredSize.Height));
        x += line.DesiredSize.Width + Gap;
        length.Arrange(new Rect(x, (height - length.DesiredSize.Height) / 2, length.DesiredSize.Width, length.DesiredSize.Height));
        return finalSize;
    }
}
