using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A payment card as it appears in a wallet: the drawn card face (masked number, holder, expiry and an invented network
/// mark), the available balance with the month's spending against its limit, the latest transactions, and two actions.
/// Freeze covers the face with frost and can wait for your server (<see cref="SetFrozenAsync"/>); Show details reveals the
/// full number and security code, offers to copy the number and hides them again after <see cref="DetailsTimeout"/>.
/// <code>
/// &lt;arc:WalletCard HolderName="Ines Moreau" CardNumber="4929 1820 5531 4821" Expiry="08/29" SecurityCode="317"
///                 Balance="2481.56" MonthlyLimit="3000" SpentThisMonth="1240.18" /&gt;
/// </code>
/// </summary>
[TemplatePart(FacePart, typeof(WalletCardFacePanel))]
[TemplatePart(ArtPart, typeof(WalletCardArt))]
[TemplatePart(FreezePart, typeof(Button))]
[TemplatePart(DetailsPart, typeof(Button))]
[TemplatePart(CopyPart, typeof(CopyButton))]
[TemplatePart(RowsPart, typeof(StackPanel))]
[TemplatePart(MeterPart, typeof(WalletCardMeter))]
[TemplatePart(AnnouncerPart, typeof(FinanceAnnouncer))]
[PseudoClasses(FrozenPseudoClass, DetailsPseudoClass, CompactPseudoClass, BusyPseudoClass)]
public class WalletCard : TemplatedControl
{
    public const string FacePart = "PART_Face";
    public const string ArtPart = "PART_Art";
    public const string FreezePart = "PART_Freeze";
    public const string DetailsPart = "PART_Details";
    public const string CopyPart = "PART_Copy";
    public const string RowsPart = "PART_Rows";
    public const string MeterPart = "PART_Meter";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The card is frozen: frost covers the face and the frozen note shows.</summary>
    public const string FrozenPseudoClass = ":frozen";

    /// <summary>The full number and security code are shown on the face.</summary>
    public const string DetailsPseudoClass = ":details";

    /// <summary>The block is narrower than its compact threshold: the face sits above the balance.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>A freeze or unfreeze is waiting for <see cref="SetFrozenAsync"/>.</summary>
    public const string BusyPseudoClass = ":busy";

    // Lucide "snowflake" (ISC).
    private static readonly Geometry s_snowflake = ArcIcons.Parse(
        "M10 20l-1.25-2.5L6 18 M10 4 8.75 6.5 6 6 M14 20l1.25-2.5L18 18 M14 4l1.25 2.5L18 6 M17 21l-3-6h-4 M17 3l-3 6 1.5 3 M2 12h6.5L10 9 M20 10l-1.5 2 1.5 2 M22 12h-6.5L14 15 M4 10l1.5 2L4 14 M7 21l3-6-1.5-3 M7 3l3 6h4");

    #region Properties

    /// <summary>The issuer printed at the top left of the face (an invented bank by default).</summary>
    public static readonly StyledProperty<string?> IssuerNameProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(IssuerName), "Fernhill");

    /// <summary>The card network, printed small at the top right and read by screen readers; the mark is drawn.</summary>
    public static readonly StyledProperty<string> NetworkNameProperty =
        AvaloniaProperty.Register<WalletCard, string>(nameof(NetworkName), "Orbit");

    /// <summary>The cardholder's name as printed.</summary>
    public static readonly StyledProperty<string?> HolderNameProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(HolderName));

    /// <summary>The full card number; only its digits count. The face shows the last four unless details are shown.</summary>
    public static readonly StyledProperty<string?> CardNumberProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(CardNumber));

    /// <summary>The expiry as printed ("08/29").</summary>
    public static readonly StyledProperty<string?> ExpiryProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(Expiry));

    /// <summary>The security code, shown only with the details.</summary>
    public static readonly StyledProperty<string?> SecurityCodeProperty =
        AvaloniaProperty.Register<WalletCard, string?>(nameof(SecurityCode));

    /// <summary>The printed finish of the face.</summary>
    public static readonly StyledProperty<WalletCardFinish> FinishProperty =
        AvaloniaProperty.Register<WalletCard, WalletCardFinish>(nameof(Finish));

    /// <summary>The money available to spend.</summary>
    public static readonly StyledProperty<decimal> BalanceProperty =
        AvaloniaProperty.Register<WalletCard, decimal>(nameof(Balance));

    /// <summary>The monthly spending limit; 0 hides the meter and its line.</summary>
    public static readonly StyledProperty<decimal> MonthlyLimitProperty =
        AvaloniaProperty.Register<WalletCard, decimal>(nameof(MonthlyLimit));

    /// <summary>What was spent this month, measured against <see cref="MonthlyLimit"/>.</summary>
    public static readonly StyledProperty<decimal> SpentThisMonthProperty =
        AvaloniaProperty.Register<WalletCard, decimal>(nameof(SpentThisMonth));

    /// <summary>The symbol written before amounts ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<WalletCard, string>(nameof(CurrencySymbol), "$");

    /// <summary>Whether the card is frozen (two-way). Setting it applies at once; the button goes through <see cref="SetFrozenAsync"/>.</summary>
    public static readonly StyledProperty<bool> IsFrozenProperty =
        AvaloniaProperty.Register<WalletCard, bool>(nameof(IsFrozen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the full number and security code are shown (two-way).</summary>
    public static readonly StyledProperty<bool> IsDetailsVisibleProperty =
        AvaloniaProperty.Register<WalletCard, bool>(nameof(IsDetailsVisible), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How long the details stay shown before they hide again (30 seconds); zero keeps them until hidden.</summary>
    public static readonly StyledProperty<TimeSpan> DetailsTimeoutProperty =
        AvaloniaProperty.Register<WalletCard, TimeSpan>(nameof(DetailsTimeout), TimeSpan.FromSeconds(30));

    /// <summary>
    /// Called when the freeze button is pressed with the state asked for; return true when the change went through. While it
    /// runs the button shows a spinner. Null applies the change at once.
    /// </summary>
    public static readonly StyledProperty<Func<bool, CancellationToken, Task<bool>>?> SetFrozenAsyncProperty =
        AvaloniaProperty.Register<WalletCard, Func<bool, CancellationToken, Task<bool>>?>(nameof(SetFrozenAsync));

    /// <summary>Most transactions listed (5).</summary>
    public static readonly StyledProperty<int> MaxTransactionsProperty =
        AvaloniaProperty.Register<WalletCard, int>(nameof(MaxTransactions), 5, coerce: (_, v) => Math.Max(0, v));

    /// <summary>The day relative dates are counted from; null uses the local date.</summary>
    public static readonly StyledProperty<DateTime?> TodayProperty =
        AvaloniaProperty.Register<WalletCard, DateTime?>(nameof(Today));

    /// <summary>The block's built-in text.</summary>
    public static readonly StyledProperty<WalletCardLabels> LabelsProperty =
        AvaloniaProperty.Register<WalletCard, WalletCardLabels>(nameof(Labels), new WalletCardLabels());

    /// <summary>True while <see cref="SetFrozenAsync"/> runs.</summary>
    public static readonly DirectProperty<WalletCard, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<WalletCard, bool>(nameof(IsBusy), o => o.IsBusy);

    internal static readonly StyledProperty<double> CompactThresholdProperty =
        AvaloniaProperty.Register<WalletCard, double>(nameof(CompactThreshold), 680);

    #endregion

    #region Events

    /// <summary>Raised when the card is frozen or unfrozen, by the button or by setting <see cref="IsFrozen"/>.</summary>
    public static readonly RoutedEvent<WalletCardFrozenChangedEventArgs> FrozenChangedEvent =
        RoutedEvent.Register<WalletCard, WalletCardFrozenChangedEventArgs>(nameof(FrozenChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when an activity row is pressed (to open the transaction).</summary>
    public static readonly RoutedEvent<WalletTransactionEventArgs> TransactionInvokedEvent =
        RoutedEvent.Register<WalletCard, WalletTransactionEventArgs>(nameof(TransactionInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="FrozenChangedEvent"/>
    public event EventHandler<WalletCardFrozenChangedEventArgs>? FrozenChanged
    {
        add => AddHandler(FrozenChangedEvent, value);
        remove => RemoveHandler(FrozenChangedEvent, value);
    }

    /// <inheritdoc cref="TransactionInvokedEvent"/>
    public event EventHandler<WalletTransactionEventArgs>? TransactionInvoked
    {
        add => AddHandler(TransactionInvokedEvent, value);
        remove => RemoveHandler(TransactionInvokedEvent, value);
    }

    #endregion

    private readonly List<WalletCardRow> _rows = new();
    private WalletCardFacePanel? _face;
    private WalletCardArt? _art;
    private Button? _freeze;
    private Button? _details;
    private CopyButton? _copy;
    private StackPanel? _rowsHost;
    private WalletCardMeter? _meter;
    private FinanceAnnouncer? _announcer;
    private INameScope? _scope;
    private DispatcherTimer? _detailsTimer;
    private CancellationTokenSource? _freezeCancel;
    private bool _isBusy;
    private bool _isCompact;

    static WalletCard()
    {
        FocusableProperty.OverrideDefaultValue<WalletCard>(false);
    }

    public WalletCard()
    {
        Transactions.CollectionChanged += (_, _) => BuildRows();
    }

    /// <inheritdoc cref="IssuerNameProperty"/>
    public string? IssuerName { get => GetValue(IssuerNameProperty); set => SetValue(IssuerNameProperty, value); }

    /// <inheritdoc cref="NetworkNameProperty"/>
    public string NetworkName { get => GetValue(NetworkNameProperty); set => SetValue(NetworkNameProperty, value); }

    /// <inheritdoc cref="HolderNameProperty"/>
    public string? HolderName { get => GetValue(HolderNameProperty); set => SetValue(HolderNameProperty, value); }

    /// <inheritdoc cref="CardNumberProperty"/>
    public string? CardNumber { get => GetValue(CardNumberProperty); set => SetValue(CardNumberProperty, value); }

    /// <inheritdoc cref="ExpiryProperty"/>
    public string? Expiry { get => GetValue(ExpiryProperty); set => SetValue(ExpiryProperty, value); }

    /// <inheritdoc cref="SecurityCodeProperty"/>
    public string? SecurityCode { get => GetValue(SecurityCodeProperty); set => SetValue(SecurityCodeProperty, value); }

    /// <inheritdoc cref="FinishProperty"/>
    public WalletCardFinish Finish { get => GetValue(FinishProperty); set => SetValue(FinishProperty, value); }

    /// <inheritdoc cref="BalanceProperty"/>
    public decimal Balance { get => GetValue(BalanceProperty); set => SetValue(BalanceProperty, value); }

    /// <inheritdoc cref="MonthlyLimitProperty"/>
    public decimal MonthlyLimit { get => GetValue(MonthlyLimitProperty); set => SetValue(MonthlyLimitProperty, value); }

    /// <inheritdoc cref="SpentThisMonthProperty"/>
    public decimal SpentThisMonth { get => GetValue(SpentThisMonthProperty); set => SetValue(SpentThisMonthProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="IsFrozenProperty"/>
    public bool IsFrozen { get => GetValue(IsFrozenProperty); set => SetValue(IsFrozenProperty, value); }

    /// <inheritdoc cref="IsDetailsVisibleProperty"/>
    public bool IsDetailsVisible { get => GetValue(IsDetailsVisibleProperty); set => SetValue(IsDetailsVisibleProperty, value); }

    /// <inheritdoc cref="DetailsTimeoutProperty"/>
    public TimeSpan DetailsTimeout { get => GetValue(DetailsTimeoutProperty); set => SetValue(DetailsTimeoutProperty, value); }

    /// <inheritdoc cref="SetFrozenAsyncProperty"/>
    public Func<bool, CancellationToken, Task<bool>>? SetFrozenAsync { get => GetValue(SetFrozenAsyncProperty); set => SetValue(SetFrozenAsyncProperty, value); }

    /// <inheritdoc cref="MaxTransactionsProperty"/>
    public int MaxTransactions { get => GetValue(MaxTransactionsProperty); set => SetValue(MaxTransactionsProperty, value); }

    /// <inheritdoc cref="TodayProperty"/>
    public DateTime? Today { get => GetValue(TodayProperty); set => SetValue(TodayProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public WalletCardLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsBusyProperty"/>
    public bool IsBusy { get => _isBusy; private set => SetAndRaise(IsBusyProperty, ref _isBusy, value); }

    internal double CompactThreshold { get => GetValue(CompactThresholdProperty); set => SetValue(CompactThresholdProperty, value); }

    /// <summary>The recent transactions, newest first (the first <see cref="MaxTransactions"/> are listed).</summary>
    [Content]
    public AvaloniaList<WalletTransaction> Transactions { get; } = new();

    /// <summary>The last four digits of <see cref="CardNumber"/> (empty when it has none).</summary>
    public string LastFour
    {
        get
        {
            var digits = Digits(CardNumber);
            return digits.Length <= 4 ? digits : digits[^4..];
        }
    }

    /// <summary>True while the block is narrower than its compact threshold (the face sits above the balance).</summary>
    public bool IsCompact => _isCompact;

    /// <summary>The last text sent to the live region (tests and diagnostics).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <summary>
    /// Asks for the card to be frozen or unfrozen the way the button does: through <see cref="SetFrozenAsync"/> when set.
    /// Returns true when the card ends in the state asked for.
    /// </summary>
    public async Task<bool> RequestFrozenAsync(bool frozen)
    {
        if (frozen == IsFrozen)
        {
            return true;
        }
        if (IsBusy)
        {
            return false;
        }
        var handler = SetFrozenAsync;
        if (handler is null)
        {
            SetCurrentValue(IsFrozenProperty, frozen);
            return true;
        }
        _freezeCancel = new CancellationTokenSource();
        SetBusy(true);
        bool ok;
        try
        {
            ok = await handler(frozen, _freezeCancel.Token);
        }
        catch (OperationCanceledException)
        {
            ok = false;
        }
        catch (Exception)
        {
            ok = false;
        }
        finally
        {
            _freezeCancel.Dispose();
            _freezeCancel = null;
            SetBusy(false);
        }
        if (ok)
        {
            SetCurrentValue(IsFrozenProperty, frozen);
        }
        else
        {
            _announcer?.Announce(Labels.AnnounceFreezeFailed);
        }
        return ok;
    }

    /// <summary>Shows or hides the full number and security code.</summary>
    public void ToggleDetails() => SetCurrentValue(IsDetailsVisibleProperty, !IsDetailsVisible);

    /// <summary>The number as shown: masked to the last four, or in full, in groups of four.</summary>
    internal string FormatNumber(bool full)
    {
        var digits = Digits(CardNumber);
        if (digits.Length == 0)
        {
            return string.Empty;
        }
        var chars = full ? digits : new string('•', Math.Max(0, digits.Length - 4)) + LastFour;
        var parts = new List<string>();
        for (var i = 0; i < chars.Length; i += 4)
        {
            parts.Add(chars.Substring(i, Math.Min(4, chars.Length - i)));
        }
        return string.Join(" ", parts);
    }

    /// <summary>"$1,240.18": an amount with the currency symbol and two decimals in the labels' culture.</summary>
    internal string Money(decimal amount)
    {
        var format = (NumberFormatInfo)Labels.Culture.NumberFormat.Clone();
        format.CurrencySymbol = CurrencySymbol;
        format.CurrencyNegativePattern = 1;
        return amount.ToString("C2", format);
    }

    private static string Digits(string? text) => text is null ? string.Empty : new string(text.Where(char.IsAsciiDigit).ToArray());

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_freeze is not null)
        {
            _freeze.Click -= OnFreezeClick;
        }
        if (_details is not null)
        {
            _details.Click -= OnDetailsClick;
        }
        _rowsHost?.Children.Clear();
        base.OnApplyTemplate(e);
        _scope = e.NameScope;
        _face = _scope.Find<WalletCardFacePanel>(FacePart);
        _art = _scope.Find<WalletCardArt>(ArtPart);
        _freeze = _scope.Find<Button>(FreezePart);
        _details = _scope.Find<Button>(DetailsPart);
        _copy = _scope.Find<CopyButton>(CopyPart);
        _rowsHost = _scope.Find<StackPanel>(RowsPart);
        _meter = _scope.Find<WalletCardMeter>(MeterPart);
        _announcer = _scope.Find<FinanceAnnouncer>(AnnouncerPart);
        if (_scope.Find<ArcGlyph>("PART_FrostGlyph") is { } glyph)
        {
            glyph.Data = s_snowflake;
        }
        if (_freeze is not null)
        {
            _freeze.Click += OnFreezeClick;
        }
        if (_details is not null)
        {
            _details.Click += OnDetailsClick;
        }
        UpdateFace();
        UpdateTexts();
        UpdateBalance();
        UpdateStates();
        BuildRows();
        UpdateCompact(force: true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == IsFrozenProperty)
        {
            UpdateStates();
            _announcer?.Announce(IsFrozen ? Labels.AnnounceFrozen : Labels.AnnounceUnfrozen);
            RaiseEvent(new WalletCardFrozenChangedEventArgs(FrozenChangedEvent, IsFrozen));
        }
        else if (p == IsDetailsVisibleProperty)
        {
            UpdateStates();
            UpdateFace();
            RestartDetailsTimer();
            _announcer?.Announce(IsDetailsVisible ? Labels.AnnounceDetailsShown : Labels.AnnounceDetailsHidden);
        }
        else if (p == DetailsTimeoutProperty)
        {
            RestartDetailsTimer();
        }
        else if (p == CardNumberProperty || p == HolderNameProperty || p == ExpiryProperty || p == SecurityCodeProperty
                 || p == IssuerNameProperty || p == NetworkNameProperty || p == FinishProperty)
        {
            UpdateFace();
        }
        else if (p == BalanceProperty || p == MonthlyLimitProperty || p == SpentThisMonthProperty || p == CurrencySymbolProperty)
        {
            UpdateBalance();
            if (p == CurrencySymbolProperty)
            {
                BuildRows();
            }
        }
        else if (p == LabelsProperty)
        {
            UpdateTexts();
            UpdateFace();
            UpdateBalance();
            UpdateStates();
            BuildRows();
        }
        else if (p == MaxTransactionsProperty || p == TodayProperty)
        {
            BuildRows();
        }
        else if (p == BoundsProperty || p == CompactThresholdProperty)
        {
            UpdateCompact();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _detailsTimer?.Stop();
        _freezeCancel?.Cancel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RestartDetailsTimer();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        if (e.Key == Key.Escape && IsDetailsVisible)
        {
            SetCurrentValue(IsDetailsVisibleProperty, false);
            e.Handled = true;
            return;
        }
        var index = _rows.FindIndex(r => r.IsFocused);
        if (index < 0)
        {
            return;
        }
        var target = e.Key switch
        {
            Key.Down => Math.Min(index + 1, _rows.Count - 1),
            Key.Up => Math.Max(index - 1, 0),
            Key.Home => 0,
            Key.End => _rows.Count - 1,
            _ => -1,
        };
        if (target >= 0)
        {
            _rows[target].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }

    private async void OnFreezeClick(object? sender, RoutedEventArgs e) => await RequestFrozenAsync(!IsFrozen);

    private void OnDetailsClick(object? sender, RoutedEventArgs e) => ToggleDetails();

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_freeze is not null)
        {
            _freeze.IsLoading = busy;
        }
    }

    private void RestartDetailsTimer()
    {
        _detailsTimer?.Stop();
        if (!IsDetailsVisible || DetailsTimeout <= TimeSpan.Zero || VisualRoot is null)
        {
            return;
        }
        if (_detailsTimer is null)
        {
            _detailsTimer = new DispatcherTimer();
            _detailsTimer.Tick += (_, _) =>
            {
                _detailsTimer!.Stop();
                SetCurrentValue(IsDetailsVisibleProperty, false);
            };
        }
        _detailsTimer.Interval = DetailsTimeout;
        _detailsTimer.Start();
    }

    private T? Part<T>(string name) where T : Control => _scope?.Find<T>(name);

    private void UpdateFace()
    {
        var labels = Labels;
        if (_art is not null)
        {
            _art.Finish = Finish;
        }
        SetText("PART_Issuer", IssuerName);
        SetText("PART_Network", NetworkName);
        SetText("PART_Masked", FormatNumber(full: false));
        SetText("PART_Full", FormatNumber(full: true));
        SetText("PART_Holder", HolderName);
        SetText("PART_Expiry", Expiry);
        SetText("PART_ExpiryLabel", labels.ValidThru);
        SetText("PART_CvcValue", SecurityCode);
        SetText("PART_CvcLabel", labels.SecurityCode);
        if (Part<StackPanel>("PART_Cvc") is { } cvc)
        {
            cvc.IsVisible = !string.IsNullOrEmpty(SecurityCode);
        }
        if (_face is not null)
        {
            AutomationProperties.SetName(_face, labels.FaceName(NetworkName, HolderName ?? string.Empty, LastFour, IsFrozen));
        }
        if (_copy is not null)
        {
            _copy.Value = Digits(CardNumber);
        }
    }

    private void UpdateTexts()
    {
        var labels = Labels;
        SetText("PART_BalanceLabel", labels.Balance);
        SetText("PART_FrostLabel", labels.Frozen);
        SetText("PART_FrozenNoteText", labels.FrozenNote);
        SetText("PART_ActivityTitle", labels.RecentActivity);
        SetText("PART_Empty", labels.NoActivity);
        if (_copy is not null)
        {
            _copy.Label = labels.CopyNumber;
        }
    }

    private void UpdateBalance()
    {
        SetText("PART_Balance", Money(Balance));
        var limit = MonthlyLimit;
        var hasLimit = limit > 0;
        if (_meter is not null)
        {
            _meter.IsVisible = hasLimit;
            _meter.Fraction = hasLimit ? (double)Math.Clamp(SpentThisMonth / limit, 0, 1) : 0;
            _meter.IsOver = hasLimit && SpentThisMonth > limit;
        }
        if (Part<TextBlock>("PART_LimitLine") is { } line)
        {
            line.IsVisible = hasLimit;
            line.Text = hasLimit ? Labels.LimitLine(Money(SpentThisMonth), Money(limit)) : null;
        }
    }

    private void UpdateStates()
    {
        var labels = Labels;
        PseudoClasses.Set(FrozenPseudoClass, IsFrozen);
        PseudoClasses.Set(DetailsPseudoClass, IsDetailsVisible);
        if (_freeze is not null)
        {
            _freeze.Content = IsFrozen ? labels.Unfreeze : labels.Freeze;
            AutomationProperties.SetName(_freeze, IsFrozen ? labels.Unfreeze : labels.Freeze);
        }
        if (_details is not null)
        {
            _details.Content = IsDetailsVisible ? labels.HideDetails : labels.ShowDetails;
            AutomationProperties.SetName(_details, IsDetailsVisible ? labels.HideDetails : labels.ShowDetails);
        }
        if (_copy is not null)
        {
            _copy.IsVisible = IsDetailsVisible;
        }
        if (Part<Border>("PART_FrozenNote") is { } note)
        {
            note.IsVisible = IsFrozen;
        }
        if (_face is not null)
        {
            AutomationProperties.SetName(_face, labels.FaceName(NetworkName, HolderName ?? string.Empty, LastFour, IsFrozen));
        }
    }

    private void UpdateCompact(bool force = false)
    {
        var width = Bounds.Width;
        var compact = width > 0 ? width < CompactThreshold : _isCompact;
        if (!force && compact == _isCompact)
        {
            return;
        }
        _isCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
    }

    private void BuildRows()
    {
        if (_rowsHost is null)
        {
            return;
        }
        foreach (var row in _rows)
        {
            row.Click -= OnRowClick;
        }
        _rows.Clear();
        _rowsHost.Children.Clear();
        var labels = Labels;
        var today = Today?.Date ?? DateTime.Today;
        foreach (var transaction in Transactions.Take(MaxTransactions))
        {
            var row = BuildRow(transaction, labels, today);
            row.Click += OnRowClick;
            _rows.Add(row);
            _rowsHost.Children.Add(row);
        }
        if (Part<TextBlock>("PART_Empty") is { } empty)
        {
            empty.IsVisible = _rows.Count == 0;
        }
    }

    private WalletCardRow BuildRow(WalletTransaction transaction, WalletCardLabels labels, DateTime today)
    {
        var amount = Money(transaction.Amount);
        var signed = transaction.IsCredit ? "+" + amount : "−" + amount;
        var date = labels.Date(transaction.At, today);
        string? status = transaction.Status switch
        {
            WalletTransactionStatus.Pending => labels.Pending,
            WalletTransactionStatus.Declined => labels.Declined,
            _ => null,
        };

        var disc = new WalletCardMerchantDisc
        {
            Child = new TextBlock
            {
                Text = Initials(transaction.Merchant),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        disc.Classes.Add(transaction.IsCredit ? "credit" : "s" + (1 + StableHash(transaction.Merchant) % 4));

        var meta = string.IsNullOrEmpty(transaction.Category) ? date : $"{date} · {transaction.Category}";
        var texts = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Medium(new TextBlock { Text = transaction.Merchant, TextTrimming = TextTrimming.CharacterEllipsis }),
                Small(Muted(new TextBlock { Text = meta, TextTrimming = TextTrimming.CharacterEllipsis }))
            }
        };

        var amountText = new TextBlock
        {
            Text = signed,
            HorizontalAlignment = HorizontalAlignment.Right,
            FontFeatures = FontFeatureCollection.Parse("+tnum")
        };
        Medium(amountText);
        if (transaction.IsCredit)
        {
            ArcTokens.Bind(amountText, TextBlock.ForegroundProperty, ArcDesignTokenKind.Success);
        }
        if (transaction.Status == WalletTransactionStatus.Declined)
        {
            amountText.TextDecorations = TextDecorations.Strikethrough;
            ArcTokens.Bind(amountText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        }
        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Children = { amountText } };
        if (status is not null)
        {
            var tag = Small(new TextBlock { Text = status, HorizontalAlignment = HorizontalAlignment.Right });
            ArcTokens.Bind(tag, TextBlock.ForegroundProperty,
                transaction.Status == WalletTransactionStatus.Declined ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextMuted);
            right.Children.Add(tag);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(texts, 1);
        Grid.SetColumn(right, 2);
        grid.Children.Add(disc);
        grid.Children.Add(texts);
        grid.Children.Add(right);

        var row = new WalletCardRow { Content = grid, Transaction = transaction };
        AutomationProperties.SetName(row, labels.RowName(transaction.Merchant, signed, date, status));
        return row;
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is WalletCardRow { Transaction: { } transaction })
        {
            RaiseEvent(new WalletTransactionEventArgs(TransactionInvokedEvent, transaction));
        }
    }

    private static TextBlock Medium(TextBlock text)
    {
        ArcTokens.Bind(text, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        return text;
    }

    private static TextBlock Small(TextBlock text)
    {
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        return text;
    }

    private static TextBlock Muted(TextBlock text)
    {
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return text;
    }

    private void SetText(string part, string? text)
    {
        if (Part<TextBlock>(part) is { } block)
        {
            block.Text = text;
        }
    }

    /// <summary>"JG" for "Juniper Grocers", "N" for "Nimbus".</summary>
    internal static string Initials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "?",
            1 => char.ToUpperInvariant(words[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(words[0][0]), char.ToUpperInvariant(words[1][0]))
        };
    }

    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var c in text)
        {
            hash = unchecked(hash * 31 + c);
        }
        return Math.Abs(hash % 1000);
    }
}
