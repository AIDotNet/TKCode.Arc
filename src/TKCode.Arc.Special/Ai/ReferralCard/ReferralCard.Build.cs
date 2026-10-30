using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// The parts of ReferralCard, built once when the template applies and then updated in place.
public partial class ReferralCard
{
    private static readonly ReferralShareChannel[] s_channels =
        [ReferralShareChannel.Post, ReferralShareChannel.Network, ReferralShareChannel.Email];

    /// <summary>The funnel numbers roll to a new value in 600ms on Arc's standard curve.</summary>
    internal static readonly Motion.MotionSpec NumberRoll =
        Motion.MotionSpec.Tween(TimeSpan.FromMilliseconds(600), Motion.ArcEasings.Standard);

    private Panel? _root;

    // Header
    private TextBlock? _title;
    private TextBlock? _description;

    // Link and share
    private TextBlock? _linkLabel;
    private Grid? _linkGrid;
    private Border? _linkField;
    private TextBlock? _linkText;
    private Run? _linkDomain;
    private Run? _linkCode;
    private CopyButton? _copy;
    private Grid? _shareGrid;
    private readonly Button[] _shareButtons = new Button[3];
    private readonly ArcContentSwap[] _shareGlyphs = new ArcContentSwap[3];

    // Email
    private TextBlock? _emailLabel;
    private ReferralCardFrame? _emailFrame;
    private ScrollViewer? _chipScroller;
    private StackPanel? _chipPanel;
    private ReferralCardEmailBox? _editor;
    private Button? _send;
    private ArcContentSwap? _hint;

    // Funnel
    private readonly TextBlock[] _funnelLabels = new TextBlock[3];
    private readonly RollingDigits[] _funnelNumbers = new RollingDigits[3];
    private readonly StackPanel[] _funnelCells = new StackPanel[3];

    // Milestone
    private StackPanel? _milestoneSection;
    private ReferralCardMilestoneBar? _bar;
    private ArcContentSwap? _milestoneLine;

    // People
    private TextBlock? _listLabel;
    private ScrollViewer? _listScroller;
    private StackPanel? _listPanel;
    private TextBlock? _listEmpty;

    private TextBlock? _status;

    private void Build(Panel root)
    {
        _root = root;
        var column = new StackPanel().Tok(StackPanel.SpacingProperty, ReferralCardTokenKind.SectionGap);
        column.Children.Add(BuildHeader());
        column.Children.Add(BuildLink());
        column.Children.Add(BuildEmail());
        column.Children.Add(Hairline());
        column.Children.Add(BuildFunnel());
        column.Children.Add(BuildMilestone());
        column.Children.Add(BuildList());
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
        if (_copy is not null)
        {
            _copy.Copied -= OnCopied;
        }
        if (_editor is not null)
        {
            _editor.Owner = null;
        }
        _chips.Clear();
        _rows.Clear();
        _shownTitle = null;
        _shownMilestoneKey = null;
        _shownHintKey = null;
        _breakpoint = null;
    }

    private static Border Hairline() =>
        new Border { Height = 1 }.ReferralArc(Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);

    private static TextBlock SmallLabel() =>
        new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.LabelFontSize)
            .ReferralArc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);

    private Control BuildHeader()
    {
        var glyph = new ArcGlyph { Data = ReferralCardIcons.Gift, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            .Tok(ArcGlyph.SizeProperty, ReferralCardTokenKind.MarkGlyphSize)
            .ReferralArc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Accent);
        var mark = new Border { Child = glyph, VerticalAlignment = VerticalAlignment.Top }
            .Tok(WidthProperty, ReferralCardTokenKind.MarkSize)
            .Tok(HeightProperty, ReferralCardTokenKind.MarkSize)
            .ReferralArc(Border.BackgroundProperty, ArcDesignTokenKind.AccentSubtle)
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        AutomationProperties.SetAccessibilityView(mark, AccessibilityView.Raw);
        _title = new TextBlock { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold }
            .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.TitleFontSize)
            .Tok(TextBlock.LineHeightProperty, ReferralCardTokenKind.TitleLineHeight)
            .ReferralArc(TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily)
            .ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        AutomationProperties.SetHeadingLevel(_title, 2);
        _description = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.LineHeightProperty, ReferralCardTokenKind.DescriptionLineHeight)
            .ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var words = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(_title);
        words.Children.Add(_description);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 14 };
        header.Children.Add(mark);
        header.Children.Add(words);
        Grid.SetColumn(words, 1);
        return header;
    }

    private Control BuildLink()
    {
        _linkLabel = SmallLabel();
        _linkDomain = new Run().ReferralArc(TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _linkCode = new Run().ReferralArc(TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _linkText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.PrefixCharacterEllipsis,
            FontWeight = FontWeight.Medium
        }.ReferralArc(TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        _linkText.Inlines!.Add(_linkDomain);
        _linkText.Inlines.Add(_linkCode);
        _copy = new CopyButton { VerticalAlignment = VerticalAlignment.Center };
        _copy.Copied += OnCopied;
        var fieldGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        fieldGrid.Children.Add(_linkText);
        fieldGrid.Children.Add(_copy);
        Grid.SetColumn(_copy, 1);
        _linkField = new Border { Child = fieldGrid }
            .Tok(HeightProperty, ReferralCardTokenKind.FieldHeight)
            .Tok(Border.PaddingProperty, ReferralCardTokenKind.LinkPadding)
            .ReferralArc(Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted)
            .BindCornerRadius(ArcDesignTokenKind.RadiusControl);

        _shareGrid = new Grid().Tok(Grid.ColumnSpacingProperty, ReferralCardTokenKind.InlineGap);
        for (var i = 0; i < s_channels.Length; i++)
        {
            var channel = s_channels[i];
            var swap = new ArcContentSwap { Mode = ContentSwapMode.None, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            swap.Content = ShareGlyph(ReferralCardIcons.Share(channel));
            var button = new Button
            {
                Variant = ButtonVariant.Secondary,
                Content = swap,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center
            }.Tok(HeightProperty, ReferralCardTokenKind.ShareSize);
            button.Click += (_, _) => Share(channel);
            _shareButtons[i] = button;
            _shareGlyphs[i] = swap;
            Grid.SetColumn(button, i);
            _shareGrid.Children.Add(button);
        }

        _linkGrid = new Grid().Tok(Grid.ColumnSpacingProperty, ReferralCardTokenKind.InlineGap).Tok(Grid.RowSpacingProperty, ReferralCardTokenKind.InlineGap);
        _linkGrid.Children.Add(_linkField);
        _linkGrid.Children.Add(_shareGrid);
        var section = new StackPanel().Tok(StackPanel.SpacingProperty, ReferralCardTokenKind.LabelGap);
        section.Children.Add(_linkLabel);
        section.Children.Add(_linkGrid);
        return section;
    }

    private static ArcGlyph ShareGlyph(Geometry data) =>
        new ArcGlyph { Data = data, VerticalAlignment = VerticalAlignment.Center }
            .Tok(ArcGlyph.SizeProperty, ReferralCardTokenKind.ShareGlyphSize);

    private Control BuildEmail()
    {
        _emailLabel = SmallLabel();
        _editor = new ReferralCardEmailBox { Owner = this, VerticalAlignment = VerticalAlignment.Center }
            .Tok(MinWidthProperty, ReferralCardTokenKind.EditorMinWidth);
        _chipPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }
            .Tok(StackPanel.SpacingProperty, ReferralCardTokenKind.ChipGap);
        _chipPanel.Children.Add(_editor);
        _chipScroller = new ScrollViewer
        {
            Content = _chipPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Center
        };
        _emailFrame = new ReferralCardFrame { Content = _chipScroller }
            .Tok(HeightProperty, ReferralCardTokenKind.FieldHeight)
            .Tok(PaddingProperty, ReferralCardTokenKind.EmailPadding);
        _emailFrame.AddHandler(PointerPressedEvent, OnFramePressed, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: false);
        _send = new Button { Variant = ButtonVariant.Primary, VerticalAlignment = VerticalAlignment.Center }
            .Tok(HeightProperty, ReferralCardTokenKind.FieldHeight);
        _send.Click += (_, _) => _ = SendAsync();
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }.Tok(Grid.ColumnSpacingProperty, ReferralCardTokenKind.InlineGap);
        row.Children.Add(_emailFrame);
        row.Children.Add(_send);
        Grid.SetColumn(_send, 1);
        _hint = new ArcContentSwap
        {
            Mode = ContentSwapMode.None,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center
        }.Tok(MinHeightProperty, ReferralCardTokenKind.HintLineHeight);
        var section = new StackPanel().Tok(StackPanel.SpacingProperty, ReferralCardTokenKind.LabelGap);
        section.Children.Add(_emailLabel);
        section.Children.Add(row);
        section.Children.Add(_hint);
        return section;
    }

    private void OnFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (source == _emailFrame || source == _chipScroller || source == _chipPanel))
        {
            _editor?.Focus(NavigationMethod.Pointer);
        }
    }

    private Control BuildFunnel()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*") }.Tok(Grid.ColumnSpacingProperty, ReferralCardTokenKind.InlineGap);
        for (var i = 0; i < 3; i++)
        {
            var label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
                .Tok(TextBlock.FontSizeProperty, ReferralCardTokenKind.LabelFontSize)
                .ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            var number = new RollingDigits
            {
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Left,
                IsShortestTurn = false,
                IsInstant = true,
                WheelMotion = NumberRoll
            }
                .Tok(RollingDigits.FontSizeProperty, ReferralCardTokenKind.NumberFontSize)
                .Tok(HeightProperty, ReferralCardTokenKind.NumberHeight)
                .ReferralArc(RollingDigits.ForegroundProperty, i == 2 ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Foreground);
            var cell = new StackPanel { Spacing = 2 };
            cell.Children.Add(label);
            cell.Children.Add(number);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
            _funnelLabels[i] = label;
            _funnelNumbers[i] = number;
            _funnelCells[i] = cell;
        }
        return grid;
    }

    private Control BuildMilestone()
    {
        _bar = new ReferralCardMilestoneBar()
            .Tok(ReferralCardMilestoneBar.SegmentHeightProperty, ReferralCardTokenKind.SegmentHeight)
            .Tok(ReferralCardMilestoneBar.SegmentGapProperty, ReferralCardTokenKind.SegmentGap)
            .Tok(ReferralCardMilestoneBar.SegmentCornerRadiusProperty, ReferralCardTokenKind.SegmentCornerRadius)
            .ReferralArc(ReferralCardMilestoneBar.TrackBrushProperty, ArcDesignTokenKind.SurfaceMuted)
            .ReferralArc(ReferralCardMilestoneBar.FillBrushProperty, ArcDesignTokenKind.Accent)
            .ReferralArc(ReferralCardMilestoneBar.DoneBrushProperty, ArcDesignTokenKind.Success);
        _milestoneLine = new ArcContentSwap
        {
            Mode = ContentSwapMode.None,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top
        }.Tok(MinHeightProperty, ReferralCardTokenKind.MilestoneLineHeight);
        _milestoneSection = new StackPanel { Spacing = 10 };
        _milestoneSection.Children.Add(_bar);
        _milestoneSection.Children.Add(_milestoneLine);
        return _milestoneSection;
    }

    /// <summary>The milestone line: a gift (or a check once unlocked), the words, and the detail once unlocked.</summary>
    private Control MilestoneLineView(bool unlocked, string text, string? detail, bool narrow)
    {
        var glyph = new ArcGlyph { Data = unlocked ? ArcIcons.CircleCheck : ReferralCardIcons.Gift, Size = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) }
            .ReferralArc(ArcGlyph.ForegroundProperty, unlocked ? ArcDesignTokenKind.Success : ArcDesignTokenKind.TextSecondary);
        var words = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxLines = narrow ? 2 : 1, TextTrimming = TextTrimming.CharacterEllipsis }
            .Tok(TextBlock.LineHeightProperty, ReferralCardTokenKind.MilestoneLineHeight)
            .ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var main = new Run(text);
        if (unlocked)
        {
            main.FontWeight = FontWeight.Medium;
        }
        words.Inlines!.Add(main);
        if (unlocked && !string.IsNullOrWhiteSpace(detail))
        {
            words.Inlines.Add(new Run(" · " + detail.Trim()).ReferralArc(TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary));
        }
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 8 };
        line.Children.Add(glyph);
        line.Children.Add(words);
        Grid.SetColumn(words, 1);
        return line;
    }

    private Control BuildList()
    {
        _listLabel = SmallLabel();
        _listPanel = new StackPanel().Tok(StackPanel.SpacingProperty, ReferralCardTokenKind.ListRowGap);
        _listScroller = new ScrollViewer
        {
            Content = _listPanel,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        _listEmpty = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 320,
            IsVisible = false
        }.ReferralArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var box = new Panel().Tok(HeightProperty, ReferralCardTokenKind.ListHeight);
        box.Children.Add(_listScroller);
        box.Children.Add(_listEmpty);
        var section = new StackPanel().Tok(StackPanel.SpacingProperty, ReferralCardTokenKind.LabelGap);
        section.Children.Add(_listLabel);
        section.Children.Add(box);
        return section;
    }

    /// <summary>The fixed words: labels, names, tooltips and the placeholder.</summary>
    private void RefreshWords()
    {
        if (_root is null)
        {
            return;
        }
        var labels = Labels;
        _linkLabel!.Text = labels.LinkLabel;
        _copy!.Label = labels.Copy;
        _copy.CopiedLabel = labels.Copied;
        AutomationProperties.SetName(_copy, labels.CopyLink);
        for (var i = 0; i < s_channels.Length; i++)
        {
            var name = ShareName(s_channels[i], labels);
            AutomationProperties.SetName(_shareButtons[i], name);
            Tooltip.SetTip(_shareButtons[i], name);
        }
        _emailLabel!.Text = labels.EmailLabel;
        _editor!.PlaceholderText = labels.EmailPlaceholder;
        AutomationProperties.SetName(_editor, labels.EmailLabel);
        _send!.Content = labels.Send;
        _funnelLabels[0].Text = labels.Invited;
        _funnelLabels[1].Text = labels.SignedUp;
        _funnelLabels[2].Text = labels.Earned;
        _listLabel!.Text = labels.ListLabel;
        AutomationProperties.SetName(_listPanel!, labels.ListLabel);
        _listEmpty!.Text = labels.ListEmpty;
        AutomationProperties.SetName(_bar!, labels.MilestoneName);
        _shownHintKey = null;
        _shownMilestoneKey = null;
    }

    private static string ShareName(ReferralShareChannel channel, ReferralCardLabels labels) => channel switch
    {
        ReferralShareChannel.Network => labels.ShareNetwork,
        ReferralShareChannel.Email => labels.ShareEmail,
        _ => labels.SharePost
    };

    /// <summary>
    /// Lays the link row out for the card's width: the share buttons sit beside the link from 470px, and take their own full
    /// row below it (three equal buttons); the milestone line keeps room for two lines.
    /// </summary>
    private void ApplyBreakpoint(double width, bool force)
    {
        if (_root is null)
        {
            return;
        }
        if (!(width > 0))
        {
            width = 560;
        }
        var narrow = width < NarrowWidth;
        if (!force && _breakpoint == narrow)
        {
            return;
        }
        _breakpoint = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        var grid = _linkGrid!;
        grid.ColumnDefinitions = narrow ? new ColumnDefinitions("*") : new ColumnDefinitions("*,Auto");
        grid.RowDefinitions = narrow ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
        Grid.SetRow(_shareGrid!, narrow ? 1 : 0);
        Grid.SetColumn(_shareGrid!, narrow ? 0 : 1);
        _shareGrid!.ColumnDefinitions = narrow ? new ColumnDefinitions("*,*,*") : new ColumnDefinitions("Auto,Auto,Auto");
        foreach (var button in _shareButtons)
        {
            if (narrow)
            {
                button.Width = double.NaN;
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            else
            {
                button.Tok(WidthProperty, ReferralCardTokenKind.ShareSize);
                button.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }
        _milestoneLine!.Tok(MinHeightProperty, narrow ? ReferralCardTokenKind.MilestoneTwoLineHeight : ReferralCardTokenKind.MilestoneLineHeight);
        _shownMilestoneKey = null;
        RefreshMilestone(animate: false);
    }
}
