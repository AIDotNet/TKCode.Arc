using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One step of an <see cref="AgentRun"/>'s timeline: a marker on the rail (the kind's glyph, a spinner while it runs, a
/// cross when it failed), the header (title, tool name, time, chevron) and the detail that opens under it. A step that
/// arrives while the run is on screen rises 6px into place while it fades in and its height opens.
/// </summary>
[TemplatePart(HeaderPart, typeof(AgentRunStepHeader))]
[TemplatePart(DetailPart, typeof(AiCollapsible))]
[TemplatePart(MarkerHostPart, typeof(Panel))]
[TemplatePart(PresencePart, typeof(PresenceHost))]
[PseudoClasses(PendingPseudoClass, RunningPseudoClass, DonePseudoClass, FailedPseudoClass, StoppedPseudoClass, LastPseudoClass, MessagePseudoClass)]
internal sealed class AgentRunStepView : TemplatedControl
{
    public const string HeaderPart = "PART_Header";
    public const string DetailPart = "PART_Detail";
    public const string MarkerHostPart = "PART_MarkerHost";
    public const string PresencePart = "PART_Presence";

    public const string PendingPseudoClass = ":pending";
    public const string RunningPseudoClass = ":running";
    public const string DonePseudoClass = ":done";
    public const string FailedPseudoClass = ":failed";
    public const string StoppedPseudoClass = ":stopped";
    public const string LastPseudoClass = ":last";
    public const string MessagePseudoClass = ":message";

    /// <summary>The marker's glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AgentRunStepView, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    private AgentRunStep? _step;
    private bool _expanded;
    private bool _isNew;
    private AgentRunStepHeader? _header;
    private AiCollapsible? _detail;
    private Panel? _markerHost;
    private PresenceHost? _presence;
    private ArcSpinner? _spinner;
    private string? _detailKey;

    public AgentRun? Owner { get; set; }

    public AgentRunStep? Step => _step;

    public AgentRunStepHeader? Header => _header;

    /// <summary>The detail panel's content once built (tests read it).</summary>
    internal Control? DetailContent => _detail?.Content;

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }
            _expanded = value;
            Refresh();
        }
    }

    /// <summary>Shows <paramref name="step"/>; <paramref name="isNew"/> plays the arrival.</summary>
    public void Bind(AgentRunStep step, bool expanded, bool isNew)
    {
        _step = step;
        _expanded = expanded;
        _isNew = isNew;
        _detailKey = null;
        Refresh();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_header is not null)
        {
            _header.Click -= OnHeaderClick;
        }
        base.OnApplyTemplate(e);
        _header = e.NameScope.Find<AgentRunStepHeader>(HeaderPart);
        _detail = e.NameScope.Find<AiCollapsible>(DetailPart);
        _markerHost = e.NameScope.Find<Panel>(MarkerHostPart);
        _presence = e.NameScope.Find<PresenceHost>(PresencePart);
        _spinner = null;
        _detailKey = null;
        if (_header is not null)
        {
            _header.Click += OnHeaderClick;
        }
        if (_presence is not null && _isNew)
        {
            // Applied without motion first, then shown: the step rises in once it is in the window.
            _presence.IsPresent = false;
            Dispatcher.UIThread.Post(() =>
            {
                if (_presence is not null)
                {
                    _presence.IsPresent = true;
                }
            }, DispatcherPriority.Render);
        }
        _isNew = false;
        Refresh();
    }

    private void OnHeaderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_step is null || Owner is null || !_step.HasDetail || _step.Kind == AgentRunStepKind.Message)
        {
            return;
        }
        Owner.SetStepExpanded(_step, !_expanded);
    }

    /// <summary>Shows the step's current values.</summary>
    public void Refresh()
    {
        var step = _step;
        if (step is null)
        {
            return;
        }
        var labels = Owner?.Labels ?? new AgentRunLabels();
        var status = step.Status;
        PseudoClasses.Set(PendingPseudoClass, status == AgentRunStepStatus.Pending);
        PseudoClasses.Set(RunningPseudoClass, status == AgentRunStepStatus.Running);
        PseudoClasses.Set(DonePseudoClass, status == AgentRunStepStatus.Done);
        PseudoClasses.Set(FailedPseudoClass, status == AgentRunStepStatus.Failed);
        PseudoClasses.Set(StoppedPseudoClass, status == AgentRunStepStatus.Stopped);
        PseudoClasses.Set(LastPseudoClass, Owner?.IsLast(step) ?? true);
        PseudoClasses.Set(MessagePseudoClass, step.Kind == AgentRunStepKind.Message);
        Glyph = status switch
        {
            AgentRunStepStatus.Failed => ArcIcons.X,
            AgentRunStepStatus.Stopped => ArcIcons.Square,
            _ => step.Kind switch
            {
                AgentRunStepKind.Thinking => ArcIcons.Brain,
                AgentRunStepKind.ToolCall => step.ToolName is { } tool && tool.Contains("command", StringComparison.OrdinalIgnoreCase)
                    ? ArcIcons.Terminal
                    : ArcIcons.Wrench,
                AgentRunStepKind.FileEdit => ArcIcons.FileCode,
                _ => ArcIcons.MessageSquare
            }
        };
        UpdateSpinner(status == AgentRunStepStatus.Running);

        var meta = step.Duration is { } duration && status != AgentRunStepStatus.Running ? labels.Duration(duration) : null;
        if (step.Kind == AgentRunStepKind.FileEdit && step.Files is { Count: > 0 } files)
        {
            var count = labels.FileCount(files.Count);
            meta = meta is null ? count : $"{count} · {meta}";
        }
        var isMessage = step.Kind == AgentRunStepKind.Message;
        var expanded = isMessage || (_expanded && step.HasDetail);
        if (_header is not null)
        {
            _header.Title = step.Title;
            _header.ToolName = step.Kind == AgentRunStepKind.ToolCall ? step.ToolName : null;
            _header.Meta = meta;
            _header.HasDetail = step.HasDetail && !isMessage;
            _header.IsExpanded = expanded;
            var name = labels.StepName(labels.Kind(step.Kind), step.Title, labels.StepStatus(status), meta);
            AutomationProperties.SetName(_header, name);
            AutomationProperties.SetHelpText(_header, step.ToolName);
        }
        if (_detail is not null)
        {
            if (expanded)
            {
                var key = DetailKey(step);
                if (key != _detailKey)
                {
                    _detailKey = key;
                    _detail.Content = BuildDetail(step, labels);
                }
            }
            _detail.IsExpanded = expanded && step.HasDetail;
        }
    }

    private static string DetailKey(AgentRunStep step) =>
        $"{step.Kind}|{step.Text}|{step.Input}|{step.Output}|{step.Status}|{step.Files?.Count}";

    private void UpdateSpinner(bool running)
    {
        if (running && _spinner is null && _markerHost is not null)
        {
            _spinner = new ArcSpinner { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            ArcTokens.Bind(_spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.Accent);
            _markerHost.Children.Add(_spinner);
        }
        if (_spinner is not null)
        {
            _spinner.IsVisible = running;
        }
    }

    #region Detail

    private static Control BuildDetail(AgentRunStep step, AgentRunLabels labels)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(8, 2, 8, 10) };
        switch (step.Kind)
        {
            case AgentRunStepKind.Thinking:
            case AgentRunStepKind.Message:
                panel.Children.Add(Prose(step.Text, step.Kind == AgentRunStepKind.Thinking));
                break;
            case AgentRunStepKind.ToolCall:
                if (!string.IsNullOrWhiteSpace(step.Input))
                {
                    panel.Children.Add(Caption(labels.Input));
                    panel.Children.Add(Code(step.Input!, false));
                }
                if (!string.IsNullOrWhiteSpace(step.Output))
                {
                    panel.Children.Add(Caption(labels.Output));
                    panel.Children.Add(Code(step.Output!, step.Status == AgentRunStepStatus.Failed));
                }
                break;
            default:
                if (step.Files is { } files)
                {
                    var list = new StackPanel { Spacing = 2 };
                    foreach (var file in files)
                    {
                        list.Children.Add(FileRow(file, labels));
                    }
                    panel.Children.Add(list);
                }
                break;
        }
        return panel;
    }

    private static SelectableTextBlock Prose(string? text, bool quiet)
    {
        var block = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 21 };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, quiet ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground);
        if (quiet)
        {
            block.FontStyle = FontStyle.Italic;
        }
        return block;
    }

    private static TextBlock Caption(string text)
    {
        var caption = new TextBlock { Text = text };
        ArcTokens.Bind(caption, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(caption, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(caption, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        return caption;
    }

    private static Border Code(string text, bool failed)
    {
        var block = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, LineHeight = 18 };
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, failed ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextSecondary);
        var frame = new Border { Child = block, Padding = new Thickness(12, 10) }.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        return frame;
    }

    private static Control FileRow(AgentRunFile file, AgentRunLabels labels)
    {
        var row = new ArcShrinkRow { Spacing = 8 };
        var glyph = new ArcGlyph
        {
            Data = file.Change switch
            {
                AgentRunFileChange.Created => ArcIcons.FilePlus,
                AgentRunFileChange.Deleted => ArcIcons.Trash2,
                _ => ArcIcons.File
            },
            Size = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, file.Change == AgentRunFileChange.Deleted ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextMuted);
        var path = new TextBlock { Text = file.Path, TextTrimming = TextTrimming.PrefixCharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(path, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(path, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(path, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcShrinkRow.SetIsShrinkable(path, true);
        row.Children.Add(glyph);
        row.Children.Add(path);
        if (file.Added > 0)
        {
            row.Children.Add(Count($"+{file.Added}", ArcDesignTokenKind.Success));
        }
        if (file.Removed > 0)
        {
            row.Children.Add(Count($"−{file.Removed}", ArcDesignTokenKind.Danger));
        }
        AutomationProperties.SetName(row, $"{file.Path}, {labels.FileChange(file.Change)}");
        return row;
    }

    private static TextBlock Count(string text, ArcDesignTokenKind color)
    {
        var block = new TextBlock { Text = text, FontFeatures = FontFeatureCollection.Parse("tnum"), VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        return block;
    }

    #endregion
}

/// <summary>The pressable header of an <see cref="AgentRunStepView"/>: title, tool name, time and the chevron.</summary>
[PseudoClasses(ExpandedPseudoClass, DetailPseudoClass)]
internal sealed class AgentRunStepHeader : AiAgentButton
{
    public const string ExpandedPseudoClass = ":expanded";
    public const string DetailPseudoClass = ":has-detail";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentRunStepHeader, string?>(nameof(Title));

    public static readonly StyledProperty<string?> ToolNameProperty =
        AvaloniaProperty.Register<AgentRunStepHeader, string?>(nameof(ToolName));

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AgentRunStepHeader, string?>(nameof(Meta));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AgentRunStepHeader, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<bool> HasDetailProperty =
        AvaloniaProperty.Register<AgentRunStepHeader, bool>(nameof(HasDetail));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? ToolName { get => GetValue(ToolNameProperty); set => SetValue(ToolNameProperty, value); }

    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    public bool HasDetail { get => GetValue(HasDetailProperty); set => SetValue(HasDetailProperty, value); }

    /// <summary>The step this header opens (through its view).</summary>
    public AgentRunStep? Step => (TemplatedParent as AgentRunStepView)?.Step ?? this.FindAncestorOfType<AgentRunStepView>()?.Step;

    protected override AutomationPeer OnCreateAutomationPeer() => new HeaderPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, change.GetNewValue<bool>());
        }
        else if (change.Property == HasDetailProperty)
        {
            PseudoClasses.Set(DetailPseudoClass, change.GetNewValue<bool>());
        }
    }

    /// <summary>Reads as an expand / collapse button while the step has a detail to open.</summary>
    private sealed class HeaderPeer(AgentRunStepHeader owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private AgentRunStepHeader Header => (AgentRunStepHeader)Owner;

        public ExpandCollapseState ExpandCollapseState => !Header.HasDetail ? ExpandCollapseState.LeafNode
            : Header.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Header.HasDetail && !Header.IsExpanded)
            {
                Invoke();
            }
        }

        public void Collapse()
        {
            if (Header.HasDetail && Header.IsExpanded)
            {
                Invoke();
            }
        }
    }
}

/// <summary>
/// The run's progress as one short segment per step, colored by its status: done in the accent, running in the accent at
/// half strength, failed in the danger color, stopped and waiting in the border color. Drawn in one pass.
/// </summary>
internal sealed class AgentRunProgressBar : Control
{
    public static readonly StyledProperty<IBrush?> DoneBrushProperty =
        AvaloniaProperty.Register<AgentRunProgressBar, IBrush?>(nameof(DoneBrush));

    public static readonly StyledProperty<IBrush?> FailedBrushProperty =
        AvaloniaProperty.Register<AgentRunProgressBar, IBrush?>(nameof(FailedBrush));

    public static readonly StyledProperty<IBrush?> RestBrushProperty =
        AvaloniaProperty.Register<AgentRunProgressBar, IBrush?>(nameof(RestBrush));

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<AgentRunProgressBar, double>(nameof(Gap), 3);

    public IBrush? DoneBrush { get => GetValue(DoneBrushProperty); set => SetValue(DoneBrushProperty, value); }

    public IBrush? FailedBrush { get => GetValue(FailedBrushProperty); set => SetValue(FailedBrushProperty, value); }

    public IBrush? RestBrush { get => GetValue(RestBrushProperty); set => SetValue(RestBrushProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private AgentRunStepStatus[] _statuses = [];
    private int _count;

    static AgentRunProgressBar()
    {
        AffectsRender<AgentRunProgressBar>(DoneBrushProperty, FailedBrushProperty, RestBrushProperty, GapProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<AgentRunProgressBar>(AccessibilityView.Raw);
    }

    /// <summary>The statuses drawn (tests read them).</summary>
    public ReadOnlySpan<AgentRunStepStatus> Statuses => _statuses.AsSpan(0, _count);

    public void SetSteps(IReadOnlyList<AgentRunStep> steps)
    {
        if (_statuses.Length < steps.Count)
        {
            _statuses = new AgentRunStepStatus[Math.Max(8, steps.Count * 2)];
        }
        var changed = _count != steps.Count;
        for (var i = 0; i < steps.Count; i++)
        {
            var status = steps[i].Status;
            changed |= _statuses[i] != status;
            _statuses[i] = status;
        }
        _count = steps.Count;
        if (changed)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (_count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            if (RestBrush is { } empty && bounds.Width > 0)
            {
                context.DrawRectangle(empty, null, new Rect(bounds.Size), bounds.Height / 2, bounds.Height / 2);
            }
            return;
        }
        var gap = Gap;
        var width = (bounds.Width - gap * (_count - 1)) / _count;
        var radius = bounds.Height / 2;
        for (var i = 0; i < _count; i++)
        {
            var status = _statuses[i];
            var brush = status switch
            {
                AgentRunStepStatus.Done => DoneBrush,
                AgentRunStepStatus.Failed => FailedBrush,
                AgentRunStepStatus.Running => DoneBrush,
                _ => RestBrush
            };
            if (brush is null)
            {
                continue;
            }
            var rect = new Rect(i * (width + gap), 0, Math.Max(0, width), bounds.Height);
            if (status == AgentRunStepStatus.Running)
            {
                using (context.PushOpacity(0.45))
                {
                    context.DrawRectangle(brush, null, rect, radius, radius);
                }
            }
            else
            {
                context.DrawRectangle(brush, null, rect, radius, radius);
            }
        }
    }
}
