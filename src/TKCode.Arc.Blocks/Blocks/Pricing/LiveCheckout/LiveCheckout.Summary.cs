using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>The summary side: rows and totals, the coupon, the payment, the receipt sequence and the reset.</summary>
public partial class LiveCheckout
{
    private sealed class RowEntry
    {
        public required string Key { get; init; }
        public required LiveCheckoutCollapse Wrapper { get; init; }
        public required LiveCheckoutSummaryRow Row { get; init; }
        public bool Leaving { get; set; }
    }

    private readonly List<RowEntry> _rows = new();
    private List<LiveCheckoutLine> _lines = new();

    private Control? _summary;
    private Control? _headTitle;
    private LiveCheckoutButton? _toggle;
    private ArcGlyph? _toggleChevron;
    private LiveCheckoutAmount? _toggleTotal;
    private Control? _receiptHead;
    private StrokeDrawGlyph? _receiptCheck;
    private LiveCheckoutHeading? _receiptTitle;
    private LiveCheckoutCollapse? _bodyRow;
    private StackPanel? _linesHost;
    private LiveCheckoutCollapse? _couponRow;
    private LiveCheckoutCouponSlot? _coupon;
    private LiveCheckoutAmount? _subtotal;
    private LiveCheckoutCollapse? _discountRow;
    private LiveCheckoutAmount? _discount;
    private LiveCheckoutAmount? _tax;
    private LiveCheckoutAmount? _grand;
    private Control? _meta;
    private LiveCheckoutButton? _pdfButton;
    private LiveCheckoutSwap? _pdfLabel;
    private Control? _pdfIdle;
    private Control? _pdfSaved;
    private LiveCheckoutButton? _startOver;

    private LiveCheckoutPresence? _titlePresence;
    private LiveCheckoutPresence? _togglePresence;
    private LiveCheckoutPresence? _receiptPresence;
    private LiveCheckoutPresence? _metaPresence;
    private MotionValue<Vector>? _flip;
    private readonly TranslateTransform _flipTransform = new();

    private decimal _couponPercent;
    private CancellationTokenSource? _work;
    private LiveCheckoutOrder? _paidOrder;
    private Snapshot? _defaults;

    /// <summary>The state "Start over" returns to: the values the block was shown with.</summary>
    private sealed record Snapshot(string? Plan, int Seats, LiveCheckoutBilling Billing, string[] Addons, string? Country,
        string? Email, string? Name, string? Address, string? City, string? Postal);

    private void OnApplySummaryTemplate(TemplateAppliedEventArgs e)
    {
        _summary = Part<Control>(e, "PART_Summary");
        _headTitle = Part<Control>(e, "PART_SummaryTitle");
        _toggle = Part<LiveCheckoutButton>(e, "PART_Toggle");
        _toggleChevron = Part<ArcGlyph>(e, "PART_ToggleChevron");
        _toggleTotal = Part<LiveCheckoutAmount>(e, "PART_ToggleTotal");
        _receiptHead = Part<Control>(e, "PART_ReceiptHead");
        _receiptCheck = Part<StrokeDrawGlyph>(e, "PART_ReceiptCheck");
        _receiptTitle = Part<LiveCheckoutHeading>(e, "PART_ReceiptTitle");
        _bodyRow = Part<LiveCheckoutCollapse>(e, "PART_BodyRow");
        _linesHost = Part<StackPanel>(e, "PART_Lines");
        _couponRow = Part<LiveCheckoutCollapse>(e, "PART_CouponRow");
        _coupon = Part<LiveCheckoutCouponSlot>(e, "PART_Coupon");
        _subtotal = Part<LiveCheckoutAmount>(e, "PART_Subtotal");
        _discountRow = Part<LiveCheckoutCollapse>(e, "PART_DiscountRow");
        _discount = Part<LiveCheckoutAmount>(e, "PART_Discount");
        _tax = Part<LiveCheckoutAmount>(e, "PART_Tax");
        _grand = Part<LiveCheckoutAmount>(e, "PART_Grand");
        _meta = Part<Control>(e, "PART_Meta");
        _pdfButton = Part<LiveCheckoutButton>(e, "PART_ReceiptPdf");
        _pdfLabel = Part<LiveCheckoutSwap>(e, "PART_PdfLabel");
        _startOver = Part<LiveCheckoutButton>(e, "PART_StartOver");

        _rows.Clear();
        _linesHost?.Children.Clear();
        if (_summary is not null)
        {
            _summary.RenderTransform = Part<LiveCheckoutShell>(e, ShellPart) is { } shell
                ? new TransformGroup { Children = { _flipTransform, shell.StickTransform } }
                : _flipTransform;
            _flip = new MotionValue<Vector>(_summary, default, MotionVectors.Vector);
            _flip.Changed += v =>
            {
                _flipTransform.X = v.X;
                _flipTransform.Y = v.Y;
            };
        }
        if (_headTitle is not null)
        {
            _titlePresence = new LiveCheckoutPresence(_headTitle);
        }
        if (_toggle is not null)
        {
            _togglePresence = new LiveCheckoutPresence(_toggle);
            _toggle.Click += OnToggle;
        }
        if (_receiptHead is not null)
        {
            _receiptPresence = new LiveCheckoutPresence(_receiptHead, visible: IsPaid);
        }
        if (_meta is not null)
        {
            _metaPresence = new LiveCheckoutPresence(_meta, visible: IsPaid);
        }
        if (_bodyRow is not null)
        {
            _bodyRow.Motion = LiveCheckoutCollapseMotion.Unfold;
        }
        _discountRow?.SetOpen(false, false);
        if (_coupon is not null)
        {
            _coupon.ApplyRequested += OnCouponApply;
            _coupon.RemoveRequested += OnCouponRemove;
            _coupon.FocusMethod = () => FocusMethod;
        }
        if (_pdfButton is not null)
        {
            _pdfButton.Click += OnReceiptPdf;
        }
        if (_startOver is not null)
        {
            _startOver.Click += OnStartOver;
        }
        BuildPdfLabel();
    }

    private void DetachSummaryParts()
    {
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggle;
        }
        if (_coupon is not null)
        {
            _coupon.ApplyRequested -= OnCouponApply;
            _coupon.RemoveRequested -= OnCouponRemove;
        }
        if (_pdfButton is not null)
        {
            _pdfButton.Click -= OnReceiptPdf;
        }
        if (_startOver is not null)
        {
            _startOver.Click -= OnStartOver;
        }
        _flip?.Dispose();
        _flip = null;
    }

    private void BuildPdfLabel()
    {
        var download = new ArcGlyph { Data = ArcIcons.Download, Size = 15, StrokeThickness = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var pdf = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        _pdfIdle = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { download, pdf } };
        var check = new ArcGlyph { Data = ArcIcons.Check, Size = 15, StrokeThickness = 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var saved = new TextBlock { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        _pdfSaved = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { check, saved } };
        if (_pdfLabel is not null)
        {
            _pdfLabel.Distance = 6;
            _pdfLabel.BlurRadius = 3;
            _pdfLabel.KeepsWidth = true;
            _pdfLabel.Show(_pdfIdle, animate: false);
        }
    }

    // --- labels -----------------------------------------------------------------------------------------------------

    private void SyncLabels()
    {
        var l = Labels;
        SetText("PART_PlanHeading", l.PlanHeading);
        SetText("PART_AddonsHeading", l.AddonsHeading);
        SetText("PART_BillingHeading", l.BillingHeading);
        SetText("PART_CardHeading", l.CardHeading);
        SetText("PART_OrDivider", l.OrPayWithCard);
        SetText("PART_SeatsLabel", l.Seats);
        SetText("PART_EmailLabel", l.Email);
        SetText("PART_NameLabel", l.CardholderName);
        SetText("PART_CountryLabel", l.Country);
        SetText("PART_AddressLabel", l.Address);
        SetText("PART_CityLabel", l.City);
        SetText("PART_PostalLabel", l.PostalCode);
        SetText("PART_CardNumberLabel", l.CardNumber);
        SetText("PART_ExpiryLabel", l.Expiry);
        SetText("PART_CvcLabel", l.Cvc);
        SetText("PART_SecureText", l.SecureNote);
        SetText("PART_SummaryTitle", l.OrderSummary);
        SetText("PART_SubtotalLabel", l.Subtotal);
        SetText("PART_DiscountLabel", l.Discount);
        SetText("PART_TaxNotCharged", l.TaxNotCharged);
        SetText("PART_ReceiptTitle", l.PaymentSuccessful);
        SetText("PART_MetaOrderLabel", l.OrderNumber);
        SetText("PART_MetaMethodLabel", l.PaidWith);
        SetText("PART_MetaDateLabel", l.Date);
        SetText("PART_MetaEmailLabel", l.ReceiptSentTo);
        SetText("PART_StartOverText", l.StartOver);
        SetText("PART_TestCardText", l.UseTestCard);
        SetText("PART_AddCouponText", l.AddCoupon);
        SetText("PART_GrandLabel", IsPaid ? l.PaidToday : l.DueToday);
        if (_secureNote is not null)
        {
            _secureNote.IsVisible = !string.IsNullOrEmpty(l.SecureNote);
        }
        if (_testCardButton is not null)
        {
            _testCardButton.IsVisible = TestCard is not null;
            AutomationProperties.SetName(_testCardButton, l.UseTestCard);
        }
        if (_expressSection is not null)
        {
            AutomationProperties.SetName(_expressSection, l.ExpressCheckout);
        }
        if (_seatStepper is not null)
        {
            _seatStepper.RemoveLabel = l.RemoveSeat;
            _seatStepper.AddLabel = l.AddSeat;
            _seatStepper.BoxLabel = l.Seats;
        }
        if (_coupon is not null)
        {
            _coupon.AddLabel = l.AddCoupon;
            _coupon.Placeholder = l.CouponPlaceholder;
            _coupon.FieldLabel = l.CouponField;
            _coupon.ApplyLabel = l.Apply;
            _coupon.RemoveLabel = l.RemoveCoupon;
        }
        foreach (var (field, label, placeholder) in new (LiveCheckoutField, string, string?)[]
                 {
                     (LiveCheckoutField.Email, l.Email, null), (LiveCheckoutField.Name, l.CardholderName, null),
                     (LiveCheckoutField.Address, l.Address, null), (LiveCheckoutField.City, l.City, null),
                     (LiveCheckoutField.PostalCode, l.PostalCode, null),
                     (LiveCheckoutField.CardNumber, l.CardNumber, l.CardNumberPlaceholder),
                     (LiveCheckoutField.Expiry, l.Expiry, l.ExpiryPlaceholder), (LiveCheckoutField.Cvc, l.Cvc, l.CvcPlaceholder)
                 })
        {
            if (_fields[field].Input is { } input)
            {
                input.Label = label;
                input.PlaceholderText = placeholder;
            }
        }
        if (_countrySelect is not null)
        {
            AutomationProperties.SetName(_countrySelect, l.Country);
        }
        if (_pdfIdle is StackPanel { Children: [_, TextBlock pdf] })
        {
            pdf.Text = l.ReceiptPdf;
        }
        if (_pdfSaved is StackPanel { Children: [_, TextBlock saved] })
        {
            saved.Text = l.ReceiptSaved;
        }
        if (_pdfButton is not null)
        {
            AutomationProperties.SetName(_pdfButton, l.ReceiptPdf);
        }
        if (_startOver is not null)
        {
            AutomationProperties.SetName(_startOver, l.StartOver);
        }
        foreach (var (brand, text) in _brandTexts)
        {
            text.Text = l.BrandName(brand);
        }
        SyncToggle();
        SyncPayTexts();
        SyncBilling();
        SyncAddonRows();
    }

    private void SyncToggle()
    {
        SetText("PART_ToggleLabel", IsSummaryOpen ? Labels.HideSummary : Labels.ShowSummary);
        if (_toggleChevron is not null)
        {
            _toggleChevron.Data = IsSummaryOpen ? ArcIcons.ChevronUp : ArcIcons.ChevronDown;
        }
        if (_toggle is not null)
        {
            AutomationProperties.SetName(_toggle, IsSummaryOpen ? Labels.HideSummary : Labels.ShowSummary);
            AutomationProperties.SetIsOffscreenBehavior(_toggle, IsOffscreenBehavior.Default);
        }
    }

    private void OnToggle(object? sender, RoutedEventArgs e) => IsSummaryOpen = !IsSummaryOpen;

    // --- recalculation ----------------------------------------------------------------------------------------------

    private void Recalculate(bool raise)
    {
        var culture = Labels.Culture;
        _lines = LiveCheckoutMath.Lines(SelectedPlan, Seats, Billing, YearlyDiscount, _addons, _selectedAddonIds, Labels, Currency, culture);
        var country = SelectedCountry;
        var totals = LiveCheckoutMath.Totals(_lines.Select(l => l.Amount), CouponCode is null ? 0 : _couponPercent,
            country?.TaxRate ?? 0);
        SyncRows();
        foreach (var amount in new[] { _subtotal, _discount, _tax, _grand, _toggleTotal, _payAmount })
        {
            if (amount is not null)
            {
                amount.Currency = Currency;
                amount.Culture = culture;
            }
        }
        if (_subtotal is not null)
        {
            _subtotal.Value = totals.Subtotal;
        }
        if (_discount is not null)
        {
            _discount.Sign = "−";
            _discount.Value = totals.Discount;
        }
        var charged = country is { TaxRate: > 0 };
        SetText("PART_TaxLabel", LiveCheckoutMath.TaxName(country, Labels, culture));
        if (_tax is not null)
        {
            _tax.IsVisible = charged;
            _tax.Value = totals.Tax;
        }
        if (Text("PART_TaxNotCharged") is { } notCharged)
        {
            notCharged.IsVisible = !charged;
        }
        if (_grand is not null)
        {
            _grand.Value = totals.Total;
        }
        if (_toggleTotal is not null)
        {
            _toggleTotal.Value = totals.Total;
        }
        if (_payAmount is not null)
        {
            _payAmount.Value = totals.Total;
        }
        SyncPayTexts();
        var changed = totals != Totals;
        Totals = totals;
        if (raise && changed)
        {
            RaiseEvent(new LiveCheckoutTotalsChangedEventArgs(TotalsChangedEvent, totals));
        }
    }

    /// <summary>Summary rows follow the lines: new rows open and leaving rows close on the smooth spring.</summary>
    private void SyncRows()
    {
        if (_linesHost is null)
        {
            return;
        }
        var animate = LiveCheckoutMotion.CanAnimate(this);
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        RowEntry? previous = null;
        foreach (var line in _lines)
        {
            var key = line.Kind == LiveCheckoutLineKind.Plan ? "plan" : "addon:" + line.Id;
            wanted.Add(key);
            var entry = _rows.FirstOrDefault(r => r.Key == key && !r.Leaving);
            if (entry is null)
            {
                var row = new LiveCheckoutSummaryRow();
                var wrapper = new LiveCheckoutCollapse { Child = row };
                entry = new RowEntry { Key = key, Wrapper = wrapper, Row = row };
                var index = previous is null ? 0 : _linesHost.Children.IndexOf(previous.Wrapper) + 1;
                _linesHost.Children.Insert(Math.Clamp(index, 0, _linesHost.Children.Count), wrapper);
                _rows.Add(entry);
                if (animate)
                {
                    wrapper.SetOpen(false, false);
                    wrapper.SetOpen(true, true);
                }
            }
            entry.Row.Title = line.Title;
            entry.Row.Detail = line.Detail;
            entry.Row.Currency = Currency;
            entry.Row.Culture = Labels.Culture;
            entry.Row.Amount = line.Amount;
            previous = entry;
        }
        foreach (var entry in _rows.Where(r => !r.Leaving && !wanted.Contains(r.Key)).ToList())
        {
            entry.Leaving = true;
            if (animate)
            {
                var leaving = entry;
                entry.Wrapper.Closed += (_, _) => RemoveRow(leaving);
                entry.Wrapper.SetOpen(false, true);
            }
            else
            {
                RemoveRow(entry);
            }
        }
    }

    private void RemoveRow(RowEntry entry)
    {
        if (!entry.Leaving)
        {
            return;
        }
        _rows.Remove(entry);
        _linesHost?.Children.Remove(entry.Wrapper);
    }

    // --- coupon -----------------------------------------------------------------------------------------------------

    private async void OnCouponApply(object? sender, string code)
    {
        if (_coupon is null || IsPaid)
        {
            return;
        }
        _work?.Cancel();
        var cts = _work = new CancellationTokenSource();
        _coupon.IsChecking = true;
        LiveCheckoutCouponResult result;
        try
        {
            var check = ValidateCouponAsync ?? (_sampleCoupons ??= LiveCheckoutSamples.CreateCouponValidator());
            result = await check(code.ToUpperInvariant(), cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            result = LiveCheckoutCouponResult.Reject(code, ex.Message);
        }
        if (cts.IsCancellationRequested || _coupon is null)
        {
            return;
        }
        _coupon.IsChecking = false;
        if (!result.IsValid)
        {
            _coupon.SetError(result.Message ?? Labels.CouponNotFound(string.IsNullOrEmpty(result.Code) ? code : result.Code), true);
            return;
        }
        var applied = string.IsNullOrEmpty(result.Code) ? code.ToUpperInvariant() : result.Code;
        _couponPercent = Math.Clamp(result.Percent, 0, 100);
        CouponCode = applied;
        SetText("PART_DiscountCode", applied);
        _coupon.ShowApplied(applied, Labels.CouponOff(LiveCheckoutMath.Percent(_couponPercent / 100m, Labels.Culture)), true, focus: true);
        _discountRow?.SetOpen(true, true);
        Recalculate(raise: true);
        _announcer?.Announce(Labels.CouponAppliedAnnouncement(applied));
        RaiseEvent(new LiveCheckoutCouponEventArgs(CouponAppliedEvent, applied, _couponPercent));
    }

    private Func<string, CancellationToken, Task<LiveCheckoutCouponResult>>? _sampleCoupons;
    private Func<LiveCheckoutOrder, CancellationToken, Task<LiveCheckoutReceipt?>>? _samplePayment;

    private void OnCouponRemove(object? sender, EventArgs e)
    {
        if (CouponCode is not { } code || IsProcessing)
        {
            return;
        }
        var percent = _couponPercent;
        CouponCode = null;
        _couponPercent = 0;
        _coupon?.Close(true, focusButton: true);
        _discountRow?.SetOpen(false, true);
        Recalculate(raise: true);
        _announcer?.Announce(Labels.CouponRemovedAnnouncement(code));
        RaiseEvent(new LiveCheckoutCouponEventArgs(CouponRemovedEvent, code, percent));
    }

    // --- payment ----------------------------------------------------------------------------------------------------

    private void OnPay(object? sender, RoutedEventArgs e) => _ = SubmitAsync();

    private void OnWallet(object? sender, RoutedEventArgs e)
    {
        if (sender is LiveCheckoutButton { Tag: LiveCheckoutWallet wallet } button && !IsProcessing && !IsPaid)
        {
            _busyWallet = button;
            if (button.Content is Panel { Children: [StackPanel row, ArcSpinner spinner] })
            {
                row.Opacity = 0;
                spinner.IsVisible = true;
            }
            // Express wallets skip the card checks, as measured.
            _ = ChargeAsync(BuildOrder(LiveCheckoutPaymentMethod.Wallet, wallet.Id));
        }
    }

    /// <summary>
    /// Validates the form and pays with the card, as the pay button does. Returns true when the payment went through;
    /// false when a field is invalid (the first one is focused), a payment is already running, or it failed.
    /// </summary>
    public async Task<bool> SubmitAsync()
    {
        if (IsProcessing || IsPaid)
        {
            return false;
        }
        if (ValidateAll() is { } invalid)
        {
            invalid.Input?.Focus(NavigationMethod.Tab);
            return false;
        }
        return await ChargeAsync(BuildOrder(LiveCheckoutPaymentMethod.Card, null));
    }

    private LiveCheckoutOrder BuildOrder(LiveCheckoutPaymentMethod method, string? walletId)
    {
        var card = method == LiveCheckoutPaymentMethod.Card;
        var addonIds = _addons.Where(a => _selectedAddonIds.Contains(a.Id)).Select(a => a.Id).ToArray();
        return new LiveCheckoutOrder
        {
            PlanId = SelectedPlan?.Id ?? string.Empty,
            Seats = Seats,
            Billing = Billing,
            AddonIds = addonIds,
            CouponCode = CouponCode,
            CouponPercent = CouponCode is null ? 0 : _couponPercent,
            CountryCode = SelectedCountry?.Code ?? string.Empty,
            Email = Email ?? string.Empty,
            CardholderName = CardholderName ?? string.Empty,
            Address = Address ?? string.Empty,
            City = City ?? string.Empty,
            PostalCode = PostalCode ?? string.Empty,
            PaymentMethod = method,
            WalletId = walletId,
            CardNumber = card ? LiveCheckoutValidation.Digits(_cardNumber) : string.Empty,
            Expiry = card ? _expiry : string.Empty,
            Cvc = card ? _cvc : string.Empty,
            CardBrand = card ? LiveCheckoutValidation.Brand(_cardNumber) : LiveCheckoutCardBrand.None,
            Lines = _lines.ToArray(),
            Totals = Totals,
            Currency = Currency
        };
    }

    private async Task<bool> ChargeAsync(LiveCheckoutOrder order)
    {
        _work?.Cancel();
        var cts = _work = new CancellationTokenSource();
        IsProcessing = true;
        ClearAlert();
        SetFormEnabled(false);
        if (_busyWallet is null)
        {
            _payLabel?.Show(_payBusy, animate: true);
        }
        SyncPayTexts();
        _announcer?.Announce(Labels.Processing);
        LiveCheckoutReceipt? receipt;
        string? failure = null;
        try
        {
            var pay = PayAsync ?? (_samplePayment ??= LiveCheckoutSamples.CreatePaymentSimulator());
            receipt = await pay(order, cts.Token);
        }
        catch (OperationCanceledException)
        {
            receipt = null;
            failure = cts.IsCancellationRequested ? null : Labels.PaymentFailed;
            if (cts.IsCancellationRequested)
            {
                EndProcessing();
                return false;
            }
        }
        catch (Exception ex)
        {
            receipt = null;
            failure = string.IsNullOrWhiteSpace(ex.Message) ? Labels.PaymentFailed : ex.Message;
        }
        if (cts.IsCancellationRequested)
        {
            return false;
        }
        EndProcessing();
        if (failure is not null)
        {
            ShowAlert(failure);
            RaiseEvent(new LiveCheckoutPaymentFailedEventArgs(PaymentFailedEvent, order, failure));
            return false;
        }
        var filled = FillReceipt(order, receipt);
        _paidOrder = order;
        Receipt = filled;
        EnterPaid(animate: true);
        RaiseEvent(new LiveCheckoutPaidEventArgs(PaidEvent, order, filled));
        return true;
    }

    private void EndProcessing()
    {
        if (_busyWallet?.Content is Panel { Children: [StackPanel row, ArcSpinner spinner] })
        {
            row.Opacity = 1;
            spinner.IsVisible = false;
        }
        _busyWallet = null;
        IsProcessing = false;
        SetFormEnabled(true);
        _payLabel?.Show(_payIdle, animate: true);
        SyncPayTexts();
    }

    private LiveCheckoutReceipt FillReceipt(LiveCheckoutOrder order, LiveCheckoutReceipt? receipt)
    {
        string method;
        if (order.PaymentMethod == LiveCheckoutPaymentMethod.Wallet)
        {
            method = _wallets.FirstOrDefault(w => w.Id == order.WalletId)?.Name ?? order.WalletId ?? string.Empty;
        }
        else
        {
            var brand = Labels.BrandName(order.CardBrand);
            var digits = order.CardNumber;
            method = Labels.CardEnding(string.IsNullOrEmpty(brand) ? Labels.GenericCard : brand,
                digits.Length >= 4 ? digits[^4..] : digits);
        }
        return new LiveCheckoutReceipt
        {
            OrderNumber = receipt?.OrderNumber ?? Random.Shared.Next(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture),
            PaidAt = receipt?.PaidAt ?? Clock.GetLocalNow(),
            PaymentText = receipt?.PaymentText ?? method,
            Email = receipt?.Email ?? order.Email
        };
    }

    // --- receipt ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// The success sequence (all measured, t = 0 at the success frame): the summary's layout jumps to the centre column
    /// and its size to the receipt's, while a translate-only spring (smooth) carries it from where it was seen; background
    /// and shadow change over 240 ms (CSS in the theme); the old heading fades out (~150 ms) and the receipt heading fades
    /// in over 240 ms while it rises 8px after 75 ms; the receipt details fade in over 240 ms and rise 12px as one block
    /// after 176 ms; the check mark draws over 470 ms after 320 ms; the form fades out and moves 24px left over 240 ms on
    /// the exit curve while it stays in place out of the layout; the coupon row leaves at once. In one column the folded
    /// summary unfolds (480 ms rows, 240 ms fade). Unlike the reference (which jumps twice when the page was scrolled),
    /// the scroll view first brings the receipt into view and the glide starts from where the card was on screen.
    /// </summary>
    private void EnterPaid(bool animate)
    {
        var motion = animate && LiveCheckoutMotion.CanAnimate(this);
        var before = ScreenPosition();
        IsPaid = true;
        if (_shell is not null)
        {
            _shell.BeginFormExit();
            _shell.IsPaid = true;
        }
        _couponRow?.SetOpen(false, false);
        SetText("PART_GrandLabel", Labels.PaidToday);
        FillReceiptTexts();

        _titlePresence?.Hide(motion, MotionSpec.Tween(LiveCheckoutMotion.HeadOut, LiveCheckoutMotion.Standard));
        _togglePresence?.Hide(motion, MotionSpec.Tween(LiveCheckoutMotion.HeadOut, LiveCheckoutMotion.Standard));
        var token = LiveCheckoutTokenValues();
        if (_receiptPresence is not null)
        {
            _receiptPresence.Offset = new Vector(0, token.HeadRise);
            _receiptPresence.Show(motion, MotionSpec.Tween(ArcMotionPresets.Standard, LiveCheckoutMotion.Standard),
                MotionSpec.Spring(LiveCheckoutMotion.Smooth, token.HeadRiseDelay));
        }
        if (_metaPresence is not null)
        {
            _metaPresence.Offset = new Vector(0, token.MetaRise);
            _metaPresence.Show(motion, MotionSpec.Tween(ArcMotionPresets.Standard, LiveCheckoutMotion.Standard),
                MotionSpec.Spring(LiveCheckoutMotion.Smooth, token.MetaRiseDelay));
        }
        if (_receiptCheck is not null)
        {
            if (motion)
            {
                _receiptCheck.Progress = 0;
                _ = _receiptCheck.Play(token.CheckDuration, LiveCheckoutMotion.Enter, token.CheckDelay);
            }
            else
            {
                _receiptCheck.Progress = 1;
            }
        }
        _bodyRow?.SetOpen(true, motion);
        if (_formExit is not null && _form is not null)
        {
            _formExit.Offset = new Vector(-token.FormExitOffset, 0);
            var spec = MotionSpec.Tween(LiveCheckoutMotion.FormExit, LiveCheckoutMotion.Exit);
            _form.IsHitTestVisible = false;
            _formExit.Hide(motion, spec);
        }
        Glide(before);
        _receiptTitle?.Focus(NavigationMethod.Unspecified);
    }

    private (double HeadRise, double MetaRise, TimeSpan HeadRiseDelay, TimeSpan MetaRiseDelay, TimeSpan CheckDelay,
        TimeSpan CheckDuration, double FormExitOffset) LiveCheckoutTokenValues()
    {
        var t = new LiveCheckoutToken();
        t.CalculateTokenValues(false);
        return (t.HeadRise, t.MetaRise, t.HeadRiseDelay, t.MetaRiseDelay, t.CheckDelay, t.CheckDuration, t.FormExitOffset);
    }

    private void FillReceiptTexts()
    {
        var plan = SelectedPlan;
        SetText("PART_ReceiptText", Labels.ReceiptSentence(plan?.Name ?? string.Empty, Seats));
        SetText("PART_MetaOrder", Receipt?.OrderNumber);
        SetText("PART_MetaMethod", Receipt?.PaymentText);
        SetText("PART_MetaDate", Receipt?.PaidAt is { } at ? SettingsFormat.Date(at.DateTime, Labels.Culture) : null);
        SetText("PART_MetaEmail", Receipt?.Email);
    }

    private Point? ScreenPosition()
    {
        if (_summary is null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return null;
        }
        return _summary.TranslatePoint(default, top);
    }

    /// <summary>
    /// FLIP: lays the new state out now, keeps the receipt's top in a scrolled view, and slides the card from
    /// <paramref name="before"/> (its last screen position, including a glide in progress) to its new place on the smooth
    /// spring. Translate only: the size has already jumped.
    /// </summary>
    /// <remarks>Code-driven because the start and end come from two layout passes, which a ControlTheme cannot know.</remarks>
    private void Glide(Point? before)
    {
        if (_summary is null || _flip is null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }
        _flip.Jump(default);
        top.UpdateLayout();
        _summary.BringIntoView(new Rect(0, -16, _summary.Bounds.Width, Math.Min(_summary.Bounds.Height + 16, 240)));
        top.UpdateLayout();
        if (before is not { } from || _summary.TranslatePoint(default, top) is not { } to)
        {
            return;
        }
        var delta = from - to;
        if (!LiveCheckoutMotion.CanAnimate(this) || (Math.Abs(delta.X) < 0.5 && Math.Abs(delta.Y) < 0.5))
        {
            return;
        }
        _flip.Jump(new Vector(delta.X, delta.Y));
        _ = _flip.AnimateTo(default, MotionSpec.Spring(LiveCheckoutMotion.Smooth));
    }

    /// <summary>The current glide offset of the summary (tests).</summary>
    internal Vector GlideOffset => _flip?.Value ?? default;

    private void OnReceiptPdf(object? sender, RoutedEventArgs e)
    {
        if (_paidOrder is null || Receipt is null)
        {
            return;
        }
        _pdfLabel?.Show(_pdfSaved, animate: true);
        if (_pdfButton is not null)
        {
            AutomationProperties.SetName(_pdfButton, Labels.ReceiptSaved);
        }
        _announcer?.Announce(Labels.ReceiptSaved);
        RaiseEvent(new LiveCheckoutPaidEventArgs(ReceiptRequestedEvent, _paidOrder, Receipt));
    }

    private void OnStartOver(object? sender, RoutedEventArgs e) => Reset();

    /// <summary>
    /// Puts everything back as the block was first shown (plan, seats, billing, add-ons, country and billing details),
    /// clears the card fields and the coupon, and returns from the receipt: the form appears at once and the summary glides
    /// back on the same spring. Focus moves to the first wallet button (or the first plan).
    /// </summary>
    public void Reset()
    {
        _work?.Cancel();
        var wasPaid = IsPaid;
        var before = ScreenPosition();
        if (IsProcessing)
        {
            EndProcessing();
        }
        RestoreDefaults();
        _cardNumber = string.Empty;
        _expiry = string.Empty;
        _cvc = string.Empty;
        foreach (var state in _fields.Values)
        {
            state.IsDirty = false;
            state.IsShown = false;
            state.Error = LiveCheckoutFieldError.None;
            if (state.Input is { } input)
            {
                input.ErrorMessage = null;
            }
            state.CheckPresence?.Jump(false);
        }
        SyncFieldTexts();
        UpdateBrand(animate: false);
        _alertRow?.SetOpen(false, false);
        CouponCode = null;
        _couponPercent = 0;
        _coupon?.Reset();
        _discountRow?.SetOpen(false, false);
        Receipt = null;
        _paidOrder = null;
        IsPaid = false;
        IsSummaryOpen = false;
        if (_shell is not null)
        {
            _shell.EndFormExit();
            _shell.IsPaid = false;
        }
        if (_form is not null)
        {
            _form.IsHitTestVisible = true;
        }
        _formExit?.Jump(true);
        SetText("PART_GrandLabel", Labels.DueToday);
        _receiptPresence?.Jump(false);
        _metaPresence?.Jump(false);
        _pdfLabel?.Show(_pdfIdle, animate: false);
        if (_pdfButton is not null)
        {
            AutomationProperties.SetName(_pdfButton, Labels.ReceiptPdf);
        }
        SyncLayoutState(resetHeads: true);
        // The coupon row comes back on the smooth spring (as measured); everything else is instant.
        _couponRow?.SetOpen(true, wasPaid);
        Recalculate(raise: true);
        if (wasPaid)
        {
            Glide(before);
        }
        if (_walletButtons.FirstOrDefault(b => b.IsVisible) is { } first)
        {
            first.Focus(FocusMethod);
        }
        else
        {
            _planCards.FirstOrDefault(c => c.IsChecked == true)?.Focus(FocusMethod);
        }
        RaiseEvent(new RoutedEventArgs(RestartedEvent));
    }

    private void CaptureDefaults()
    {
        _defaults ??= new Snapshot(SelectedPlanId, Seats, Billing, _selectedAddonIds.ToArray(), CountryCode, Email, CardholderName,
            Address, City, PostalCode);
    }

    private void RestoreDefaults()
    {
        if (_defaults is not { } d)
        {
            return;
        }
        SelectedPlanId = d.Plan;
        Seats = d.Seats;
        Billing = d.Billing;
        _selectedAddonIds.Clear();
        foreach (var id in d.Addons)
        {
            _selectedAddonIds.Add(id);
        }
        SyncAddonChecks();
        CountryCode = d.Country;
        Email = d.Email;
        CardholderName = d.Name;
        Address = d.Address;
        City = d.City;
        PostalCode = d.Postal;
    }

    // --- layout state -----------------------------------------------------------------------------------------------

    private bool _narrow;
    private bool _stacked;

    /// <summary>True below the two-column width (tests).</summary>
    internal bool IsNarrowLayout => _narrow;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        var narrow = LiveCheckoutShell.IsNarrowFor(width, TwoColumnWidth);
        var stacked = LiveCheckoutShell.IsNarrowFor(width, StackWidth);
        if (narrow != _narrow || stacked != _stacked)
        {
            _narrow = narrow;
            _stacked = stacked;
            SyncLayoutState();
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Applies the one-column / stacked / paid state to the heads, the summary body and the grids.</summary>
    private void SyncLayoutState(bool resetHeads = false)
    {
        PseudoClasses.Set(NarrowPseudoClass, _narrow);
        PseudoClasses.Set(StackedPseudoClass, _stacked);
        PseudoClasses.Set(SummaryOpenPseudoClass, IsSummaryOpen && _narrow);
        if (_shell is not null)
        {
            _shell.TwoColumnWidth = TwoColumnWidth;
        }
        SyncStacked(_stacked);
        if (!IsPaid)
        {
            _titlePresence?.Jump(!_narrow);
            _togglePresence?.Jump(_narrow);
            if (resetHeads)
            {
                _receiptPresence?.Jump(false);
            }
            _bodyRow?.SetOpen(!_narrow || IsSummaryOpen, false);
        }
        else
        {
            _titlePresence?.Jump(false);
            _togglePresence?.Jump(false);
            _bodyRow?.SetOpen(true, false);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        CaptureDefaults();
        UpdateRingFill();
        ApplyWalletColors();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _work?.Cancel();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == SelectedPlanIdProperty)
        {
            SyncPlanCards(animate: true);
            Recalculate(raise: true);
        }
        else if (p == SeatsProperty)
        {
            SyncSeats();
            Recalculate(raise: true);
        }
        else if (p == MinSeatsProperty || p == MaxSeatsProperty)
        {
            CoerceValue(SeatsProperty);
            SyncSeats();
        }
        else if (p == BillingProperty || p == YearlyDiscountProperty)
        {
            SyncBilling();
            SyncPlanCards(animate: false);
            Recalculate(raise: true);
        }
        else if (p == CurrencyProperty)
        {
            SyncPlanCards(animate: false);
            SyncAddonRows();
            Recalculate(raise: true);
        }
        else if (p == CountryCodeProperty)
        {
            if (_countrySelect is not null && !ReferenceEquals(_countrySelect.SelectedItem, SelectedCountry))
            {
                _syncingFields = true;
                _countrySelect.SelectedItem = SelectedCountry;
                _syncingFields = false;
            }
            Recalculate(raise: true);
            // A touched postal code is checked again under the new country's rule at once.
            var postal = _fields[LiveCheckoutField.PostalCode];
            if (postal.IsShown)
            {
                Validate(postal);
            }
        }
        else if (p == EmailProperty || p == CardholderNameProperty || p == AddressProperty || p == CityProperty ||
                 p == PostalCodeProperty)
        {
            SyncFieldTexts();
        }
        else if (p == LabelsProperty)
        {
            SyncLabels();
            SyncPlanCards(animate: false);
            Recalculate(raise: false);
        }
        else if (p == TestCardProperty)
        {
            if (_testCardButton is not null)
            {
                _testCardButton.IsVisible = TestCard is not null;
            }
        }
        else if (p == IsSummaryOpenProperty)
        {
            SyncToggle();
            PseudoClasses.Set(SummaryOpenPseudoClass, IsSummaryOpen && _narrow);
            if (_narrow && !IsPaid)
            {
                _bodyRow?.SetOpen(IsSummaryOpen, true);
            }
        }
        else if (p == TwoColumnWidthProperty || p == StackWidthProperty)
        {
            InvalidateMeasure();
        }
    }
}
