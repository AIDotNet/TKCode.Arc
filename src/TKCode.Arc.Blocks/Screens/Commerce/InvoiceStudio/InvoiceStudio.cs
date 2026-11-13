using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// An invoice editor with a live printed preview: choose the client and the payment terms, edit line items with quantity,
/// unit price and tax rate, give a discount as a percentage or an amount, and write a note. The subtotal, discount, tax and
/// total follow every keystroke, the sheet on the right shows the invoice as it will print, and Send checks the invoice and
/// hands it to <see cref="SendAsync"/>. In a narrow block a switch shows the form or the preview.
/// <code>
/// &lt;arc:InvoiceStudio SenderName="Fernhill Design Co." InvoiceNumber="INV-0042"&gt;
///     &lt;arc:InvoiceLineItem Description="Brand workshop" Quantity="1" UnitPrice="1800" TaxRate="0.08" /&gt;
/// &lt;/arc:InvoiceStudio&gt;
/// </code>
/// </summary>
[TemplatePart(FormPart, typeof(StackPanel))]
[TemplatePart(PaperPart, typeof(InvoiceStudioPaper))]
[TemplatePart(ViewSwitchPart, typeof(SegmentedControl))]
[TemplatePart(AnnouncerPart, typeof(FinanceAnnouncer))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, SendingPseudoClass, SentPseudoClass)]
public class InvoiceStudio : TemplatedControl
{
    public const string FormPart = "PART_Form";
    public const string PaperPart = "PART_Paper";
    public const string ViewSwitchPart = "PART_ViewSwitch";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>The block is narrower than its compact threshold: the form and the preview take turns.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>The block is narrower than 560 px: each line puts its numbers under its description.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>The send handler is running.</summary>
    public const string SendingPseudoClass = ":sending";

    /// <summary>The invoice was sent and has not been edited since.</summary>
    public const string SentPseudoClass = ":sent";

    private const double NarrowWidth = 560;

    #region Properties

    /// <summary>Who sends the invoice, printed at the top of the sheet.</summary>
    public static readonly StyledProperty<string?> SenderNameProperty =
        AvaloniaProperty.Register<InvoiceStudio, string?>(nameof(SenderName), "Your company");

    /// <summary>The sender's address and contact lines, one per line break.</summary>
    public static readonly StyledProperty<string?> SenderAddressProperty =
        AvaloniaProperty.Register<InvoiceStudio, string?>(nameof(SenderAddress));

    /// <summary>The clients offered by the "Bill to" select.</summary>
    public static readonly StyledProperty<IEnumerable<InvoiceClient>?> ClientsProperty =
        AvaloniaProperty.Register<InvoiceStudio, IEnumerable<InvoiceClient>?>(nameof(Clients));

    /// <summary>The client billed (two-way).</summary>
    public static readonly StyledProperty<InvoiceClient?> SelectedClientProperty =
        AvaloniaProperty.Register<InvoiceStudio, InvoiceClient?>(nameof(SelectedClient), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The invoice number (two-way).</summary>
    public static readonly StyledProperty<string?> InvoiceNumberProperty =
        AvaloniaProperty.Register<InvoiceStudio, string?>(nameof(InvoiceNumber), "INV-0001", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The date of issue; null uses today.</summary>
    public static readonly StyledProperty<DateTime?> IssueDateProperty =
        AvaloniaProperty.Register<InvoiceStudio, DateTime?>(nameof(IssueDate));

    /// <summary>Days from issue until payment is due (two-way); 0 is due on receipt.</summary>
    public static readonly StyledProperty<int> PaymentTermsProperty =
        AvaloniaProperty.Register<InvoiceStudio, int>(nameof(PaymentTerms), 30, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => Math.Max(0, v));

    /// <summary>The terms offered, in days (on receipt, 15, 30 and 45 by default).</summary>
    public static readonly StyledProperty<IReadOnlyList<int>> TermsOptionsProperty =
        AvaloniaProperty.Register<InvoiceStudio, IReadOnlyList<int>>(nameof(TermsOptions), [0, 15, 30, 45]);

    /// <summary>The tax rates a line can take, as shares (none, 5%, 8% and 20% by default).</summary>
    public static readonly StyledProperty<IReadOnlyList<decimal>> TaxRatesProperty =
        AvaloniaProperty.Register<InvoiceStudio, IReadOnlyList<decimal>>(nameof(TaxRates), [0m, 0.05m, 0.08m, 0.2m]);

    /// <summary>Whether the discount is a percentage or an amount (two-way).</summary>
    public static readonly StyledProperty<InvoiceDiscountKind> DiscountKindProperty =
        AvaloniaProperty.Register<InvoiceStudio, InvoiceDiscountKind>(nameof(DiscountKind), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The discount: a percentage (0–100) or an amount, by <see cref="DiscountKind"/> (two-way).</summary>
    public static readonly StyledProperty<decimal> DiscountValueProperty =
        AvaloniaProperty.Register<InvoiceStudio, decimal>(nameof(DiscountValue), defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => Math.Max(0, v));

    /// <summary>The note printed under the totals (two-way).</summary>
    public static readonly StyledProperty<string?> NotesProperty =
        AvaloniaProperty.Register<InvoiceStudio, string?>(nameof(Notes), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The symbol written before amounts ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<InvoiceStudio, string>(nameof(CurrencySymbol), "$");

    /// <summary>Delivers the invoice; return true when it was sent. Null marks it sent at once.</summary>
    public static readonly StyledProperty<Func<InvoiceDraft, CancellationToken, Task<bool>>?> SendAsyncProperty =
        AvaloniaProperty.Register<InvoiceStudio, Func<InvoiceDraft, CancellationToken, Task<bool>>?>(nameof(SendAsync));

    /// <summary>The block's built-in text.</summary>
    public static readonly StyledProperty<InvoiceStudioLabels> LabelsProperty =
        AvaloniaProperty.Register<InvoiceStudio, InvoiceStudioLabels>(nameof(Labels), new InvoiceStudioLabels());

    /// <summary>The sums of the current lines and discount.</summary>
    public static readonly DirectProperty<InvoiceStudio, InvoiceTotals> TotalsProperty =
        AvaloniaProperty.RegisterDirect<InvoiceStudio, InvoiceTotals>(nameof(Totals), o => o.Totals);

    /// <summary>Draft, sending or sent.</summary>
    public static readonly DirectProperty<InvoiceStudio, InvoiceStudioState> StateProperty =
        AvaloniaProperty.RegisterDirect<InvoiceStudio, InvoiceStudioState>(nameof(State), o => o.State);

    /// <summary>In a compact block, whether the preview is shown instead of the form (two-way).</summary>
    public static readonly StyledProperty<bool> IsPreviewShownProperty =
        AvaloniaProperty.Register<InvoiceStudio, bool>(nameof(IsPreviewShown), defaultBindingMode: BindingMode.TwoWay);

    internal static readonly StyledProperty<double> CompactThresholdProperty =
        AvaloniaProperty.Register<InvoiceStudio, double>(nameof(CompactThreshold), 900);

    #endregion

    /// <summary>Raised after <see cref="SendAsync"/> reports the invoice sent.</summary>
    public static readonly RoutedEvent<InvoiceSentEventArgs> InvoiceSentEvent =
        RoutedEvent.Register<InvoiceStudio, InvoiceSentEventArgs>(nameof(InvoiceSent), RoutingStrategies.Bubble);

    /// <inheritdoc cref="InvoiceSentEvent"/>
    public event EventHandler<InvoiceSentEventArgs>? InvoiceSent
    {
        add => AddHandler(InvoiceSentEvent, value);
        remove => RemoveHandler(InvoiceSentEvent, value);
    }

    private readonly List<InvoiceStudioLineRow> _rows = new();
    private InvoiceTotals _totals = new(0, 0, 0, 0);
    private InvoiceStudioState _state;
    private StackPanel? _form;
    private InvoiceStudioPaper? _paper;
    private SegmentedControl? _viewSwitch;
    private FinanceAnnouncer? _announcer;
    private Select? _client;
    private Input? _number;
    private Select? _terms;
    private StackPanel? _lineHost;
    private Grid? _lineHeader;
    private SegmentedControl? _discountKind;
    private NumberField? _discountValue;
    private Textarea? _notes;
    private Button? _send;
    private TextBlock? _error;
    private TextBlock? _sentLine;
    private TextBlock? _subtotal;
    private TextBlock? _discount;
    private TextBlock? _tax;
    private TextBlock? _total;
    private Control? _discountRow;
    private bool _syncing;
    private bool _previewPending;
    private bool _isCompact;
    private bool _isNarrow;
    private CancellationTokenSource? _sendCancel;

    static InvoiceStudio()
    {
        FocusableProperty.OverrideDefaultValue<InvoiceStudio>(false);
    }

    public InvoiceStudio()
    {
        Lines.CollectionChanged += OnLinesChanged;
    }

    /// <inheritdoc cref="SenderNameProperty"/>
    public string? SenderName { get => GetValue(SenderNameProperty); set => SetValue(SenderNameProperty, value); }

    /// <inheritdoc cref="SenderAddressProperty"/>
    public string? SenderAddress { get => GetValue(SenderAddressProperty); set => SetValue(SenderAddressProperty, value); }

    /// <inheritdoc cref="ClientsProperty"/>
    public IEnumerable<InvoiceClient>? Clients { get => GetValue(ClientsProperty); set => SetValue(ClientsProperty, value); }

    /// <inheritdoc cref="SelectedClientProperty"/>
    public InvoiceClient? SelectedClient { get => GetValue(SelectedClientProperty); set => SetValue(SelectedClientProperty, value); }

    /// <inheritdoc cref="InvoiceNumberProperty"/>
    public string? InvoiceNumber { get => GetValue(InvoiceNumberProperty); set => SetValue(InvoiceNumberProperty, value); }

    /// <inheritdoc cref="IssueDateProperty"/>
    public DateTime? IssueDate { get => GetValue(IssueDateProperty); set => SetValue(IssueDateProperty, value); }

    /// <inheritdoc cref="PaymentTermsProperty"/>
    public int PaymentTerms { get => GetValue(PaymentTermsProperty); set => SetValue(PaymentTermsProperty, value); }

    /// <inheritdoc cref="TermsOptionsProperty"/>
    public IReadOnlyList<int> TermsOptions { get => GetValue(TermsOptionsProperty); set => SetValue(TermsOptionsProperty, value); }

    /// <inheritdoc cref="TaxRatesProperty"/>
    public IReadOnlyList<decimal> TaxRates { get => GetValue(TaxRatesProperty); set => SetValue(TaxRatesProperty, value); }

    /// <inheritdoc cref="DiscountKindProperty"/>
    public InvoiceDiscountKind DiscountKind { get => GetValue(DiscountKindProperty); set => SetValue(DiscountKindProperty, value); }

    /// <inheritdoc cref="DiscountValueProperty"/>
    public decimal DiscountValue { get => GetValue(DiscountValueProperty); set => SetValue(DiscountValueProperty, value); }

    /// <inheritdoc cref="NotesProperty"/>
    public string? Notes { get => GetValue(NotesProperty); set => SetValue(NotesProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="SendAsyncProperty"/>
    public Func<InvoiceDraft, CancellationToken, Task<bool>>? SendAsync { get => GetValue(SendAsyncProperty); set => SetValue(SendAsyncProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public InvoiceStudioLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="TotalsProperty"/>
    public InvoiceTotals Totals { get => _totals; private set => SetAndRaise(TotalsProperty, ref _totals, value); }

    /// <inheritdoc cref="StateProperty"/>
    public InvoiceStudioState State { get => _state; private set => SetAndRaise(StateProperty, ref _state, value); }

    /// <inheritdoc cref="IsPreviewShownProperty"/>
    public bool IsPreviewShown { get => GetValue(IsPreviewShownProperty); set => SetValue(IsPreviewShownProperty, value); }

    internal double CompactThreshold { get => GetValue(CompactThresholdProperty); set => SetValue(CompactThresholdProperty, value); }

    /// <summary>The line items, in order.</summary>
    [Content]
    public AvaloniaList<InvoiceLineItem> Lines { get; } = new();

    /// <summary>The issue date in use: <see cref="IssueDate"/> or today.</summary>
    public DateTime EffectiveIssueDate => (IssueDate ?? DateTime.Today).Date;

    /// <summary>The due date: the issue date plus <see cref="PaymentTerms"/> days.</summary>
    public DateTime DueDate => EffectiveIssueDate.AddDays(PaymentTerms);

    /// <summary>True while the block is narrower than its compact threshold.</summary>
    public bool IsCompact => _isCompact;

    /// <summary>The last text sent to the live region (tests and diagnostics).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    /// <summary>The text of the inline error under the send button (tests).</summary>
    internal string? ErrorText => _error is { IsVisible: true } ? _error.Text : null;

    /// <summary>The printed sheet (tests read its text).</summary>
    internal InvoiceStudioPaper? Paper => _paper;

    /// <summary>Adds an empty line at the end (taxed like the last line) and returns it.</summary>
    public InvoiceLineItem AddLine()
    {
        var line = new InvoiceLineItem { TaxRate = Lines.Count > 0 ? Lines[^1].TaxRate : 0 };
        Lines.Add(line);
        return line;
    }

    /// <summary>The invoice as it would be sent now, or null with the reason when it is not ready.</summary>
    public InvoiceDraft? CreateDraft(out string? problem)
    {
        var labels = Labels;
        if (SelectedClient is not { } client)
        {
            problem = labels.NeedClient;
            return null;
        }
        var billable = Lines.Where(InvoiceStudioMath.IsBillable).ToList();
        if (billable.Count == 0)
        {
            problem = labels.NeedLine;
            return null;
        }
        problem = null;
        var snapshot = billable.Select(l => new InvoiceLineSnapshot(l.Description!.Trim(), l.Quantity, l.UnitPrice, l.TaxRate, l.Amount)).ToList();
        var totals = InvoiceStudioMath.Compute(billable, DiscountKind, DiscountValue);
        return new InvoiceDraft(InvoiceNumber ?? string.Empty, client, EffectiveIssueDate, DueDate, snapshot, totals,
            string.IsNullOrWhiteSpace(Notes) ? null : Notes);
    }

    /// <summary>
    /// Checks the invoice and sends it through <see cref="SendAsync"/>. Shows the problem and moves the focus to it when the
    /// invoice is not ready. Returns true when it was sent.
    /// </summary>
    public async Task<bool> SendInvoiceAsync()
    {
        if (State == InvoiceStudioState.Sending)
        {
            return false;
        }
        var draft = CreateDraft(out var problem);
        if (draft is null)
        {
            ShowError(problem);
            if (SelectedClient is null)
            {
                _client?.Focus();
            }
            else if (_rows.FirstOrDefault() is { } first)
            {
                first.Description.Focus();
            }
            return false;
        }
        ShowError(null);
        SetState(InvoiceStudioState.Sending);
        var ok = true;
        if (SendAsync is { } handler)
        {
            _sendCancel = new CancellationTokenSource();
            try
            {
                ok = await handler(draft, _sendCancel.Token);
            }
            catch (Exception)
            {
                ok = false;
            }
            finally
            {
                _sendCancel.Dispose();
                _sendCancel = null;
            }
        }
        if (!ok)
        {
            SetState(InvoiceStudioState.Draft);
            ShowError(Labels.SendFailed);
            return false;
        }
        SetState(InvoiceStudioState.Sent);
        var to = draft.Client.Email ?? draft.Client.Name;
        _announcer?.Announce(Labels.SentTo(to));
        RaiseEvent(new InvoiceSentEventArgs(InvoiceSentEvent, draft));
        return true;
    }

    internal string Money(decimal amount)
    {
        var format = (NumberFormatInfo)Labels.Culture.NumberFormat.Clone();
        format.CurrencySymbol = CurrencySymbol;
        format.CurrencyNegativePattern = 1;
        return amount.ToString("C2", format);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged -= OnViewSwitchChanged;
        }
        base.OnApplyTemplate(e);
        _form = e.NameScope.Find<StackPanel>(FormPart);
        _paper = e.NameScope.Find<InvoiceStudioPaper>(PaperPart);
        _viewSwitch = e.NameScope.Find<SegmentedControl>(ViewSwitchPart);
        _announcer = e.NameScope.Find<FinanceAnnouncer>(AnnouncerPart);
        if (_paper is not null)
        {
            AutomationProperties.SetName(_paper, Labels.PreviewName);
        }
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged += OnViewSwitchChanged;
        }
        BuildForm();
        Recompute();
        UpdateLayoutMode(force: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _sendCancel?.Cancel();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == LabelsProperty || p == TaxRatesProperty || p == TermsOptionsProperty || p == ClientsProperty || p == CurrencySymbolProperty)
        {
            BuildForm();
            Recompute();
        }
        else if (p == SelectedClientProperty || p == InvoiceNumberProperty || p == PaymentTermsProperty || p == DiscountKindProperty
                 || p == DiscountValueProperty || p == NotesProperty)
        {
            SyncForm();
            Edited();
        }
        else if (p == SenderNameProperty || p == SenderAddressProperty || p == IssueDateProperty)
        {
            SchedulePreview();
        }
        else if (p == IsPreviewShownProperty)
        {
            UpdateLayoutMode(force: true);
        }
        else if (p == BoundsProperty || p == CompactThresholdProperty)
        {
            UpdateLayoutMode();
        }
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var item in e.OldItems?.OfType<InvoiceLineItem>() ?? [])
        {
            item.PropertyChanged -= OnLineChanged;
        }
        foreach (var item in e.NewItems?.OfType<InvoiceLineItem>() ?? [])
        {
            item.PropertyChanged += OnLineChanged;
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var item in Lines)
            {
                item.PropertyChanged -= OnLineChanged;
                item.PropertyChanged += OnLineChanged;
            }
        }
        BuildLines();
        Edited();
    }

    private void OnLineChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_syncing && _rows.FirstOrDefault(r => r.Line == sender) is { } row)
        {
            SyncRow(row);
        }
        Edited();
    }

    /// <summary>Any change: the totals and the preview follow, and a sent invoice becomes a draft again.</summary>
    private void Edited()
    {
        if (State == InvoiceStudioState.Sent)
        {
            SetState(InvoiceStudioState.Draft);
        }
        if (_error is { IsVisible: true } && CreateDraft(out _) is not null)
        {
            ShowError(null);
        }
        Recompute();
    }

    private void Recompute()
    {
        Totals = InvoiceStudioMath.Compute(Lines, DiscountKind, DiscountValue);
        foreach (var row in _rows)
        {
            if (row.AmountText is TextBlock amount)
            {
                amount.Text = Money(row.Line.Amount);
            }
        }
        if (_subtotal is not null)
        {
            _subtotal.Text = Money(Totals.Subtotal);
            _discount!.Text = "−" + Money(Totals.Discount);
            _discountRow!.IsVisible = Totals.Discount > 0;
            _tax!.Text = Money(Totals.Tax);
            _total!.Text = Money(Totals.Total);
        }
        SchedulePreview();
    }

    private void SetState(InvoiceStudioState state)
    {
        State = state;
        PseudoClasses.Set(SendingPseudoClass, state == InvoiceStudioState.Sending);
        PseudoClasses.Set(SentPseudoClass, state == InvoiceStudioState.Sent);
        UpdateSendButton();
    }

    private void UpdateSendButton()
    {
        if (_send is null)
        {
            return;
        }
        var labels = Labels;
        _send.IsLoading = State == InvoiceStudioState.Sending;
        _send.Content = State switch
        {
            InvoiceStudioState.Sending => labels.Sending,
            InvoiceStudioState.Sent => labels.Sent,
            _ => labels.Send,
        };
        if (_sentLine is not null)
        {
            _sentLine.IsVisible = State == InvoiceStudioState.Sent;
            _sentLine.Text = SelectedClient is { } client ? labels.SentTo(client.Email ?? client.Name) : null;
        }
    }

    private void ShowError(string? text)
    {
        if (_error is null)
        {
            return;
        }
        _error.Text = text;
        _error.IsVisible = text is not null;
        if (text is not null)
        {
            _announcer?.Announce(text);
        }
    }

    private void UpdateLayoutMode(bool force = false)
    {
        var width = Bounds.Width;
        var compact = width > 0 ? width < CompactThreshold : _isCompact;
        var narrow = width > 0 ? width < NarrowWidth : _isNarrow;
        if (!force && compact == _isCompact && narrow == _isNarrow)
        {
            return;
        }
        _isCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (narrow != _isNarrow || force)
        {
            _isNarrow = narrow;
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            foreach (var row in _rows)
            {
                row.Arrange(narrow);
            }
            if (_lineHeader is not null)
            {
                _lineHeader.IsVisible = !narrow;
            }
        }
        if (_viewSwitch is not null)
        {
            _viewSwitch.IsVisible = compact;
            _syncing = true;
            _viewSwitch.SelectedValue = IsPreviewShown ? "preview" : "edit";
            _syncing = false;
        }
        if (_form is not null && _paper?.Parent is Control previewColumn)
        {
            _form.IsVisible = !compact || !IsPreviewShown;
            previewColumn.IsVisible = !compact || IsPreviewShown;
        }
    }

    private void OnViewSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _viewSwitch?.SelectedValue is string value)
        {
            SetCurrentValue(IsPreviewShownProperty, value == "preview");
        }
    }

    #region Form

    private void BuildForm()
    {
        if (_form is null)
        {
            return;
        }
        var labels = Labels;
        _form.Children.Clear();
        if (_viewSwitch is not null)
        {
            _viewSwitch.ItemsSource = new[] { new SegmentedOption("edit", labels.EditTab), new SegmentedOption("preview", labels.PreviewTab) };
        }

        _client = new Select
        {
            Label = labels.BillTo,
            Placeholder = labels.ChooseClient,
            ItemsSource = Clients?.ToList(),
            TextSelector = o => (o as InvoiceClient)?.Name,
            MinWidth = 200
        };
        _client.SelectionChanged += (_, _) =>
        {
            if (!_syncing)
            {
                SetCurrentValue(SelectedClientProperty, _client.SelectedItem as InvoiceClient);
            }
        };
        _number = new Input { Label = labels.Number, Width = 150 };
        _number.TextChanged += (_, _) =>
        {
            if (!_syncing)
            {
                SetCurrentValue(InvoiceNumberProperty, _number.Text);
            }
        };
        _terms = new Select
        {
            Label = labels.Terms,
            ItemsSource = TermsOptions.Cast<object>().ToList(),
            TextSelector = o => o is int days ? labels.TermsOption(days) : null,
            MinWidth = 150
        };
        _terms.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _terms.SelectedItem is int days)
            {
                SetCurrentValue(PaymentTermsProperty, days);
            }
        };
        var header = new WrapPanel { ItemSpacing = 12, LineSpacing = 12, Children = { _client, _number, _terms } };
        _form.Children.Add(header);

        // Lines.
        _lineHeader = new Grid { ColumnDefinitions = new ColumnDefinitions(InvoiceStudioLineRow.WideColumns), ColumnSpacing = 8 };
        string[] heads = [labels.Description, labels.Quantity, labels.Price, labels.Tax, labels.Amount];
        for (var i = 0; i < heads.Length; i++)
        {
            var head = Muted(new TextBlock { Text = heads[i], TextAlignment = i == 4 ? TextAlignment.Right : TextAlignment.Left });
            ArcTokens.Bind(head, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            Grid.SetColumn(head, i);
            _lineHeader.Children.Add(head);
        }
        _lineHost = new StackPanel();
        var addLine = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            HorizontalAlignment = HorizontalAlignment.Left,
            Content = Glyphed(ArcIcons.Plus, labels.AddLine),
            Margin = new Thickness(0, 4, 0, 0)
        };
        AutomationProperties.SetName(addLine, labels.AddLine);
        addLine.Click += (_, _) =>
        {
            var line = AddLine();
            _rows.FirstOrDefault(r => r.Line == line)?.Description.Focus();
        };
        _form.Children.Add(new StackPanel { Spacing = 4, Children = { _lineHeader, _lineHost, addLine } });

        // Discount and notes.
        _discountKind = new SegmentedControl
        {
            ItemsSource = new[]
            {
                new SegmentedOption(InvoiceDiscountKind.Percent, "%"),
                new SegmentedOption(InvoiceDiscountKind.Amount, CurrencySymbol)
            },
            VerticalAlignment = VerticalAlignment.Bottom
        };
        AutomationProperties.SetName(_discountKind, labels.Discount);
        _discountKind.SelectionChanged += (_, _) =>
        {
            if (!_syncing && _discountKind.SelectedValue is InvoiceDiscountKind kind)
            {
                SetCurrentValue(DiscountKindProperty, kind);
            }
        };
        _discountValue = new NumberField { Label = labels.Discount, Minimum = 0, Maximum = 1_000_000_000m, Step = 5, Width = 140 };
        _discountValue.PropertyChanged += (_, e) =>
        {
            if (e.Property == NumberField.ValueProperty && !_syncing)
            {
                SetCurrentValue(DiscountValueProperty, _discountValue.Value);
            }
        };
        _notes = new Textarea { Label = labels.Notes, Placeholder = labels.NotesPlaceholder, MinHeight = 72 };
        _notes.TextChanged += (_, _) =>
        {
            if (!_syncing)
            {
                SetCurrentValue(NotesProperty, _notes.Text);
            }
        };
        _form.Children.Add(new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Children = { _discountValue, _discountKind } });
        _form.Children.Add(_notes);

        // Totals and send.
        _subtotal = Figure();
        _discount = Figure();
        _tax = Figure();
        _total = Figure();
        ArcTokens.Bind(_total, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXL);
        ArcTokens.Bind(_total, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        var totals = new StackPanel { Spacing = 6 };
        totals.Children.Add(TotalRow(labels.Subtotal, _subtotal));
        _discountRow = TotalRow(labels.Discount, _discount);
        totals.Children.Add(_discountRow);
        totals.Children.Add(TotalRow(labels.Tax, _tax));
        totals.Children.Add(TotalRow(labels.Total, _total));

        _send = new Button { Variant = ButtonVariant.Primary, HorizontalAlignment = HorizontalAlignment.Right };
        _send.Click += async (_, _) => await SendInvoiceAsync();
        _error = new TextBlock { IsVisible = false, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Right };
        ArcTokens.Bind(_error, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        _sentLine = new TextBlock { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Right };
        ArcTokens.Bind(_sentLine, TextBlock.ForegroundProperty, ArcDesignTokenKind.Success);
        var footer = new Border { Padding = new Thickness(0, 14, 0, 0), Child = new StackPanel { Spacing = 10, Children = { totals, _send, _error, _sentLine } } };
        ArcTokens.Bind(footer, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(footer, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
        _form.Children.Add(footer);

        BuildLines();
        SyncForm();
        UpdateSendButton();
    }

    private void SyncForm()
    {
        if (_client is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _client.SelectedItem = SelectedClient;
            if (_number!.Text != InvoiceNumber)
            {
                _number.Text = InvoiceNumber;
            }
            _terms!.SelectedItem = TermsOptions.Contains(PaymentTerms) ? PaymentTerms : null;
            _discountKind!.SelectedValue = DiscountKind;
            _discountValue!.Value = DiscountValue;
            _discountValue.Maximum = DiscountKind == InvoiceDiscountKind.Percent ? 100 : 1_000_000_000m;
            if (_notes!.Text != Notes)
            {
                _notes.Text = Notes;
            }
        }
        finally
        {
            _syncing = false;
        }
        UpdateSendButton();
    }

    private void BuildLines()
    {
        if (_lineHost is null)
        {
            return;
        }
        _lineHost.Children.Clear();
        _rows.Clear();
        var labels = Labels;
        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            var description = new Input { Placeholder = labels.DescriptionPlaceholder, IsLabelVisible = false };
            AutomationProperties.SetName(description, labels.Description);
            var quantity = new NumberField { Minimum = 0, Maximum = 100_000, Step = 1, IsLabelVisible = false };
            AutomationProperties.SetName(quantity, labels.Quantity);
            var price = new NumberField { Minimum = 0, Maximum = 1_000_000_000m, Step = 10, IsLabelVisible = false, Prefix = CurrencySymbol };
            AutomationProperties.SetName(price, labels.Price);
            var tax = new Select
            {
                ItemsSource = TaxRates.Cast<object>().ToList(),
                TextSelector = o => o is decimal rate ? labels.TaxOption(rate) : null
            };
            AutomationProperties.SetName(tax, labels.Tax);
            var amount = new TextBlock
            {
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FontFeatures = FontFeatureCollection.Parse("+tnum")
            };
            var remove = new Button
            {
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                Content = new ArcGlyph { Data = ArcIcons.X, Size = 16 },
                VerticalAlignment = VerticalAlignment.Center
            };
            var row = new InvoiceStudioLineRow(line, description, quantity, price, tax, amount, remove);
            row.Arrange(_isNarrow);
            SyncRow(row);
            description.TextChanged += (_, _) =>
            {
                if (!_syncing)
                {
                    line.Description = description.Text;
                }
            };
            quantity.PropertyChanged += (_, e) =>
            {
                if (e.Property == NumberField.ValueProperty && !_syncing)
                {
                    line.Quantity = quantity.Value;
                }
            };
            price.PropertyChanged += (_, e) =>
            {
                if (e.Property == NumberField.ValueProperty && !_syncing)
                {
                    line.UnitPrice = price.Value;
                }
            };
            tax.SelectionChanged += (_, _) =>
            {
                if (!_syncing && tax.SelectedItem is decimal rate)
                {
                    line.TaxRate = rate;
                }
            };
            remove.Click += (_, _) => RemoveRow(row);
            _rows.Add(row);
            _lineHost.Children.Add(row);
        }
        Recompute();
    }

    private void SyncRow(InvoiceStudioLineRow row)
    {
        var labels = Labels;
        var line = row.Line;
        _syncing = true;
        try
        {
            var description = (Input)row.Description;
            if (description.Text != line.Description)
            {
                description.Text = line.Description;
            }
            ((NumberField)row.Quantity).Value = line.Quantity;
            ((NumberField)row.Price).Value = line.UnitPrice;
            ((Select)row.Tax).SelectedItem = TaxRates.Contains(line.TaxRate) ? line.TaxRate : null;
            ((TextBlock)row.AmountText).Text = Money(line.Amount);
            var index = _rows.IndexOf(row);
            var what = string.IsNullOrWhiteSpace(line.Description) ? labels.LineNumber((index < 0 ? _rows.Count : index) + 1) : line.Description!;
            AutomationProperties.SetName(row.Remove, labels.RemoveLine(what));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RemoveRow(InvoiceStudioLineRow row)
    {
        var index = _rows.IndexOf(row);
        Lines.Remove(row.Line);
        if (_rows.Count > 0)
        {
            _rows[Math.Clamp(index, 0, _rows.Count - 1)].Remove.Focus();
        }
    }

    private static Control TotalRow(string label, TextBlock value)
    {
        var row = new DockPanel();
        DockPanel.SetDock(value, Dock.Right);
        row.Children.Add(value);
        row.Children.Add(Muted(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }));
        return row;
    }

    private static TextBlock Figure() => new() { FontFeatures = FontFeatureCollection.Parse("+tnum"), TextAlignment = TextAlignment.Right };

    private static Control Glyphed(Geometry glyph, string text) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Children = { new ArcGlyph { Data = glyph, Size = 14, VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = text } }
    };

    private static TextBlock Muted(TextBlock text)
    {
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        return text;
    }

    #endregion

    #region Preview

    private void SchedulePreview()
    {
        if (_previewPending || _paper is null)
        {
            return;
        }
        _previewPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _previewPending = false;
            BuildPreview();
        }, DispatcherPriority.Background);
    }

    /// <summary>Rebuilds the sheet from the current invoice (coalesced: once per batch of edits).</summary>
    internal void BuildPreview()
    {
        if (_paper is null)
        {
            return;
        }
        var labels = Labels;
        var sheet = new StackPanel { Spacing = 2 };

        var top = new DockPanel();
        var heading = Ink(labels.InvoiceHeading, "heading");
        DockPanel.SetDock(heading, Dock.Right);
        top.Children.Add(heading);
        top.Children.Add(new StackPanel { Children = { Ink(SenderName, "strong"), Ink(SenderAddress, "muted") } });
        sheet.Children.Add(top);
        sheet.Children.Add(new InvoiceStudioRule());

        var meta = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        var billTo = new StackPanel { Children = { Ink(labels.BillTo, "muted") } };
        if (SelectedClient is { } client)
        {
            billTo.Children.Add(Ink(client.Name, "strong"));
            billTo.Children.Add(Ink(client.Address, "muted"));
            billTo.Children.Add(Ink(client.Email, "muted"));
        }
        else
        {
            billTo.Children.Add(Ink("—", "muted"));
        }
        var dates = new StackPanel
        {
            Children =
            {
                Pair(labels.Number, InvoiceNumber),
                Pair(labels.Issued, labels.Date(EffectiveIssueDate)),
                Pair(labels.Due, labels.Date(DueDate))
            }
        };
        Grid.SetColumn(dates, 1);
        meta.Children.Add(billTo);
        meta.Children.Add(dates);
        sheet.Children.Add(meta);
        sheet.Children.Add(new InvoiceStudioRule());

        var table = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), ColumnSpacing = 10, RowSpacing = 4 };
        var r = 0;
        void Cell(string? text, int column, string classes)
        {
            var ink = Ink(text, classes);
            Grid.SetRow(ink, r);
            Grid.SetColumn(ink, column);
            table.Children.Add(ink);
        }
        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Cell(labels.Description, 0, "muted");
        Cell(labels.Quantity, 1, "muted number");
        Cell(labels.Price, 2, "muted number");
        Cell(labels.Amount, 3, "muted number");
        foreach (var line in Lines.Where(l => !string.IsNullOrWhiteSpace(l.Description) || l.Amount > 0))
        {
            r++;
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Cell(line.Description, 0, string.Empty);
            Cell(line.Quantity.ToString("0.##", labels.Culture), 1, "number");
            Cell(Money(line.UnitPrice), 2, "number");
            Cell(Money(line.Amount), 3, "number");
        }
        sheet.Children.Add(table);
        sheet.Children.Add(new InvoiceStudioRule());

        var totals = Totals;
        var sums = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 180 };
        sums.Children.Add(Sum(labels.Subtotal, Money(totals.Subtotal), false));
        if (totals.Discount > 0)
        {
            sums.Children.Add(Sum(labels.Discount, "−" + Money(totals.Discount), false));
        }
        sums.Children.Add(Sum(labels.Tax, Money(totals.Tax), false));
        sums.Children.Add(Sum(labels.AmountDue, Money(totals.Total), true));
        sheet.Children.Add(sums);
        if (!string.IsNullOrWhiteSpace(Notes))
        {
            var notes = Ink(Notes, "muted");
            notes.Margin = new Thickness(0, 16, 0, 0);
            sheet.Children.Add(notes);
        }
        _paper.Child = sheet;
    }

    private static InvoiceStudioInk Ink(string? text, string classes)
    {
        var ink = new InvoiceStudioInk { Text = text, IsVisible = !string.IsNullOrEmpty(text) };
        foreach (var c in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            ink.Classes.Add(c);
        }
        return ink;
    }

    private static Control Pair(string label, string? value)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(Ink(label, "muted"));
        row.Children.Add(Ink(value, "number"));
        return row;
    }

    private static Control Sum(string label, string value, bool total)
    {
        var row = new DockPanel { Margin = new Thickness(0, total ? 6 : 1, 0, 0) };
        var amount = Ink(value, total ? "number total" : "number");
        amount.Margin = new Thickness(16, 0, 0, 0);
        DockPanel.SetDock(amount, Dock.Right);
        row.Children.Add(amount);
        row.Children.Add(Ink(label, total ? "strong" : "muted"));
        return row;
    }

    #endregion
}
