using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// The parts of UpgradePrompt, built once when the template applies and then updated in place.
public partial class UpgradePrompt
{
    internal const int OfferPanel = 0;
    internal const int SuccessPanel = 1;
    internal const int ReminderPanel = 2;

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    /// <summary>The price rolls between monthly and yearly on a 0.28s spring without bounce.</summary>
    internal static readonly Motion.MotionSpec PriceRoll = Motion.MotionSpec.Spring(Motion.SpringSpec.FromVisual(0.28, 0));

    private Panel? _root;

    // Header
    private Border? _badge;
    private UpgradePromptTone? _badgeFill;
    private UpgradePromptTone? _badgeInk;
    private ArcContentSwap? _badgeSwap;
    private Geometry? _badgeGlyph;
    private UpgradePromptText? _title;
    private UpgradePromptText? _description;

    // Meter
    private Control? _meter;
    private UpgradePromptText? _meterLabel;
    private UpgradePromptText? _meterCount;
    private UpgradePromptMeterBar? _bar;
    private UpgradePromptTone? _barFill;
    private ArcContentSwap? _statusIconSwap;
    private Geometry? _statusGlyph;
    private UpgradePromptTone? _statusInk;
    private UpgradePromptText? _statusText;
    private UpgradePromptTone? _statusTextInk;
    private UpgradePromptText? _meterNote;

    // Body panels
    private UpgradePromptPanels? _body;
    private Control? _priceGroup;
    private TextBlock? _planName;
    private RollingDigits? _price;
    private TextBlock? _period;
    private BillingToggle? _toggle;
    private UpgradePromptText? _billingNote;
    private Control? _errorLine;
    private UpgradePromptText? _errorText;
    private TextBlock? _unlocksHeading;
    private StackPanel? _features;
    private string? _featuresKey;
    private TextBlock? _limitsHeading;
    private StackPanel? _limits;
    private string? _limitsKey;
    private UpgradePromptText? _chargedNote;
    private UpgradePromptText? _reminderLine;
    private UpgradePromptText? _reminderHint;

    // Footer panels
    private UpgradePromptPanels? _footer;
    private Button? _later;
    private Button? _upgrade;
    private Button? _continue;
    private Button? _seeAgain;

    private TextBlock? _status;
    private UpgradePromptPriceRow? _priceRow;

    // Parts read by the tests.
    internal UpgradePromptText? TitlePart => _title;
    internal UpgradePromptText? DescriptionPart => _description;
    internal UpgradePromptText? MeterCountPart => _meterCount;
    internal UpgradePromptText? StatusTextPart => _statusText;
    internal UpgradePromptText? BillingNotePart => _billingNote;
    internal UpgradePromptText? ErrorTextPart => _errorText;
    internal Control? ErrorLinePart => _errorLine;
    internal UpgradePromptText? ChargedNotePart => _chargedNote;
    internal UpgradePromptMeterBar? BarPart => _bar;
    internal Control? MeterPart => _meter;
    internal Control? PriceGroupPart => _priceGroup;
    internal RollingDigits? PricePart => _price;
    internal BillingToggle? TogglePart => _toggle;
    internal UpgradePromptPriceRow? PriceRowPart => _priceRow;
    internal StackPanel? FeaturesPart => _features;
    internal StackPanel? LimitsPart => _limits;
    internal UpgradePromptPanels? BodyPanels => _body;
    internal UpgradePromptPanels? FooterPanels => _footer;
    internal Button? LaterButton => _later;
    internal Button? UpgradeButton => _upgrade;
    internal Button? ContinueButton => _continue;
    internal Button? SeeAgainButton => _seeAgain;
    internal TextBlock? StatusRegion => _status;

    // The parts an empty card (no plan) hides.
    private Control? _meterBlock;
    private Control? _hairline;
    private Control? _bodyHost;

    private void Build(Panel root)
    {
        _root = root;
        var column = new StackPanel().PromptTok(StackPanel.SpacingProperty, UpgradePromptTokenKind.SectionGap);
        column.Children.Add(BuildHeader());
        column.Children.Add(_meterBlock = BuildMeter());
        column.Children.Add(_hairline = Hairline());
        column.Children.Add(_bodyHost = BuildBody());
        column.Children.Add(BuildFooter());
        root.Children.Add(column);
        _status = new TextBlock { Height = 0, Opacity = 0, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        root.Children.Add(_status);
    }

    private void Teardown()
    {
        _root?.Children.Clear();
        _root = null;
        if (_toggle is not null)
        {
            _toggle.ValueChanged -= OnToggleChanged;
            _toggle.Loaded -= OnToggleLoaded;
        }
        _viewport?.Dispose();
        _viewport = null;
        _revealed = false;
        _toggleKey = null;
        _featuresKey = null;
        _limitsKey = null;
        _badgeGlyph = null;
        _statusGlyph = null;
    }

    private static Border Hairline() =>
        new Border { Height = 1 }.PromptArc(Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);

    private static UpgradePromptText Small() =>
        new UpgradePromptText()
            .PromptTok(UpgradePromptText.FontSizeProperty, UpgradePromptTokenKind.SmallFontSize)
            .PromptTok(UpgradePromptText.LineHeightProperty, UpgradePromptTokenKind.SmallLineHeight)
            .PromptArc(UpgradePromptText.ForegroundProperty, ArcDesignTokenKind.TextMuted);

    private static TextBlock Heading() =>
        new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            .PromptTok(TextBlock.FontSizeProperty, UpgradePromptTokenKind.SmallFontSize)
            .PromptTok(TextBlock.LineHeightProperty, UpgradePromptTokenKind.SmallLineHeight)
            .PromptArc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .PromptArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);

    private Control BuildHeader()
    {
        _badgeSwap = new ArcContentSwap
        {
            Mode = ContentSwapMode.None,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _badge = new Border { VerticalAlignment = VerticalAlignment.Top, Child = _badgeSwap }
            .PromptTok(WidthProperty, UpgradePromptTokenKind.BadgeSize)
            .PromptTok(HeightProperty, UpgradePromptTokenKind.BadgeSize)
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        AutomationProperties.SetAccessibilityView(_badge, AccessibilityView.Raw);
        _badgeFill = new UpgradePromptTone(_badge, Border.BackgroundProperty);
        _badgeInk = new UpgradePromptTone(_badgeSwap, TextBlock.ForegroundProperty);

        _title = new UpgradePromptText()
            .PromptTok(UpgradePromptText.FontSizeProperty, UpgradePromptTokenKind.TitleFontSize)
            .PromptTok(UpgradePromptText.LineHeightProperty, UpgradePromptTokenKind.TitleLineHeight)
            .PromptTok(UpgradePromptText.LetterSpacingProperty, UpgradePromptTokenKind.TitleLetterSpacing)
            .PromptArc(UpgradePromptText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _title.FontWeight = FontWeight.SemiBold;
        AutomationProperties.SetHeadingLevel(_title, 2);
        AutomationProperties.SetControlTypeOverride(_title, AutomationControlType.Text);
        _description = new UpgradePromptText { Lines = 2 }
            .PromptTok(UpgradePromptText.LineHeightProperty, UpgradePromptTokenKind.BodyLineHeight)
            .PromptArc(UpgradePromptText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var texts = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(_title);
        texts.Children.Add(_description);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") }
            .PromptTok(Grid.ColumnSpacingProperty, UpgradePromptTokenKind.HeaderGap);
        header.Children.Add(_badge);
        header.Children.Add(texts);
        Grid.SetColumn(texts, 1);
        return header;
    }

    private Control BuildMeter()
    {
        _meterLabel = new UpgradePromptText()
            .PromptTok(UpgradePromptText.LineHeightProperty, UpgradePromptTokenKind.BodyLineHeight)
            .PromptArc(UpgradePromptText.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .PromptArc(UpgradePromptText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _meterCount = new UpgradePromptText { TextAlignment = TextAlignment.Right, HorizontalAlignment = HorizontalAlignment.Right }
            .PromptTok(UpgradePromptText.LineHeightProperty, UpgradePromptTokenKind.BodyLineHeight)
            .PromptArc(UpgradePromptText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var labelRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        labelRow.Children.Add(_meterLabel);
        labelRow.Children.Add(_meterCount);
        Grid.SetColumn(_meterCount, 1);

        _bar = new UpgradePromptMeterBar()
            .PromptTok(UpgradePromptMeterBar.BarHeightProperty, UpgradePromptTokenKind.MeterHeight)
            .PromptArc(UpgradePromptMeterBar.TrackBrushProperty, ArcDesignTokenKind.SurfaceMuted);
        _barFill = new UpgradePromptTone(_bar, UpgradePromptMeterBar.FillBrushProperty);
        AutomationProperties.SetAccessibilityView(_bar, AccessibilityView.Raw);

        _statusIconSwap = new ArcContentSwap
        {
            Mode = ContentSwapMode.None,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _statusInk = new UpgradePromptTone(_statusIconSwap, TextBlock.ForegroundProperty);
        _statusText = Small();
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _statusTextInk = new UpgradePromptTone(_statusText, UpgradePromptText.ForegroundProperty);
        _meterNote = Small();
        _meterNote.TextAlignment = TextAlignment.Right;
        _meterNote.HorizontalAlignment = HorizontalAlignment.Right;
        _meterNote.VerticalAlignment = VerticalAlignment.Center;
        var statusRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") }
            .PromptTok(Grid.ColumnSpacingProperty, UpgradePromptTokenKind.StatusGap);
        statusRow.Children.Add(_statusIconSwap);
        statusRow.Children.Add(_statusText);
        statusRow.Children.Add(_meterNote);
        Grid.SetColumn(_statusText, 1);
        Grid.SetColumn(_meterNote, 2);
        _meterNote.Margin = new Thickness(6, 0, 0, 0);

        var meter = new StackPanel().PromptTok(StackPanel.SpacingProperty, UpgradePromptTokenKind.MeterGap);
        meter.Children.Add(labelRow);
        meter.Children.Add(_bar);
        meter.Children.Add(statusRow);
        AutomationProperties.SetControlTypeOverride(meter, AutomationControlType.ProgressBar);
        _meter = meter;
        return meter;
    }

    private Control BuildBody()
    {
        _body = new UpgradePromptPanels();
        _body.Add(BuildOffer());
        _body.Add(BuildSuccess());
        _body.Add(BuildReminder());
        return _body.Host;
    }

    private Control BuildOffer()
    {
        _planName = new TextBlock()
            .PromptTok(TextBlock.LineHeightProperty, UpgradePromptTokenKind.BodyLineHeight)
            .PromptArc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .PromptArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _price = new RollingDigits
            {
                WheelMotion = PriceRoll,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Bottom
            }
            .PromptTok(RollingDigits.FontSizeProperty, UpgradePromptTokenKind.PriceFontSize)
            .PromptArc(RollingDigits.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily)
            .PromptArc(RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        AutomationProperties.SetAccessibilityView(_price, AccessibilityView.Raw);
        _period = new TextBlock { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6) }
            .PromptArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        AutomationProperties.SetAccessibilityView(_period, AccessibilityView.Raw);
        var amount = new StackPanel { Orientation = Orientation.Horizontal }
            .PromptTok(StackPanel.SpacingProperty, UpgradePromptTokenKind.PeriodGap)
            .PromptTok(HeightProperty, UpgradePromptTokenKind.PriceHeight);
        amount.Children.Add(_price);
        amount.Children.Add(_period);
        var priceGroup = new StackPanel { Spacing = 2 };
        priceGroup.Children.Add(_planName);
        priceGroup.Children.Add(amount);
        _priceGroup = priceGroup;

        _toggle = new BillingToggle();
        _toggle.ValueChanged += OnToggleChanged;
        _toggle.Loaded += OnToggleLoaded;
        var priceRow = _priceRow = new UpgradePromptPriceRow().PromptTok(UpgradePromptPriceRow.GapProperty, UpgradePromptTokenKind.PriceToggleGap);
        priceRow.Children.Add(priceGroup);
        priceRow.Children.Add(_toggle);

        // The note under the price and the error share one slot of two reserved lines.
        _billingNote = Small();
        _billingNote.Lines = 2;
        var errorGlyph = new ArcGlyph { Data = ArcIcons.CircleAlert, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0) }
            .PromptTok(ArcGlyph.SizeProperty, UpgradePromptTokenKind.StatusIconSize)
            .PromptArc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
        _errorText = Small().PromptArc(UpgradePromptText.ForegroundProperty, ArcDesignTokenKind.Danger);
        _errorText.Lines = 2;
        var errorLine = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Opacity = 0, IsHitTestVisible = false }
            .PromptTok(Grid.ColumnSpacingProperty, UpgradePromptTokenKind.StatusGap);
        errorLine.Children.Add(errorGlyph);
        errorLine.Children.Add(_errorText);
        Grid.SetColumn(_errorText, 1);
        AutomationProperties.SetLiveSetting(_errorText, AutomationLiveSetting.Assertive);
        _errorLine = errorLine;
        var noteSlot = new Grid();
        noteSlot.Children.Add(_billingNote);
        noteSlot.Children.Add(errorLine);

        var plan = new StackPanel { Spacing = 6 };
        plan.Children.Add(priceRow);
        plan.Children.Add(noteSlot);

        _unlocksHeading = Heading();
        _features = new StackPanel().PromptTok(StackPanel.SpacingProperty, UpgradePromptTokenKind.RowGap);
        AutomationProperties.SetControlTypeOverride(_features, AutomationControlType.List);
        var unlocks = new StackPanel { Spacing = 10 };
        unlocks.Children.Add(_unlocksHeading);
        unlocks.Children.Add(_features);

        var offer = new StackPanel { VerticalAlignment = VerticalAlignment.Top }
            .PromptTok(StackPanel.SpacingProperty, UpgradePromptTokenKind.SectionGap);
        offer.Children.Add(plan);
        offer.Children.Add(unlocks);
        return offer;
    }

    private Control BuildSuccess()
    {
        _limitsHeading = Heading();
        _limits = new StackPanel().PromptTok(StackPanel.SpacingProperty, UpgradePromptTokenKind.RowGap);
        AutomationProperties.SetControlTypeOverride(_limits, AutomationControlType.List);
        _chargedNote = Small();
        _chargedNote.Lines = 2;
        var success = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Top };
        success.Children.Add(_limitsHeading);
        success.Children.Add(_limits);
        success.Children.Add(_chargedNote);
        return success;
    }

    private Control BuildReminder()
    {
        var glyph = new ArcGlyph { Data = ArcIcons.Clock, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
            .PromptTok(ArcGlyph.SizeProperty, UpgradePromptTokenKind.MarkSize)
            .PromptArc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var tile = MarkTile(glyph);
        tile.VerticalAlignment = VerticalAlignment.Top;
        _reminderLine = new UpgradePromptText { Lines = 2 }
            .PromptTok(UpgradePromptText.LineHeightProperty, UpgradePromptTokenKind.BodyLineHeight)
            .PromptArc(UpgradePromptText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _reminderHint = Small();
        _reminderHint.Lines = 2;
        var texts = new StackPanel { Spacing = 4 };
        texts.Children.Add(_reminderLine);
        texts.Children.Add(_reminderHint);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Top }
            .PromptTok(Grid.ColumnSpacingProperty, UpgradePromptTokenKind.MarkGap);
        row.Children.Add(tile);
        row.Children.Add(texts);
        Grid.SetColumn(texts, 1);
        return row;
    }

    private Control BuildFooter()
    {
        _footer = new UpgradePromptPanels();

        _later = new Button { Variant = ButtonVariant.Secondary };
        _later.Click += (_, _) => Dismiss();
        _upgrade = new Button { Variant = ButtonVariant.Primary, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        _upgrade.Click += (_, _) => _ = UpgradeAsync();
        var offer = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") }
            .PromptTok(Grid.ColumnSpacingProperty, UpgradePromptTokenKind.ButtonGap);
        offer.Children.Add(_later);
        offer.Children.Add(_upgrade);
        Grid.SetColumn(_upgrade, 1);
        _footer.Add(offer);

        _continue = new Button { Variant = ButtonVariant.Primary, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        _continue.Click += (_, _) => Continue();
        _footer.Add(_continue);

        _seeAgain = new Button { Variant = ButtonVariant.Secondary, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        _seeAgain.Click += (_, _) => ShowOffer();
        _footer.Add(_seeAgain);
        return _footer.Host;
    }

    /// <summary>A 28px tile on the muted surface holding a mark.</summary>
    private static Border MarkTile(Control content)
    {
        var tile = new Border { Child = content, VerticalAlignment = VerticalAlignment.Center }
            .PromptTok(WidthProperty, UpgradePromptTokenKind.MarkTileSize)
            .PromptTok(HeightProperty, UpgradePromptTokenKind.MarkTileSize)
            .PromptTok(Border.BackgroundProperty, UpgradePromptTokenKind.MarkTileFill)
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep8);
        AutomationProperties.SetAccessibilityView(tile, AccessibilityView.Raw);
        return tile;
    }

    /// <summary>A 16px mark: the given glyph in its color, or the accent; a check when there is none.</summary>
    private static ArcGlyph Mark(Geometry? data, IBrush? brush, ArcDesignTokenKind fallback)
    {
        var glyph = new ArcGlyph { Data = data ?? ArcIcons.Check, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }
            .PromptTok(ArcGlyph.SizeProperty, UpgradePromptTokenKind.MarkSize);
        if (brush is not null)
        {
            glyph.Foreground = brush;
        }
        else
        {
            glyph.PromptArc(ArcGlyph.ForegroundProperty, fallback);
        }
        return glyph;
    }

    private static Control FeatureRow(UpgradePromptFeature feature)
    {
        var label = new TextBlock { Text = feature.Label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }
            .PromptArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") }
            .PromptTok(Grid.ColumnSpacingProperty, UpgradePromptTokenKind.MarkGap)
            .PromptTok(MinHeightProperty, UpgradePromptTokenKind.RowHeight);
        row.Children.Add(MarkTile(Mark(feature.Mark, feature.MarkBrush, ArcDesignTokenKind.Accent)));
        row.Children.Add(label);
        Grid.SetColumn(label, 1);
        AutomationProperties.SetName(row, feature.Label);
        AutomationProperties.SetControlTypeOverride(row, AutomationControlType.ListItem);
        return row;
    }

    private static Control LimitRow(UpgradePromptLimit limit)
    {
        var label = new TextBlock { Text = limit.Label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }
            .PromptArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var value = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (limit.Mark is not null)
        {
            value.Children.Add(Mark(limit.Mark, limit.MarkBrush, ArcDesignTokenKind.Foreground));
        }
        value.Children.Add(new TextBlock { Text = limit.Value, VerticalAlignment = VerticalAlignment.Center, FontFeatures = s_tabular }
            .PromptArc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .PromptArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground));
        var check = Mark(ArcIcons.Check, null, ArcDesignTokenKind.Success);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") }
            .PromptTok(Grid.ColumnSpacingProperty, UpgradePromptTokenKind.MarkGap)
            .PromptTok(MinHeightProperty, UpgradePromptTokenKind.RowHeight);
        row.Children.Add(MarkTile(check));
        row.Children.Add(label);
        row.Children.Add(value);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(value, 2);
        AutomationProperties.SetName(row, limit.ToString());
        AutomationProperties.SetControlTypeOverride(row, AutomationControlType.ListItem);
        return row;
    }
}
