using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ArcButton = TKCode.Arc.Button;
using TKCode.Arc.DesignTokens;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>The card's parts, built in code inside the template's root panel.</summary>
public partial class CodeReview
{
    private Panel? _root;
    private DeveloperAnnouncer? _announcer;
    private readonly CodeReviewPalette _palette = new();

    private Border? _header;
    private Grid? _headerGrid;
    private StackPanel? _titleBlock;
    private TextBlock? _title;
    private TextBlock? _byline;
    private ArcButton? _approve;
    private CodeReviewBand? _statusBand;
    private TextBlock? _statusText;
    private Border? _toolbar;
    private Grid? _toolbarGrid;
    private CodeReviewTabStrip? _tabs;
    private CodeReviewProgress? _progress;
    private CodeReviewPanelSwitch? _switch;
    private readonly Dictionary<string, CodeReviewFilePanel> _panels = new(StringComparer.Ordinal);
    private bool _compact;

    /// <summary>The polite live region (tests and demos read its history).</summary>
    internal DeveloperAnnouncer? Announcer => _announcer;

    internal CodeReviewTabStrip? TabStrip => _tabs;

    internal CodeReviewProgress? Progress => _progress;

    internal ArcButton? ApproveButton => _approve;

    internal CodeReviewBand? StatusBand => _statusBand;

    /// <summary>The panel of the file shown (tests).</summary>
    internal CodeReviewFilePanel? CurrentPanel => _switch?.Current as CodeReviewFilePanel;

    /// <summary>The panel of a file, built on first use (tests).</summary>
    internal CodeReviewFilePanel PanelOf(string fileId) => Panel(_files.First(f => f.Id == fileId));

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Panel>(RootPart);
        _announcer = e.NameScope.Find<DeveloperAnnouncer>(AnnouncerPart);
        Build();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        RefreshPalette();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        _submit?.Cancel();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => RefreshPalette();

    private void RefreshPalette()
    {
        if (VisualRoot is null)
        {
            return;
        }
        _palette.Refresh(this);
        foreach (var panel in _panels.Values)
        {
            panel.Diff.Repaint();
        }
    }

    private void Build()
    {
        if (_root is null)
        {
            return;
        }
        _root.Children.Clear();
        _panels.Clear();
        var stack = new StackPanel();

        // Header: title block and the approve button.
        _title = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeight.Medium,
            LineHeight = 18.7,
            LetterSpacing = -0.17,
            TextWrapping = TextWrapping.Wrap
        };
        ArcTokens.Bind(_title, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_title, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        AutomationProperties.SetHeadingLevel(_title, 2);
        _byline = new TextBlock { FontSize = 13, LineHeight = 21, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(_byline, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_byline, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _bylineRow = new Grid { ColumnDefinitions = new ColumnDefinitions("20,8,*"), Margin = new Thickness(0, 6, 0, 0) };
        Grid.SetColumn(_byline, 2);
        _bylineRow.Children.Add(_byline);
        _titleBlock = new StackPanel();
        _titleBlock.Children.Add(_title);
        _titleBlock.Children.Add(_bylineRow);

        _approve = CodeReviewButtons.Medium(Labels.Approve, ButtonVariant.Primary);
        _approve.Click += (_, _) => OnApproveClicked();
        _headerGrid = new Grid();
        _header = new Border { Child = _headerGrid };
        stack.Children.Add(_header);

        // Status after approving.
        _statusText = new TextBlock { FontSize = 13, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        ArcTokens.Bind(_statusText, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_statusText, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var undo = CodeReviewButtons.Small(Labels.Undo, ButtonVariant.Ghost);
        undo.Click += (_, _) => Withdraw();
        DockPanel.SetDock(undo, Dock.Right);
        var check = CodeReviewUi.Glyph(ArcIcons.CircleCheck, 16, ArcDesignTokenKind.Accent);
        check.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(check, Dock.Left);
        var statusRow = new DockPanel { LastChildFill = true };
        statusRow.Children.Add(check);
        statusRow.Children.Add(undo);
        statusRow.Children.Add(_statusText);
        var status = new Border { Padding = new Thickness(14, 6, 6, 6), Child = statusRow }.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        CodeReviewUi.Bind(status, Border.BackgroundProperty, CodeReviewTokenKind.StatusFill);
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Off);
        _statusBand = new CodeReviewBand { Child = new Border { Child = status }, IsExpanded = false };
        stack.Children.Add(_statusBand);

        // Tabs and progress.
        _tabs = new CodeReviewTabStrip();
        AutomationProperties.SetName(_tabs, Labels.FilesName);
        _tabs.Chosen += index => SelectIndex(index);
        _progress = new CodeReviewProgress();
        _toolbarGrid = new Grid();
        _toolbar = new Border { Child = _toolbarGrid };
        stack.Children.Add(_toolbar);

        _switch = new CodeReviewPanelSwitch();
        stack.Children.Add(_switch);
        _root.Children.Add(stack);

        LoadData();
        ApplyLayout(force: true);
    }

    private Grid? _bylineRow;
    private CodeReviewAvatar? _authorAvatar;

    /// <summary>Fills the header from <see cref="Pull"/> and the tabs from the files.</summary>
    private void FillHeader()
    {
        if (_title is null || _byline is null || _bylineRow is null)
        {
            return;
        }
        var pull = EffectivePull;
        var labels = Labels;
        _title.Inlines = new InlineCollection { new Run(pull.Title + " "), Muted("#" + pull.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), FontWeight.Normal) };
        if (_authorAvatar is not null)
        {
            _bylineRow.Children.Remove(_authorAvatar);
        }
        _authorAvatar = new CodeReviewAvatar(pull.Author.Name, 20) { Margin = new Thickness(0, 1, 0, 0) };
        _bylineRow.Children.Add(_authorAvatar);
        var author = new Run(pull.Author.Name) { FontWeight = FontWeight.Medium };
        author.Bind(TextElement.ForegroundProperty, author.GetResourceObservable(ArcDesignTokenKind.Foreground));
        _byline.Inlines = new InlineCollection
        {
            author,
            new Run(" " + labels.MergeLine(pull.CommitCount) + " "),
            new InlineUIContainer(Chip(pull.BaseBranch)),
            new Run(" " + labels.From + " "),
            new InlineUIContainer(Chip(pull.HeadBranch))
        };
        AutomationProperties.SetName(this, labels.ReviewName(pull.Number));
    }

    private static Run Muted(string text, FontWeight weight)
    {
        var run = new Run(text) { FontWeight = weight };
        run.Bind(TextElement.ForegroundProperty, run.GetResourceObservable(ArcDesignTokenKind.TextMuted));
        return run;
    }

    private static Control Chip(string text)
    {
        var block = CodeReviewUi.Mono(text, ArcDesignTokenKind.Foreground);
        block.LineHeight = 16.8;
        var chip = new Border { Padding = new Thickness(6, 1), Child = block, Margin = new Thickness(0, 0, 0, -4) }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
        ArcTokens.Bind(chip, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        return chip;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyLayout(force: false);
    }

    /// <summary>Places the header and toolbar parts for the wide or compact layout.</summary>
    private void ApplyLayout(bool force)
    {
        var width = Bounds.Width;
        var compact = width > 0 && width < CompactWidth;
        if (!force && compact == _compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_header is null || _headerGrid is null || _titleBlock is null || _approve is null || _toolbar is null
            || _toolbarGrid is null || _tabs is null || _progress is null || _statusBand is null)
        {
            return;
        }
        _headerGrid.Children.Clear();
        _toolbarGrid.Children.Clear();
        if (compact)
        {
            CodeReviewUi.Bind(_header, Border.PaddingProperty, CodeReviewTokenKind.CompactHeaderPadding);
            _headerGrid.ColumnDefinitions = new ColumnDefinitions("*");
            _headerGrid.RowDefinitions = new RowDefinitions("Auto,14,Auto");
            Grid.SetColumn(_titleBlock, 0);
            Grid.SetRow(_titleBlock, 0);
            Grid.SetColumn(_approve, 0);
            Grid.SetRow(_approve, 2);
            _approve.HorizontalAlignment = HorizontalAlignment.Left;
            _toolbar.Padding = new Thickness(_palette.CompactSidePadding, 0, _palette.CompactSidePadding, 12);
            _toolbarGrid.ColumnDefinitions = new ColumnDefinitions("*");
            _toolbarGrid.RowDefinitions = new RowDefinitions("Auto,10,Auto");
            Grid.SetRow(_progress, 0);
            Grid.SetColumn(_progress, 0);
            Grid.SetRow(_tabs, 2);
            Grid.SetColumn(_tabs, 0);
            _progress.IsCompact = true;
            _progress.Margin = new Thickness(0, 6, 0, 0);
        }
        else
        {
            CodeReviewUi.Bind(_header, Border.PaddingProperty, CodeReviewTokenKind.HeaderPadding);
            _headerGrid.ColumnDefinitions = new ColumnDefinitions("*,24,Auto");
            _headerGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetColumn(_titleBlock, 0);
            Grid.SetRow(_titleBlock, 0);
            Grid.SetColumn(_approve, 2);
            Grid.SetRow(_approve, 0);
            _approve.HorizontalAlignment = HorizontalAlignment.Right;
            _toolbar.Padding = new Thickness(_palette.SidePadding, 0, _palette.SidePadding, 12);
            _toolbarGrid.ColumnDefinitions = new ColumnDefinitions("*,20,Auto");
            _toolbarGrid.RowDefinitions = new RowDefinitions("Auto");
            Grid.SetRow(_progress, 0);
            Grid.SetColumn(_progress, 2);
            Grid.SetRow(_tabs, 0);
            Grid.SetColumn(_tabs, 0);
            _progress.IsCompact = false;
            _progress.Margin = default;
        }
        _headerGrid.Children.Add(_titleBlock);
        _headerGrid.Children.Add(_approve);
        _toolbarGrid.Children.Add(_tabs);
        _toolbarGrid.Children.Add(_progress);
        if (_statusBand.Child is Border statusHost)
        {
            var side = compact ? _palette.CompactSidePadding : _palette.SidePadding;
            statusHost.Padding = new Thickness(side, 0, side, 16);
        }
        foreach (var panel in _panels.Values)
        {
            panel.UpdateLayoutMode();
        }
    }

    /// <summary>The panel of a file, built on first use and kept (open boxes survive switching files).</summary>
    private CodeReviewFilePanel Panel(CodeReviewFileState file)
    {
        if (!_panels.TryGetValue(file.Id, out var panel))
        {
            panel = new CodeReviewFilePanel(this, file, () => _threads.Where(t => t.FileId == file.Id));
            panel.ViewedToggle.SetChecked(file.IsViewed);
            panel.ViewedToggle.Toggled += on => SetViewed(file, on);
            _panels[file.Id] = panel;
        }
        return panel;
    }

    /// <summary>Refreshes the tabs (selection, viewed, open counts) and the progress.</summary>
    private void UpdateChrome(bool animate)
    {
        if (_tabs is null || _progress is null)
        {
            return;
        }
        var labels = Labels;
        for (var i = 0; i < _files.Count && i < _tabs.Tabs.Count; i++)
        {
            var file = _files[i];
            var open = _threads.Count(t => t.FileId == file.Id && !t.IsResolved);
            _tabs.Tabs[i].Set(i == _selected, file.IsViewed, open, labels);
        }
        int added = 0, deleted = 0;
        foreach (var file in _files)
        {
            var (a, d) = CodeReviewDiff.Stat(file.Lines);
            added += a;
            deleted += d;
        }
        ViewedCount = _files.Count(f => f.IsViewed);
        _progress.Set(ViewedCount, _files.Count, added, deleted, labels, animate);
    }

    private void UpdateStatusText(int comments)
    {
        if (_statusText is null)
        {
            return;
        }
        var muted = Labels.WithComments(comments);
        var inlines = new InlineCollection { new Run(Labels.ApprovedStatus) };
        if (muted.Length > 0)
        {
            inlines.Add(Muted(" " + muted, FontWeight.Normal));
        }
        _statusText.Inlines = inlines;
    }
}
