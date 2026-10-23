using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The preview of a cited source, shown above its citation: the site's mark, domain and the citation number on the first
/// row, then the title and an excerpt. 288 wide on the raised surface with a 26 radius (the popup adds the floating
/// shadow). Read-only and never focused: the citation keeps focus while it is open. With <see cref="ShowsMeta"/> (the
/// answer's preview) a last row reads "domain · date" in muted 12px text.
/// </summary>
internal sealed class AiCitationCard : TemplatedControl
{
    /// <summary>The site's mark: a <see cref="Geometry"/> in a 24 × 24 box or an <see cref="IImage"/>; null shows <see cref="Initial"/>.</summary>
    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<AiCitationCard, object?>(nameof(Mark));

    /// <summary>The fill of a geometry mark; null uses the secondary text color.</summary>
    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<AiCitationCard, IBrush?>(nameof(MarkBrush));

    /// <summary>The domain's first letter, shown when there is no mark.</summary>
    public static readonly StyledProperty<string?> InitialProperty =
        AvaloniaProperty.Register<AiCitationCard, string?>(nameof(Initial));

    /// <summary>The site or place, such as "docs.example.org".</summary>
    public static readonly StyledProperty<string?> DomainProperty =
        AvaloniaProperty.Register<AiCitationCard, string?>(nameof(Domain));

    /// <summary>The citation number, right-aligned on the first row.</summary>
    public static readonly StyledProperty<string?> NumberProperty =
        AvaloniaProperty.Register<AiCitationCard, string?>(nameof(Number));

    /// <summary>The title of the source.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiCitationCard, string?>(nameof(Title));

    /// <summary>The excerpt; hidden when empty.</summary>
    public static readonly StyledProperty<string?> SnippetProperty =
        AvaloniaProperty.Register<AiCitationCard, string?>(nameof(Snippet));

    /// <summary>When the source was published, as text ("Sep 3, 2026"); shown in the meta row.</summary>
    public static readonly StyledProperty<string?> DateProperty =
        AvaloniaProperty.Register<AiCitationCard, string?>(nameof(Date));

    /// <summary>Whether the last row shows "domain · date" (the answer's preview; the chat's has no such row).</summary>
    public static readonly StyledProperty<bool> ShowsMetaProperty =
        AvaloniaProperty.Register<AiCitationCard, bool>(nameof(ShowsMeta));

    /// <summary>Whether the meta row shows: <see cref="ShowsMeta"/> and a domain or a date (read-only).</summary>
    public static readonly DirectProperty<AiCitationCard, bool> HasMetaProperty =
        AvaloniaProperty.RegisterDirect<AiCitationCard, bool>(nameof(HasMeta), o => o.HasMeta);

    /// <summary>Whether the meta row has both a domain and a date, with the dot between them (read-only).</summary>
    public static readonly DirectProperty<AiCitationCard, bool> HasMetaDotProperty =
        AvaloniaProperty.RegisterDirect<AiCitationCard, bool>(nameof(HasMetaDot), o => o.HasMetaDot);

    /// <summary>Whether <see cref="Mark"/> is set (the theme swaps the mark for the initial).</summary>
    public static readonly DirectProperty<AiCitationCard, bool> HasMarkProperty =
        AvaloniaProperty.RegisterDirect<AiCitationCard, bool>(nameof(HasMark), o => o.HasMark);

    private bool _hasMark;
    private bool _hasMeta;
    private bool _hasMetaDot;

    static AiCitationCard()
    {
        FocusableProperty.OverrideDefaultValue<AiCitationCard>(false);
    }

    /// <inheritdoc cref="MarkProperty"/>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="MarkBrushProperty"/>
    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <inheritdoc cref="InitialProperty"/>
    public string? Initial { get => GetValue(InitialProperty); set => SetValue(InitialProperty, value); }

    /// <inheritdoc cref="DomainProperty"/>
    public string? Domain { get => GetValue(DomainProperty); set => SetValue(DomainProperty, value); }

    /// <inheritdoc cref="NumberProperty"/>
    public string? Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SnippetProperty"/>
    public string? Snippet { get => GetValue(SnippetProperty); set => SetValue(SnippetProperty, value); }

    /// <inheritdoc cref="DateProperty"/>
    public string? Date { get => GetValue(DateProperty); set => SetValue(DateProperty, value); }

    /// <inheritdoc cref="ShowsMetaProperty"/>
    public bool ShowsMeta { get => GetValue(ShowsMetaProperty); set => SetValue(ShowsMetaProperty, value); }

    /// <inheritdoc cref="HasMetaProperty"/>
    public bool HasMeta { get => _hasMeta; private set => SetAndRaise(HasMetaProperty, ref _hasMeta, value); }

    /// <inheritdoc cref="HasMetaDotProperty"/>
    public bool HasMetaDot { get => _hasMetaDot; private set => SetAndRaise(HasMetaDotProperty, ref _hasMetaDot, value); }

    /// <inheritdoc cref="HasMarkProperty"/>
    public bool HasMark { get => _hasMark; private set => SetAndRaise(HasMarkProperty, ref _hasMark, value); }

    /// <summary>Shows <paramref name="source"/> cited as <paramref name="number"/>.</summary>
    public void Show(AiSource source, int number)
    {
        Mark = source.Mark;
        MarkBrush = source.MarkBrush;
        Domain = source.Domain;
        Initial = string.IsNullOrEmpty(source.Domain) ? null : char.ToUpperInvariant(source.Domain[0]).ToString();
        Number = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Title = source.Title;
        Snippet = string.IsNullOrWhiteSpace(source.Snippet) ? null : source.Snippet;
        Date = string.IsNullOrWhiteSpace(source.Date) ? null : source.Date;
        AutomationProperties.SetName(this, string.IsNullOrEmpty(source.Domain) ? source.Title : source.Title + ", " + source.Domain);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkProperty)
        {
            HasMark = change.NewValue is not null;
        }
        else if (change.Property == ShowsMetaProperty || change.Property == DomainProperty || change.Property == DateProperty)
        {
            var domain = !string.IsNullOrEmpty(Domain);
            var date = !string.IsNullOrEmpty(Date);
            HasMeta = ShowsMeta && (domain || date);
            HasMetaDot = domain && date;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    /// <summary>A tooltip-like description of the focused citation (Arc: <c>role=tooltip</c>).</summary>
    private sealed class CardPeer(AiCitationCard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolTip;
    }
}
