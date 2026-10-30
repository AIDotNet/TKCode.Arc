using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The full-row button of a provider with an incident: transparent at rest, SurfaceMuted under the pointer, a keyboard
/// ring drawn just inside the row. It reports its expanded state (Arc's <c>aria-expanded</c>).
/// </summary>
internal sealed class ProviderStatusRowButton : ToolCallDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(ProviderStatusRowButton);
}

/// <summary>The rows' container: a list to assistive technology, each row one of its items.</summary>
internal sealed class ProviderStatusRowList : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class ListPeer(ProviderStatusRowList owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);
    }
}

/// <summary>One update of an open incident: a dot on the rail in the stage's color, the stage and time, then the message.</summary>
internal sealed class ProviderStatusUpdateView : TemplatedControl
{
    internal const string FirstPseudoClass = ":first";
    internal const string LastPseudoClass = ":last";

    public static readonly StyledProperty<string?> StageTextProperty =
        AvaloniaProperty.Register<ProviderStatusUpdateView, string?>(nameof(StageText));

    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<ProviderStatusUpdateView, string?>(nameof(TimeText));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<ProviderStatusUpdateView, string?>(nameof(Message));

    public static readonly StyledProperty<ProviderStatusStage> StageProperty =
        AvaloniaProperty.Register<ProviderStatusUpdateView, ProviderStatusStage>(nameof(Stage));

    public string? StageText { get => GetValue(StageTextProperty); set => SetValue(StageTextProperty, value); }

    public string? TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }

    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public ProviderStatusStage Stage { get => GetValue(StageProperty); set => SetValue(StageProperty, value); }

    /// <summary>The update this view shows (the key it is reused by).</summary>
    internal ProviderStatusUpdate? Update { get; set; }

    public ProviderStatusUpdateView()
    {
        ApplyStage(Stage);
    }

    /// <summary>Sets where the update sits on the rail: the line runs between the first and the last dot.</summary>
    internal void SetPosition(bool first, bool last)
    {
        PseudoClasses.Set(FirstPseudoClass, first);
        PseudoClasses.Set(LastPseudoClass, last);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StageProperty)
        {
            ApplyStage(Stage);
        }
    }

    private void ApplyStage(ProviderStatusStage stage)
    {
        PseudoClasses.Set(":investigating", stage == ProviderStatusStage.Investigating);
        PseudoClasses.Set(":identified", stage == ProviderStatusStage.Identified);
        PseudoClasses.Set(":monitoring", stage == ProviderStatusStage.Monitoring);
        PseudoClasses.Set(":resolved", stage == ProviderStatusStage.Resolved);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new UpdatePeer(this);

    /// <summary>Read as one line: "Identified, 14:08 UTC. A bad configuration change ...".</summary>
    private sealed class UpdatePeer(ProviderStatusUpdateView owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string? GetNameCore()
        {
            var view = (ProviderStatusUpdateView)Owner;
            var head = string.IsNullOrEmpty(view.TimeText) ? view.StageText : view.StageText + ", " + view.TimeText;
            return head + ". " + view.Message;
        }

        protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => [];
    }
}

/// <summary>
/// The shared day tooltip of a card: the date (or Today), the health with its icon and the uptime, and the day's note.
/// Drawn as Arc's tooltip (inverted surface). Its texts change in place while the pointer scrubs, so moving between days
/// never animates the content; the popup only moves.
/// </summary>
internal sealed class ProviderStatusDayTip : TemplatedControl
{
    public static readonly StyledProperty<string?> DateTextProperty =
        AvaloniaProperty.Register<ProviderStatusDayTip, string?>(nameof(DateText));

    public static readonly StyledProperty<string?> HealthTextProperty =
        AvaloniaProperty.Register<ProviderStatusDayTip, string?>(nameof(HealthText));

    public static readonly StyledProperty<string?> UptimeTextProperty =
        AvaloniaProperty.Register<ProviderStatusDayTip, string?>(nameof(UptimeText));

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<ProviderStatusDayTip, string?>(nameof(Note));

    public static readonly StyledProperty<Avalonia.Media.Geometry?> IconProperty =
        AvaloniaProperty.Register<ProviderStatusDayTip, Avalonia.Media.Geometry?>(nameof(Icon));

    public string? DateText { get => GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }

    public string? HealthText { get => GetValue(HealthTextProperty); set => SetValue(HealthTextProperty, value); }

    public string? UptimeText { get => GetValue(UptimeTextProperty); set => SetValue(UptimeTextProperty, value); }

    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    public Avalonia.Media.Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Shows a day.</summary>
    internal void Show(ProviderStatusDayCell cell, bool isToday, ProviderStatusLabels labels)
    {
        DateText = isToday ? labels.Today : labels.Date(cell.Date);
        HealthText = cell.Health is { } health ? labels.HealthText(health) : labels.NoData;
        UptimeText = cell.Uptime is { } uptime ? labels.DayUptime(labels.Percent(uptime)) : null;
        Note = string.IsNullOrWhiteSpace(cell.Note) ? null : cell.Note;
        Icon = ProviderStatusRow.IconOf(cell.Health);
        PseudoClasses.Set(":operational", cell.Health == ProviderStatusHealth.Operational);
        PseudoClasses.Set(":degraded", cell.Health == ProviderStatusHealth.Degraded);
        PseudoClasses.Set(":outage", cell.Health == ProviderStatusHealth.Outage);
        PseudoClasses.Set(":empty", cell.Health is null);
        PseudoClasses.Set(":note", Note is not null);
        PseudoClasses.Set(":uptime", UptimeText is not null);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TipPeer(this);

    /// <summary>Hidden from assistive technology: the bar's live region already reads the day.</summary>
    private sealed class TipPeer(ProviderStatusDayTip owner) : ControlAutomationPeer(owner)
    {
        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;
    }
}

/// <summary>
/// Skeleton rows for the first load: the same columns and row height as real rows, quiet blocks in SurfaceMuted. They
/// pulse (opacity 1 → 0.55 → 1 over 1.6s) under full motion only, on a <see cref="LiveLoop"/> that rests off screen;
/// reduced motion keeps them still.
/// </summary>
internal sealed class ProviderStatusSkeleton : StackPanel
{
    public static readonly StyledProperty<int> RowCountProperty =
        AvaloniaProperty.Register<ProviderStatusSkeleton, int>(nameof(RowCount), 4);

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ProviderStatusSkeleton, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<Thickness> RowPaddingProperty =
        AvaloniaProperty.Register<ProviderStatusSkeleton, Thickness>(nameof(RowPadding));

    public static readonly StyledProperty<Thickness> NarrowRowPaddingProperty =
        AvaloniaProperty.Register<ProviderStatusSkeleton, Thickness>(nameof(NarrowRowPadding));

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<ProviderStatusSkeleton, double>(nameof(RowHeight), 60);

    /// <summary>One pulse in seconds.</summary>
    public const double PulseSeconds = 1.6;

    private readonly LiveLoop _pulse;
    private double _phase;

    public ProviderStatusSkeleton()
    {
        _pulse = new LiveLoop(this, dt =>
        {
            _phase = (_phase + dt / PulseSeconds) % 1;
            Opacity = 1 - 0.45 * (0.5 - 0.5 * Math.Cos(_phase * 2 * Math.PI));
            return true;
        }) { MinimumLevel = ArcMotionLevel.Full };
        _pulse.RunningChanged += running =>
        {
            if (!running)
            {
                Opacity = 1;
            }
        };
        Rebuild();
    }

    public int RowCount { get => GetValue(RowCountProperty); set => SetValue(RowCountProperty, value); }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    public Thickness RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }

    public Thickness NarrowRowPadding { get => GetValue(NarrowRowPaddingProperty); set => SetValue(NarrowRowPaddingProperty, value); }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <summary>True while the pulse runs.</summary>
    internal bool IsPulsing => _pulse.IsRunning;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowCountProperty)
        {
            Rebuild();
        }
        else if (change.Property == IsNarrowProperty || change.Property == RowPaddingProperty
                 || change.Property == NarrowRowPaddingProperty || change.Property == RowHeightProperty)
        {
            foreach (var child in Children)
            {
                if (child is Border { Child: ProviderStatusRowPanel panel } row)
                {
                    panel.IsNarrow = IsNarrow;
                    row.Padding = IsNarrow ? NarrowRowPadding : RowPadding;
                    row.MinHeight = IsNarrow ? 0 : RowHeight;
                }
            }
        }
        else if (change.Property == IsVisibleProperty)
        {
            UpdatePulse();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdatePulse();
    }

    private void UpdatePulse()
    {
        if (IsVisible)
        {
            _pulse.Start();
        }
        else
        {
            _pulse.Stop();
            Opacity = 1;
        }
    }

    private void Rebuild()
    {
        Children.Clear();
        for (var i = 0; i < Math.Max(0, RowCount); i++)
        {
            var panel = new ProviderStatusRowPanel { IsNarrow = IsNarrow };
            panel.Children.Add(Block(16, 16, true));
            var name = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(Block(96 + (i % 3) * 18, 10, false));
            name.Children.Add(Block(72, 8, false));
            panel.Children.Add(name);
            panel.Children.Add(Block(84, 10, false));
            panel.Children.Add(Block(44, 10, false, HorizontalAlignment.Right));
            panel.Children.Add(Block(double.NaN, 24, false));
            panel.Children.Add(Block(44, 10, false, HorizontalAlignment.Right));
            panel.Children.Add(new Control());
            var row = new Border
            {
                Child = panel,
                Padding = IsNarrow ? NarrowRowPadding : RowPadding,
                MinHeight = IsNarrow ? 0 : RowHeight
            };
            ArcTokens.Bind(row, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            ArcTokens.Bind(row, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThicknessTop);
            Children.Add(row);
        }

        static Border Block(double width, double height, bool square, HorizontalAlignment alignment = HorizontalAlignment.Left)
        {
            var block = new Border
            {
                Width = width,
                Height = height,
                HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : alignment,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            }.BindCornerRadius(square ? ArcDesignTokenKind.RadiusStep4 : ArcDesignTokenKind.RadiusPill);
            ArcTokens.Bind(block, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            return block;
        }
    }
}
