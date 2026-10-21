using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One line of the <see cref="BillingOverview"/> invoice table: date, number, a right-aligned amount in tabular figures,
/// a status badge and a download icon button, with a hairline on top. The column header is the same part with
/// <see cref="IsHeader"/> set, so header and rows share one column layout. Under <c>:narrow</c> the number column hides.
/// Rows are created once by the block and reused when the list changes.
/// </summary>
[TemplatePart(DownloadPart, typeof(Button))]
[TemplatePart(AmountPart, typeof(TextBlock))]
[PseudoClasses(HeaderPseudoClass, NarrowPseudoClass, MutedPseudoClass)]
internal sealed class BillingOverviewInvoiceRow : TemplatedControl
{
    public const string DownloadPart = "PART_Download";
    public const string AmountPart = "PART_Amount";
    public const string HeaderPseudoClass = ":header";
    public const string NarrowPseudoClass = ":narrow";
    public const string MutedPseudoClass = ":muted";

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    public static readonly StyledProperty<string?> DateTextProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, string?>(nameof(DateText));

    public static readonly StyledProperty<string?> NumberTextProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, string?>(nameof(NumberText));

    public static readonly StyledProperty<string?> AmountTextProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, string?>(nameof(AmountText));

    public static readonly StyledProperty<string?> StatusTextProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, string?>(nameof(StatusText));

    public static readonly StyledProperty<BadgeTone> StatusToneProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, BadgeTone>(nameof(StatusTone));

    public static readonly StyledProperty<string?> DownloadNameProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, string?>(nameof(DownloadName));

    public static readonly StyledProperty<bool> IsHeaderProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, bool>(nameof(IsHeader));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<bool> IsMutedProperty =
        AvaloniaProperty.Register<BillingOverviewInvoiceRow, bool>(nameof(IsMuted));

    private Button? _download;

    public string? DateText { get => GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }
    public string? NumberText { get => GetValue(NumberTextProperty); set => SetValue(NumberTextProperty, value); }
    public string? AmountText { get => GetValue(AmountTextProperty); set => SetValue(AmountTextProperty, value); }
    public string? StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }
    public BadgeTone StatusTone { get => GetValue(StatusToneProperty); set => SetValue(StatusToneProperty, value); }
    public string? DownloadName { get => GetValue(DownloadNameProperty); set => SetValue(DownloadNameProperty, value); }
    public bool IsHeader { get => GetValue(IsHeaderProperty); set => SetValue(IsHeaderProperty, value); }
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }
    public bool IsMuted { get => GetValue(IsMutedProperty); set => SetValue(IsMutedProperty, value); }

    /// <summary>The invoice this row shows (null for the header).</summary>
    public BillingInvoice? Invoice { get; set; }

    /// <summary>The row's download button was pressed.</summary>
    public event EventHandler? DownloadRequested;

    /// <summary>The download button, once the template is applied.</summary>
    public Button? DownloadButton => _download;

    static BillingOverviewInvoiceRow()
    {
        FocusableProperty.OverrideDefaultValue<BillingOverviewInvoiceRow>(false);
    }

    protected override Type StyleKeyOverride => typeof(BillingOverviewInvoiceRow);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_download is not null)
        {
            _download.Click -= OnDownloadClick;
        }
        base.OnApplyTemplate(e);
        _download = e.NameScope.Find<Button>(DownloadPart);
        if (_download is not null)
        {
            _download.Click += OnDownloadClick;
        }
        if (e.NameScope.Find<TextBlock>(AmountPart) is { } amount)
        {
            // Tabular figures keep the right-aligned amounts lined up digit for digit.
            amount.FontFeatures = s_tabular;
        }
        ApplyDownloadName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsHeaderProperty)
        {
            PseudoClasses.Set(HeaderPseudoClass, IsHeader);
            UpdateAccessibleName();
        }
        else if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
        }
        else if (change.Property == IsMutedProperty)
        {
            PseudoClasses.Set(MutedPseudoClass, IsMuted);
        }
        else if (change.Property == DownloadNameProperty)
        {
            ApplyDownloadName();
        }
        else if (change.Property == DateTextProperty || change.Property == NumberTextProperty ||
                 change.Property == AmountTextProperty || change.Property == StatusTextProperty)
        {
            UpdateAccessibleName();
        }
    }

    private void ApplyDownloadName()
    {
        if (_download is null)
        {
            return;
        }
        AutomationProperties.SetName(_download, DownloadName);
        Tooltip.SetTip(_download, DownloadName);
    }

    private void UpdateAccessibleName()
    {
        if (IsHeader)
        {
            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
            return;
        }
        var parts = new[] { DateText, NumberText, AmountText, StatusText }.Where(p => !string.IsNullOrEmpty(p));
        AutomationProperties.SetName(this, string.Join(", ", parts));
    }

    private void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DownloadRequested?.Invoke(this, EventArgs.Empty);
    }
}
