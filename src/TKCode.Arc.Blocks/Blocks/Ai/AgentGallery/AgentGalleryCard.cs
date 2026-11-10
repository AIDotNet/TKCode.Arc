using TKCode.Arc.DesignTokens;
using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One agent in <see cref="AgentGallery"/>'s grid: mark, name and maker, a Featured badge, the pitch (search hits
/// marked), the first two tags, the rating and installs, and an install button whose label follows the install state.
/// The card itself is a button (Enter, Space or a click opens the detail view); its install button is a separate
/// control the gallery handles. Cards are pooled: <see cref="Show"/> repaints a card for another agent.
/// </summary>
[TemplatePart(RingHostPart, typeof(Panel))]
[TemplatePart(MarkPart, typeof(AgentGalleryMark))]
[TemplatePart(InstallPart, typeof(Button))]
[PseudoClasses(PressedPseudoClass, InstalledPseudoClass)]
internal sealed class AgentGalleryCard : TemplatedControl
{
    public const string RingHostPart = "PART_RingHost";
    public const string MarkPart = "PART_Mark";
    public const string NamePart = "PART_Name";
    public const string MakerPart = "PART_Maker";
    public const string VerifiedPart = "PART_Verified";
    public const string FeaturedPart = "PART_Featured";
    public const string PitchPart = "PART_Pitch";
    public const string TagsPart = "PART_Tags";
    public const string RatingPart = "PART_Rating";
    public const string InstallsPart = "PART_Installs";
    public const string InstallPart = "PART_Install";

    public const string PressedPseudoClass = ":pressed";
    public const string InstalledPseudoClass = ":installed";

    /// <summary>Raised when the card is activated (click, Enter or Space): the gallery opens the detail view.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ActivatedEvent =
        RoutedEvent.Register<AgentGalleryCard, RoutedEventArgs>("Activated", RoutingStrategies.Bubble);

    /// <summary>Raised when the card's install button is pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> InstallRequestedEvent =
        RoutedEvent.Register<AgentGalleryCard, RoutedEventArgs>("InstallRequested", RoutingStrategies.Bubble);

    private readonly AgentGalleryTag[] _tags = [new(), new()];
    private LazyFocusRing? _focusRing;
    private AgentGalleryMark? _mark;
    private HighlightedText? _name;
    private TextBlock? _maker;
    private Control? _verified;
    private Control? _featured;
    private HighlightedText? _pitch;
    private Panel? _tagsHost;
    private TextBlock? _rating;
    private TextBlock? _installs;
    private Button? _install;
    private bool _pressed;
    private bool _isCurrent;

    static AgentGalleryCard()
    {
        FocusableProperty.OverrideDefaultValue<AgentGalleryCard>(true);
        ClipToBoundsProperty.OverrideDefaultValue<AgentGalleryCard>(false);
    }

    public AgentGalleryCard()
    {
        KeyboardNavigation.SetIsTabStop(this, false);
    }

    /// <summary>The agent shown (null for a spare card of the pool).</summary>
    public AgentGalleryItem? Agent { get; private set; }

    /// <summary>The install state shown.</summary>
    public AgentGalleryInstallState State { get; private set; }

    /// <summary>The install button (null until the template is applied).</summary>
    public Button? InstallButton => _install;

    /// <summary>The name text (tests read its marks).</summary>
    internal HighlightedText? NameText => _name;

    /// <summary>The pitch text (tests read its marks).</summary>
    internal HighlightedText? PitchText => _pitch;

    /// <summary>The rating text.</summary>
    internal TextBlock? RatingText => _rating;

    /// <summary>The installs text.</summary>
    internal TextBlock? InstallsText => _installs;

    /// <summary>Whether the Featured badge shows.</summary>
    internal bool IsFeaturedShown => _featured?.IsVisible == true;

    /// <summary>The tags shown, in order.</summary>
    internal IEnumerable<string> ShownTags => _tags.Where(t => t.IsVisible).Select(t => t.Text ?? string.Empty);

    /// <summary>Whether this card is the grid's tab stop (its install button is one too).</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            _isCurrent = value;
            KeyboardNavigation.SetIsTabStop(this, value);
            if (_install is not null)
            {
                KeyboardNavigation.SetIsTabStop(_install, value);
            }
        }
    }

    private AgentGalleryLabels _labels = AgentGalleryLabels.Default;
    private string? _query;
    private CultureInfo _culture = CultureInfo.InvariantCulture;

    /// <summary>Repaints the card for <paramref name="agent"/>.</summary>
    public void Show(AgentGalleryItem agent, AgentGalleryInstallState state, string? query, AgentGalleryLabels labels, CultureInfo culture)
    {
        Agent = agent;
        State = state;
        _query = query;
        _labels = labels;
        _culture = culture;
        Apply();
    }

    /// <summary>Changes only the install state (the rest stays).</summary>
    public void SetState(AgentGalleryInstallState state)
    {
        if (State == state)
        {
            return;
        }
        State = state;
        ApplyState();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_install is not null)
        {
            _install.Click -= OnInstallClick;
        }
        _mark = e.NameScope.Find<AgentGalleryMark>(MarkPart);
        _name = e.NameScope.Find<HighlightedText>(NamePart);
        _maker = e.NameScope.Find<TextBlock>(MakerPart);
        _verified = e.NameScope.Find<Control>(VerifiedPart);
        _featured = e.NameScope.Find<Control>(FeaturedPart);
        _pitch = e.NameScope.Find<HighlightedText>(PitchPart);
        _tagsHost = e.NameScope.Find<Panel>(TagsPart);
        _rating = e.NameScope.Find<TextBlock>(RatingPart);
        _installs = e.NameScope.Find<TextBlock>(InstallsPart);
        _install = e.NameScope.Find<Button>(InstallPart);
        if (_install is not null)
        {
            _install.Click += OnInstallClick;
            KeyboardNavigation.SetIsTabStop(_install, _isCurrent);
        }
        if (_tagsHost is not null)
        {
            foreach (var tag in _tags)
            {
                (tag.Parent as Panel)?.Children.Remove(tag);
                _tagsHost.Children.Add(tag);
            }
        }
        _focusRing ??= new LazyFocusRing(this, AgentGalleryTokens.Key(AgentGalleryTokenKind.FocusRingOutset),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep24));
        _focusRing.Attach(e.NameScope.Find<Panel>(RingHostPart), below: false);
        Apply();
    }

    private void Apply()
    {
        if (Agent is not { } agent)
        {
            return;
        }
        _mark?.SetAgent(agent);
        if (_name is not null)
        {
            _name.Text = agent.Name;
            _name.Query = _query;
        }
        if (_maker is not null)
        {
            _maker.Text = agent.Maker is { Length: > 0 } maker ? _labels.ByMaker(maker) : null;
            _maker.IsVisible = agent.Maker is { Length: > 0 };
        }
        if (_verified is not null)
        {
            _verified.IsVisible = agent.IsVerified && agent.Maker is { Length: > 0 };
            Tooltip.SetTip(_verified, _labels.Verified);
        }
        if (_featured is not null)
        {
            _featured.IsVisible = agent.IsFeatured;
            if (_featured is ContentControl badge)
            {
                badge.Content = _labels.Featured;
            }
        }
        if (_pitch is not null)
        {
            _pitch.Text = agent.Pitch;
            _pitch.Query = _query;
        }
        for (var i = 0; i < _tags.Length; i++)
        {
            var has = i < agent.Tags.Count;
            _tags[i].IsVisible = has;
            _tags[i].Text = has ? agent.Tags[i] : null;
        }
        if (_tagsHost is not null)
        {
            _tagsHost.IsVisible = agent.Tags.Count > 0;
        }
        if (_rating is not null)
        {
            _rating.Text = AgentGalleryFormat.Rating(agent, _labels, _culture);
        }
        if (_installs is not null)
        {
            _installs.Text = _labels.Installs(AgentGalleryFormat.Compact(agent.Installs, _culture));
        }
        ApplyState();
    }

    private void ApplyState()
    {
        if (Agent is not { } agent)
        {
            return;
        }
        PseudoClasses.Set(InstalledPseudoClass, State == AgentGalleryInstallState.Installed);
        if (_install is not null)
        {
            _install.Content = State switch
            {
                AgentGalleryInstallState.Installing => _labels.Installing,
                AgentGalleryInstallState.Installed => _labels.Installed,
                AgentGalleryInstallState.Failed => _labels.Retry,
                _ => _labels.Install
            };
            _install.IsLoading = State == AgentGalleryInstallState.Installing;
            _install.Variant = State == AgentGalleryInstallState.Installed ? ButtonVariant.Ghost : ButtonVariant.Secondary;
            AutomationProperties.SetName(_install, State == AgentGalleryInstallState.Installed
                ? _labels.CardName(agent, _labels.Installed)
                : _labels.InstallName(agent.Name));
        }
        AutomationProperties.SetName(this, _labels.CardName(agent, StateWord()));
        AutomationProperties.SetHelpText(this, agent.Pitch);
    }

    private string? StateWord() => State switch
    {
        AgentGalleryInstallState.Installed => _labels.Installed,
        AgentGalleryInstallState.Installing => _labels.Installing,
        AgentGalleryInstallState.Failed => _labels.Retry,
        _ => null
    };

    private void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(InstallRequestedEvent, this));
    }

    /// <summary>Opens the card as a click would.</summary>
    public void Activate() => RaiseEvent(new RoutedEventArgs(ActivatedEvent, this));

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source != this)
        {
            return;
        }
        if (e.Key is Key.Enter or Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Activate();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _pressed = true;
        PseudoClasses.Set(PressedPseudoClass, true);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        PseudoClasses.Set(PressedPseudoClass, false);
        var point = e.GetPosition(this);
        if (new Rect(Bounds.Size).Contains(point))
        {
            e.Handled = true;
            Activate();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _pressed = false;
        PseudoClasses.Set(PressedPseudoClass, false);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AgentGalleryCardPeer(this);
}

/// <summary>Reports a card as a button named by its agent (and install state) that can be invoked.</summary>
internal sealed class AgentGalleryCardPeer(AgentGalleryCard owner) : ControlAutomationPeer(owner), Avalonia.Automation.Provider.IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    protected override string? GetNameCore() => AutomationProperties.GetName(Owner);

    protected override bool IsContentElementCore() => true;

    public void Invoke() => ((AgentGalleryCard)Owner).Activate();
}

/// <summary>The number and date formats of the gallery.</summary>
internal static class AgentGalleryFormat
{
    /// <summary>"4.8 (2,310)", or the not-rated word for an agent without ratings.</summary>
    public static string Rating(AgentGalleryItem agent, AgentGalleryLabels labels, CultureInfo culture)
        => agent.RatingCount <= 0 || agent.Rating <= 0
            ? labels.NotRated
            : $"{agent.Rating.ToString("0.0", culture)} ({agent.RatingCount.ToString("N0", culture)})";

    /// <summary>Installs in compact form: 950, 8.9K, 48.2K, 1.2M.</summary>
    public static string Compact(long value, CultureInfo culture)
    {
        if (value < 1_000)
        {
            return value.ToString("N0", culture);
        }
        if (value < 1_000_000)
        {
            return (Math.Floor(value / 100.0) / 10).ToString("0.#", culture) + "K";
        }
        return (Math.Floor(value / 100_000.0) / 10).ToString("0.#", culture) + "M";
    }

    /// <summary>A short date ("Mar 14, 2026" in en-US).</summary>
    public static string Date(DateOnly date, CultureInfo culture)
        => date.ToDateTime(TimeOnly.MinValue).ToString(culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "MMM d, yyyy" : "d", culture);
}
