using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// A word diff in one wrapped text (Arc's side panel change card and prompt studio compare view): the new text with the
/// changes against the old one in place. Kept words read normally, deleted words stay where they were with a strikethrough,
/// inserted words sit on a soft tint with rounded corners that repeats on every line a run wraps over, and the white space
/// between two changed words belongs to the change (one tint, no gaps). <see cref="Appearance"/> picks the card (muted
/// deletions, accent tint, no signs) or the studio look (Success / Danger tints, each changed run starting with a "+" or
/// "−", newlines kept); with <see cref="Atoms"/> and <see cref="ChipTone"/> template variables are compared whole and drawn
/// as static colored chips. Nothing animates.
/// </summary>
/// <remarks>
/// <para>The diff is <see cref="WordDiff"/> (word-level LCS after trimming the common prefix and suffix; CJK one character a
/// word), computed when <see cref="Before"/>, <see cref="After"/> or <see cref="Atoms"/> change; the layout is built once
/// per change and the decorations repaint without relayout.</para>
/// <para>The text can be selected with the pointer and copied (Ctrl/Cmd+C); a copy holds the selected words of both
/// versions in reading order without the signs and paddings, and a chip copies as its <c>{{name}}</c> text.</para>
/// <para>Screen readers get <see cref="SummaryName"/> ("12 words added, 3 words removed", set by the owner) as the name and
/// the new text (<see cref="After"/>) as the help text; without a summary the new text is the name. The drawn text with its
/// signs and both versions is hidden from them.</para>
/// </remarks>
[TemplatePart(TextPart, typeof(AiDiffTextBlock))]
[PseudoClasses(CardPseudoClass, StudioPseudoClass)]
internal class AiDiffText : TemplatedControl
{
    public const string TextPart = "PART_Text";

    public const string CardPseudoClass = ":card";
    public const string StudioPseudoClass = ":studio";

    /// <summary>Chip names are this share of the font size (Arc <c>font-size: 0.93em</c>).</summary>
    internal const double ChipFontScale = 0.93;

    /// <summary>Studio signs mix the tint color into the inserted-word color (Arc <c>color-mix(success 72%, foreground)</c>).</summary>
    internal const double SignMix = 0.72;

    /// <summary>Chip fill: the tone's series color at 12%.</summary>
    internal const double ChipFillAlpha = 0.12;

    /// <summary>Chip inner outline: the series color at 22%.</summary>
    internal const double ChipRingAlpha = 0.22;

    /// <summary>Chip name: the series color mixed 68% into the inserted-word color.</summary>
    internal const double ChipTextMix = 0.68;

    #region Properties

    /// <summary>The old text.</summary>
    public static readonly StyledProperty<string> BeforeProperty =
        AvaloniaProperty.Register<AiDiffText, string>(nameof(Before), string.Empty);

    /// <summary>The new text.</summary>
    public static readonly StyledProperty<string> AfterProperty =
        AvaloniaProperty.Register<AiDiffText, string>(nameof(After), string.Empty);

    /// <summary>Card (side panel) or Studio (compare view).</summary>
    public static readonly StyledProperty<AiDiffAppearance> AppearanceProperty =
        AvaloniaProperty.Register<AiDiffText, AiDiffAppearance>(nameof(Appearance));

    /// <summary>
    /// Marker pairs of template variables, open then close (<c>["{{", "}}"]</c>): from an open marker to the next close
    /// marker is one word for the diff and, with <see cref="ChipTone"/>, a chip. An odd last entry and empty markers are ignored.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> AtomsProperty =
        AvaloniaProperty.Register<AiDiffText, IReadOnlyList<string>?>(nameof(Atoms));

    /// <summary>The chip tone (1 to 4, see <see cref="PromptTone"/>) of a variable name; null draws atoms as plain words.</summary>
    public static readonly StyledProperty<Func<string, int>?> ChipToneProperty =
        AvaloniaProperty.Register<AiDiffText, Func<string, int>?>(nameof(ChipTone));

    /// <summary>The accessible name: a summary of the change ("12 words added, 3 words removed").</summary>
    public static readonly StyledProperty<string?> SummaryNameProperty =
        AvaloniaProperty.Register<AiDiffText, string?>(nameof(SummaryName));

    /// <summary>Line height of the text (card 20.8, studio 23.8).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        Avalonia.Controls.TextBlock.LineHeightProperty.AddOwner<AiDiffText>();

    /// <summary>Inserted words (the theme gives Foreground).</summary>
    public static readonly StyledProperty<IBrush?> InsertForegroundProperty =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(InsertForeground));

    /// <summary>Deleted words (card TextMuted, studio TextSecondary).</summary>
    public static readonly StyledProperty<IBrush?> DeleteForegroundProperty =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(DeleteForeground));

    /// <summary>The color of the inserted-run tint (card Accent, studio Success), used at <see cref="InsertTintOpacity"/>.</summary>
    public static readonly StyledProperty<IBrush?> InsertTintProperty =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(InsertTint));

    /// <summary>Strength of the inserted-run tint (card 0.13, studio 0.14).</summary>
    public static readonly StyledProperty<double> InsertTintOpacityProperty =
        AvaloniaProperty.Register<AiDiffText, double>(nameof(InsertTintOpacity), 0.13);

    /// <summary>The color of the deleted-run tint (studio Danger; none on the card), used at <see cref="DeleteTintOpacity"/>.</summary>
    public static readonly StyledProperty<IBrush?> DeleteTintProperty =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(DeleteTint));

    /// <summary>Strength of the deleted-run tint (studio 0.11).</summary>
    public static readonly StyledProperty<double> DeleteTintOpacityProperty =
        AvaloniaProperty.Register<AiDiffText, double>(nameof(DeleteTintOpacity), 0.11);

    /// <summary>The color of the strikethrough (card TextMuted, studio Danger), used at <see cref="StrikeOpacity"/>.</summary>
    public static readonly StyledProperty<IBrush?> StrikeColorProperty =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(StrikeColor));

    /// <summary>Strength of the strikethrough color (card 0.70, studio 0.55).</summary>
    public static readonly StyledProperty<double> StrikeOpacityProperty =
        AvaloniaProperty.Register<AiDiffText, double>(nameof(StrikeOpacity), 0.7);

    /// <summary>Series color of chip tone 1 (the theme gives the Series1 token).</summary>
    public static readonly StyledProperty<IBrush?> Series1Property =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(Series1));

    /// <summary>Series color of chip tone 2.</summary>
    public static readonly StyledProperty<IBrush?> Series2Property =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(Series2));

    /// <summary>Series color of chip tone 3.</summary>
    public static readonly StyledProperty<IBrush?> Series3Property =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(Series3));

    /// <summary>Series color of chip tone 4.</summary>
    public static readonly StyledProperty<IBrush?> Series4Property =
        AvaloniaProperty.Register<AiDiffText, IBrush?>(nameof(Series4));

    /// <summary>Words of the new text that are not in the old one (an atom counts as one).</summary>
    public static readonly DirectProperty<AiDiffText, int> InsertedWordsProperty =
        AvaloniaProperty.RegisterDirect<AiDiffText, int>(nameof(InsertedWords), o => o.InsertedWords);

    /// <summary>Words of the old text that are not in the new one (an atom counts as one).</summary>
    public static readonly DirectProperty<AiDiffText, int> DeletedWordsProperty =
        AvaloniaProperty.RegisterDirect<AiDiffText, int>(nameof(DeletedWords), o => o.DeletedWords);

    /// <summary>Whether the texts differ (white space included).</summary>
    public static readonly DirectProperty<AiDiffText, bool> HasChangesProperty =
        AvaloniaProperty.RegisterDirect<AiDiffText, bool>(nameof(HasChanges), o => o.HasChanges);

    /// <inheritdoc cref="BeforeProperty"/>
    public string Before { get => GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }

    /// <inheritdoc cref="AfterProperty"/>
    public string After { get => GetValue(AfterProperty); set => SetValue(AfterProperty, value); }

    /// <inheritdoc cref="AppearanceProperty"/>
    public AiDiffAppearance Appearance { get => GetValue(AppearanceProperty); set => SetValue(AppearanceProperty, value); }

    /// <inheritdoc cref="AtomsProperty"/>
    public IReadOnlyList<string>? Atoms { get => GetValue(AtomsProperty); set => SetValue(AtomsProperty, value); }

    /// <inheritdoc cref="ChipToneProperty"/>
    public Func<string, int>? ChipTone { get => GetValue(ChipToneProperty); set => SetValue(ChipToneProperty, value); }

    /// <inheritdoc cref="SummaryNameProperty"/>
    public string? SummaryName { get => GetValue(SummaryNameProperty); set => SetValue(SummaryNameProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="InsertForegroundProperty"/>
    public IBrush? InsertForeground { get => GetValue(InsertForegroundProperty); set => SetValue(InsertForegroundProperty, value); }

    /// <inheritdoc cref="DeleteForegroundProperty"/>
    public IBrush? DeleteForeground { get => GetValue(DeleteForegroundProperty); set => SetValue(DeleteForegroundProperty, value); }

    /// <inheritdoc cref="InsertTintProperty"/>
    public IBrush? InsertTint { get => GetValue(InsertTintProperty); set => SetValue(InsertTintProperty, value); }

    /// <inheritdoc cref="InsertTintOpacityProperty"/>
    public double InsertTintOpacity { get => GetValue(InsertTintOpacityProperty); set => SetValue(InsertTintOpacityProperty, value); }

    /// <inheritdoc cref="DeleteTintProperty"/>
    public IBrush? DeleteTint { get => GetValue(DeleteTintProperty); set => SetValue(DeleteTintProperty, value); }

    /// <inheritdoc cref="DeleteTintOpacityProperty"/>
    public double DeleteTintOpacity { get => GetValue(DeleteTintOpacityProperty); set => SetValue(DeleteTintOpacityProperty, value); }

    /// <inheritdoc cref="StrikeColorProperty"/>
    public IBrush? StrikeColor { get => GetValue(StrikeColorProperty); set => SetValue(StrikeColorProperty, value); }

    /// <inheritdoc cref="StrikeOpacityProperty"/>
    public double StrikeOpacity { get => GetValue(StrikeOpacityProperty); set => SetValue(StrikeOpacityProperty, value); }

    /// <inheritdoc cref="Series1Property"/>
    public IBrush? Series1 { get => GetValue(Series1Property); set => SetValue(Series1Property, value); }

    /// <inheritdoc cref="Series2Property"/>
    public IBrush? Series2 { get => GetValue(Series2Property); set => SetValue(Series2Property, value); }

    /// <inheritdoc cref="Series3Property"/>
    public IBrush? Series3 { get => GetValue(Series3Property); set => SetValue(Series3Property, value); }

    /// <inheritdoc cref="Series4Property"/>
    public IBrush? Series4 { get => GetValue(Series4Property); set => SetValue(Series4Property, value); }

    /// <inheritdoc cref="InsertedWordsProperty"/>
    public int InsertedWords
    {
        get => _insertedWords;
        private set => SetAndRaise(InsertedWordsProperty, ref _insertedWords, value);
    }

    /// <inheritdoc cref="DeletedWordsProperty"/>
    public int DeletedWords
    {
        get => _deletedWords;
        private set => SetAndRaise(DeletedWordsProperty, ref _deletedWords, value);
    }

    /// <inheritdoc cref="HasChangesProperty"/>
    public bool HasChanges
    {
        get => _hasChanges;
        private set => SetAndRaise(HasChangesProperty, ref _hasChanges, value);
    }

    #endregion

    private AiDiffDocument _document = AiDiffDocument.Empty;
    private AiDiffPalette? _palette;
    private AiDiffTextBlock? _text;
    private int _insertedWords;
    private int _deletedWords;
    private bool _hasChanges;
    private bool _diffDirty = true;

    public AiDiffText()
    {
        UpdateClasses();
    }

    /// <summary>The document shown (the diff, its runs and marks).</summary>
    internal AiDiffDocument Document
    {
        get
        {
            EnsureDiff();
            return _document;
        }
    }

    /// <summary>The colors in use.</summary>
    internal AiDiffPalette Palette => _palette ??= BuildPalette();

    /// <summary>The text block of the template.</summary>
    internal AiDiffTextBlock? TextView => _text;

    /// <summary>The marker pairs of <see cref="Atoms"/> in the form <see cref="WordDiffOptions.Atoms"/> takes.</summary>
    internal static IReadOnlyList<(string Open, string Close)> AtomPairs(IReadOnlyList<string>? atoms)
    {
        if (atoms is null || atoms.Count < 2)
        {
            return [];
        }
        var pairs = new List<(string, string)>(atoms.Count / 2);
        for (var i = 0; i + 1 < atoms.Count; i += 2)
        {
            if (!string.IsNullOrEmpty(atoms[i]) && !string.IsNullOrEmpty(atoms[i + 1]))
            {
                pairs.Add((atoms[i], atoms[i + 1]));
            }
        }
        return pairs;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<AiDiffTextBlock>(TextPart);
        if (_text is not null)
        {
            EnsureDiff();
            _text.Palette = Palette;
            _text.Show(_document, Appearance);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == BeforeProperty || property == AfterProperty || property == AtomsProperty || property == ChipToneProperty)
        {
            Invalidate();
        }
        else if (property == AppearanceProperty)
        {
            UpdateClasses();
            Invalidate();
        }
        else if (property == ForegroundProperty || property == InsertForegroundProperty || property == DeleteForegroundProperty ||
                 property == InsertTintProperty || property == InsertTintOpacityProperty || property == DeleteTintProperty ||
                 property == DeleteTintOpacityProperty || property == StrikeColorProperty || property == StrikeOpacityProperty ||
                 property == Series1Property || property == Series2Property || property == Series3Property || property == Series4Property)
        {
            _palette = null;
            if (_text is not null)
            {
                _text.Palette = Palette;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiDiffTextAutomationPeer(this);

    /// <summary>Recomputes the diff now (the owner changed a list it passed as <see cref="Atoms"/> in place).</summary>
    internal void Refresh() => Invalidate();

    private void Invalidate()
    {
        _diffDirty = true;
        EnsureDiff();
        _text?.Show(_document, Appearance);
    }

    private void EnsureDiff()
    {
        if (!_diffDirty)
        {
            return;
        }
        _diffDirty = false;
        _document = AiDiffDocument.Compare(Before ?? string.Empty, After ?? string.Empty, Appearance, AtomPairs(Atoms), ChipTone);
        InsertedWords = _document.InsertedWords;
        DeletedWords = _document.DeletedWords;
        HasChanges = _document.HasChanges;
    }

    private void UpdateClasses()
    {
        PseudoClasses.Set(CardPseudoClass, Appearance == AiDiffAppearance.Card);
        PseudoClasses.Set(StudioPseudoClass, Appearance == AiDiffAppearance.Studio);
    }

    /// <summary>The palette from the theme brushes: tints at their opacities, signs and chips mixed with the inserted-word color.</summary>
    private AiDiffPalette BuildPalette()
    {
        var strong = ColorOf(InsertForeground) ?? ColorOf(Foreground) ?? Colors.Black;
        var insertTint = ColorOf(InsertTint);
        var deleteTint = ColorOf(DeleteTint);
        var strike = ColorOf(StrikeColor);
        var fills = new IBrush?[4];
        var rings = new IPen?[4];
        var names = new IBrush?[4];
        var series = new[] { Series1, Series2, Series3, Series4 };
        for (var i = 0; i < 4; i++)
        {
            if (ColorOf(series[i]) is { } color)
            {
                fills[i] = new ImmutableSolidColorBrush(PromptTone.WithOpacity(color, ChipFillAlpha));
                rings[i] = new ImmutablePen(new ImmutableSolidColorBrush(PromptTone.WithOpacity(color, ChipRingAlpha)), 1);
                names[i] = new ImmutableSolidColorBrush(PromptTone.Mix(strong, color, ChipTextMix));
            }
        }
        return new AiDiffPalette
        {
            Equal = Foreground,
            Insert = InsertForeground ?? Foreground,
            Delete = DeleteForeground ?? Foreground,
            InsertSign = insertTint is { } plus ? new ImmutableSolidColorBrush(PromptTone.Mix(strong, Opaque(plus), SignMix)) : InsertForeground,
            DeleteSign = (deleteTint ?? strike) is { } minus ? new ImmutableSolidColorBrush(PromptTone.Mix(strong, Opaque(minus), SignMix)) : DeleteForeground,
            InsertTint = insertTint is { } tint ? new ImmutableSolidColorBrush(PromptTone.WithOpacity(tint, InsertTintOpacity)) : null,
            DeleteTint = deleteTint is { } red ? new ImmutableSolidColorBrush(PromptTone.WithOpacity(red, DeleteTintOpacity)) : null,
            Strike = strike is { } line ? new ImmutablePen(new ImmutableSolidColorBrush(PromptTone.WithOpacity(line, StrikeOpacity)), 1) : null,
            ChipFill = fills,
            ChipRing = rings,
            ChipText = names
        };
    }

    /// <summary>The color of a solid brush with the brush's opacity applied; null for other brushes.</summary>
    private static Color? ColorOf(IBrush? brush) =>
        brush is ISolidColorBrush solid ? PromptTone.WithOpacity(solid.Color, solid.Opacity) : null;

    private static Color Opaque(Color color) => Color.FromRgb(color.R, color.G, color.B);

    /// <summary>Text for screen readers: the summary as the name and the new text as the help text.</summary>
    private sealed class AiDiffTextAutomationPeer : ControlAutomationPeer
    {
        public AiDiffTextAutomationPeer(AiDiffText owner) : base(owner)
        {
        }

        private new AiDiffText Owner => (AiDiffText)base.Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
            return string.IsNullOrEmpty(Owner.SummaryName) ? Owner.Document.AfterText : Owner.SummaryName;
        }

        protected override string? GetHelpTextCore()
        {
            var help = base.GetHelpTextCore();
            if (!string.IsNullOrEmpty(help))
            {
                return help;
            }
            return string.IsNullOrEmpty(Owner.SummaryName) ? null : Owner.Document.AfterText;
        }

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
