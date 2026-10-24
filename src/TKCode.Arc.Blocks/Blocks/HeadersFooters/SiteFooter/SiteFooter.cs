// Ported from Arc's free site-footer block (MIT, Copyright (c) 2026 Elia Kuratli): site-footer.tsx and
// site-footer.module.css, made data driven (columns, links and status as properties, newsletter through SubscribeAsync).
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A website footer in three layouts: link columns with a newsletter signup (<see cref="SiteFooterVariant.Columns"/>), a
/// single quiet row (<see cref="SiteFooterVariant.Minimal"/>), and a closing layout where the Arc arch rises in, oversized
/// and cropped by the bottom edge, fading into the page (<see cref="SiteFooterVariant.Logo"/>). The newsletter validates
/// in place, keeps its width while the button morphs, and confirms without a toast. Every text, link, column and the
/// status line can be replaced; with nothing set the footer shows Arc's sample footer.
/// </summary>
/// <remarks>
/// Ported from Arc's free site-footer block (MIT, Copyright (c) 2026 Elia Kuratli). Breakpoints follow the control's own
/// width: below 760 the intro stacks over the columns, below 520 the columns take two across and the legal row stacks.
/// </remarks>
[TemplatePart(ColumnsPart, typeof(Panel))]
[TemplatePart(LegalPart, typeof(Panel))]
[TemplatePart(SocialPart, typeof(Panel))]
[TemplatePart(MinimalLinksPart, typeof(Panel))]
[TemplatePart(MinimalSocialPart, typeof(Panel))]
[TemplatePart(MarkPart, typeof(Control))]
[PseudoClasses(NarrowPseudoClass, StackedPseudoClass, ColumnsPseudoClass, MinimalPseudoClass, LogoPseudoClass)]
public class SiteFooter : TemplatedControl
{
    public const string ColumnsPart = "PART_Columns";
    public const string LegalPart = "PART_Legal";
    public const string SocialPart = "PART_Social";
    public const string MinimalLinksPart = "PART_MinimalLinks";
    public const string MinimalSocialPart = "PART_MinimalSocial";
    public const string MarkPart = "PART_Mark";

    /// <summary>Set while the control is at most 520 wide: two link columns, a stacked legal row.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the control is at most 760 wide: the intro sits above the link columns.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while <see cref="Variant"/> is Columns.</summary>
    public const string ColumnsPseudoClass = ":columns";

    /// <summary>Set while <see cref="Variant"/> is Minimal.</summary>
    public const string MinimalPseudoClass = ":minimal";

    /// <summary>Set while <see cref="Variant"/> is Logo.</summary>
    public const string LogoPseudoClass = ":logo";

    /// <summary>The widest a footer is stacked (Arc: container max-width 760).</summary>
    internal const double StackedWidth = 760;

    /// <summary>The widest a footer is narrow (Arc: container max-width 520).</summary>
    internal const double NarrowWidth = 520;

    #region Public properties

    /// <summary>The layout: Columns (default), Minimal or Logo.</summary>
    public static readonly StyledProperty<SiteFooterVariant> VariantProperty =
        AvaloniaProperty.Register<SiteFooter, SiteFooterVariant>(nameof(Variant));

    /// <summary>The brand name, next to the mark and in the copyright line.</summary>
    public static readonly StyledProperty<string> BrandNameProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(BrandName), "Arc");

    /// <summary>Where the brand name leads; without it the name only raises the navigate event.</summary>
    public static readonly StyledProperty<string?> BrandHrefProperty =
        AvaloniaProperty.Register<SiteFooter, string?>(nameof(BrandHref));

    /// <summary>The brand mark; null draws Arc's arch.</summary>
    public static readonly StyledProperty<object?> BrandMarkProperty =
        AvaloniaProperty.Register<SiteFooter, object?>(nameof(BrandMark));

    /// <summary>One short line under the brand (and in the copyright line of Minimal); empty hides it.</summary>
    public static readonly StyledProperty<string?> TaglineProperty =
        AvaloniaProperty.Register<SiteFooter, string?>(nameof(Tagline), "Interface components that move with intent.");

    /// <summary>The link columns; empty (and without <see cref="ColumnsSource"/>) shows Arc's three sample columns.</summary>
    public static readonly DirectProperty<SiteFooter, AvaloniaList<SiteFooterColumn>> ColumnsProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, AvaloniaList<SiteFooterColumn>>(nameof(Columns), o => o.Columns);

    /// <summary>The link columns from a collection; when set it wins over <see cref="Columns"/> and is observed.</summary>
    public static readonly StyledProperty<IEnumerable<SiteFooterColumn>?> ColumnsSourceProperty =
        AvaloniaProperty.Register<SiteFooter, IEnumerable<SiteFooterColumn>?>(nameof(ColumnsSource));

    /// <summary>The one-row links of the Minimal layout; null shows the first link of each column and the first two legal links.</summary>
    public static readonly StyledProperty<AvaloniaList<SiteFooterLink>?> LinksProperty =
        AvaloniaProperty.Register<SiteFooter, AvaloniaList<SiteFooterLink>?>(nameof(Links));

    /// <summary>Small links beside the copyright; empty shows Privacy, Terms and Licenses.</summary>
    public static readonly DirectProperty<SiteFooter, AvaloniaList<SiteFooterLink>> LegalLinksProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, AvaloniaList<SiteFooterLink>>(nameof(LegalLinks), o => o.LegalLinks);

    /// <summary>Social links at the end of the legal row; empty shows X, GitHub and LinkedIn.</summary>
    public static readonly DirectProperty<SiteFooter, AvaloniaList<SiteFooterLink>> SocialLinksProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, AvaloniaList<SiteFooterLink>>(nameof(SocialLinks), o => o.SocialLinks);

    /// <summary>Whether the newsletter signup shows (Columns layout only).</summary>
    public static readonly StyledProperty<bool> IsNewsletterVisibleProperty =
        AvaloniaProperty.Register<SiteFooter, bool>(nameof(IsNewsletterVisible), true);

    /// <summary>The newsletter's title.</summary>
    public static readonly StyledProperty<string?> NewsletterTitleProperty =
        AvaloniaProperty.Register<SiteFooter, string?>(nameof(NewsletterTitle), "Get the monthly release notes");

    /// <summary>The hint under the newsletter input.</summary>
    public static readonly StyledProperty<string?> NewsletterDescriptionProperty =
        AvaloniaProperty.Register<SiteFooter, string?>(nameof(NewsletterDescription), "One email a month. Unsubscribe anytime.");

    /// <summary>The placeholder of the newsletter input.</summary>
    public static readonly StyledProperty<string?> NewsletterPlaceholderProperty =
        AvaloniaProperty.Register<SiteFooter, string?>(nameof(NewsletterPlaceholder), "you@example.com");

    /// <summary>The accessible name of the newsletter input.</summary>
    public static readonly StyledProperty<string> NewsletterEmailLabelProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(NewsletterEmailLabel), "Email address");

    /// <summary>The text of the subscribe button.</summary>
    public static readonly StyledProperty<string> NewsletterSubscribeLabelProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(NewsletterSubscribeLabel), "Subscribe");

    /// <summary>The text of the button after subscribing.</summary>
    public static readonly StyledProperty<string> NewsletterSubscribedLabelProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(NewsletterSubscribedLabel), "Subscribed");

    /// <summary>The error for an empty address.</summary>
    public static readonly StyledProperty<string> NewsletterEmptyEmailMessageProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(NewsletterEmptyEmailMessage), "Enter your email address");

    /// <summary>The error for an address that does not look like <c>name@domain.tld</c>.</summary>
    public static readonly StyledProperty<string> NewsletterInvalidEmailMessageProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(NewsletterInvalidEmailMessage), "That email doesn't look right");

    /// <summary>The error shown when <see cref="SubscribeAsync"/> throws.</summary>
    public static readonly StyledProperty<string> NewsletterFailedMessageProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(NewsletterFailedMessage), "We couldn't subscribe you. Try again in a moment");

    /// <summary>The confirmation after subscribing; <c>{0}</c> is the address.</summary>
    public static readonly StyledProperty<string> NewsletterConfirmFormatProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(NewsletterConfirmFormat), "Check {0} to confirm");

    /// <summary>Called with a valid address; throw to show the failure message, return to show the subscribed state. Null simulates a 900 ms success.</summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task>?> SubscribeAsyncProperty =
        AvaloniaProperty.Register<SiteFooter, Func<string, CancellationToken, Task>?>(nameof(SubscribeAsync));

    /// <summary>Whether the status link shows.</summary>
    public static readonly StyledProperty<bool> IsStatusVisibleProperty =
        AvaloniaProperty.Register<SiteFooter, bool>(nameof(IsStatusVisible), true);

    /// <summary>The text of the status link.</summary>
    public static readonly StyledProperty<string> StatusLabelProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(StatusLabel), "All systems normal");

    /// <summary>The color of the status dot.</summary>
    public static readonly StyledProperty<SiteStatusTone> StatusToneProperty =
        AvaloniaProperty.Register<SiteFooter, SiteStatusTone>(nameof(StatusTone));

    /// <summary>Where the status leads; without it the status only raises the navigate event.</summary>
    public static readonly StyledProperty<string?> StatusHrefProperty =
        AvaloniaProperty.Register<SiteFooter, string?>(nameof(StatusHref));

    /// <summary>The year of the copyright line; null uses this year.</summary>
    public static readonly StyledProperty<int?> YearProperty =
        AvaloniaProperty.Register<SiteFooter, int?>(nameof(Year));

    /// <summary>The accessible name of the link navigation.</summary>
    public static readonly StyledProperty<string> NavigationLabelProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(NavigationLabel), "Footer");

    /// <summary>The accessible name of the legal links.</summary>
    public static readonly StyledProperty<string> LegalLabelProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(LegalLabel), "Legal");

    /// <summary>The accessible name of the social links.</summary>
    public static readonly StyledProperty<string> SocialLabelProperty =
        AvaloniaProperty.Register<SiteFooter, string>(nameof(SocialLabel), "Social");

    /// <summary>Runs when a link is pressed, after <see cref="Navigate"/>; its parameter is the event arguments.</summary>
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<SiteFooter, ICommand?>(nameof(NavigateCommand));

    /// <summary>A link, the brand or the status was pressed. Mark it handled to keep the footer from opening the address.</summary>
    public static readonly RoutedEvent<SiteFooterNavigateEventArgs> NavigateEvent =
        RoutedEvent.Register<SiteFooter, SiteFooterNavigateEventArgs>(nameof(Navigate), RoutingStrategies.Bubble);

    /// <summary>The newsletter signup succeeded.</summary>
    public static readonly RoutedEvent<SiteFooterSubscribedEventArgs> SubscribedEvent =
        RoutedEvent.Register<SiteFooter, SiteFooterSubscribedEventArgs>(nameof(Subscribed), RoutingStrategies.Bubble);

    private readonly AvaloniaList<SiteFooterColumn> _columns = new();
    private readonly AvaloniaList<SiteFooterLink> _legalLinks = new();
    private readonly AvaloniaList<SiteFooterLink> _socialLinks = new();

    /// <inheritdoc cref="VariantProperty"/>
    public SiteFooterVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="BrandNameProperty"/>
    public string BrandName { get => GetValue(BrandNameProperty); set => SetValue(BrandNameProperty, value); }

    /// <inheritdoc cref="BrandHrefProperty"/>
    public string? BrandHref { get => GetValue(BrandHrefProperty); set => SetValue(BrandHrefProperty, value); }

    /// <inheritdoc cref="BrandMarkProperty"/>
    public object? BrandMark { get => GetValue(BrandMarkProperty); set => SetValue(BrandMarkProperty, value); }

    /// <inheritdoc cref="TaglineProperty"/>
    public string? Tagline { get => GetValue(TaglineProperty); set => SetValue(TaglineProperty, value); }

    /// <inheritdoc cref="ColumnsProperty"/>
    [Content]
    public AvaloniaList<SiteFooterColumn> Columns => _columns;

    /// <inheritdoc cref="ColumnsSourceProperty"/>
    public IEnumerable<SiteFooterColumn>? ColumnsSource { get => GetValue(ColumnsSourceProperty); set => SetValue(ColumnsSourceProperty, value); }

    /// <inheritdoc cref="LinksProperty"/>
    public AvaloniaList<SiteFooterLink>? Links { get => GetValue(LinksProperty); set => SetValue(LinksProperty, value); }

    /// <inheritdoc cref="LegalLinksProperty"/>
    public AvaloniaList<SiteFooterLink> LegalLinks => _legalLinks;

    /// <inheritdoc cref="SocialLinksProperty"/>
    public AvaloniaList<SiteFooterLink> SocialLinks => _socialLinks;

    /// <inheritdoc cref="IsNewsletterVisibleProperty"/>
    public bool IsNewsletterVisible { get => GetValue(IsNewsletterVisibleProperty); set => SetValue(IsNewsletterVisibleProperty, value); }

    /// <inheritdoc cref="NewsletterTitleProperty"/>
    public string? NewsletterTitle { get => GetValue(NewsletterTitleProperty); set => SetValue(NewsletterTitleProperty, value); }

    /// <inheritdoc cref="NewsletterDescriptionProperty"/>
    public string? NewsletterDescription { get => GetValue(NewsletterDescriptionProperty); set => SetValue(NewsletterDescriptionProperty, value); }

    /// <inheritdoc cref="NewsletterPlaceholderProperty"/>
    public string? NewsletterPlaceholder { get => GetValue(NewsletterPlaceholderProperty); set => SetValue(NewsletterPlaceholderProperty, value); }

    /// <inheritdoc cref="NewsletterEmailLabelProperty"/>
    public string NewsletterEmailLabel { get => GetValue(NewsletterEmailLabelProperty); set => SetValue(NewsletterEmailLabelProperty, value); }

    /// <inheritdoc cref="NewsletterSubscribeLabelProperty"/>
    public string NewsletterSubscribeLabel { get => GetValue(NewsletterSubscribeLabelProperty); set => SetValue(NewsletterSubscribeLabelProperty, value); }

    /// <inheritdoc cref="NewsletterSubscribedLabelProperty"/>
    public string NewsletterSubscribedLabel { get => GetValue(NewsletterSubscribedLabelProperty); set => SetValue(NewsletterSubscribedLabelProperty, value); }

    /// <inheritdoc cref="NewsletterEmptyEmailMessageProperty"/>
    public string NewsletterEmptyEmailMessage { get => GetValue(NewsletterEmptyEmailMessageProperty); set => SetValue(NewsletterEmptyEmailMessageProperty, value); }

    /// <inheritdoc cref="NewsletterInvalidEmailMessageProperty"/>
    public string NewsletterInvalidEmailMessage { get => GetValue(NewsletterInvalidEmailMessageProperty); set => SetValue(NewsletterInvalidEmailMessageProperty, value); }

    /// <inheritdoc cref="NewsletterFailedMessageProperty"/>
    public string NewsletterFailedMessage { get => GetValue(NewsletterFailedMessageProperty); set => SetValue(NewsletterFailedMessageProperty, value); }

    /// <inheritdoc cref="NewsletterConfirmFormatProperty"/>
    public string NewsletterConfirmFormat { get => GetValue(NewsletterConfirmFormatProperty); set => SetValue(NewsletterConfirmFormatProperty, value); }

    /// <inheritdoc cref="SubscribeAsyncProperty"/>
    public Func<string, CancellationToken, Task>? SubscribeAsync { get => GetValue(SubscribeAsyncProperty); set => SetValue(SubscribeAsyncProperty, value); }

    /// <inheritdoc cref="IsStatusVisibleProperty"/>
    public bool IsStatusVisible { get => GetValue(IsStatusVisibleProperty); set => SetValue(IsStatusVisibleProperty, value); }

    /// <inheritdoc cref="StatusLabelProperty"/>
    public string StatusLabel { get => GetValue(StatusLabelProperty); set => SetValue(StatusLabelProperty, value); }

    /// <inheritdoc cref="StatusToneProperty"/>
    public SiteStatusTone StatusTone { get => GetValue(StatusToneProperty); set => SetValue(StatusToneProperty, value); }

    /// <inheritdoc cref="StatusHrefProperty"/>
    public string? StatusHref { get => GetValue(StatusHrefProperty); set => SetValue(StatusHrefProperty, value); }

    /// <inheritdoc cref="YearProperty"/>
    public int? Year { get => GetValue(YearProperty); set => SetValue(YearProperty, value); }

    /// <inheritdoc cref="NavigationLabelProperty"/>
    public string NavigationLabel { get => GetValue(NavigationLabelProperty); set => SetValue(NavigationLabelProperty, value); }

    /// <inheritdoc cref="LegalLabelProperty"/>
    public string LegalLabel { get => GetValue(LegalLabelProperty); set => SetValue(LegalLabelProperty, value); }

    /// <inheritdoc cref="SocialLabelProperty"/>
    public string SocialLabel { get => GetValue(SocialLabelProperty); set => SetValue(SocialLabelProperty, value); }

    /// <inheritdoc cref="NavigateCommandProperty"/>
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }

    /// <inheritdoc cref="NavigateEvent"/>
    public event EventHandler<SiteFooterNavigateEventArgs>? Navigate
    {
        add => AddHandler(NavigateEvent, value);
        remove => RemoveHandler(NavigateEvent, value);
    }

    /// <inheritdoc cref="SubscribedEvent"/>
    public event EventHandler<SiteFooterSubscribedEventArgs>? Subscribed
    {
        add => AddHandler(SubscribedEvent, value);
        remove => RemoveHandler(SubscribedEvent, value);
    }

    #endregion

    #region Template properties

    internal static readonly DirectProperty<SiteFooter, bool> IsNarrowProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(IsNarrow), o => o.IsNarrow);

    internal static readonly DirectProperty<SiteFooter, bool> IsStackedProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(IsStacked), o => o.IsStacked);

    internal static readonly DirectProperty<SiteFooter, bool> IsColumnsProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(IsColumns), o => o.IsColumns);

    internal static readonly DirectProperty<SiteFooter, bool> IsMinimalProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(IsMinimal), o => o.IsMinimal);

    internal static readonly DirectProperty<SiteFooter, bool> IsLogoProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(IsLogo), o => o.IsLogo);

    internal static readonly DirectProperty<SiteFooter, bool> HasTopProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(HasTop), o => o.HasTop);

    internal static readonly DirectProperty<SiteFooter, bool> HasNewsletterProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(HasNewsletter), o => o.HasNewsletter);

    internal static readonly DirectProperty<SiteFooter, bool> HasTaglineProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(HasTagline), o => o.HasTagline);

    internal static readonly DirectProperty<SiteFooter, int> ColumnCountProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, int>(nameof(ColumnCount), o => o.ColumnCount);

    internal static readonly DirectProperty<SiteFooter, string> CopyrightProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, string>(nameof(Copyright), o => o.Copyright);

    internal static readonly DirectProperty<SiteFooter, string> MinimalCopyrightProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, string>(nameof(MinimalCopyright), o => o.MinimalCopyright);

    internal static readonly DirectProperty<SiteFooter, SiteFooterLink> BrandLinkProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, SiteFooterLink>(nameof(BrandLink), o => o.BrandLink);

    internal static readonly DirectProperty<SiteFooter, SiteFooterLink> StatusLinkProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, SiteFooterLink>(nameof(StatusLink), o => o.StatusLink);

    internal static readonly DirectProperty<SiteFooter, bool> HasSocialProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(HasSocial), o => o.HasSocial);

    internal static readonly DirectProperty<SiteFooter, bool> HasLegalProperty =
        AvaloniaProperty.RegisterDirect<SiteFooter, bool>(nameof(HasLegal), o => o.HasLegal);

    private bool _isNarrow;
    private bool _isStacked;
    private bool _isColumns = true;
    private bool _isMinimal;
    private bool _isLogo;
    private bool _hasTop = true;
    private bool _hasNewsletter = true;
    private bool _hasTagline = true;
    private int _columnCount = 3;
    private string _copyright = string.Empty;
    private string _minimalCopyright = string.Empty;
    private SiteFooterLink _brandLink = new();
    private SiteFooterLink _statusLink = new();
    private bool _hasSocial = true;
    private bool _hasLegal = true;

    /// <summary>At most 520 wide.</summary>
    internal bool IsNarrow { get => _isNarrow; private set => SetAndRaise(IsNarrowProperty, ref _isNarrow, value); }

    /// <summary>At most 760 wide.</summary>
    internal bool IsStacked { get => _isStacked; private set => SetAndRaise(IsStackedProperty, ref _isStacked, value); }

    internal bool IsColumns { get => _isColumns; private set => SetAndRaise(IsColumnsProperty, ref _isColumns, value); }

    internal bool IsMinimal { get => _isMinimal; private set => SetAndRaise(IsMinimalProperty, ref _isMinimal, value); }

    internal bool IsLogo { get => _isLogo; private set => SetAndRaise(IsLogoProperty, ref _isLogo, value); }

    /// <summary>The intro and link columns show (Columns and Logo).</summary>
    internal bool HasTop { get => _hasTop; private set => SetAndRaise(HasTopProperty, ref _hasTop, value); }

    internal bool HasNewsletter { get => _hasNewsletter; private set => SetAndRaise(HasNewsletterProperty, ref _hasNewsletter, value); }

    internal bool HasTagline { get => _hasTagline; private set => SetAndRaise(HasTaglineProperty, ref _hasTagline, value); }

    /// <summary>Columns across: Arc's grid has three tracks, two when narrow.</summary>
    internal int ColumnCount { get => _columnCount; private set => SetAndRaise(ColumnCountProperty, ref _columnCount, value); }

    /// <summary>"© {year} {brand}".</summary>
    internal string Copyright { get => _copyright; private set => SetAndRaise(CopyrightProperty, ref _copyright, value); }

    /// <summary>"© {year} {brand}. {tagline}".</summary>
    internal string MinimalCopyright { get => _minimalCopyright; private set => SetAndRaise(MinimalCopyrightProperty, ref _minimalCopyright, value); }

    internal SiteFooterLink BrandLink { get => _brandLink; private set => SetAndRaise(BrandLinkProperty, ref _brandLink, value); }

    internal SiteFooterLink StatusLink { get => _statusLink; private set => SetAndRaise(StatusLinkProperty, ref _statusLink, value); }

    internal bool HasSocial { get => _hasSocial; private set => SetAndRaise(HasSocialProperty, ref _hasSocial, value); }

    internal bool HasLegal { get => _hasLegal; private set => SetAndRaise(HasLegalProperty, ref _hasLegal, value); }

    #endregion

    private static readonly SiteFooterColumn[] s_sampleColumns =
    [
        new("Product", new("Components"), new("Blocks"), new("Templates"), new("Pricing")),
        new("Resources", new("Documentation"), new("Changelog"), new("Guides"), new("Figma kit", isExternal: true)),
        new("Company", new("About"), new("Customers"), new("Careers"), new("Contact"))
    ];

    private static readonly SiteFooterLink[] s_sampleLegal = [new("Privacy"), new("Terms"), new("Licenses")];
    private static readonly SiteFooterLink[] s_sampleSocial = [new("X", isExternal: true), new("GitHub", isExternal: true), new("LinkedIn", isExternal: true)];

    private readonly List<SiteFooterColumn> _watchedColumns = new();
    private INotifyCollectionChanged? _watchedSource;
    private INotifyCollectionChanged? _watchedLinks;
    // The data the footer shows can outlive it (a view model's lists, the static sample columns): it listens only while
    // it is in a visual tree and catches up when it comes back.
    private bool _watching;
    private Panel? _columnsPanel;
    private Panel? _legalPanel;
    private Panel? _socialPanel;
    private Panel? _minimalLinksPanel;
    private Panel? _minimalSocialPanel;
    private bool _rebuildQueued;
    private bool _templateApplied;

    static SiteFooter()
    {
        FocusableProperty.OverrideDefaultValue<SiteFooter>(false);
        VariantProperty.Changed.AddClassHandler<SiteFooter>((o, _) => o.OnVariantChanged());
        AffectsMeasure<SiteFooter>(VariantProperty);
    }

    /// <summary>Creates a footer with Arc's sample content.</summary>
    public SiteFooter()
    {
        _columns.CollectionChanged += OnDataChanged;
        _legalLinks.CollectionChanged += OnDataChanged;
        _socialLinks.CollectionChanged += OnDataChanged;
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.ContentInfo);
        UpdateDerived();
        UpdateVariantState();
    }

    /// <summary>The columns shown: <see cref="ColumnsSource"/>, else <see cref="Columns"/>, else Arc's sample columns.</summary>
    internal IReadOnlyList<SiteFooterColumn> EffectiveColumns()
    {
        if (ColumnsSource is { } source)
        {
            return source as IReadOnlyList<SiteFooterColumn> ?? source.ToList();
        }
        return _columns.Count > 0 ? _columns : s_sampleColumns;
    }

    /// <summary>The legal links shown.</summary>
    internal IReadOnlyList<SiteFooterLink> EffectiveLegal() => _legalLinks.Count > 0 ? _legalLinks : s_sampleLegal;

    /// <summary>The social links shown.</summary>
    internal IReadOnlyList<SiteFooterLink> EffectiveSocial() => _socialLinks.Count > 0 ? _socialLinks : s_sampleSocial;

    /// <summary>The one-row links of Minimal: <see cref="Links"/>, else the first link of each column plus the first two legal links.</summary>
    internal IReadOnlyList<SiteFooterLink> EffectiveLinks()
    {
        if (Links is { } links)
        {
            return links;
        }
        var result = new List<SiteFooterLink>();
        foreach (var column in EffectiveColumns())
        {
            if (column.Links.Count > 0)
            {
                result.Add(column.Links[0]);
            }
        }
        var legal = EffectiveLegal();
        for (var i = 0; i < Math.Min(2, legal.Count); i++)
        {
            result.Add(legal[i]);
        }
        return result;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _columnsPanel = e.NameScope.Find<Panel>(ColumnsPart);
        _legalPanel = e.NameScope.Find<Panel>(LegalPart);
        _socialPanel = e.NameScope.Find<Panel>(SocialPart);
        _minimalLinksPanel = e.NameScope.Find<Panel>(MinimalLinksPart);
        _minimalSocialPanel = e.NameScope.Find<Panel>(MinimalSocialPart);
        _templateApplied = true;
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ColumnsSourceProperty)
        {
            WatchSource(change.GetNewValue<IEnumerable<SiteFooterColumn>?>());
            QueueRebuild();
        }
        else if (property == LinksProperty)
        {
            WatchLinks(change.GetNewValue<AvaloniaList<SiteFooterLink>?>());
            QueueRebuild();
        }
        else if (property == BrandNameProperty || property == BrandHrefProperty || property == TaglineProperty
                 || property == YearProperty || property == StatusLabelProperty || property == StatusHrefProperty
                 || property == IsNewsletterVisibleProperty || property == IsStatusVisibleProperty)
        {
            UpdateDerived();
        }
    }

    private void OnVariantChanged()
    {
        UpdateVariantState();
        UpdateDerived();
        QueueRebuild();
    }

    private void UpdateVariantState()
    {
        var variant = Variant;
        IsColumns = variant == SiteFooterVariant.Columns;
        IsMinimal = variant == SiteFooterVariant.Minimal;
        IsLogo = variant == SiteFooterVariant.Logo;
        HasTop = variant != SiteFooterVariant.Minimal;
        PseudoClasses.Set(ColumnsPseudoClass, IsColumns);
        PseudoClasses.Set(MinimalPseudoClass, IsMinimal);
        PseudoClasses.Set(LogoPseudoClass, IsLogo);
    }

    private void UpdateDerived()
    {
        var year = Year ?? DateTime.Now.Year;
        Copyright = $"© {year} {BrandName}";
        MinimalCopyright = string.IsNullOrEmpty(Tagline) ? Copyright : $"{Copyright}. {Tagline}";
        HasTagline = !string.IsNullOrEmpty(Tagline);
        HasNewsletter = IsNewsletterVisible && Variant == SiteFooterVariant.Columns;
        BrandLink = new SiteFooterLink(BrandName, BrandHref);
        StatusLink = new SiteFooterLink(StatusLabel, StatusHref);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchSource(ColumnsSource);
        WatchLinks(Links);
        // The column link lists are watched by Rebuild, which also picks up whatever changed while the footer was away.
        QueueRebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        WatchSource(null);
        WatchLinks(null);
        UnwatchColumns();
    }

    private void WatchSource(IEnumerable<SiteFooterColumn>? source)
    {
        if (_watchedSource is not null)
        {
            _watchedSource.CollectionChanged -= OnDataChanged;
        }
        _watchedSource = _watching ? source as INotifyCollectionChanged : null;
        if (_watchedSource is not null)
        {
            _watchedSource.CollectionChanged += OnDataChanged;
        }
    }

    private void WatchLinks(AvaloniaList<SiteFooterLink>? links)
    {
        if (_watchedLinks is not null)
        {
            _watchedLinks.CollectionChanged -= OnDataChanged;
        }
        _watchedLinks = _watching ? links : null;
        if (_watchedLinks is not null)
        {
            _watchedLinks.CollectionChanged += OnDataChanged;
        }
    }

    private void UnwatchColumns()
    {
        foreach (var column in _watchedColumns)
        {
            column.Links.CollectionChanged -= OnDataChanged;
        }
        _watchedColumns.Clear();
    }

    private void OnDataChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRebuild();

    private void QueueRebuild()
    {
        if (!_templateApplied || _rebuildQueued)
        {
            return;
        }
        _rebuildQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _rebuildQueued = false;
            Rebuild();
        }, DispatcherPriority.Normal);
    }

    /// <summary>Fills the link lists from the data now (the footer does this itself when its data changes).</summary>
    internal void Rebuild()
    {
        if (!_templateApplied)
        {
            return;
        }
        var columns = EffectiveColumns();
        UnwatchColumns();
        // The sample columns never change: nothing to watch.
        if (_watching && !ReferenceEquals(columns, s_sampleColumns))
        {
            foreach (var column in columns)
            {
                column.Links.CollectionChanged += OnDataChanged;
                _watchedColumns.Add(column);
            }
        }
        FillColumns(columns);
        var legal = EffectiveLegal();
        var social = EffectiveSocial();
        HasLegal = legal.Count > 0;
        HasSocial = social.Count > 0;
        FillLinks(_legalPanel, legal, SiteFooterNavigationKind.Legal, LegalLabel);
        FillLinks(_socialPanel, social, SiteFooterNavigationKind.Social, SocialLabel);
        FillLinks(_minimalLinksPanel, EffectiveLinks(), SiteFooterNavigationKind.Column, NavigationLabel);
        FillLinks(_minimalSocialPanel, social, SiteFooterNavigationKind.Social, SocialLabel);
    }

    private void FillColumns(IReadOnlyList<SiteFooterColumn> columns)
    {
        if (_columnsPanel is null)
        {
            return;
        }
        _columnsPanel.Children.Clear();
        foreach (var column in columns)
        {
            _columnsPanel.Children.Add(new SiteFooterColumnView { Column = column });
        }
    }

    private static void FillLinks(Panel? panel, IReadOnlyList<SiteFooterLink> links, SiteFooterNavigationKind kind, string label)
    {
        if (panel is null)
        {
            return;
        }
        panel.Children.Clear();
        foreach (var link in links)
        {
            panel.Children.Add(new SiteFooterLinkButton { Link = link, Kind = kind });
        }
        AutomationProperties.SetName(panel, label);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        // Arc's container queries: the footer's own width, not the window's.
        var width = result.Width;
        var narrow = width > 0 && width <= NarrowWidth;
        var stacked = width > 0 && width <= StackedWidth;
        if (narrow != IsNarrow || stacked != IsStacked)
        {
            IsNarrow = narrow;
            IsStacked = stacked;
            ColumnCount = narrow ? 2 : 3;
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            PseudoClasses.Set(StackedPseudoClass, stacked);
            // The two-column / stacked layouts change the desired size: measure again in the same pass.
            InvalidateMeasure();
        }
        return result;
    }

    /// <summary>
    /// Raises <see cref="NavigateEvent"/> for <paramref name="link"/>, runs <see cref="NavigateCommand"/>, and, when nobody
    /// handled the event, opens an absolute http, https or mailto address.
    /// </summary>
    internal void RaiseNavigate(SiteFooterLink link, SiteFooterNavigationKind kind)
    {
        var args = new SiteFooterNavigateEventArgs(NavigateEvent, this, link, kind);
        RaiseEvent(args);
        if (NavigateCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
        if (!args.Handled)
        {
            SiteLinks.TryLaunch(this, link.Href);
        }
    }

    /// <summary>The newsletter of the template, or null while it is hidden (tests read it).</summary>
    internal SiteFooterNewsletter? Newsletter => this.GetVisualDescendants().OfType<SiteFooterNewsletter>().FirstOrDefault();
}

/// <summary>One column of the footer: the title above its links (12 between, 8 between the links).</summary>
[TemplatePart(LinksPart, typeof(StackPanel))]
internal sealed class SiteFooterColumnView : TemplatedControl
{
    public const string LinksPart = "PART_Links";

    public static readonly StyledProperty<SiteFooterColumn?> ColumnProperty =
        AvaloniaProperty.Register<SiteFooterColumnView, SiteFooterColumn?>(nameof(Column));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SiteFooterColumnView, string?>(nameof(Title));

    private StackPanel? _links;

    static SiteFooterColumnView()
    {
        FocusableProperty.OverrideDefaultValue<SiteFooterColumnView>(false);
    }

    public SiteFooterColumn? Column { get => GetValue(ColumnProperty); set => SetValue(ColumnProperty, value); }

    /// <summary>The column's title (the heading).</summary>
    public string? Title { get => GetValue(TitleProperty); private set => SetValue(TitleProperty, value); }

    protected override Type StyleKeyOverride => typeof(SiteFooterColumnView);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColumnProperty)
        {
            Title = change.GetNewValue<SiteFooterColumn?>()?.Title;
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _links = e.NameScope.Find<StackPanel>(LinksPart);
        if (_links is null)
        {
            return;
        }
        _links.Children.Clear();
        AutomationProperties.SetControlTypeOverride(_links, AutomationControlType.List);
        AutomationProperties.SetName(_links, Title ?? string.Empty);
        if (Column is { } column)
        {
            foreach (var link in column.Links)
            {
                _links.Children.Add(new SiteFooterLinkButton { Link = link, Kind = SiteFooterNavigationKind.Column });
            }
        }
    }
}
