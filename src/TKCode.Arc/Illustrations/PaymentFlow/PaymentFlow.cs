using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="PaymentFlow.PaymentCompleted"/>.</summary>
public sealed class PaymentCompletedEventArgs(bool approved, decimal amount) : EventArgs
{
    /// <summary>Whether the issuer approved the payment.</summary>
    public bool Approved { get; } = approved;

    /// <summary>The amount of the payment.</summary>
    public decimal Amount { get; } = amount;
}

/// <summary>Every visible word of a <see cref="PaymentFlow"/> (override for another language or domain).</summary>
public sealed class PaymentFlowLabels
{
    public string Customer { get; init; } = "Customer";
    public string Checkout { get; init; } = "Checkout";
    public string Gateway { get; init; } = "Gateway";
    public string GatewayDetail { get; init; } = "tokenize · route";
    public string Network { get; init; } = "Card network";
    public string NetworkDetail { get; init; } = "authorization";
    public string Issuer { get; init; } = "Issuing bank";
    public string IssuerDetail { get; init; } = "funds · risk";
    public string Merchant { get; init; } = "Merchant";
    public string MerchantDetail { get; init; } = "payout account";
    public string Tokenized { get; init; } = "Tokenized";
    public string Approved { get; init; } = "Approved";
    public string Declined { get; init; } = "Declined";
    public string Paid { get; init; } = "Paid";
    public string Failed { get; init; } = "Payment failed";
    public Func<string, string> Balance { get; init; } = static amount => $"Balance {amount}";
    public Func<string, string, string> Pay { get; init; } = static (amount, merchant) => $"The customer pays {amount} at {merchant}";
    public string Tokenize { get; init; } = "Card details are tokenized at the payment gateway";
    public string Authorize { get; init; } = "The authorization request travels to the issuing bank";
    public string Check { get; init; } = "The issuer checks funds and risk";
    public string ApprovedStep { get; init; } = "Approved: the response travels back";
    public string DeclinedStep { get; init; } = "Declined: insufficient funds";
    public string Confirmed { get; init; } = "The order is confirmed";
    public string Retry { get; init; } = "The customer is asked for another card";
    public string Settle { get; init; } = "Funds settle to the merchant account";
    public string Summary { get; init; } = "A card payment is tokenized, authorized by the issuing bank and settled to the merchant";
}

/// <summary>
/// An animated card payment: the customer pays at checkout, the gateway tokenizes the card, the authorization request
/// travels through the card network to the issuing bank, the issuer approves or declines, the response comes back
/// and approved payments settle to the merchant account, whose balance grows.
/// </summary>
public class PaymentFlow : Illustration
{
    /// <summary>The amount paid (default 42).</summary>
    public static readonly StyledProperty<decimal> AmountProperty =
        AvaloniaProperty.Register<PaymentFlow, decimal>(nameof(Amount), 42m);

    /// <summary>ISO currency code (default USD).</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<PaymentFlow, string>(nameof(Currency), "USD");

    /// <summary>The merchant shown at checkout.</summary>
    public static readonly StyledProperty<string> MerchantNameProperty =
        AvaloniaProperty.Register<PaymentFlow, string>(nameof(MerchantName), "Northwind");

    /// <summary>Last four digits of the card.</summary>
    public static readonly StyledProperty<string> CardLast4Property =
        AvaloniaProperty.Register<PaymentFlow, string>(nameof(CardLast4), "4242");

    /// <summary>Every N-th payment is declined (0 never declines).</summary>
    public static readonly StyledProperty<int> DeclineEveryProperty =
        AvaloniaProperty.Register<PaymentFlow, int>(nameof(DeclineEvery), 4, coerce: static (_, v) => Math.Max(0, v));

    public static readonly StyledProperty<PaymentFlowLabels> LabelsProperty =
        AvaloniaProperty.Register<PaymentFlow, PaymentFlowLabels>(nameof(Labels), new PaymentFlowLabels());

    public decimal Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    public string MerchantName { get => GetValue(MerchantNameProperty); set => SetValue(MerchantNameProperty, value); }

    public string CardLast4 { get => GetValue(CardLast4Property); set => SetValue(CardLast4Property, value); }

    public int DeclineEvery { get => GetValue(DeclineEveryProperty); set => SetValue(DeclineEveryProperty, value); }

    public PaymentFlowLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>Raised when the issuer decides a payment.</summary>
    public event EventHandler<PaymentCompletedEventArgs>? PaymentCompleted;

    /// <summary>The merchant account's balance (grows with every approved payment).</summary>
    public decimal Balance => _balance;

    /// <summary>Payments decided since the story started.</summary>
    public int PaymentCount => _payment;

    public override int StepCount => 7;

    private const double SceneW = 844;
    private const double SceneH = 270;
    private const double Hop = 0.6;
    private const double RequestLane = -4;
    private const double ResponseLane = 4;
    private const decimal OpeningBalance = 1_284m;

    private IllustrationNode _customer = null!;
    private IllustrationNode _checkout = null!;
    private IllustrationNode _gateway = null!;
    private IllustrationNode _network = null!;
    private IllustrationNode _issuer = null!;
    private IllustrationNode _merchant = null!;
    private IllustrationEdge _toCheckout = null!;
    private IllustrationEdge _toGateway = null!;
    private IllustrationEdge _toNetwork = null!;
    private IllustrationEdge _toIssuer = null!;
    private IllustrationEdge _toMerchant = null!;
    private IllustrationLabel _balanceLabel = null!;
    private decimal _balance = OpeningBalance;
    private int _payment;
    private bool _approved = true;

    static PaymentFlow()
    {
        LabelProperty.OverrideDefaultValue<PaymentFlow>("Payment flow illustration");
    }

    private protected override double SceneWidth => SceneW;

    private protected override double SceneHeight => SceneH;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelsProperty || change.Property == MerchantNameProperty || change.Property == CardLast4Property)
        {
            Rebuild();
        }
    }

    private protected override void BuildScene()
    {
        var labels = Labels;
        const double w = 150;
        const double h = 52;
        const double gap = 72;
        double Column(int i) => 14 + i * (w + gap);
        const double rowA = 70;
        const double rowB = 196;
        Rect At(int column, double centerY) => new(Column(column), centerY - h / 2, w, h);
        _customer = AddNode(new IllustrationNode("customer", At(0, rowA), ArcIcons.CreditCard, labels.Customer, $"•••• {CardLast4}"));
        _checkout = AddNode(new IllustrationNode("checkout", At(1, rowA), IllustrationGlyphs.ShoppingCart, labels.Checkout, MerchantName));
        _gateway = AddNode(new IllustrationNode("gateway", At(2, rowA), ArcIcons.Shield, labels.Gateway, labels.GatewayDetail));
        _network = AddNode(new IllustrationNode("network", At(3, rowA), IllustrationGlyphs.Network, labels.Network, labels.NetworkDetail));
        _issuer = AddNode(new IllustrationNode("issuer", At(3, rowB), IllustrationGlyphs.Landmark, labels.Issuer, labels.IssuerDetail));
        _merchant = AddNode(new IllustrationNode("merchant", At(1, rowB), IllustrationGlyphs.Building, labels.Merchant, labels.MerchantDetail));
        _toCheckout = AddEdge(new IllustrationEdge(_customer, _checkout, IllustrationEdge.Curve(_customer.Right, _checkout.Left), curved: true));
        _toGateway = AddEdge(new IllustrationEdge(_checkout, _gateway, IllustrationEdge.Curve(_checkout.Right, _gateway.Left), curved: true));
        _toNetwork = AddEdge(new IllustrationEdge(_gateway, _network, IllustrationEdge.Curve(_gateway.Right, _network.Left), curved: true));
        _toIssuer = AddEdge(new IllustrationEdge(_network, _issuer, IllustrationEdge.Curve(_network.Bottom, _issuer.Top, vertical: true), curved: true));
        // Settlement drops out of the gateway and turns toward the merchant account.
        var corner = new Point(_gateway.Bottom.X, _merchant.Center.Y);
        _toMerchant = AddEdge(new IllustrationEdge(_gateway, _merchant, [_gateway.Bottom, corner, corner, _merchant.Right], curved: true, dashed: true));
        _balanceLabel = AddLabel(new IllustrationLabel(new Point(_merchant.Center.X, _merchant.Bounds.Bottom + 8), string.Empty)
        {
            Align = 0,
            IsMono = true
        });
    }

    private protected override void BuildScript(IllustrationScript script)
    {
        script.At(0, StartPayment);
        script.At(0.5, () =>
        {
            Spawn(_toCheckout, Hop, lane: RequestLane, eased: true);
            _toCheckout.Active.Tween(0.8, Now, 0.3);
        });
        script.At(0.5 + Hop, () =>
        {
            _checkout.SetTone(IllustrationTone.Accent, Now);
            SetStep(1, Labels.Tokenize);
            Spawn(_toGateway, Hop, lane: RequestLane, eased: true);
            _toGateway.Active.Tween(0.8, Now, 0.3);
        });
        script.At(0.5 + 2 * Hop, () =>
        {
            _gateway.SetTone(IllustrationTone.Accent, Now);
            ShowBadge(_gateway, Labels.Tokenized, IllustrationTone.Neutral, ArcIcons.Lock);
            SetStep(2, Labels.Authorize);
            Spawn(_toNetwork, Hop, lane: RequestLane, eased: true);
            _toNetwork.Active.Tween(0.8, Now, 0.3);
        });
        script.At(0.5 + 3 * Hop, () =>
        {
            _network.SetTone(IllustrationTone.Accent, Now);
            Spawn(_toIssuer, Hop, lane: RequestLane, eased: true);
            _toIssuer.Active.Tween(0.8, Now, 0.3);
        });
        script.At(0.5 + 4 * Hop, () =>
        {
            _issuer.IsBusy = true;
            _issuer.Emphasis.Tween(0.6, Now, 0.3);
            SetStep(3, Labels.Check);
        });
        script.At(3.6, Decide);
        script.At(5.5, Conclude);
        script.At(6.2, () =>
        {
            if (_approved)
            {
                SetStep(6, Labels.Settle);
                _toMerchant.ActiveTone = IllustrationTone.Success;
                _toMerchant.Active.Tween(0.9, Now, 0.3);
                Spawn(_toMerchant, 0.9, IllustrationTone.Success, eased: true, tag: 1);
            }
        });
        script.At(8.4, () => { });
        script.Length = 8.4;
    }

    private bool IsDeclinedPayment(int index) => DeclineEvery > 0 && index % DeclineEvery == DeclineEvery - 1;

    private void StartPayment()
    {
        foreach (var node in Nodes)
        {
            node.SetTone(IllustrationTone.Neutral, Now);
            node.Emphasis.Tween(0, Now, 0.2);
            node.IsBusy = false;
            HideBadge(node);
        }
        foreach (var edge in Edges)
        {
            edge.Active.Tween(0, Now, 0.3);
            edge.ActiveTone = IllustrationTone.Accent;
        }
        _approved = !IsDeclinedPayment(_payment);
        var amount = FormatAmount(Amount);
        SetStep(0, Labels.Pay(amount, MerchantName));
        _customer.Emphasis.Tween(0.6, Now, 0.3);
        ShowBadge(_customer, amount, IllustrationTone.Neutral);
        UpdateBalance();
    }

    private void Decide()
    {
        _issuer.IsBusy = false;
        _issuer.Emphasis.Tween(0, Now, 0.3);
        var tone = _approved ? IllustrationTone.Success : IllustrationTone.Danger;
        _issuer.SetTone(tone, Now);
        ShowBadge(_issuer, _approved ? Labels.Approved : Labels.Declined, tone, _approved ? ArcIcons.CircleCheck : ArcIcons.CircleX, below: true);
        SetStep(4, _approved ? Labels.ApprovedStep : Labels.DeclinedStep);
        // The response retraces the route on the other lane.
        Spawn(_toIssuer, Hop * 0.9, tone, reverse: true, lane: ResponseLane, eased: true);
        Spawn(_toNetwork, Hop * 0.9, tone, reverse: true, lane: ResponseLane, eased: true, delay: Hop * 0.9);
        Spawn(_toGateway, Hop * 0.9, tone, reverse: true, lane: ResponseLane, eased: true, delay: Hop * 1.8);
        foreach (var edge in new[] { _toIssuer, _toNetwork, _toGateway })
        {
            edge.ActiveTone = tone;
        }
        _payment++;
        PaymentCompleted?.Invoke(this, new PaymentCompletedEventArgs(_approved, Amount));
    }

    private void Conclude()
    {
        var tone = _approved ? IllustrationTone.Success : IllustrationTone.Danger;
        HideBadge(_gateway);
        HideBadge(_customer);
        _customer.Emphasis.Tween(0, Now, 0.3);
        _checkout.SetTone(tone, Now);
        _customer.SetTone(tone, Now);
        ShowBadge(_checkout, _approved ? Labels.Paid : Labels.Failed, tone, _approved ? ArcIcons.Check : ArcIcons.X);
        SetStep(5, _approved ? Labels.Confirmed : Labels.Retry);
    }

    private protected override void OnPacketArrived(IllustrationEdge edge, int tag, IllustrationTone tone)
    {
        if (ReferenceEquals(edge, _toMerchant) && tag == 1)
        {
            _merchant.SetTone(IllustrationTone.Success, Now);
            ShowBadge(_merchant, "+" + FormatAmount(Amount), IllustrationTone.Success, below: false);
            _balance += Amount;
            UpdateBalance();
        }
    }

    private protected override void OnRestart()
    {
        _balance = OpeningBalance;
        _payment = 0;
        _approved = true;
        UpdateBalance();
    }

    private protected override void ApplyPoster()
    {
        var labels = Labels;
        SetStep(0, labels.Summary);
        foreach (var edge in Edges)
        {
            edge.ActiveTone = IllustrationTone.Success;
            edge.Active.Jump(0.6);
        }
        _issuer.SetTone(IllustrationTone.Success, Now, animate: false);
        _checkout.SetTone(IllustrationTone.Success, Now, animate: false);
        _merchant.SetTone(IllustrationTone.Success, Now, animate: false);
        ShowBadge(_gateway, labels.Tokenized, IllustrationTone.Neutral, ArcIcons.Lock);
        ShowBadge(_issuer, labels.Approved, IllustrationTone.Success, ArcIcons.CircleCheck, below: true);
        ShowBadge(_checkout, labels.Paid, IllustrationTone.Success, ArcIcons.Check);
        ShowBadge(_customer, FormatAmount(Amount), IllustrationTone.Neutral);
        _balance = OpeningBalance + Amount;
        UpdateBalance();
    }

    private void UpdateBalance() => _balanceLabel.Text = Labels.Balance(FormatAmount(_balance));

    /// <summary>"$42.00", "€42.00", "¥42" or "CHF 42.00" (symbol for common currencies, else the ISO code).</summary>
    internal string FormatAmount(decimal amount)
    {
        var code = (Currency ?? "USD").ToUpperInvariant();
        var (symbol, decimals) = code switch
        {
            "USD" => ("$", 2),
            "EUR" => ("€", 2),
            "GBP" => ("£", 2),
            "JPY" => ("¥", 0),
            "CNY" => ("¥", 2),
            "INR" => ("₹", 2),
            "KRW" => ("₩", 0),
            _ => (code + " ", 2)
        };
        return symbol + amount.ToString("N" + decimals, CultureInfo.InvariantCulture);
    }
}
