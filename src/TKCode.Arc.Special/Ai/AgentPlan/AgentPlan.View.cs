using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

public partial class AgentPlan
{
    private Border? _header;
    private Panel? _headRow;
    private TextBlock? _goal;
    private AgentPlanProgress? _progress;
    private Panel? _listHost;
    private Border? _footer;
    private ExplainerAnnouncer? _announcer;

    // Header.
    private readonly ArcGlyph _mark = new() { Size = 16, StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _heading = new() { FontSize = 14, LineHeight = 15.4, LetterSpacing = -0.14, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _headingBefore = new() { FontSize = 14, LineHeight = 15.4, LetterSpacing = -0.14, VerticalAlignment = VerticalAlignment.Center };
    private readonly AgentPlanCount _headingCount = new() { FontSize = 14, LineHeight = 15.4, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _headingAfter = new() { FontSize = 14, LineHeight = 15.4, LetterSpacing = -0.14, VerticalAlignment = VerticalAlignment.Center };
    private readonly AgentPlanSwap _meta = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _modelName = new() { FontSize = 12, LineHeight = 20, LetterSpacing = -0.14, HorizontalAlignment = HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly StackPanel _position = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly TextBlock[] _positionWords = [new(), new(), new()];
    private readonly AgentPlanCount[] _positionCounts = [new(), new()];
    private bool _positionFirstFirst = true;
    private string? _positionKey;

    // List.
    private AgentPlanList? _list;
    private readonly AgentPlanCollapse _emptyBlock = new() { IsOpen = false };
    private readonly TextBlock _emptyText = new() { FontSize = 14, LineHeight = 20, LetterSpacing = -0.14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(44, 8, 12, 8) };
    private readonly AgentPlanCollapse _addBlock = new() { IsOpen = true };
    private readonly AgentPlanButton _addButton = new() { Classes = { "add" }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _addLabel = new() { FontSize = 14, LineHeight = 20, LetterSpacing = -0.14, VerticalAlignment = VerticalAlignment.Center };

    // Footer.
    private readonly AgentPlanSwap _footerSwap = new();
    private readonly Grid _draftRow = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
    private readonly Button _keepButton = new() { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Button _approveButton = new() { Variant = ButtonVariant.Primary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Grid _runRow = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
    private readonly ArcContentSwap _statusText = new() { Mode = ContentSwapMode.Label, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _pauseButton = new() { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _doneRow = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
    private readonly AgentPlanText _doneText = new() { LineHeight = 20, LetterSpacing = -0.14 };
    private readonly Button _startOverButton = new() { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _keepLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _pauseLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _startOverLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ArcGlyph _pauseGlyph = new() { Size = 16, VerticalAlignment = VerticalAlignment.Center };
    private bool _narrowApplied;
    private bool _built;

    internal AgentPlanList? List => _list;

    internal ExplainerAnnouncer? Announcer => _announcer;

    /// <summary>The accessible name of the plan.</summary>
    internal string AccessibleName => Labels.PlanName(EffectiveModel?.Name, EffectiveGoal);

    internal AgentModel? EffectiveModel => Model ?? (_sampleFilled ? AgentPlanSamples.SampleModel : null);

    internal string? EffectiveGoal => Goal ?? (_sampleFilled ? AgentPlanSamples.Goal : null);

    internal string? EffectiveSummary => Summary ?? (Status is null ? ActiveSimulation?.Summary : null);

    // Test access.
    internal AgentPlanProgress? ProgressLine => _progress;
    internal Button KeepButton => _keepButton;
    internal Button ApproveButton => _approveButton;
    internal Button PauseButton => _pauseButton;
    internal Button StartOverButton => _startOverButton;
    internal AgentPlanButton AddButton => _addButton;
    internal AgentPlanText DoneText => _doneText;
    internal AgentPlanCount HeadingCount => _headingCount;
    internal AgentPlanSwap MetaSwap => _meta;
    internal AgentPlanSwap FooterSwap => _footerSwap;
    internal TextBlock EmptyText => _emptyText;
    internal AgentPlanCollapse EmptyBlock => _emptyBlock;
    internal ArcContentSwap StatusText => _statusText;
    internal IEnumerable<AgentPlanRow> Rows => _list?.Live ?? Enumerable.Empty<AgentPlanRow>();
    internal Grid DraftRow => _draftRow;
    internal Grid RunRow => _runRow;
    internal Grid DoneRow => _doneRow;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _header = e.NameScope.Find<Border>(HeaderPart);
        _headRow = e.NameScope.Find<Panel>(HeadRowPart);
        _goal = e.NameScope.Find<TextBlock>(GoalPart);
        _progress = e.NameScope.Find<AgentPlanProgress>(ProgressPart);
        _listHost = e.NameScope.Find<Panel>(ListHostPart);
        _footer = e.NameScope.Find<Border>(FooterPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (!_built)
        {
            Build();
            _built = true;
        }
        AttachParts();
        RebuildRows();
        RefreshAll(animate: false);
    }

    private void Build()
    {
        // Header: the mark's 20px slot, the heading, the meta on the right.
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("20,*,Auto"), ColumnSpacing = 12, Height = 20 };
        head.Children.Add(_mark);
        var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
        title.Children.Add(_heading);
        title.Children.Add(_headingBefore);
        title.Children.Add(_headingCount);
        title.Children.Add(_headingAfter);
        Grid.SetColumn(title, 1);
        head.Children.Add(title);
        _position.Children.Add(_positionWords[0]);
        _position.Children.Add(_positionCounts[0]);
        _position.Children.Add(_positionWords[1]);
        _position.Children.Add(_positionCounts[1]);
        _position.Children.Add(_positionWords[2]);
        foreach (var word in _positionWords)
        {
            word.FontSize = 12;
            word.LineHeight = 20;
            word.LetterSpacing = -0.14;
            word.VerticalAlignment = VerticalAlignment.Center;
        }
        foreach (var count in _positionCounts)
        {
            count.FontSize = 12;
            count.LineHeight = 20;
            count.VerticalAlignment = VerticalAlignment.Center;
        }
        _meta.Children.Add(_modelName);
        _meta.Children.Add(_position);
        Grid.SetColumn(_meta, 2);
        head.Children.Add(_meta);
        _headGrid = head;

        // The list column: the empty sentence, the rows, Add step.
        _list = new AgentPlanList();
        _list.DragSwapped += OnDragSwapped;
        _list.DragEnded += OnDragEnded;
        _emptyBlock.Child = _emptyText;
        var add = new Grid { ColumnDefinitions = new ColumnDefinitions("20,Auto"), ColumnSpacing = 12 };
        add.Children.Add(new ArcGlyph { Data = ArcIcons.Plus, Size = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(_addLabel, 1);
        add.Children.Add(_addLabel);
        _addButton.Content = add;
        _addButton.Click += (_, e) =>
        {
            e.Handled = true;
            AddStep();
        };
        _addBlock.Child = _addButton;

        // Footer layers.
        _draftRow.Children.Add(_keepButton);
        _draftRow.Children.Add(_approveButton);
        Grid.SetColumn(_keepButton, 1);
        Grid.SetColumn(_approveButton, 2);
        _runRow.Children.Add(_statusText);
        Grid.SetColumn(_pauseButton, 1);
        _runRow.Children.Add(_pauseButton);
        var doneBox = new Border { Padding = new Thickness(0, 8), Child = _doneText };
        _doneRow.Children.Add(doneBox);
        Grid.SetColumn(_startOverButton, 1);
        _doneRow.Children.Add(_startOverButton);
        _footerSwap.Children.Add(_draftRow);
        _footerSwap.Children.Add(_runRow);
        _footerSwap.Children.Add(_doneRow);
        _footerSwap.Show(_draftRow, animate: false);

        _keepButton.Content = _keepLabel;
        _approveButton.Content = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _pauseButton.Content = FaceOf(_pauseGlyph, _pauseLabel);
        _startOverButton.Content = FaceOf(new ArcGlyph { Data = ArcIcons.RotateCcw, Size = 16, VerticalAlignment = VerticalAlignment.Center }, _startOverLabel);
        _keepButton.Click += (_, e) =>
        {
            e.Handled = true;
            KeepPlanning();
        };
        _approveButton.Click += (_, e) =>
        {
            e.Handled = true;
            Approve();
        };
        _pauseButton.Click += (_, e) =>
        {
            e.Handled = true;
            SetPaused(CurrentStatus == AgentPlanStatus.Running);
        };
        _startOverButton.Click += (_, e) =>
        {
            e.Handled = true;
            StartOver();
        };

        // Inks that do not come from a theme setter.
        ArcTokens.Bind(_headingBefore, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_headingAfter, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_headingCount, AgentPlanCount.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_heading, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_heading, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_headingBefore, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_headingAfter, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_headingCount, AgentPlanCount.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        ArcTokens.Bind(_modelName, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        foreach (var word in _positionWords)
        {
            ArcTokens.Bind(word, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        }
        foreach (var count in _positionCounts)
        {
            ArcTokens.Bind(count, AgentPlanCount.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        }
        ArcTokens.Bind(_emptyText, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_addLabel, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        TextElementForeground(_statusText, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_doneText, AgentPlanText.PrefixForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_doneText, AgentPlanText.ForegroundProperty, ArcDesignTokenKind.TextSecondary);

        AutomationProperties.SetControlTypeOverride(_list, Avalonia.Automation.Peers.AutomationControlType.List);
    }

    private Grid? _headGrid;

    private static void TextElementForeground(Control control, ArcDesignTokenKind kind) =>
        ArcTokens.Bind(control, Avalonia.Controls.Documents.TextElement.ForegroundProperty, kind);

    private static StackPanel FaceOf(Control glyph, Control label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(glyph);
        row.Children.Add(label);
        return row;
    }

    private void AttachParts()
    {
        if (_headRow is not null && _headGrid is not null && !_headRow.Children.Contains(_headGrid))
        {
            _headRow.Children.Add(_headGrid);
        }
        if (_listHost is not null && _list is not null && !_listHost.Children.Contains(_list))
        {
            _listHost.Children.Add(_emptyBlock);
            _listHost.Children.Add(_list);
            _listHost.Children.Add(_addBlock);
        }
        if (_footer is not null && _footer.Child != _footerSwap)
        {
            _footer.Child = _footerSwap;
        }
        if (_progress is not null)
        {
            ArcTokens.Bind(_progress, AgentPlanProgress.FillProperty, ArcDesignTokenKind.Accent);
        }
    }

    // Refresh ------------------------------------------------------------------------------------------------------------

    private void RefreshAll(bool animate)
    {
        if (_header is null || _list is null)
        {
            return;
        }
        var labels = Labels;
        var status = CurrentStatus;
        var steps = Steps;
        var draft = status == AgentPlanStatus.Draft;
        var count = steps.Count;

        // Header.
        var model = EffectiveModel;
        _mark.IsVisible = model is not null;
        _mark.Data = model?.Mark ?? ArcIcons.Sparkles;
        if (model?.MarkBrush is { } markBrush)
        {
            _mark.Foreground = markBrush;
        }
        else
        {
            ArcTokens.Bind(_mark, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Foreground);
        }
        AutomationProperties.SetAccessibilityView(_mark, AccessibilityView.Raw);
        _heading.Text = labels.Heading;
        var (before, number, after) = AgentPlanFormat.SplitOne(labels.StepCount(count), count);
        _headingBefore.Text = labels.HeadingSeparator + before;
        _headingAfter.Text = after;
        _headingCount.IsVisible = number >= 0;
        _headingCount.Value = Math.Max(0, count);
        if (_goal is not null)
        {
            var goal = EffectiveGoal;
            _goal.Text = goal;
            _goal.IsVisible = !string.IsNullOrEmpty(goal);
        }
        _modelName.Text = model?.Name ?? string.Empty;
        RefreshPosition(labels, count);
        _meta.Show(draft ? _modelName : _position, animate);

        // Progress.
        if (_progress is not null)
        {
            _progress.Maximum = Math.Max(1, count);
            _progress.Value = AgentPlanFormat.Finished(steps);
            _progress.IsShown = !draft;
            var finished = AgentPlanFormat.Finished(steps);
            AutomationProperties.SetName(_progress, labels.ProgressName);
            AutomationProperties.SetHelpText(_progress, labels.ProgressValue(finished, count));
            AutomationProperties.SetItemStatus(_progress, labels.ProgressValue(finished, count));
        }

        // Rows.
        foreach (var row in _rows.Values)
        {
            row.Refresh(animate);
        }
        AutomationProperties.SetName(_list, labels.StepsName);

        // Empty sentence and Add step: only while it is a draft.
        _emptyText.Text = labels.EmptyPlan;
        _emptyBlock.IsOpen = draft && count == 0;
        _addLabel.Text = labels.AddStep;
        _addBlock.IsOpen = draft;
        AutomationProperties.SetName(_addButton, labels.AddStep);

        RefreshFooter(animate);
        AutomationProperties.SetName(this, AccessibleName);
        UpdatePseudoClasses();
    }

    private void RefreshPosition(AgentPlanLabels labels, int count)
    {
        var split = AgentPlanFormat.SplitTwo(labels.Position);
        var key = split is { } s ? string.Join("\u0001", s.Words) + s.FirstComesFirst : null;
        if (split is { } parts)
        {
            if (key != _positionKey)
            {
                _positionKey = key;
                _positionFirstFirst = parts.FirstComesFirst;
                _positionWords[0].Text = parts.Words[0];
                _positionWords[1].Text = parts.Words[1];
                _positionWords[2].Text = parts.Words[2];
            }
            _positionCounts[1].IsVisible = true;
            var current = AgentPlanFormat.Position(Steps);
            _positionCounts[0].Value = _positionFirstFirst ? current : count;
            _positionCounts[1].Value = _positionFirstFirst ? count : current;
        }
        else
        {
            _positionKey = null;
            _positionWords[0].Text = labels.Position(AgentPlanFormat.Position(Steps), count);
            _positionWords[1].Text = string.Empty;
            _positionWords[2].Text = string.Empty;
            _positionCounts[0].IsVisible = false;
            _positionCounts[1].IsVisible = false;
        }
    }

    private AgentPlanStatus _footerShown = AgentPlanStatus.Draft;

    private void RefreshFooter(bool animate)
    {
        var labels = Labels;
        var status = CurrentStatus;
        var steps = Steps;
        _keepLabel.Text = labels.KeepPlanning;
        ((TextBlock)_approveButton.Content!).Text = labels.Approve;
        AutomationProperties.SetName(_keepButton, labels.KeepPlanning);
        AutomationProperties.SetName(_approveButton, labels.Approve);
        _keepButton.IsLoading = IsPlanning;
        _keepButton.IsEnabled = CanKeepPlanning;
        _approveButton.IsEnabled = steps.Count > 0;

        var failed = AgentPlanFormat.FailedIndex(steps);
        var position = AgentPlanFormat.Position(steps);
        _statusText.Content = failed >= 0 ? labels.Failed(failed + 1)
            : status == AgentPlanStatus.Paused ? labels.Paused(position)
            : status == AgentPlanStatus.Done ? string.Empty
            : labels.Working(position);
        var paused = status == AgentPlanStatus.Paused;
        _pauseLabel.Text = paused ? labels.Resume : labels.Pause;
        _pauseGlyph.Data = paused ? ArcIcons.Play : ArcIcons.Pause;
        AutomationProperties.SetName(_pauseButton, paused ? labels.Resume : labels.Pause);
        _pauseButton.IsVisible = failed < 0;
        _startOverLabel.Text = labels.StartOver;
        AutomationProperties.SetName(_startOverButton, labels.StartOver);

        var layer = status switch
        {
            AgentPlanStatus.Draft => _draftRow,
            AgentPlanStatus.Done => _doneRow,
            _ => _runRow
        };
        if (status == AgentPlanStatus.Done && _footerShown != AgentPlanStatus.Done)
        {
            var total = labels.Duration(AgentPlanFormat.Total(steps));
            _doneText.Prefix = labels.DoneIn(total);
            _doneText.Text = EffectiveSummary ?? string.Empty;
            _footerSwap.Show(layer, animate);
            if (!string.IsNullOrEmpty(_doneText.Text))
            {
                _doneText.Stream(_doneText.Prefix.Length);
            }
        }
        else
        {
            if (status == AgentPlanStatus.Done)
            {
                _doneText.Prefix = labels.DoneIn(labels.Duration(AgentPlanFormat.Total(steps)));
            }
            _footerSwap.Show(layer, animate);
        }
        _footerShown = status;
        ApplyNarrow();
    }

    private void ApplyNarrow()
    {
        var narrow = Bounds.Width > 0 && Bounds.Width < NarrowWidth;
        if (narrow == _narrowApplied)
        {
            return;
        }
        _narrowApplied = narrow;
        _draftRow.ColumnDefinitions = new ColumnDefinitions(narrow ? "*,*" : "*,Auto,Auto");
        Grid.SetColumn(_keepButton, narrow ? 0 : 1);
        Grid.SetColumn(_approveButton, narrow ? 1 : 2);
        _keepButton.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        _approveButton.HorizontalAlignment = narrow ? HorizontalAlignment.Stretch : HorizontalAlignment.Right;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
    }

    private void UpdatePseudoClasses()
    {
        var status = CurrentStatus;
        PseudoClasses.Set(DraftPseudoClass, status == AgentPlanStatus.Draft);
        PseudoClasses.Set(RunningPseudoClass, status == AgentPlanStatus.Running);
        PseudoClasses.Set(PausedPseudoClass, status == AgentPlanStatus.Paused);
        PseudoClasses.Set(DonePseudoClass, status == AgentPlanStatus.Done);
        PseudoClasses.Set(FailedPseudoClass, AgentPlanFormat.FailedIndex(Steps) >= 0);
        PseudoClasses.Set(EmptyPseudoClass, Steps.Count == 0);
        PseudoClasses.Set(NarrowPseudoClass, _narrowApplied);
    }
}
