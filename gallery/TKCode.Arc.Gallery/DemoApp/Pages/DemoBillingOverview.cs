using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev's BillingOverview block (Fieldnote Labs on the Growth plan), built from smaller controls: our
/// <see cref="BillingOverview"/> is a different design (plan, payment and invoice cards without a plan picker, usage
/// meters or a card form). One bordered card: the header, "Current plan" with an inline plan picker beside "Payment
/// method" with an inline card form (one column below 780 wide), "Usage this period" with three meters and the invoice
/// list with "Pay now" and download buttons. Every action is simulated with uiarc.dev's timings (0.9 s for a plan change
/// or a card, 1 s to pay, 0.7 s to prepare a download).
/// </summary>
internal sealed class DemoBillingOverview : Border
{
    private sealed record Plan(string Key, string Name, int Rank, decimal Monthly, decimal Yearly, int Seats, int Calls, int Storage);

    private sealed record Term(string Renews, string PeriodEnd, int Days);

    private sealed record Subscription(string Plan, string Billing, string Renews, string PeriodEnd, int DaysLeft, int PeriodDays);

    private sealed class Invoice(string id, string date, string title, string detail, decimal amount, string status)
    {
        public string Id { get; } = id;
        public string Date { get; } = date;
        public string Title { get; } = title;
        public string Detail { get; set; } = detail;
        public decimal Amount { get; } = amount;
        public string Status { get; set; } = status;
    }

    private const int SeatsInUse = 12;
    private const int CallsUsed = 912_480;
    private const double StorageUsed = 38.4;

    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    private static readonly Plan[] Plans =
    [
        new("starter", "Starter", 0, 49, 39, 5, 250_000, 25),
        new("growth", "Growth", 1, 199, 159, 15, 1_000_000, 100),
        new("scale", "Scale", 2, 499, 399, 50, 5_000_000, 500)
    ];

    private static readonly Dictionary<string, Term> Terms = new()
    {
        ["monthly"] = new("Oct 22, 2026", "Oct 21", 30),
        ["yearly"] = new("Sep 22, 2027", "Sep 21, 2027", 365)
    };

    private Subscription _subscription = new("growth", "monthly", "Oct 14, 2026", "Oct 13", 22, 30);
    private string _pickPlan = "scale";
    private string _pickBilling = "monthly";
    private bool _picking;
    private bool _changing;
    private string? _planNotice;
    private string _last4 = "7719";
    private string _expiry = "04/28";
    private bool _editingCard;
    private bool _savingCard;
    private string? _cardNotice;
    private string? _paying;
    private int _changes;
    private readonly Dictionary<string, string> _downloads = new();
    private readonly List<Invoice> _invoices =
    [
        new("sep-renewal", "Sep 14, 2026", "Growth plan", "Sep 14 – Oct 13", 199, "paid"),
        new("sep-retention", "Sep 3, 2026", "Log retention add-on", "Card declined by the bank", 36, "failed"),
        new("aug-workshop", "Aug 28, 2026", "Onboarding workshop", "Bank transfer, due Sep 27", 600, "due"),
        new("aug-renewal", "Aug 14, 2026", "Growth plan", "Aug 14 – Sep 13", 199, "paid")
    ];

    private readonly Grid _top = new();
    private readonly Border _planPanel;
    private readonly Border _paymentPanel;
    private readonly Border _usagePanel;
    private readonly Border _invoicesPanel;
    private readonly StackPanel _planBody = new();
    private readonly StackPanel _paymentBody = new();
    private readonly Grid _meters = new();
    private readonly StackPanel _invoiceList = new();
    private readonly Input _cardNumber = new() { Label = "Card number", Placeholder = "1234 1234 1234 1234" };
    private readonly Input _cardExpiry = new() { Label = "Expiry", Placeholder = "MM/YY" };
    private readonly Input _cardCode = new() { Label = "Security code", Placeholder = "123" };
    private Control? _picker;
    private double _width = double.NaN;

    public DemoBillingOverview()
    {
        CornerRadius = new CornerRadius(DemoUi.CardRadius);
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);

        var header = new WrapPanel { Margin = new Thickness(20, 16) };
        header.Children.Add(DemoUi.Text("Billing", ArcDesignTokenKind.Foreground, 15, FontWeight.Medium));
        var workspace = DemoUi.Caption("Fieldnote Labs workspace");
        workspace.Margin = new Thickness(16, 0, 0, 0);
        workspace.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(workspace);
        var headerRow = new Panel();
        headerRow.Children.Add(header);

        _planPanel = Section(_planBody);
        _paymentPanel = Section(_paymentBody);
        _top.Children.Add(_planPanel);
        _top.Children.Add(_paymentPanel);

        var usage = new StackPanel();
        usage.Children.Add(PanelHead("Usage this period", DemoUi.Caption("Sep 14 – Oct 13")));
        _meters.Margin = new Thickness(0, 12, 0, 0);
        usage.Children.Add(_meters);
        _usagePanel = Section(usage);

        var invoices = new StackPanel();
        var recipient = DemoUi.Actions(new Avatar { DisplayName = "Emma Collins", Source = DemoPhotos.For("Emma Collins"), Size = AvatarSize.Small }, DemoUi.Caption("Sent to Emma Collins"));
        recipient.Children[1].VerticalAlignment = VerticalAlignment.Center;
        invoices.Children.Add(PanelHead("Invoices", recipient));
        _invoiceList.Margin = new Thickness(-12, 8, -12, -8);
        invoices.Children.Add(_invoiceList);
        _invoicesPanel = Section(invoices);

        var column = new StackPanel();
        column.Children.Add(headerRow);
        column.Children.Add(_top);
        column.Children.Add(_usagePanel);
        column.Children.Add(_invoicesPanel);
        Child = column;

        foreach (var input in new[] { _cardNumber, _cardExpiry, _cardCode })
        {
            input.PropertyChanged += OnCardFieldChanged;
            input.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    SaveCard();
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    CloseCardForm();
                }
            };
        }

        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
        ApplyWidth(900);
        RenderPlan();
        RenderPayment();
        RenderUsage();
        RenderInvoices();
    }

    private Plan Current => PlanOf(_subscription.Plan);

    private static Plan PlanOf(string key) => Plans.First(plan => plan.Key == key);

    #region Layout

    private static Border Section(Control content)
    {
        var section = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 16, 20, 20), Child = content };
        ArcTokens.Bind(section, BorderBrushProperty, ArcDesignTokenKind.Border);
        return section;
    }

    private static Control PanelHead(string title, Control? note = null)
    {
        var head = new DockPanel { MinHeight = 32 };
        if (note is not null)
        {
            note.VerticalAlignment = VerticalAlignment.Center;
            note.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(note, Dock.Right);
            head.Children.Add(note);
        }
        var heading = DemoUi.Text(title, ArcDesignTokenKind.Foreground, 13, FontWeight.Medium);
        heading.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(heading);
        return head;
    }

    // uiarc.dev's container queries: plan and payment stack at 780, meters and invoice rows fold at 700, padding drops at 480.
    private void ApplyWidth(double width)
    {
        var old = _width;
        _width = width;
        var stackedTop = width <= 780;
        _top.ColumnDefinitions.Clear();
        _top.RowDefinitions.Clear();
        if (stackedTop)
        {
            _top.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _top.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(_paymentPanel, 0);
            Grid.SetRow(_paymentPanel, 1);
            _paymentPanel.BorderThickness = new Thickness(0, 1, 0, 0);
        }
        else
        {
            _top.ColumnDefinitions.Add(new ColumnDefinition(1.4, GridUnitType.Star));
            _top.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Grid.SetColumn(_paymentPanel, 1);
            Grid.SetRow(_paymentPanel, 0);
            _paymentPanel.BorderThickness = new Thickness(1, 1, 0, 0);
        }
        var padding = width <= 480 ? new Thickness(16) : new Thickness(20, 16, 20, 20);
        foreach (var panel in new[] { _planPanel, _paymentPanel, _usagePanel, _invoicesPanel })
        {
            panel.Padding = padding;
        }
        if (double.IsNaN(old) || (old <= 700) != (width <= 700))
        {
            RenderUsage();
            RenderInvoices();
        }
    }

    private bool Narrow => _width <= 700;

    #endregion

    #region Current plan

    private static string Money(decimal amount) => amount.ToString("C", En);

    private static string WholeMoney(decimal amount) => amount.ToString("C0", En);

    private static decimal TermPrice(string plan, string billing) =>
        billing == "monthly" ? PlanOf(plan).Monthly : 12 * PlanOf(plan).Yearly;

    private static string Summary(Plan plan)
    {
        var calls = plan.Calls >= 1_000_000 ? $"{plan.Calls / 1_000_000}M" : $"{plan.Calls / 1000}k";
        return $"{plan.Seats} seats · {calls} API calls · {plan.Storage} GB";
    }

    /// <summary>uiarc.dev's quote: the unused part of this term is credited; a new term is charged in full.</summary>
    private (decimal Amount, decimal Credit, bool SameTerm, string Renews, bool Unchanged) Quote()
    {
        var s = _subscription;
        var credit = TermPrice(s.Plan, s.Billing) * s.DaysLeft / s.PeriodDays;
        var sameTerm = _pickBilling == s.Billing;
        var price = sameTerm ? TermPrice(_pickPlan, _pickBilling) * s.DaysLeft / s.PeriodDays : TermPrice(_pickPlan, _pickBilling);
        return (Math.Round(price - credit, 2), Math.Round(credit, 2), sameTerm, sameTerm ? s.Renews : Terms[_pickBilling].Renews,
            _pickPlan == s.Plan && sameTerm);
    }

    private void RenderPlan()
    {
        var plan = Current;
        var s = _subscription;
        _planBody.Children.Clear();
        _planBody.Children.Add(PanelHead("Current plan",
            DemoUi.Caption(s.Billing == "monthly" ? "Billed monthly" : $"{WholeMoney(12 * plan.Yearly)} billed yearly")));

        var line = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var price = DemoUi.Actions(
            new AnimatedCounter { Value = (double)(s.Billing == "monthly" ? plan.Monthly : plan.Yearly), Prefix = "$", FontSize = 24, FontWeight = FontWeight.Medium },
            DemoUi.Text("/ month", ArcDesignTokenKind.TextMuted, 13));
        price.Spacing = 6;
        price.Children[1].VerticalAlignment = VerticalAlignment.Bottom;
        price.Children[1].Margin = new Thickness(0, 0, 0, 4);
        DockPanel.SetDock(price, Dock.Right);
        line.Children.Add(price);
        line.Children.Add(new TextMorph { Text = plan.Name, FontSize = 24, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center });
        _planBody.Children.Add(line);

        if (_picking)
        {
            _picker ??= BuildPicker();
            UpdatePicker();
            _planBody.Children.Add(_picker);
            return;
        }

        var summary = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var top = plan.Rank == Plans.Length - 1;
        var upgrade = DemoUi.Button(top ? "Change plan" : "Upgrade", top ? ButtonVariant.Secondary : ButtonVariant.Primary);
        upgrade.VerticalAlignment = VerticalAlignment.Bottom;
        upgrade.Click += (_, _) => OpenPicker();
        DockPanel.SetDock(upgrade, Dock.Right);
        summary.Children.Add(upgrade);
        var facts = new StackPanel { Spacing = 2 };
        facts.Children.Add(DemoUi.Text($"Renews {s.Renews}", ArcDesignTokenKind.TextSecondary, 13));
        facts.Children.Add(DemoUi.Caption(Summary(plan)));
        summary.Children.Add(facts);
        _planBody.Children.Add(summary);
        if (_planNotice is not null)
        {
            _planBody.Children.Add(Notice(_planNotice));
        }
    }

    private void OpenPicker()
    {
        var current = Current;
        _pickPlan = Plans.FirstOrDefault(plan => plan.Rank > current.Rank)?.Key ?? current.Key;
        _pickBilling = _subscription.Billing;
        _planNotice = null;
        _picking = true;
        RenderPlan();
    }

    private void ClosePicker()
    {
        if (_changing)
        {
            return;
        }
        _picking = false;
        RenderPlan();
    }

    // The picker's controls are built once and refreshed; the option rows keep these parts.
    private readonly Dictionary<string, (Panel Row, Border Highlight, Border Dot, TextBlock Current, TextBlock Meta, AnimatedCounter Amount, TextBlock Billed)> _options = new();
    private SegmentedControl? _period;
    private TextBlock? _quoteLabel;
    private AnimatedCounter? _quoteAmount;
    private TextBlock? _quoteDetail;
    private Button? _confirm;
    private Button? _cancelPick;

    private Control BuildPicker()
    {
        var picker = new StackPanel { Spacing = 12, Margin = new Thickness(0, 16, 0, 0) };
        picker.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                ClosePicker();
            }
        };

        var head = new DockPanel();
        _period = new SegmentedControl();
        Avalonia.Automation.AutomationProperties.SetName(_period, "Billing period");
        _period.Items.Add(new SegmentedOption { Value = "monthly", Label = "Monthly" });
        _period.Items.Add(new SegmentedOption { Value = "yearly", Label = "Yearly" });
        _period.SelectedValue = _pickBilling;
        _period.SelectionChanged += (_, _) =>
        {
            if (!_changing && _period.SelectedValue is string billing && billing != _pickBilling)
            {
                _pickBilling = billing;
                UpdatePicker();
            }
        };
        _period.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_period, Dock.Right);
        head.Children.Add(_period);
        var headText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        headText.Children.Add(DemoUi.Text("Change plan", ArcDesignTokenKind.Foreground, 13, FontWeight.Medium));
        headText.Children.Add(DemoUi.Caption("Yearly billing saves 20%"));
        head.Children.Add(headText);
        var headRule = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12, 0, 0), Child = head };
        ArcTokens.Bind(headRule, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        picker.Children.Add(headRule);

        var options = new StackPanel { Spacing = 2 };
        var optionsBox = new Border { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(2), Child = options };
        ArcTokens.Bind(optionsBox, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(optionsBox, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        Avalonia.Automation.AutomationProperties.SetName(optionsBox, "Change plan");
        foreach (var plan in Plans)
        {
            options.Children.Add(BuildOption(plan));
        }
        picker.Children.Add(optionsBox);

        var quote = new DockPanel();
        _cancelPick = DemoUi.Button("Cancel", ButtonVariant.Ghost);
        _cancelPick.Click += (_, _) => ClosePicker();
        _confirm = DemoUi.Button("Upgrade", ButtonVariant.Primary);
        _confirm.Click += (_, _) => ConfirmChange();
        var actions = DemoUi.Actions(_cancelPick, _confirm);
        actions.VerticalAlignment = VerticalAlignment.Bottom;
        DockPanel.SetDock(actions, Dock.Right);
        quote.Children.Add(actions);
        var quoteText = new StackPanel { Spacing = 2 };
        _quoteLabel = DemoUi.Text("Due today", ArcDesignTokenKind.TextSecondary, 12);
        _quoteAmount = new AnimatedCounter { Prefix = "$", Decimals = 2, FontSize = 17, FontWeight = FontWeight.Medium, HorizontalAlignment = HorizontalAlignment.Left };
        _quoteDetail = DemoUi.Caption(string.Empty);
        quoteText.Children.Add(_quoteLabel);
        quoteText.Children.Add(_quoteAmount);
        quoteText.Children.Add(_quoteDetail);
        quote.Children.Add(quoteText);
        picker.Children.Add(quote);
        return picker;
    }

    private Control BuildOption(Plan plan)
    {
        var disabled = plan.Seats < SeatsInUse;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("16,12,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };
        // The radio: a strong ring, and over it the accent ring and dot of the chosen plan.
        var ring = new Panel { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        var idle = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5) };
        ArcTokens.Bind(idle, BorderBrushProperty, ArcDesignTokenKind.BorderStrong);
        var dot = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5), Padding = new Thickness(1.5) };
        ArcTokens.Bind(dot, BorderBrushProperty, ArcDesignTokenKind.Accent);
        var inner = new Border { CornerRadius = new CornerRadius(5) };
        ArcTokens.Bind(inner, BackgroundProperty, ArcDesignTokenKind.Accent);
        dot.Child = inner;
        ring.Children.Add(idle);
        ring.Children.Add(dot);
        grid.Children.Add(ring);

        var name = DemoUi.Actions(DemoUi.Text(plan.Name, ArcDesignTokenKind.Foreground, 13, FontWeight.Medium));
        var current = DemoUi.Caption("Current");
        current.VerticalAlignment = VerticalAlignment.Center;
        name.Children.Add(current);
        Grid.SetColumn(name, 2);
        grid.Children.Add(name);

        var meta = DemoUi.Caption(disabled ? $"{SeatsInUse} seats in use, {plan.Name} allows {plan.Seats}" : Summary(plan));
        Grid.SetColumn(meta, 2);
        Grid.SetRow(meta, 1);
        grid.Children.Add(meta);

        var amount = new AnimatedCounter { Prefix = "$", FontSize = 13, FontWeight = FontWeight.Medium };
        var per = DemoUi.Text("/mo", ArcDesignTokenKind.TextMuted, 12);
        per.VerticalAlignment = VerticalAlignment.Bottom;
        var amountRow = DemoUi.Actions(amount, per);
        amountRow.Spacing = 2;
        amountRow.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(amountRow, 3);
        grid.Children.Add(amountRow);

        var billed = DemoUi.Caption("Billed monthly");
        billed.HorizontalAlignment = HorizontalAlignment.Right;
        billed.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(billed, 3);
        Grid.SetRow(billed, 1);
        grid.Children.Add(billed);

        // The chosen plan sits on a raised surface (uiarc.dev's highlight).
        var highlight = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1) };
        ArcTokens.Bind(highlight, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(highlight, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(highlight, BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
        var body = new Border { Padding = new Thickness(12, 8), Child = grid };
        var row = new Panel
        {
            MinHeight = 52,
            Background = Brushes.Transparent,
            Focusable = !disabled,
            Cursor = disabled ? Cursor.Default : new Cursor(StandardCursorType.Hand),
            Children = { highlight, body }
        };
        Avalonia.Automation.AutomationProperties.SetName(row, plan.Name);
        if (disabled)
        {
            grid.Opacity = 0.45;
        }
        else
        {
            row.Tapped += (_, _) => Pick(plan.Key);
            row.KeyDown += (_, e) =>
            {
                if (e.Key is Key.Space or Key.Enter)
                {
                    e.Handled = true;
                    Pick(plan.Key);
                }
                else if (e.Key is Key.Down or Key.Right or Key.Up or Key.Left)
                {
                    // Arrows move through the plans that hold the seats in use, wrapping around.
                    e.Handled = true;
                    var open = Plans.Where(p => p.Seats >= SeatsInUse).ToList();
                    var step = e.Key is Key.Down or Key.Right ? 1 : -1;
                    var next = open[(open.FindIndex(p => p.Key == _pickPlan) + step + open.Count) % open.Count];
                    Pick(next.Key);
                    _options[next.Key].Row.Focus();
                }
            };
        }
        _options[plan.Key] = (row, highlight, dot, current, meta, amount, billed);
        return row;
    }

    private void Pick(string plan)
    {
        if (_changing)
        {
            return;
        }
        _pickPlan = plan;
        UpdatePicker();
    }

    private void UpdatePicker()
    {
        foreach (var plan in Plans)
        {
            var (_, highlight, dot, current, _, amount, billed) = _options[plan.Key];
            var selected = plan.Key == _pickPlan;
            highlight.IsVisible = selected;
            dot.IsVisible = selected;
            current.IsVisible = plan.Key == _subscription.Plan;
            amount.Value = (double)(_pickBilling == "monthly" ? plan.Monthly : plan.Yearly);
            billed.Text = _pickBilling == "yearly" ? $"{WholeMoney(12 * plan.Yearly)} a year" : "Billed monthly";
        }

        var quote = Quote();
        var next = PlanOf(_pickPlan);
        var direction = _pickPlan == _subscription.Plan ? "billing" : next.Rank > Current.Rank ? "up" : "down";
        _quoteLabel!.Text = quote.Amount < 0 ? "Credit to next invoice" : "Due today";
        _quoteAmount!.Value = (double)Math.Abs(quote.Amount);
        _quoteDetail!.Text = quote.Unchanged
            ? "This is your current plan"
            : quote.SameTerm
                ? $"Prorated for {_subscription.DaysLeft} days"
                : $"Renews {quote.Renews} · {Money(quote.Credit)} credited";
        _confirm!.Content = quote.Unchanged
            ? "Current plan"
            : direction == "up" ? $"Upgrade to {next.Name}" : direction == "down" ? $"Move to {next.Name}" : $"Switch to {_pickBilling}";
        _confirm.IsEnabled = !quote.Unchanged;
        _confirm.IsLoading = _changing;
        _cancelPick!.IsEnabled = !_changing;
        if (_period!.SelectedValue as string != _pickBilling)
        {
            _period.SelectedValue = _pickBilling;
        }
    }

    private void ConfirmChange()
    {
        var quote = Quote();
        if (quote.Unchanged || _changing)
        {
            return;
        }
        var s = _subscription;
        var next = PlanOf(_pickPlan);
        var direction = _pickPlan == s.Plan ? "billing" : next.Rank > Current.Rank ? "up" : "down";
        var term = Terms[_pickBilling];
        var changed = quote.SameTerm
            ? s with { Plan = _pickPlan }
            : new Subscription(_pickPlan, _pickBilling, term.Renews, term.PeriodEnd, term.Days, term.Days);
        var what = direction == "billing" ? $"Switched to {_pickBilling} billing" : direction == "up" ? $"Upgraded to {next.Name}" : $"Moved to {next.Name}";
        var notice = quote.Amount > 0
            ? $"{what}. {Money(quote.Amount)} charged to card ending {_last4}."
            : quote.Amount < 0 ? $"{what}. {Money(-quote.Amount)} credited to your next invoice." : $"{what}.";
        var invoice = quote.Amount > 0
            ? new Invoice($"change-{++_changes}", "Sep 22, 2026", $"{next.Name} plan",
                quote.SameTerm ? $"Prorated, Sep 22 – {s.PeriodEnd}" : $"{(_pickBilling == "yearly" ? "Yearly" : "Monthly")}, Sep 22 – {term.PeriodEnd}",
                quote.Amount, "paid")
            : null;

        _changing = true;
        UpdatePicker();
        After(900, () =>
        {
            _changing = false;
            _subscription = changed;
            _picking = false;
            _planNotice = notice;
            if (invoice is not null)
            {
                _invoices.Insert(0, invoice);
                RenderInvoices();
            }
            RenderPlan();
            RenderUsage();
        });
    }

    private static Control Notice(string text)
    {
        var row = new DockPanel();
        var check = DemoUi.Glyph(ArcIcons.Check, 14, ArcDesignTokenKind.Success);
        check.VerticalAlignment = VerticalAlignment.Top;
        check.Margin = new Thickness(0, 1, 8, 0);
        DockPanel.SetDock(check, Dock.Left);
        row.Children.Add(check);
        row.Children.Add(DemoUi.Text(text, ArcDesignTokenKind.TextSecondary, 12));
        var notice = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12, 0, 0), Margin = new Thickness(0, 12, 0, 0), Child = row };
        ArcTokens.Bind(notice, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return notice;
    }

    #endregion

    #region Payment method

    private void RenderPayment()
    {
        _paymentBody.Children.Clear();
        _paymentBody.Children.Add(PanelHead("Payment method"));

        var row = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var face = CardFace();
        face.Margin = new Thickness(0, 0, 16, 0);
        DockPanel.SetDock(face, Dock.Left);
        row.Children.Add(face);
        var details = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(DemoUi.Text($"Card ending {_last4}", ArcDesignTokenKind.Foreground, 13, FontWeight.Medium));
        details.Children.Add(DemoUi.Caption($"Expires {_expiry}"));
        var update = DemoUi.Button("Update");
        update.HorizontalAlignment = HorizontalAlignment.Left;
        update.Margin = new Thickness(0, 6, 0, 0);
        update.IsEnabled = !_editingCard;
        update.Click += (_, _) => OpenCardForm();
        details.Children.Add(update);
        row.Children.Add(details);
        _paymentBody.Children.Add(row);

        if (_editingCard)
        {
            _paymentBody.Children.Add(CardForm());
        }
        else if (_cardNotice is not null)
        {
            _paymentBody.Children.Add(Notice(_cardNotice));
        }
    }

    /// <summary>The card art: the foreground color as the card, the background color as its print.</summary>
    private Border CardFace()
    {
        TextBlock Print(string text, double size, double opacity, FontWeight weight = FontWeight.Normal)
        {
            var block = DemoUi.Line(text, ArcDesignTokenKind.Background, size, weight);
            block.Opacity = opacity;
            return block;
        }

        var chip = new Border { Width = 26, Height = 19, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Opacity = 0.42 };
        ArcTokens.Bind(chip, BorderBrushProperty, ArcDesignTokenKind.Background);
        var top = new DockPanel();
        var kind = Print("Business", 10, 0.72);
        kind.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(kind, Dock.Right);
        top.Children.Add(kind);
        top.Children.Add(new Panel { Children = { chip }, HorizontalAlignment = HorizontalAlignment.Left });

        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        var holder = Print("Fieldnote Labs", 10, 0.72);
        Grid.SetColumnSpan(holder, 2);
        bottom.Children.Add(holder);
        var number = Print($"••••  {_last4}", 13, 1, FontWeight.Medium);
        number.LetterSpacing = 0.5;
        Grid.SetRow(number, 1);
        bottom.Children.Add(number);
        var expiry = Print(_expiry, 11, 0.8);
        expiry.VerticalAlignment = VerticalAlignment.Bottom;
        Grid.SetRow(expiry, 1);
        Grid.SetColumn(expiry, 1);
        bottom.Children.Add(expiry);

        var layout = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        layout.Children.Add(top);
        bottom.VerticalAlignment = VerticalAlignment.Bottom;
        layout.Children.Add(bottom);

        var narrow = _width <= 480;
        var face = new Border
        {
            Width = narrow ? 140 : 172,
            Height = (narrow ? 140 : 172) / 1.586,
            CornerRadius = new CornerRadius(12),
            Padding = narrow ? new Thickness(12, 10) : new Thickness(14, 12),
            Child = layout
        };
        ArcTokens.Bind(face, BackgroundProperty, ArcDesignTokenKind.Foreground);
        return face;
    }

    private Control CardForm()
    {
        var form = new StackPanel { Spacing = 12, Margin = new Thickness(0, 16, 0, 0) };
        Avalonia.Automation.AutomationProperties.SetName(form, "New card");
        form.Children.Add(_cardNumber);
        var pair = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        pair.Children.Add(_cardExpiry);
        Grid.SetColumn(_cardCode, 2);
        pair.Children.Add(_cardCode);
        form.Children.Add(pair);
        var cancel = DemoUi.Button("Cancel", ButtonVariant.Ghost);
        cancel.IsEnabled = !_savingCard;
        cancel.Click += (_, _) => CloseCardForm();
        var save = DemoUi.Button("Save card", ButtonVariant.Primary);
        save.IsLoading = _savingCard;
        save.Click += (_, _) => SaveCard();
        var actions = DemoUi.Actions(cancel, save);
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        actions.Margin = new Thickness(0, 4, 0, 0);
        form.Children.Add(actions);
        return form;
    }

    private void OpenCardForm()
    {
        foreach (var input in new[] { _cardNumber, _cardExpiry, _cardCode })
        {
            input.Text = string.Empty;
            input.ErrorMessage = null;
        }
        _cardNotice = null;
        _editingCard = true;
        RenderPayment();
        Dispatcher.UIThread.Post(() => _cardNumber.Focus(), DispatcherPriority.Background);
    }

    private void CloseCardForm()
    {
        if (_savingCard)
        {
            return;
        }
        _editingCard = false;
        RenderPayment();
    }

    private bool _formatting;

    // The card number groups itself in fours, the expiry gets its slash and the code keeps four digits.
    private void OnCardFieldChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _formatting || sender is not Input input)
        {
            return;
        }
        var digits = new string((input.Text ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        string text;
        if (input == _cardNumber)
        {
            digits = digits[..Math.Min(16, digits.Length)];
            text = string.Join(' ', Enumerable.Range(0, (digits.Length + 3) / 4).Select(i => digits.Substring(i * 4, Math.Min(4, digits.Length - i * 4))));
        }
        else if (input == _cardExpiry)
        {
            digits = digits[..Math.Min(4, digits.Length)];
            text = digits.Length > 2 ? $"{digits[..2]}/{digits[2..]}" : digits;
        }
        else
        {
            text = digits[..Math.Min(4, digits.Length)];
        }
        input.ErrorMessage = null;
        if (text != input.Text)
        {
            _formatting = true;
            input.Text = text;
            input.CaretIndex = text.Length;
            _formatting = false;
        }
    }

    private void SaveCard()
    {
        if (_savingCard)
        {
            return;
        }
        var number = new string((_cardNumber.Text ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        var expiry = _cardExpiry.Text ?? string.Empty;
        var code = _cardCode.Text ?? string.Empty;
        _cardNumber.ErrorMessage = number.Length != 16 ? "Enter all 16 digits" : null;
        _cardExpiry.ErrorMessage = ExpiryError(expiry);
        _cardCode.ErrorMessage = code.Length is 3 or 4 && code.All(char.IsAsciiDigit) ? null : "Enter 3 or 4 digits";
        var invalid = new[] { _cardNumber, _cardExpiry, _cardCode }.FirstOrDefault(input => input.ErrorMessage is not null);
        if (invalid is not null)
        {
            invalid.Focus();
            return;
        }
        _savingCard = true;
        RenderPayment();
        After(900, () =>
        {
            _savingCard = false;
            _editingCard = false;
            _last4 = number[^4..];
            _expiry = expiry;
            _cardNotice = $"Card ending {_last4} saved. Renewals and retries now use it.";
            RenderPayment();
        });
    }

    private static string? ExpiryError(string expiry)
    {
        var parts = expiry.Split('/');
        if (parts.Length != 2 || parts[0].Length != 2 || parts[1].Length != 2
            || !int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var year) || month < 1 || month > 12)
        {
            return "Use MM/YY, like 08/29";
        }
        return year < 26 || (year == 26 && month < 9) ? "This card has expired" : null;
    }

    #endregion

    #region Usage

    private void RenderUsage()
    {
        var plan = Current;
        _meters.Children.Clear();
        _meters.ColumnDefinitions.Clear();
        _meters.RowDefinitions.Clear();
        var calls = plan.Calls >= 1_000_000 ? (plan.Calls / 1_000_000.0, "M") : (plan.Calls / 1000.0, "k");
        Control[] meters =
        [
            Meter("Seats", SeatsInUse, plan.Seats, 0, null, plan.Seats, "", $"{plan.Seats - SeatsInUse} seats"),
            Meter("API calls", CallsUsed, plan.Calls, 0, null, calls.Item1, calls.Item2, $"{(plan.Calls - CallsUsed).ToString("N0", En)} calls"),
            Meter("Storage", StorageUsed, plan.Storage, 1, "GB", plan.Storage, " GB", $"{(plan.Storage - StorageUsed).ToString("0.0", En)} GB")
        ];
        for (var i = 0; i < meters.Length; i++)
        {
            if (Narrow)
            {
                _meters.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(meters[i], i);
                meters[i].Margin = new Thickness(0, i == 0 ? 0 : 20, 0, 0);
            }
            else
            {
                _meters.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                Grid.SetColumn(meters[i], i);
                meters[i].Margin = new Thickness(i == 0 ? 0 : 16, 0, i == meters.Length - 1 ? 0 : 16, 0);
            }
            _meters.Children.Add(meters[i]);
        }
    }

    private static Control Meter(string label, double used, double limit, int decimals, string? unit, double limitValue, string limitSuffix, string left)
    {
        var share = used / limit;
        var near = share >= 0.85;
        var meter = new StackPanel { Spacing = 8 };

        var head = new DockPanel { MinHeight = 22 };
        if (near)
        {
            var badge = new Badge
            {
                Tone = BadgeTone.Warning,
                Size = BadgeSize.Small,
                Icon = DemoUi.Glyph(ArcIcons.TriangleAlert, 12, ArcDesignTokenKind.Warning),
                Content = "Near limit"
            };
            DockPanel.SetDock(badge, Dock.Right);
            head.Children.Add(badge);
        }
        var name = DemoUi.Text(label, ArcDesignTokenKind.TextSecondary, 13);
        name.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(name);
        meter.Children.Add(head);

        var value = new WrapPanel();
        value.Children.Add(new AnimatedCounter { Value = used, Decimals = decimals, AnimateOnView = true, FontSize = 17, FontWeight = FontWeight.Medium });
        if (unit is not null)
        {
            var unitText = DemoUi.Text(unit, ArcDesignTokenKind.Foreground, 13, FontWeight.Medium);
            unitText.VerticalAlignment = VerticalAlignment.Bottom;
            unitText.Margin = new Thickness(6, 0, 0, 2);
            value.Children.Add(unitText);
        }
        var of = DemoUi.Text($"of {limitValue.ToString("0.##", En)}{limitSuffix}", ArcDesignTokenKind.TextMuted, 13);
        of.VerticalAlignment = VerticalAlignment.Bottom;
        of.Margin = new Thickness(6, 0, 0, 2);
        value.Children.Add(of);
        meter.Children.Add(value);

        // A 4px track; a near-limit meter turns warning.
        var track = new Panel { Height = 4, Margin = new Thickness(0, 4, 0, 0), ClipToBounds = true };
        var trackFill = new Border { CornerRadius = new CornerRadius(2), Opacity = near ? 0.14 : 0.08 };
        ArcTokens.Bind(trackFill, BackgroundProperty, near ? ArcDesignTokenKind.Warning : ArcDesignTokenKind.Foreground);
        track.Children.Add(trackFill);
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions($"{Math.Min(1, share).ToString("0.####", En)}*,{(1 - Math.Min(1, share)).ToString("0.####", En)}*") };
        var fill = new Border { CornerRadius = new CornerRadius(2) };
        ArcTokens.Bind(fill, BackgroundProperty, near ? ArcDesignTokenKind.Warning : ArcDesignTokenKind.Accent);
        bar.Children.Add(fill);
        track.Children.Add(bar);
        Avalonia.Automation.AutomationProperties.SetName(track, label);
        meter.Children.Add(track);

        meter.Children.Add(DemoUi.Caption($"{Math.Round(share * 100)}% used · {left} left"));
        return meter;
    }

    #endregion

    #region Invoices

    private void RenderInvoices()
    {
        _invoiceList.Children.Clear();
        foreach (var invoice in _invoices)
        {
            _invoiceList.Children.Add(InvoiceRow(invoice));
        }
    }

    private Control InvoiceRow(Invoice invoice)
    {
        var (tone, label, icon) = invoice.Status switch
        {
            "failed" => (BadgeTone.Danger, "Failed", ArcIcons.CircleAlert),
            "due" => (BadgeTone.Warning, "Due", ArcIcons.Clock3),
            _ => (BadgeTone.Success, "Paid", ArcIcons.Check)
        };
        var iconColor = tone switch
        {
            BadgeTone.Danger => ArcDesignTokenKind.Danger,
            BadgeTone.Warning => ArcDesignTokenKind.Warning,
            _ => ArcDesignTokenKind.Success
        };
        var badge = new Badge { Tone = tone, Size = BadgeSize.Small, Icon = DemoUi.Glyph(icon, 12, iconColor), Content = label, HorizontalAlignment = HorizontalAlignment.Left };

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(DemoUi.Line(invoice.Title, ArcDesignTokenKind.Foreground, 13, FontWeight.Medium));
        text.Children.Add(DemoUi.Line(invoice.Detail, ArcDesignTokenKind.TextMuted, 12));
        var amount = DemoUi.Text(Money(invoice.Amount), ArcDesignTokenKind.Foreground, 13, FontWeight.Medium);
        amount.TextAlignment = TextAlignment.Right;
        amount.HorizontalAlignment = HorizontalAlignment.Right;
        amount.VerticalAlignment = VerticalAlignment.Center;
        var date = DemoUi.Line(invoice.Date, Narrow ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.TextSecondary, Narrow ? 12 : 13);
        date.VerticalAlignment = VerticalAlignment.Center;

        var actions = DemoUi.Actions();
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        actions.VerticalAlignment = VerticalAlignment.Center;
        if (invoice.Status == "failed")
        {
            var pay = DemoUi.Button("Pay now");
            pay.IsLoading = _paying == invoice.Id;
            Avalonia.Automation.AutomationProperties.SetName(pay, $"Pay {Money(invoice.Amount)} for {invoice.Title}");
            pay.Click += (_, _) => Pay(invoice);
            actions.Children.Add(pay);
        }
        actions.Children.Add(DownloadButton(invoice));

        var grid = new Grid { MinHeight = 52 };
        if (Narrow)
        {
            grid.ColumnDefinitions = new ColumnDefinitions("*,Auto");
            grid.RowDefinitions = new RowDefinitions("Auto,Auto");
            amount.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(amount, 1);
            var meta = new WrapPanel { Margin = new Thickness(0, 8, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            date.Margin = new Thickness(0, 0, 8, 0);
            meta.Children.Add(date);
            meta.Children.Add(badge);
            Grid.SetRow(meta, 1);
            actions.Margin = new Thickness(12, 8, 0, 0);
            Grid.SetRow(actions, 1);
            Grid.SetColumn(actions, 1);
            grid.Children.Add(text);
            grid.Children.Add(amount);
            grid.Children.Add(meta);
            grid.Children.Add(actions);
        }
        else
        {
            // uiarc.dev's columns: date 96, text, amount 88, status 84, actions 128, 16 apart.
            grid.ColumnDefinitions = new ColumnDefinitions("96,16,*,16,88,16,84,16,128");
            Grid.SetColumn(text, 2);
            Grid.SetColumn(amount, 4);
            badge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(badge, 6);
            Grid.SetColumn(actions, 8);
            grid.Children.Add(date);
            grid.Children.Add(text);
            grid.Children.Add(amount);
            grid.Children.Add(badge);
            grid.Children.Add(actions);
        }
        var row = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(Narrow && _width <= 480 ? 8 : 12, 8), Child = grid };
        ArcTokens.Bind(row, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return row;
    }

    private Button DownloadButton(Invoice invoice)
    {
        _downloads.TryGetValue(invoice.Id, out var state);
        var done = state == "done";
        var button = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Width = 32,
            Padding = new Thickness(0),
            IsLoading = state == "preparing",
            Content = done
                ? DemoUi.Glyph(ArcIcons.Check, 16, ArcDesignTokenKind.Success)
                : new ArcGlyph { Data = ArcIcons.Download, Size = 16, StrokeThickness = 1.75 }
        };
        Avalonia.Automation.AutomationProperties.SetName(button, done ? $"Invoice from {invoice.Date} downloaded" : $"Download invoice from {invoice.Date}");
        button.Click += (_, _) =>
        {
            if (_downloads.ContainsKey(invoice.Id))
            {
                return;
            }
            _downloads[invoice.Id] = "preparing";
            RenderInvoices();
            After(700, () =>
            {
                _downloads[invoice.Id] = "done";
                RenderInvoices();
                After(2200, () =>
                {
                    _downloads.Remove(invoice.Id);
                    RenderInvoices();
                });
            });
        };
        return button;
    }

    private void Pay(Invoice invoice)
    {
        if (_paying is not null)
        {
            return;
        }
        var last4 = _last4;
        _paying = invoice.Id;
        RenderInvoices();
        After(1000, () =>
        {
            _paying = null;
            invoice.Status = "paid";
            invoice.Detail = $"Paid Sep 22 with card ending {last4}";
            RenderInvoices();
        });
    }

    #endregion

    private static void After(int milliseconds, Action action) =>
        DispatcherTimer.RunOnce(action, TimeSpan.FromMilliseconds(milliseconds));
}
