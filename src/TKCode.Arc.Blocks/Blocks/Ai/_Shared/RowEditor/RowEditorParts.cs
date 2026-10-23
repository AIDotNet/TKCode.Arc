using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The text box of a <see cref="RowEditor"/>: a plain multiline box (1px border, radius 10, padding 7 10, 14 / 22) that
/// grows with its text up to six lines and then scrolls. It is Avalonia's <see cref="AvaloniaTextBox"/> rather than Arc's
/// <c>Textarea</c>, which keeps a fixed 110px minimum height with a drag grip, draws a 3px focus ring and wraps itself in
/// label and message rows: none of that belongs here, and restyling it would replace its whole template anyway.
/// </summary>
[TemplatePart(TextPresenterPart, typeof(TextPresenter))]
[PseudoClasses(InvalidPseudoClass)]
internal sealed class RowEditorTextBox : AvaloniaTextBox
{
    public const string TextPresenterPart = "PART_TextPresenter";

    /// <summary>Set while the draft failed a submit: the border turns to the danger color.</summary>
    public const string InvalidPseudoClass = ":invalid";

    /// <summary>The draft failed a submit (Arc's <c>aria-invalid</c>).</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty =
        AvaloniaProperty.Register<RowEditorTextBox, bool>(nameof(IsInvalid));

    static RowEditorTextBox()
    {
        AcceptsReturnProperty.OverrideDefaultValue<RowEditorTextBox>(true);
        TextWrappingProperty.OverrideDefaultValue<RowEditorTextBox>(TextWrapping.Wrap);
    }

    /// <inheritdoc cref="IsInvalidProperty"/>
    public bool IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>The presenter, whose pre-edit text tells whether an input method is composing.</summary>
    public TextPresenter? Presenter { get; private set; }

    /// <summary>True while an input method is composing text (Enter and Escape belong to it then).</summary>
    public bool IsComposing => !string.IsNullOrEmpty(Presenter?.PreeditText);

    protected override Type StyleKeyOverride => typeof(RowEditorTextBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Presenter = e.NameScope.Find<TextPresenter>(TextPresenterPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsInvalidProperty)
        {
            PseudoClasses.Set(InvalidPseudoClass, change.GetNewValue<bool>());
        }
    }
}

/// <summary>
/// The bottom row of a <see cref="RowEditor"/> (Arc's wrapping flex row): the hint at the start and the buttons at the
/// end of one line, both centered on it; when they do not fit side by side with <see cref="ColumnSpacing"/> between them,
/// the hint takes its own line (and may wrap) and the buttons follow <see cref="RowSpacing"/> below it, still at the end.
/// The first child is the hint, the second the buttons.
/// </summary>
internal sealed class RowEditorFooter : Panel
{
    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<RowEditorFooter, double>(nameof(ColumnSpacing), 12);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<RowEditorFooter, double>(nameof(RowSpacing), 8);

    static RowEditorFooter()
    {
        AffectsMeasure<RowEditorFooter>(ColumnSpacingProperty, RowSpacingProperty);
    }

    public double ColumnSpacing { get => GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }

    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    /// <summary>True when the last measure put the hint and the buttons on two lines.</summary>
    public bool IsWrapped { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return base.MeasureOverride(availableSize);
        }
        var hint = Children[0];
        var buttons = Children[1];
        buttons.Measure(Size.Infinity);
        hint.Measure(Size.Infinity);
        var gap = ColumnSpacing;
        var oneLine = hint.DesiredSize.Width + gap + buttons.DesiredSize.Width;
        var width = availableSize.Width;
        if (double.IsInfinity(width) || oneLine <= width)
        {
            IsWrapped = false;
            return new Size(double.IsInfinity(width) ? oneLine : width,
                Math.Max(hint.DesiredSize.Height, buttons.DesiredSize.Height));
        }
        IsWrapped = true;
        hint.Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, hint.DesiredSize.Height + RowSpacing + buttons.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return base.ArrangeOverride(finalSize);
        }
        var hint = Children[0];
        var buttons = Children[1];
        var hintSize = hint.DesiredSize;
        var buttonsSize = buttons.DesiredSize;
        var buttonsX = Math.Max(0, finalSize.Width - buttonsSize.Width);
        if (IsWrapped)
        {
            hint.Arrange(new Rect(0, 0, finalSize.Width, hintSize.Height));
            buttons.Arrange(new Rect(buttonsX, hintSize.Height + RowSpacing, buttonsSize.Width, buttonsSize.Height));
            return finalSize;
        }
        var height = finalSize.Height;
        var hintWidth = Math.Max(0, Math.Min(hintSize.Width, buttonsX - ColumnSpacing));
        hint.Arrange(new Rect(0, (height - hintSize.Height) / 2, hintWidth, hintSize.Height));
        buttons.Arrange(new Rect(buttonsX, (height - buttonsSize.Height) / 2, buttonsSize.Width, buttonsSize.Height));
        return finalSize;
    }
}

/// <summary>
/// One state of the line under a <see cref="RowEditor"/>: the muted hint, or with <see cref="IsAlert"/> a circle-alert
/// glyph and danger-colored text (the live count over the limit, or an error). A new line is made when the kind of
/// message changes, so the editor can crossfade the old one out.
/// </summary>
[PseudoClasses(AlertPseudoClass)]
internal sealed class RowEditorHintLine : TemplatedControl
{
    public const string AlertPseudoClass = ":alert";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<RowEditorHintLine, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsAlertProperty =
        AvaloniaProperty.Register<RowEditorHintLine, bool>(nameof(IsAlert));

    static RowEditorHintLine()
    {
        FocusableProperty.OverrideDefaultValue<RowEditorHintLine>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<RowEditorHintLine>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsAlert { get => GetValue(IsAlertProperty); set => SetValue(IsAlertProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsAlertProperty)
        {
            PseudoClasses.Set(AlertPseudoClass, change.GetNewValue<bool>());
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new HintLinePeer(this);

    /// <summary>Read as its text; the glyph is decoration (the text says what is wrong).</summary>
    private sealed class HintLinePeer(RowEditorHintLine owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => ((RowEditorHintLine)Owner).Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>Which message the line under a <see cref="RowEditor"/> shows.</summary>
internal enum RowEditorHintKind
{
    /// <summary>The hint ("Enter to save, Esc to cancel").</summary>
    Hint,

    /// <summary>The live count once the draft is over the limit.</summary>
    Count,

    /// <summary>The error of a failed submit (or one the owner set).</summary>
    Error
}
