using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A custom domain setup card. The customer adds a domain and gets the DNS records to create, each with a copy button;
/// a live checker then polls the resolvers, marks every record valid, not found or misconfigured (with what was found and a
/// one-line fix) and finishes with the domain verified and its SSL certificate issued.
/// The three server steps are async delegates: <see cref="GetRecordsAction"/> lists the records, <see cref="VerifyAction"/> is
/// one check and <see cref="IssueCertificateAction"/> runs once every record is valid. The card does the polling, stops on
/// "all valid" or any conflicting record and cancels everything when it is removed or leaves the window. With none of them
/// set it plays a simulated flow (add, check, a conflicting record, fix, verified) until someone clicks or types in it.
/// <code>
/// &lt;arc:DomainVerify Title="Custom domain" DefaultDomain="" /&gt;
/// </code>
/// </summary>
public partial class DomainVerify : TemplatedControl
{
    #region Properties

    /// <summary>The card's heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DomainVerify, string?>(nameof(Title), "Custom domain");

    /// <summary>The line under the heading (at most 520 wide); null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<DomainVerify, string?>(nameof(Description), "Serve your workspace from a domain you own.");

    /// <summary>
    /// The domain the field starts with. A protocol, path, port and trailing dot are stripped before the domain is
    /// checked. Empty starts with an empty field.
    /// </summary>
    public static readonly StyledProperty<string?> DefaultDomainProperty =
        AvaloniaProperty.Register<DomainVerify, string?>(nameof(DefaultDomain), "app.acme.com");

    /// <summary>Who hosts the domain's DNS, shown as "DNS hosted at {name}" with a small mark; null hides the line.</summary>
    public static readonly StyledProperty<DomainProvider?> ProviderProperty =
        AvaloniaProperty.Register<DomainVerify, DomainProvider?>(nameof(Provider), new DomainProvider("Cloudflare"));

    /// <summary>
    /// Lists the records for a domain: awaited when the domain is added, with a token that removing the domain or the card
    /// leaving the window cancels. An exception returns to the field with the message under it and raises
    /// <see cref="Failed"/>. Null simulates a server (a CNAME, or an A record on an apex domain, an ownership TXT and a CAA).
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<IReadOnlyList<DomainRecord>>>?> GetRecordsActionProperty =
        AvaloniaProperty.Register<DomainVerify, Func<string, CancellationToken, Task<IReadOnlyList<DomainRecord>>>?>(nameof(GetRecordsAction));

    /// <summary>
    /// One check of the records: awaited on every poll with the domain, the records and which attempt of which run this is.
    /// Return a verdict per record. Return <see cref="DomainRecordStatus.Missing"/> while a record propagates and
    /// <see cref="DomainRecordStatus.Invalid"/> only for a conflicting value. The card polls while any record is missing and
    /// stops when all are valid or any is invalid. An exception stops the run, shows the message and raises
    /// <see cref="Failed"/>. Null plays a scripted check.
    /// </summary>
    public static readonly StyledProperty<Func<string, IReadOnlyList<DomainRecord>, DomainVerifyContext, CancellationToken, Task<DomainVerifyResult>>?> VerifyActionProperty =
        AvaloniaProperty.Register<DomainVerify, Func<string, IReadOnlyList<DomainRecord>, DomainVerifyContext, CancellationToken, Task<DomainVerifyResult>>?>(nameof(VerifyAction));

    /// <summary>
    /// Issues the SSL certificate once every record is valid; the domain row says "Issuing SSL certificate" until it
    /// returns. Null simulates an issuer.
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<DomainCertificate>>?> IssueCertificateActionProperty =
        AvaloniaProperty.Register<DomainVerify, Func<string, CancellationToken, Task<DomainCertificate>>?>(nameof(IssueCertificateAction));

    /// <summary>Time between two checks; null uses 800ms while the check is simulated and 5s with a <see cref="VerifyAction"/>.</summary>
    public static readonly StyledProperty<TimeSpan?> PollIntervalProperty =
        AvaloniaProperty.Register<DomainVerify, TimeSpan?>(nameof(PollInterval));

    /// <summary>
    /// Plays the simulated flow once the card is on screen: add, check, fix, verified. Any click or key press inside the
    /// card hands control to the person. Null plays only while no action is set.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<DomainVerify, bool?>(nameof(AutoPlay));

    /// <summary>Run with the <see cref="DomainVerifiedEventArgs"/> once the certificate is issued.</summary>
    public static readonly StyledProperty<ICommand?> VerifiedCommandProperty =
        AvaloniaProperty.Register<DomainVerify, ICommand?>(nameof(VerifiedCommand));

    /// <summary>Run with the <see cref="DomainRemovedEventArgs"/> when the domain is removed.</summary>
    public static readonly StyledProperty<ICommand?> RemovedCommandProperty =
        AvaloniaProperty.Register<DomainVerify, ICommand?>(nameof(RemovedCommand));

    /// <summary>Draws a bordered card with rounded corners (true); false lays the block flat on the page.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<DomainVerify, bool>(nameof(IsFramed), true);

    /// <summary>Below this width of the card the records stack and the column header leaves (544).</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<DomainVerify, double>(nameof(CompactWidth), 544);

    /// <summary>The padding of a record row; the theme sets it from the token.</summary>
    public static readonly StyledProperty<Thickness> RowPaddingProperty =
        AvaloniaProperty.Register<DomainVerify, Thickness>(nameof(RowPadding), new Thickness(20, 12));

    /// <summary>The padding of a record row in the compact layout; the theme sets it from the token.</summary>
    public static readonly StyledProperty<Thickness> CompactRowPaddingProperty =
        AvaloniaProperty.Register<DomainVerify, Thickness>(nameof(CompactRowPadding), new Thickness(16, 12));

    /// <summary>Every word the card shows or announces.</summary>
    public static readonly StyledProperty<DomainVerifyLabels> LabelsProperty =
        AvaloniaProperty.Register<DomainVerify, DomainVerifyLabels>(nameof(Labels), new DomainVerifyLabels());

    /// <summary>Where the card is in its flow.</summary>
    public static readonly DirectProperty<DomainVerify, DomainVerifyPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<DomainVerify, DomainVerifyPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The domain that was added (host name only); null before one was added and after it was removed.</summary>
    public static readonly DirectProperty<DomainVerify, string?> DomainProperty =
        AvaloniaProperty.RegisterDirect<DomainVerify, string?>(nameof(Domain), o => o.Domain);

    /// <summary>The certificate once it was issued; null before.</summary>
    public static readonly DirectProperty<DomainVerify, DomainCertificate?> CertificateProperty =
        AvaloniaProperty.RegisterDirect<DomainVerify, DomainCertificate?>(nameof(Certificate), o => o.Certificate);

    #endregion

    #region Events

    /// <summary>Raised on every phase change.</summary>
    public static readonly RoutedEvent<DomainVerifyPhaseChangedEventArgs> PhaseChangedEvent =
        RoutedEvent.Register<DomainVerify, DomainVerifyPhaseChangedEventArgs>(nameof(PhaseChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once every record is valid and the certificate was issued.</summary>
    public static readonly RoutedEvent<DomainVerifiedEventArgs> VerifiedEvent =
        RoutedEvent.Register<DomainVerify, DomainVerifiedEventArgs>(nameof(Verified), RoutingStrategies.Bubble);

    /// <summary>Raised when the domain is removed (the close button or "Add another").</summary>
    public static readonly RoutedEvent<DomainRemovedEventArgs> RemovedEvent =
        RoutedEvent.Register<DomainVerify, DomainRemovedEventArgs>(nameof(Removed), RoutingStrategies.Bubble);

    /// <summary>Raised when one of the actions threw; the card shows the message and can be tried again.</summary>
    public static readonly RoutedEvent<DomainVerifyFailedEventArgs> FailedEvent =
        RoutedEvent.Register<DomainVerify, DomainVerifyFailedEventArgs>(nameof(Failed), RoutingStrategies.Bubble);

    /// <summary>Raised on every phase change.</summary>
    public event EventHandler<DomainVerifyPhaseChangedEventArgs>? PhaseChanged
    {
        add => AddHandler(PhaseChangedEvent, value);
        remove => RemoveHandler(PhaseChangedEvent, value);
    }

    /// <summary>Raised once every record is valid and the certificate was issued.</summary>
    public event EventHandler<DomainVerifiedEventArgs>? Verified
    {
        add => AddHandler(VerifiedEvent, value);
        remove => RemoveHandler(VerifiedEvent, value);
    }

    /// <summary>Raised when the domain is removed (the close button or "Add another").</summary>
    public event EventHandler<DomainRemovedEventArgs>? Removed
    {
        add => AddHandler(RemovedEvent, value);
        remove => RemoveHandler(RemovedEvent, value);
    }

    /// <summary>Raised when one of the actions threw; the card shows the message and can be tried again.</summary>
    public event EventHandler<DomainVerifyFailedEventArgs>? Failed
    {
        add => AddHandler(FailedEvent, value);
        remove => RemoveHandler(FailedEvent, value);
    }

    #endregion

    private DomainVerifyPhase _phase;
    private string? _domain;
    private DomainCertificate? _certificate;

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="DefaultDomainProperty"/>
    public string? DefaultDomain { get => GetValue(DefaultDomainProperty); set => SetValue(DefaultDomainProperty, value); }

    /// <inheritdoc cref="ProviderProperty"/>
    public DomainProvider? Provider { get => GetValue(ProviderProperty); set => SetValue(ProviderProperty, value); }

    /// <inheritdoc cref="GetRecordsActionProperty"/>
    public Func<string, CancellationToken, Task<IReadOnlyList<DomainRecord>>>? GetRecordsAction
    {
        get => GetValue(GetRecordsActionProperty);
        set => SetValue(GetRecordsActionProperty, value);
    }

    /// <inheritdoc cref="VerifyActionProperty"/>
    public Func<string, IReadOnlyList<DomainRecord>, DomainVerifyContext, CancellationToken, Task<DomainVerifyResult>>? VerifyAction
    {
        get => GetValue(VerifyActionProperty);
        set => SetValue(VerifyActionProperty, value);
    }

    /// <inheritdoc cref="IssueCertificateActionProperty"/>
    public Func<string, CancellationToken, Task<DomainCertificate>>? IssueCertificateAction
    {
        get => GetValue(IssueCertificateActionProperty);
        set => SetValue(IssueCertificateActionProperty, value);
    }

    /// <inheritdoc cref="PollIntervalProperty"/>
    public TimeSpan? PollInterval { get => GetValue(PollIntervalProperty); set => SetValue(PollIntervalProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="VerifiedCommandProperty"/>
    public ICommand? VerifiedCommand { get => GetValue(VerifiedCommandProperty); set => SetValue(VerifiedCommandProperty, value); }

    /// <inheritdoc cref="RemovedCommandProperty"/>
    public ICommand? RemovedCommand { get => GetValue(RemovedCommandProperty); set => SetValue(RemovedCommandProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="RowPaddingProperty"/>
    public Thickness RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }

    /// <inheritdoc cref="CompactRowPaddingProperty"/>
    public Thickness CompactRowPadding { get => GetValue(CompactRowPaddingProperty); set => SetValue(CompactRowPaddingProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public DomainVerifyLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public DomainVerifyPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="DomainProperty"/>
    public string? Domain { get => _domain; private set => SetAndRaise(DomainProperty, ref _domain, value); }

    /// <inheritdoc cref="CertificateProperty"/>
    public DomainCertificate? Certificate { get => _certificate; private set => SetAndRaise(CertificateProperty, ref _certificate, value); }
}
