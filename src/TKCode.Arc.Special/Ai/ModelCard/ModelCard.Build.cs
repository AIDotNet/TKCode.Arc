using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// The parts of ModelCard, built once when the template applies and then updated in place.
public partial class ModelCard
{
    /// <summary>How many benchmark rows the card reserves: the model and two rivals.</summary>
    internal const int BenchmarkRows = 3;

    private Panel? _root;

    // Header
    private ArcContentSwap? _markSwap;
    private string? _shownMarkKey;
    private ModelCardText? _name;
    private ModelCardText? _provider;
    private Switch? _compareSwitch;

    // The stacked body
    private Grid? _stack;
    private readonly Dictionary<ModelCardPanelKind, Control> _panels = [];
    private readonly Dictionary<ModelCardPanelKind, MotionValue<double>> _fades = [];
    private ModelCardPanelKind? _shownPanel;

    // Details
    private ModelCardText? _summary;
    private WrapPanel? _chips;
    private readonly List<ModelCardChip> _chipList = [];
    private Grid? _specGrid;
    private readonly List<ModelCardSpecCell> _specCells = [];
    private ModelCardNumber? _context;
    private ModelCardNumber? _maxOutput;
    private ModelCardNumber? _pages;
    private ModelCardBar? _gauge;
    private ModelCardText? _cutoff;
    private TextBlock? _pricesHeading;
    private Grid? _priceGrid;
    private readonly List<ModelCardSpecCell> _priceCells = [];
    private ModelCardNumber? _input;
    private ModelCardNumber? _output;
    private ModelCardNumber? _cached;
    private TextBlock? _estimateHeading;
    private Grid? _estimateGrid;
    private Slider? _slider;
    private ModelCardNumber? _cost;
    private TextBlock? _mix;
    private StackPanel? _benchSection;
    private Grid? _benchHeader;
    private TextBlock? _benchHeading;
    private SegmentedControl? _switcher;
    private string? _switcherKey;
    private ModelCardText? _caption;
    private readonly ModelCardBenchmarkRow[] _rows = new ModelCardBenchmarkRow[BenchmarkRows];
    private ModelCardText? _readout;

    // Compare
    private TextBlock? _compareWithLabel;
    private Button? _rivalButton;
    private ModelCardText? _rivalButtonText;
    private MenuSurface? _rivalMenu;
    private string? _rivalMenuKey;
    private Grid? _compareHeader;
    private ModelCardText? _headModel;
    private ModelCardText? _headRival;
    private StackPanel? _compareRowsPanel;
    private readonly List<ModelCardCompareRow> _compareRows = [];
    private string? _compareRowsKey;
    private ModelCardText? _tally;
    private TextBlock? _footnote;

    // Loading and error
    private StackPanel? _skeleton;
    private TextBlock? _errorText;
    private Button? _retry;

    // Footer
    private Grid? _footer;
    private ModelCardText? _id;
    private StackPanel? _footerButtons;
    private CopyButton? _copy;
    private Button? _select;
    private ArcGlyph? _selectCheck;
    private TextBlock? _selectText;
    private TextBlock? _status;

    private static readonly (ModelCardModalities Modality, Geometry Glyph)[] s_modalities =
    [
        (ModelCardModalities.Text, ArcIcons.Type),
        (ModelCardModalities.Image, ArcIcons.Image),
        (ModelCardModalities.Pdf, ArcIcons.FileText),
        (ModelCardModalities.Audio, ArcIcons.Volume2),
        (ModelCardModalities.Video, ArcIcons.FilePlay),
        (ModelCardModalities.Tools, ArcIcons.Wrench),
        (ModelCardModalities.Reasoning, ArcIcons.Brain),
    ];

    private void Build(Panel root)
    {
        _root = root;
        var column = new StackPanel().Tok(StackPanel.SpacingProperty, ModelCardTokenKind.SectionGap);
        column.Children.Add(BuildHeader());
        column.Children.Add(Hairline());
        column.Children.Add(BuildStack());
        column.Children.Add(Hairline());
        column.Children.Add(BuildFooter());
        root.Children.Add(column);
        _status = new TextBlock { Height = 0, Opacity = 0, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        root.Children.Add(_status);
        RefreshWords();
    }

    private void Teardown()
    {
        _root?.Children.Clear();
        _root = null;
        if (_compareSwitch is not null)
        {
            _compareSwitch.IsCheckedChanged -= OnCompareToggled;
        }
        if (_rivalButton is not null)
        {
            DropdownMenu.SetMenu(_rivalButton, null);
        }
        _panels.Clear();
        _fades.Clear();
        _shownPanel = null;
        _chipList.Clear();
        _specCells.Clear();
        _priceCells.Clear();
        _compareRows.Clear();
        _compareRowsKey = null;
        _switcherKey = null;
        _rivalMenuKey = null;
        _shownMarkKey = null;
    }

    private static Border Hairline() =>
        new Border { Height = 1 }.Arc(Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);

    private static TextBlock Heading() =>
        new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.FontSizeProperty, ModelCardTokenKind.HeadingFontSize)
            .Tok(TextBlock.LineHeightProperty, ModelCardTokenKind.HeadingLineHeight)
            .Arc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .Arc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);

    private static TextBlock Caption() =>
        new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.FontSizeProperty, ModelCardTokenKind.LabelFontSize)
            .Tok(TextBlock.LineHeightProperty, ModelCardTokenKind.LabelLineHeight)
            .Arc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);

    private static ModelCardNumber Value() =>
        new ModelCardNumber { FontWeight = FontWeight.SemiBold }
            .Tok(ModelCardNumber.FontSizeProperty, ModelCardTokenKind.ValueFontSize)
            .Tok(HeightProperty, ModelCardTokenKind.ValueLineHeight)
            .Arc(ModelCardNumber.ForegroundProperty, ArcDesignTokenKind.Foreground)
            .Arc(ModelCardNumber.PlaceholderBrushProperty, ArcDesignTokenKind.TextMuted);

    private Control BuildHeader()
    {
        _markSwap = new ArcContentSwap
        {
            Mode = ContentSwapMode.None,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAccessibilityView(_markSwap, AccessibilityView.Raw);
        _name = new ModelCardText()
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.NameFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.NameLineHeight)
            .Tok(ModelCardText.FontWeightProperty, ModelCardTokenKind.NameFontWeight)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _provider = new ModelCardText()
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.ProviderFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.ProviderLineHeight)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(_name);
        names.Children.Add(_provider);
        _compareSwitch = new Switch { VerticalAlignment = VerticalAlignment.Center };
        _compareSwitch.IsCheckedChanged += OnCompareToggled;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") }
            .Tok(Grid.ColumnSpacingProperty, ModelCardTokenKind.HeaderGap);
        header.Children.Add(_markSwap);
        header.Children.Add(names);
        header.Children.Add(_compareSwitch);
        Grid.SetColumn(names, 1);
        Grid.SetColumn(_compareSwitch, 2);
        return header;
    }

    private Control BuildStack()
    {
        _stack = new Grid();
        AddPanel(ModelCardPanelKind.Details, BuildDetails());
        AddPanel(ModelCardPanelKind.Compare, BuildCompare());
        AddPanel(ModelCardPanelKind.Loading, BuildLoading());
        AddPanel(ModelCardPanelKind.Error, BuildError());
        return _stack;
    }

    private void AddPanel(ModelCardPanelKind kind, Control panel)
    {
        panel.Opacity = 0;
        panel.IsHitTestVisible = false;
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.None);
        _panels[kind] = panel;
        _stack!.Children.Add(panel);
    }

    private Control BuildDetails()
    {
        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Top }.Tok(StackPanel.SpacingProperty, ModelCardTokenKind.SectionGap);

        _summary = new ModelCardText { Lines = 2 }
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.SummaryFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.SummaryLineHeight)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        details.Children.Add(_summary);

        _chips = new WrapPanel()
            .Tok(WrapPanel.ItemSpacingProperty, ModelCardTokenKind.ChipGap)
            .Tok(WrapPanel.LineSpacingProperty, ModelCardTokenKind.ChipGap);
        foreach (var (modality, glyph) in s_modalities)
        {
            var chip = new ModelCardChip(modality, glyph);
            _chipList.Add(chip);
            _chips.Children.Add(chip);
        }
        details.Children.Add(_chips);

        // Specs: context window (with the gauge and the page count), max output, knowledge cutoff.
        _context = Value();
        _gauge = new ModelCardBar()
            .Tok(ModelCardBar.BarHeightProperty, ModelCardTokenKind.GaugeHeight)
            .Arc(ModelCardBar.TrackBrushProperty, ArcDesignTokenKind.SurfaceMuted)
            .Arc(ModelCardBar.FillBrushProperty, ArcDesignTokenKind.Accent);
        AutomationProperties.SetAccessibilityView(_gauge, AccessibilityView.Raw);
        _pages = new ModelCardNumber()
            .Tok(ModelCardNumber.FontSizeProperty, ModelCardTokenKind.LabelFontSize)
            .Arc(ModelCardNumber.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var contextExtra = new StackPanel { Spacing = 6 };
        contextExtra.Children.Add(_gauge);
        contextExtra.Children.Add(_pages);
        _maxOutput = Value();
        _cutoff = new ModelCardText { FontWeight = FontWeight.SemiBold }
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.ValueFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.ValueLineHeight)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _specCells.Add(new ModelCardSpecCell(_context, contextExtra));
        _specCells.Add(new ModelCardSpecCell(_maxOutput));
        _specCells.Add(new ModelCardSpecCell(_cutoff));
        _specGrid = new Grid().Tok(Grid.ColumnSpacingProperty, ModelCardTokenKind.CellGap);
        foreach (var cell in _specCells)
        {
            _specGrid.Children.Add(cell);
        }
        details.Children.Add(_specGrid);

        // Prices per million tokens.
        _pricesHeading = Heading();
        _input = Value();
        _output = Value();
        _cached = Value();
        _priceCells.Add(new ModelCardSpecCell(_input));
        _priceCells.Add(new ModelCardSpecCell(_output));
        _priceCells.Add(new ModelCardSpecCell(_cached));
        _priceGrid = new Grid().Tok(Grid.ColumnSpacingProperty, ModelCardTokenKind.CellGap);
        foreach (var cell in _priceCells)
        {
            _priceGrid.Children.Add(cell);
        }
        var prices = new StackPanel { Spacing = 8 };
        prices.Children.Add(_pricesHeading);
        prices.Children.Add(_priceGrid);
        details.Children.Add(prices);

        // The cost estimator: the volume slider, the estimate in a reserved width, and how it is billed.
        _estimateHeading = Heading();
        _slider = new Slider
        {
            Minimum = 0,
            Maximum = ModelCardFormat.VolumeStops.Length - 1,
            SmallChange = 1,
            LargeChange = 1,
            IsValueVisible = true,
            ValueFormatter = index => ModelCardFormat.TokensText(ModelCardFormat.VolumeStops[Math.Clamp((int)Math.Round(index), 0, ModelCardFormat.VolumeStops.Length - 1)]),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        _slider.ValueChanged += OnSliderChanged;
        _cost = new ModelCardNumber { FontWeight = FontWeight.SemiBold, Alignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom }
            .Tok(ModelCardNumber.FontSizeProperty, ModelCardTokenKind.CostFontSize)
            .Tok(WidthProperty, ModelCardTokenKind.CostWidth)
            .Arc(ModelCardNumber.ForegroundProperty, ArcDesignTokenKind.Foreground)
            .Arc(ModelCardNumber.PlaceholderBrushProperty, ArcDesignTokenKind.TextMuted);
        _cost.Margin = new Thickness(16, 0, 0, 8);
        _estimateGrid = new Grid();
        _estimateGrid.Children.Add(_slider);
        _estimateGrid.Children.Add(_cost);
        _mix = Caption();
        var estimate = new StackPanel { Spacing = 4 };
        estimate.Children.Add(_estimateHeading);
        estimate.Children.Add(_estimateGrid);
        estimate.Children.Add(_mix);
        details.Children.Add(estimate);

        // Benchmarks: heading and switcher, caption, three rows and the readout.
        _benchHeading = Heading();
        _switcher = new SegmentedControl { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        _switcher.SelectionChanged += OnSwitcherChanged;
        _benchHeader = new Grid();
        _benchHeader.Children.Add(_benchHeading);
        _benchHeader.Children.Add(_switcher);
        _caption = new ModelCardText()
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.LabelFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.LabelLineHeight)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var rows = new StackPanel { Spacing = 2 };
        for (var i = 0; i < BenchmarkRows; i++)
        {
            var row = new ModelCardBenchmarkRow();
            row.Tok(PaddingProperty, ModelCardTokenKind.RowPadding);
            var index = i;
            row.PointerEntered += (_, e) =>
            {
                if (e.Pointer.Type != PointerType.Touch)
                {
                    SetHover(index);
                }
            };
            row.PointerExited += (_, _) => ClearHover(index);
            row.GotFocus += (_, _) => SetFocusRow(index);
            row.LostFocus += (_, _) => ClearFocusRow(index);
            _rows[i] = row;
            rows.Children.Add(row);
        }
        _readout = new ModelCardText { Lines = 2 }
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.LabelFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.ReadoutLineHeight)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetAccessibilityView(_readout, AccessibilityView.Raw);
        _benchSection = new StackPanel { Spacing = 8 };
        _benchSection.Children.Add(_benchHeader);
        _benchSection.Children.Add(_caption);
        _benchSection.Children.Add(rows);
        _benchSection.Children.Add(_readout);
        details.Children.Add(_benchSection);
        return details;
    }

    private Control BuildCompare()
    {
        var compare = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Top };

        _compareWithLabel = Heading();
        _rivalButtonText = new ModelCardText()
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.HeadingLineHeight);
        _rivalButtonText.MaxWidth = 220;
        var chevron = new ArcGlyph { Data = ArcIcons.ChevronDown, Size = 14, VerticalAlignment = VerticalAlignment.Center };
        var buttonContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        buttonContent.Children.Add(_rivalButtonText);
        buttonContent.Children.Add(chevron);
        _rivalButton = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, Content = buttonContent };
        _rivalMenu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.Start, AnchorGap = 6 };
        _rivalMenu.ItemClicked += OnRivalPicked;
        DropdownMenu.SetMenu(_rivalButton, _rivalMenu);
        var pick = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        pick.Children.Add(_compareWithLabel);
        pick.Children.Add(_rivalButton);
        compare.Children.Add(pick);

        _headModel = HeadText();
        _headRival = HeadText();
        _compareHeader = new Grid();
        _compareHeader.Children.Add(_headModel);
        _compareHeader.Children.Add(_headRival);
        _compareRowsPanel = new StackPanel();
        var table = new StackPanel { Spacing = 6 };
        table.Children.Add(_compareHeader);
        table.Children.Add(_compareRowsPanel);
        compare.Children.Add(table);

        _tally = new ModelCardText()
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.HeadingFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.HeadingLineHeight)
            .Arc(ModelCardText.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _footnote = Caption().Tok(MinHeightProperty, ModelCardTokenKind.LabelLineHeight);
        var tally = new StackPanel { Spacing = 2 };
        tally.Children.Add(_tally);
        tally.Children.Add(_footnote);
        compare.Children.Add(tally);
        return compare;
    }

    private static ModelCardText HeadText() =>
        new ModelCardText { VerticalAlignment = VerticalAlignment.Bottom }
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.LabelFontSize)
            .Tok(ModelCardText.LineHeightProperty, ModelCardTokenKind.LabelLineHeight)
            .Arc(ModelCardText.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);

    private Control BuildLoading()
    {
        _skeleton = new StackPanel { VerticalAlignment = VerticalAlignment.Top }.Tok(StackPanel.SpacingProperty, ModelCardTokenKind.SectionGap);
        AutomationProperties.SetAccessibilityView(_skeleton, AccessibilityView.Raw);
        var summary = new StackPanel { Spacing = 8 };
        summary.Children.Add(new ModelCardSkeletonBlock(12, 0.92));
        summary.Children.Add(new ModelCardSkeletonBlock(12, 0.64));
        _skeleton.Children.Add(summary);
        _skeleton.Children.Add(new ModelCardSkeletonBlock(26, 0.86));
        _skeleton.Children.Add(SkeletonTiles(84));
        _skeleton.Children.Add(new ModelCardSkeletonBlock(12, 0.3));
        _skeleton.Children.Add(SkeletonTiles(64));
        _skeleton.Children.Add(new ModelCardSkeletonBlock(48));
        var bars = new StackPanel { Spacing = 14 };
        for (var i = 0; i < BenchmarkRows; i++)
        {
            bars.Children.Add(new ModelCardSkeletonBlock(10, 1 - i * 0.12));
        }
        _skeleton.Children.Add(bars);
        // The panel fade and the loading pulse both drive Opacity, so they need separate elements: the host is the
        // panel (faded by ShowPanel), the skeleton inside it pulses.
        var host = new Panel();
        host.Children.Add(_skeleton);
        return host;
    }

    private static Grid SkeletonTiles(double height)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") }.Tok(Grid.ColumnSpacingProperty, ModelCardTokenKind.CellGap);
        for (var i = 0; i < 3; i++)
        {
            var tile = new ModelCardSkeletonBlock(height);
            Grid.SetColumn(tile, i);
            grid.Children.Add(tile);
        }
        return grid;
    }

    private Control BuildError()
    {
        var glyph = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 22, HorizontalAlignment = HorizontalAlignment.Center }
            .Arc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
        _errorText = new TextBlock { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 360 }
            .Arc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetLiveSetting(_errorText, AutomationLiveSetting.Assertive);
        _retry = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Center };
        _retry.Click += (_, _) => RaiseEvent(new RoutedEventArgs(RetryRequestedEvent));
        var error = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        error.Children.Add(glyph);
        error.Children.Add(_errorText);
        error.Children.Add(_retry);
        return error;
    }

    private Control BuildFooter()
    {
        _id = new ModelCardText { VerticalAlignment = VerticalAlignment.Center }
            .Tok(ModelCardText.FontSizeProperty, ModelCardTokenKind.IdFontSize)
            .Arc(ModelCardText.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily)
            .Arc(ModelCardText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _copy = new CopyButton { VerticalAlignment = VerticalAlignment.Center };
        _copy.Copied += (_, _) => RaiseEvent(new ModelCardIdEventArgs(IdCopiedEvent, _copy.Value ?? string.Empty));
        _selectCheck = new ArcGlyph { Data = ArcIcons.Check, Size = 14, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        _selectText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var selectContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        selectContent.Children.Add(_selectCheck);
        selectContent.Children.Add(_selectText);
        _select = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, Content = selectContent, VerticalAlignment = VerticalAlignment.Center };
        _select.Click += (_, _) => RaiseEvent(new ModelCardModelEventArgs(SelectRequestedEvent, ShownModel));
        _footerButtons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }
            .Tok(StackPanel.SpacingProperty, ModelCardTokenKind.FooterGap);
        _footerButtons.Children.Add(_copy);
        _footerButtons.Children.Add(_select);
        _footer = new Grid().Tok(Grid.RowSpacingProperty, ModelCardTokenKind.FooterGap);
        _footer.Children.Add(_id);
        _footer.Children.Add(_footerButtons);
        return _footer;
    }
}
