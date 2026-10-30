using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The newest three events of a <see cref="TrainingRun"/>, newest on top: a kind icon, the text and the step in tabular
/// figures. Four rows are kept (the fourth is the one leaving at the bottom) and reused; when one new event arrives on top
/// the rows move down by one row on one bounce-free 0.24s spring while the new row fades in and the old last one fades out.
/// The slide is a render transform driven by a <see cref="MotionValue{T}"/> (code-driven because the rows trade places
/// with the data, which a ControlTheme transition cannot express); several new events at once, a rollback, reduced motion
/// and Off change at once.
/// </summary>
internal sealed class TrainingRunEventList : Control
{
    /// <summary>The rows that show; one more is kept for the row that leaves.</summary>
    internal const int VisibleRows = 3;

    /// <summary>The slide: bounce-free, 0.24s.</summary>
    internal static readonly SpringSpec SlideSpring = SpringSpec.FromVisual(0.24, 0);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<TrainingRunEventList, double>(nameof(RowHeight), 28);

    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<TrainingRunEventList, double>(nameof(GlyphSize), 14);

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<TrainingRunEventList, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<TrainingRunEventList>();

    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly Row[] _rows = new Row[VisibleRows + 1];
    private MotionValue<double>? _slide;
    private double _progress = 1;
    private string? _newestId;
    private int _count;

    public TrainingRunEventList()
    {
        ClipToBounds = true;
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i] = new Row(this);
            LogicalChildren.Add(_rows[i].Root);
            VisualChildren.Add(_rows[i].Root);
        }
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.List);
    }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>How many rows show events (up to three).</summary>
    internal int ShownCount => Math.Min(_count, VisibleRows);

    /// <summary>The text of row <paramref name="index"/> (0 is the newest).</summary>
    internal string RowText(int index) => _rows[index].Label.Text ?? string.Empty;

    /// <summary>The step text of row <paramref name="index"/>.</summary>
    internal string RowStep(int index) => _rows[index].Step.Text ?? string.Empty;

    /// <summary>The kind of row <paramref name="index"/>.</summary>
    internal TrainingRunEventKind? RowKind(int index) => _rows[index].Kind;

    /// <summary>Where the slide is: 0 just after a new event arrived, 1 at rest.</summary>
    internal double SlideProgress => _progress;

    /// <summary>Shows the newest events of <paramref name="events"/> (oldest first).</summary>
    public void SetEvents(IReadOnlyList<TrainingRunEvent> events, TrainingRunLabels labels)
    {
        var count = events.Count;
        var newest = count > 0 ? events[count - 1].Id : null;
        var slides = _newestId is not null && newest != _newestId && count >= 2 && events[count - 2].Id == _newestId
                     && ArcMotion.IsFull(this) && this.IsAttachedToVisualTree();
        for (var i = 0; i < _rows.Length; i++)
        {
            var index = count - 1 - i;
            _rows[i].Show(index >= 0 ? events[index] : null, labels);
        }
        _count = count;
        _newestId = newest;
        AutomationProperties.SetName(this, labels.EventLog);
        if (slides)
        {
            _slide ??= CreateSlide();
            _slide.Jump(0);
            Apply(0);
            _ = _slide.AnimateTo(1, MotionSpec.Spring(SlideSpring));
        }
        else
        {
            _slide?.Jump(1);
            Apply(1);
        }
    }

    private MotionValue<double> CreateSlide()
    {
        var slide = new MotionValue<double>(this, 1, MotionVectors.Double);
        slide.Changed += Apply;
        return slide;
    }

    private void Apply(double progress)
    {
        _progress = progress;
        var shift = -(1 - progress) * RowHeight;
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i].Shift.Y = shift;
        }
        _rows[0].Root.Opacity = _rows[0].HasEvent ? progress : 0;
        _rows[VisibleRows].Root.Opacity = _rows[VisibleRows].HasEvent ? 1 - progress : 0;
        for (var i = 1; i < VisibleRows; i++)
        {
            _rows[i].Root.Opacity = _rows[i].HasEvent ? 1 : 0;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowHeightProperty || change.Property == GlyphSizeProperty)
        {
            foreach (var row in _rows)
            {
                row.Glyph.Size = GlyphSize;
            }
            InvalidateMeasure();
        }
        else if (change.Property == MutedBrushProperty)
        {
            foreach (var row in _rows)
            {
                row.Step.Foreground = MutedBrush;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var rowSize = new Size(availableSize.Width, RowHeight);
        foreach (var row in _rows)
        {
            row.Root.Measure(rowSize);
        }
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, RowHeight * VisibleRows);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i].Root.Arrange(new Rect(0, i * RowHeight, finalSize.Width, RowHeight));
        }
        return finalSize;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    /// <summary>One reused row: the kind icon, the text and the step.</summary>
    private sealed class Row
    {
        private IDisposable? _colorBinding;

        public Row(TrainingRunEventList owner)
        {
            Glyph = new ArcGlyph { Size = owner.GlyphSize, VerticalAlignment = VerticalAlignment.Center };
            Label = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(10, 0, 12, 0),
            };
            Step = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontFeatures = s_tabular,
                Foreground = owner.MutedBrush,
            };
            Grid.SetColumn(Label, 1);
            Grid.SetColumn(Step, 2);
            Root = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                RenderTransform = Shift,
                Opacity = 0,
                Children = { Glyph, Label, Step },
            };
        }

        public Grid Root { get; }

        public ArcGlyph Glyph { get; }

        public TextBlock Label { get; }

        public TextBlock Step { get; }

        public TranslateTransform Shift { get; } = new();

        public TrainingRunEventKind? Kind { get; private set; }

        public bool HasEvent => Kind is not null;

        public void Show(TrainingRunEvent? item, TrainingRunLabels labels)
        {
            if (item is null)
            {
                Kind = null;
                Label.Text = null;
                Step.Text = null;
                Root.IsVisible = false;
                return;
            }
            Root.IsVisible = true;
            if (Kind != item.Kind)
            {
                Kind = item.Kind;
                Glyph.Data = TrainingRunGlyphs.Event(item.Kind);
                _colorBinding?.Dispose();
                _colorBinding = ArcTokens.Bind(Glyph, ArcGlyph.ForegroundProperty, TrainingRunGlyphs.EventColor(item.Kind));
            }
            Label.Text = item.Label;
            Step.Text = item.Step is { } step ? labels.StepAt(step) : null;
            AutomationProperties.SetName(Root, item.Step is { } at ? item.Label + ", " + labels.StepAt(at) : item.Label);
        }
    }
}

/// <summary>The glyphs and colors of <see cref="TrainingRun"/>: per event kind and per status.</summary>
internal static class TrainingRunGlyphs
{
    /// <summary>Lucide "save": a floppy disk, the checkpoint glyph.</summary>
    public static readonly Geometry Save = ArcIcons.Parse(
        "M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z" +
        "M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7M7 3v4a1 1 0 0 0 1 1h7");

    /// <summary>Lucide "rocket": the deploy glyph.</summary>
    public static readonly Geometry Rocket = ArcIcons.Parse(
        "M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z" +
        "m7.5-1.5-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z" +
        "M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5");

    public static Geometry Event(TrainingRunEventKind kind) => kind switch
    {
        TrainingRunEventKind.Start => ArcIcons.Play,
        TrainingRunEventKind.Checkpoint => Save,
        TrainingRunEventKind.Eval => ArcIcons.Gauge,
        TrainingRunEventKind.Epoch => ArcIcons.Flag,
        TrainingRunEventKind.Warning => ArcIcons.TriangleAlert,
        TrainingRunEventKind.Error => ArcIcons.CircleAlert,
        TrainingRunEventKind.Done => ArcIcons.CircleCheck,
        _ => ArcIcons.Info,
    };

    public static ArcDesignTokenKind EventColor(TrainingRunEventKind kind) => kind switch
    {
        TrainingRunEventKind.Start => ArcDesignTokenKind.Accent,
        TrainingRunEventKind.Eval => ArcDesignTokenKind.Series2,
        TrainingRunEventKind.Warning => ArcDesignTokenKind.Warning,
        TrainingRunEventKind.Error => ArcDesignTokenKind.Danger,
        TrainingRunEventKind.Done => ArcDesignTokenKind.Success,
        TrainingRunEventKind.Info => ArcDesignTokenKind.TextMuted,
        _ => ArcDesignTokenKind.TextSecondary,
    };
}
