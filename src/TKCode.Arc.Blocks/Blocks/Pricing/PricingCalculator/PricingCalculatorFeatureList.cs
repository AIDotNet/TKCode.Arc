using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A card's feature list (Arc pricing-calculator, design reference, notes 3.1 and 6.6): rows of a check and a line of text.
/// Rows are keyed; a row whose text changes swaps it in place, a new row opens from zero height on the Smooth spring
/// while fading in over 240 ms on the enter curve after 50 ms, a removed row fades out over 160 ms and closes on the same
/// spring before it goes. The cards grow and shrink with it. Reduced motion and Off change at once.
/// </summary>
internal sealed class PricingCalculatorFeatureList : Control
{
    /// <summary>Defines the <see cref="CheckBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CheckBrushProperty =
        AvaloniaProperty.Register<PricingCalculatorFeatureList, IBrush?>(nameof(CheckBrush));

    private readonly List<PricingCalculatorFeatureRow> _rows = new();

    /// <summary>Color of the check marks.</summary>
    public IBrush? CheckBrush { get => GetValue(CheckBrushProperty); set => SetValue(CheckBrushProperty, value); }

    /// <summary>The rows on screen, leaving ones included (tests read them).</summary>
    internal IReadOnlyList<PricingCalculatorFeatureRow> Rows => _rows;

    /// <summary>The texts of the rows that stay, in order (tests read them).</summary>
    internal IReadOnlyList<string?> Texts => _rows.Where(r => !r.IsLeaving).Select(r => r.Text).ToList();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CheckBrushProperty)
        {
            foreach (var row in _rows)
            {
                row.CheckBrush = CheckBrush;
            }
        }
    }

    /// <summary>Shows <paramref name="lines"/>: rows are matched by key, new ones open, missing ones close.</summary>
    public void SetLines(IReadOnlyList<PricingCalculatorMath.FeatureLine> lines)
    {
        var animate = IsLoaded && ArcMotion.IsFull(this);
        var byKey = new Dictionary<string, PricingCalculatorFeatureRow>(StringComparer.Ordinal);
        foreach (var row in _rows)
        {
            if (!row.IsLeaving)
            {
                byKey[row.Key] = row;
            }
        }
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            wanted.Add(line.Key);
        }
        // Rows that go: close in place.
        foreach (var row in byKey.Values)
        {
            if (!wanted.Contains(row.Key))
            {
                var leaving = row;
                leaving.Leave(animate, () =>
                {
                    _rows.Remove(leaving);
                    LogicalChildren.Remove(leaving);
                    VisualChildren.Remove(leaving);
                    InvalidateMeasure();
                });
            }
        }
        // Rows that stay or come, in the order of the lines; leaving rows keep their place among them.
        var insertAt = 0;
        foreach (var line in lines)
        {
            if (byKey.TryGetValue(line.Key, out var row))
            {
                row.Text = line.Text;
                insertAt = _rows.IndexOf(row) + 1;
            }
            else
            {
                var created = new PricingCalculatorFeatureRow(line.Key) { Text = line.Text, CheckBrush = CheckBrush };
                _rows.Insert(Math.Min(insertAt, _rows.Count), created);
                LogicalChildren.Add(created);
                VisualChildren.Add(created);
                created.Enter(animate && _hasLines);
                insertAt = _rows.IndexOf(created) + 1;
            }
            while (insertAt < _rows.Count && _rows[insertAt].IsLeaving)
            {
                insertAt++;
            }
        }
        _hasLines = true;
        InvalidateMeasure();
    }

    private bool _hasLines;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var row in _rows)
        {
            row.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, row.DesiredSize.Width);
            height += row.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var row in _rows)
        {
            row.Arrange(new Rect(0, y, finalSize.Width, row.DesiredSize.Height));
            y += row.DesiredSize.Height;
        }
        return finalSize;
    }
}

/// <summary>One feature row: a 16px check (1.75 stroke, 2px down) and the text, 4px above and below, 8px apart.</summary>
internal sealed class PricingCalculatorFeatureRow : Control
{
    /// <summary>The delay of the row's fade-in (50 ms).</summary>
    public static readonly TimeSpan EnterDelay = TimeSpan.FromMilliseconds(50);

    private readonly ArcGlyph _check;
    private readonly PricingCalculatorSwapText _text;
    private MotionValue<double>? _presence;
    private MotionValue<double>? _fade;

    public PricingCalculatorFeatureRow(string key)
    {
        Key = key;
        _check = new ArcGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 1.75 };
        _text = new PricingCalculatorSwapText { TextWrapping = TextWrapping.Wrap };
        LogicalChildren.Add(_check);
        VisualChildren.Add(_check);
        LogicalChildren.Add(_text);
        VisualChildren.Add(_text);
    }

    /// <summary>The row's key (its feature's index in the plan).</summary>
    public string Key { get; }

    /// <summary>True once the row is closing.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>How open the row is (0–1).</summary>
    public double Presence => _presence?.Value ?? 1;

    /// <summary>The row's text; a change swaps it in place.</summary>
    public string? Text
    {
        get => _text.Text;
        set
        {
            _text.Text = value;
            AutomationProperties.SetName(this, value);
        }
    }

    /// <summary>Color of the check.</summary>
    public IBrush? CheckBrush
    {
        set => _check.Foreground = value;
    }

    /// <summary>Opens the row (from zero height when <paramref name="animate"/>).</summary>
    public void Enter(bool animate)
    {
        if (!animate)
        {
            return;
        }
        _presence = new MotionValue<double>(this, 0, MotionVectors.Double);
        _presence.Changed += _ => InvalidateMeasure();
        _fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fade.Changed += v => Opacity = v;
        Opacity = 0;
        ClipToBounds = true;
        _ = _presence.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth)).ContinueWith(t =>
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsLeaving)
                {
                    ClipToBounds = false;
                }
            }), TaskScheduler.Default);
        _ = _fade.AnimateTo(1, MotionSpec.Tween(PricingCalculatorSwapMotion.EnterDuration, ArcEasings.Enter, EnterDelay));
    }

    /// <summary>Closes the row and calls <paramref name="removed"/> once it is gone (at once without animation).</summary>
    public void Leave(bool animate, Action removed)
    {
        IsLeaving = true;
        if (!animate)
        {
            removed();
            return;
        }
        ClipToBounds = true;
        _presence ??= CreatePresence();
        _fade ??= CreateFade();
        _ = _fade.AnimateTo(0, MotionSpec.Tween(PricingCalculatorSwapMotion.ExitDuration, ArcEasings.Standard));
        _presence.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth))
            .ContinueWith(_ => Dispatcher.UIThread.Post(removed), TaskScheduler.Default);
    }

    private MotionValue<double> CreatePresence()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    private MotionValue<double> CreateFade()
    {
        var value = new MotionValue<double>(this, Opacity, MotionVectors.Double);
        value.Changed += v => Opacity = v;
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        const double checkSize = 16;
        const double gap = 8;
        const double pad = 4;
        _check.Measure(new Size(checkSize, checkSize));
        var textWidth = Math.Max(0, availableSize.Width - checkSize - gap);
        _text.Measure(new Size(textWidth, double.PositiveInfinity));
        var natural = Math.Max(_text.DesiredSize.Height, checkSize + 2) + 2 * pad;
        return new Size(checkSize + gap + _text.DesiredSize.Width, natural * Math.Clamp(Presence, 0, 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        const double checkSize = 16;
        const double gap = 8;
        const double pad = 4;
        _check.Arrange(new Rect(0, pad + 2, checkSize, checkSize));
        _text.Arrange(new Rect(checkSize + gap, pad, Math.Max(0, finalSize.Width - checkSize - gap), _text.DesiredSize.Height));
        return finalSize;
    }
}
