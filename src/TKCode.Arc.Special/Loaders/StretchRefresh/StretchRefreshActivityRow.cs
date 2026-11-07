using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// An activity feed row for <see cref="StretchRefresh"/> (its default row for <see cref="StretchRefreshActivity"/>
/// items): a 32 px avatar or status glyph, the sentence with the actor and target in the emphasis weight, an optional
/// quote and 4:3 photo, the meta line, and an 8 px accent dot while the entry is unread. Marking the entry read fades
/// the dot (240 ms) while it shrinks to 0.4 (580 ms spring curve).
/// </summary>
[TemplatePart(SentencePart, typeof(TextBlock))]
[PseudoClasses(UnreadPseudoClass, PersonPseudoClass, GlyphPseudoClass)]
public class StretchRefreshActivityRow : TemplatedControl
{
    internal const string SentencePart = "PART_Sentence";
    internal const string UnreadPseudoClass = ":unread";
    internal const string PersonPseudoClass = ":person";
    internal const string GlyphPseudoClass = ":glyph";

    /// <summary>Lucide "receipt" (not in Core's subset).</summary>
    internal static readonly Geometry ReceiptGlyph = ArcIcons.Parse(
        "M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1Z M16 8h-6a2 2 0 1 0 0 4h4a2 2 0 1 1 0 4H8 M12 17.5v-11");

    /// <summary>The entry the row shows.</summary>
    public static readonly StyledProperty<StretchRefreshActivity?> ActivityProperty =
        AvaloniaProperty.Register<StretchRefreshActivityRow, StretchRefreshActivity?>(nameof(Activity));

    /// <summary>What a screen reader hears before an unread entry: "New.".</summary>
    public static readonly StyledProperty<string> UnreadLabelProperty =
        AvaloniaProperty.Register<StretchRefreshActivityRow, string>(nameof(UnreadLabel), "New.");

    /// <summary>The brush of the actor and target (Foreground by default; the rest of the sentence uses TextSecondary).</summary>
    public static readonly StyledProperty<IBrush?> EmphasisBrushProperty =
        AvaloniaProperty.Register<StretchRefreshActivityRow, IBrush?>(nameof(EmphasisBrush));

    internal static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<StretchRefreshActivityRow, IBrush?>(nameof(SuccessInk));

    internal static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<StretchRefreshActivityRow, IBrush?>(nameof(DangerInk));

    internal static readonly StyledProperty<IBrush?> NeutralInkProperty =
        AvaloniaProperty.Register<StretchRefreshActivityRow, IBrush?>(nameof(NeutralInk));

    internal static readonly DirectProperty<StretchRefreshActivityRow, Geometry?> GlyphProperty =
        AvaloniaProperty.RegisterDirect<StretchRefreshActivityRow, Geometry?>(nameof(Glyph), o => o.Glyph);

    internal static readonly DirectProperty<StretchRefreshActivityRow, IBrush?> GlyphInkProperty =
        AvaloniaProperty.RegisterDirect<StretchRefreshActivityRow, IBrush?>(nameof(GlyphInk), o => o.GlyphInk);

    private TextBlock? _sentence;
    private TextBlock? _quote;
    private TextBlock? _meta;
    private StretchRefreshAvatar? _avatar;
    private StretchRefreshPhoto? _photo;
    private StretchRefreshActivity? _observed;
    private Geometry? _glyph;
    private IBrush? _glyphInk;

    /// <inheritdoc cref="ActivityProperty"/>
    public StretchRefreshActivity? Activity
    {
        get => GetValue(ActivityProperty);
        set => SetValue(ActivityProperty, value);
    }

    /// <inheritdoc cref="UnreadLabelProperty"/>
    public string UnreadLabel
    {
        get => GetValue(UnreadLabelProperty);
        set => SetValue(UnreadLabelProperty, value);
    }

    /// <inheritdoc cref="EmphasisBrushProperty"/>
    public IBrush? EmphasisBrush
    {
        get => GetValue(EmphasisBrushProperty);
        set => SetValue(EmphasisBrushProperty, value);
    }

    internal IBrush? SuccessInk
    {
        get => GetValue(SuccessInkProperty);
        set => SetValue(SuccessInkProperty, value);
    }

    internal IBrush? DangerInk
    {
        get => GetValue(DangerInkProperty);
        set => SetValue(DangerInkProperty, value);
    }

    internal IBrush? NeutralInk
    {
        get => GetValue(NeutralInkProperty);
        set => SetValue(NeutralInkProperty, value);
    }

    /// <summary>The status glyph for the entry's tone (null for a person).</summary>
    internal Geometry? Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    internal IBrush? GlyphInk
    {
        get => _glyphInk;
        private set => SetAndRaise(GlyphInkProperty, ref _glyphInk, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _sentence = e.NameScope.Find<TextBlock>(SentencePart);
        _quote = e.NameScope.Find<TextBlock>("PART_Quote");
        _meta = e.NameScope.Find<TextBlock>("PART_Meta");
        _avatar = e.NameScope.Find<StretchRefreshAvatar>("PART_Avatar");
        _photo = e.NameScope.Find<StretchRefreshPhoto>("PART_Photo");
        BuildSentence();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActivityProperty)
        {
            if (_observed is not null)
            {
                _observed.PropertyChanged -= OnActivityChanged;
            }
            _observed = Activity;
            if (_observed is not null)
            {
                _observed.PropertyChanged += OnActivityChanged;
            }
            BuildSentence();
            UpdateTone();
            UpdateUnread();
        }
        else if (change.Property == SuccessInkProperty || change.Property == DangerInkProperty || change.Property == NeutralInkProperty)
        {
            UpdateTone();
        }
        else if (change.Property == UnreadLabelProperty)
        {
            UpdateName();
        }
    }

    private void OnActivityChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == StretchRefreshActivity.IsUnreadProperty)
        {
            UpdateUnread();
        }
    }

    private void UpdateUnread()
    {
        PseudoClasses.Set(UnreadPseudoClass, Activity?.IsUnread == true);
        UpdateName();
    }

    private void UpdateTone()
    {
        var tone = Activity?.Tone ?? StretchRefreshActivityTone.Person;
        PseudoClasses.Set(PersonPseudoClass, tone == StretchRefreshActivityTone.Person);
        PseudoClasses.Set(GlyphPseudoClass, tone != StretchRefreshActivityTone.Person);
        (Glyph, GlyphInk) = tone switch
        {
            StretchRefreshActivityTone.Success => (ArcIcons.CircleCheck, SuccessInk),
            StretchRefreshActivityTone.Danger => (ArcIcons.CircleX, DangerInk),
            StretchRefreshActivityTone.Neutral => (ReceiptGlyph, NeutralInk),
            _ => ((Geometry?)null, (IBrush?)null)
        };
    }

    private void UpdateName()
    {
        if (Activity is not { } a)
        {
            AutomationProperties.SetName(this, null);
            return;
        }
        var parts = new List<string>(4);
        if (a.IsUnread && !string.IsNullOrEmpty(UnreadLabel))
        {
            parts.Add(UnreadLabel);
        }
        parts.Add(a.Sentence);
        if (!string.IsNullOrEmpty(a.Quote))
        {
            parts.Add("“" + a.Quote + "”");
        }
        if (!string.IsNullOrEmpty(a.Meta))
        {
            parts.Add(a.Meta!);
        }
        AutomationProperties.SetName(this, string.Join(" ", parts));
    }

    /// <summary>Writes the sentence as runs: the actor and target in the emphasis weight and brush.</summary>
    private void BuildSentence()
    {
        if (_sentence is null)
        {
            return;
        }
        FillParts(Activity);
        var inlines = _sentence.Inlines ??= new InlineCollection();
        inlines.Clear();
        if (Activity is not { } a)
        {
            return;
        }
        inlines.Add(Emphasis(a.Actor));
        inlines.Add(new Run(string.IsNullOrEmpty(a.Target) ? " " + a.Action : " " + a.Action + " "));
        if (!string.IsNullOrEmpty(a.Target))
        {
            inlines.Add(Emphasis(a.Target!));
        }
    }

    /// <summary>Writes the quote, photo, meta and avatar seed into the template.</summary>
    private void FillParts(StretchRefreshActivity? a)
    {
        if (_quote is not null)
        {
            _quote.Text = string.IsNullOrEmpty(a?.Quote) ? null : "“" + a!.Quote + "”";
            _quote.IsVisible = !string.IsNullOrEmpty(a?.Quote);
        }
        if (_meta is not null)
        {
            _meta.Text = a?.Meta;
            _meta.IsVisible = !string.IsNullOrEmpty(a?.Meta);
        }
        if (_avatar is not null)
        {
            _avatar.Seed = a?.AvatarSeed ?? 0;
        }
        if (_photo is not null)
        {
            _photo.Seed = a?.PhotoSeed ?? 0;
            _photo.IsVisible = a?.PhotoSeed is not null;
        }
    }

    private Run Emphasis(string text)
    {
        var run = new Run(text) { FontWeight = FontWeight.Medium };
        run[!TextElement.ForegroundProperty] = this[!EmphasisBrushProperty];
        return run;
    }
}
