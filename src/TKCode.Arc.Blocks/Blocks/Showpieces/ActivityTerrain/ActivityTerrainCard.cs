using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>What the hover card shows for one hour.</summary>
internal sealed record ActivityTerrainCardInfo(
    string Day,
    string HourRange,
    string Value,
    string Unit,
    string? PreviousLabel,
    string? PreviousValue,
    string? ChangeLabel,
    string? Change,
    int ChangeTone,
    string ShareLabel,
    string Share,
    double[] DayValues,
    int Hour,
    ActivityTerrainTone Tone);

/// <summary>
/// The hover card's content (Arc Activity terrain, design reference): the day and the hour range, the value with its
/// unit, the previous week, the change and the share of the day, and the day's 24 hours as small bars with the current
/// hour in the metric's tone. 12/16.8 type, values in tabular figures.
/// </summary>
internal sealed class ActivityTerrainCardView : StackPanel
{
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private readonly TextBlock _day = new();
    private readonly TextBlock _hours = new();
    private readonly Run _value = new();
    private readonly Run _unit = new();
    private readonly TextBlock _valueLine = new();
    private readonly Grid _rows = new();
    private readonly TextBlock _previousLabel = new();
    private readonly TextBlock _previous = new();
    private readonly TextBlock _changeLabel = new();
    private readonly TextBlock _change = new();
    private readonly TextBlock _shareLabel = new();
    private readonly TextBlock _share = new();
    private readonly ActivityTerrainCardBars _bars = new();
    private IDisposable? _changeBinding;
    private int _changeTone = int.MinValue;

    public ActivityTerrainCardView()
    {
        TextElement.SetFontSize(this, 12);
        ArcTokens.Bind(this, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        _day.FontWeight = FontWeight.Medium;
        _day.TextTrimming = TextTrimming.CharacterEllipsis;
        ArcTokens.Bind(_day, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _hours.FontFeatures = s_tabular;
        ArcTokens.Bind(_hours, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        Grid.SetColumn(_hours, 1);
        header.Children.Add(_day);
        header.Children.Add(_hours);

        _value.FontSize = 18;
        _value.FontWeight = FontWeight.Medium;
        ArcTokens.Bind(_value, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _unit.FontSize = 12;
        ArcTokens.Bind(_unit, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _valueLine.Inlines = new InlineCollection { _value, new Run(" "), _unit };
        _valueLine.FontFeatures = s_tabular;
        _valueLine.LetterSpacing = -0.36;
        _valueLine.Margin = new Thickness(0, 4, 0, 8);

        _rows.ColumnDefinitions = new ColumnDefinitions("*,Auto");
        _rows.RowDefinitions = new RowDefinitions("Auto,Auto,Auto");
        _rows.RowSpacing = 2;
        AddRow(_previousLabel, _previous, 0);
        AddRow(_changeLabel, _change, 1);
        AddRow(_shareLabel, _share, 2);
        ArcTokens.Bind(_previous, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_share, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);

        _bars.Margin = new Thickness(0, 10, 0, 0);
        Children.Add(header);
        Children.Add(_valueLine);
        Children.Add(_rows);
        Children.Add(_bars);
    }

    private void AddRow(TextBlock label, TextBlock value, int row)
    {
        value.FontFeatures = s_tabular;
        value.TextAlignment = TextAlignment.Right;
        Grid.SetRow(label, row);
        Grid.SetRow(value, row);
        Grid.SetColumn(value, 1);
        _rows.Children.Add(label);
        _rows.Children.Add(value);
    }

    internal ActivityTerrainCardInfo? Info { get; private set; }

    internal ActivityTerrainCardBars Bars => _bars;

    /// <summary>Shows an hour (the small bars ease to the new day's heights).</summary>
    public void Update(ActivityTerrainCardInfo info)
    {
        Info = info;
        _day.Text = info.Day;
        _hours.Text = info.HourRange;
        _value.Text = info.Value;
        _unit.Text = info.Unit;
        var hasPrevious = info.PreviousValue is not null;
        _previousLabel.IsVisible = _previous.IsVisible = hasPrevious;
        _previousLabel.Text = info.PreviousLabel;
        _previous.Text = info.PreviousValue;
        var hasChange = info.Change is not null;
        _changeLabel.IsVisible = _change.IsVisible = hasChange;
        _changeLabel.Text = info.ChangeLabel;
        _change.Text = info.Change;
        _shareLabel.Text = info.ShareLabel;
        _share.Text = info.Share;
        if (info.ChangeTone != _changeTone)
        {
            _changeTone = info.ChangeTone;
            _changeBinding?.Dispose();
            _changeBinding = ArcTokens.Bind(_change, TextBlock.ForegroundProperty, info.ChangeTone switch
            {
                > 0 => ArcDesignTokenKind.Success,
                < 0 => ArcDesignTokenKind.Danger,
                _ => ArcDesignTokenKind.Foreground
            });
        }
        _bars.SetDay(info.DayValues, info.Hour, info.Tone);
    }
}

/// <summary>
/// The card's 24 small bars: heights max(6%, value ÷ the day's largest) × 22px, 1px apart, radius 1, BorderStrong with the
/// current hour in the metric's tone; heights ease over 240ms (standard curve) when the day changes.
/// </summary>
/// <remarks>Code-driven motion: 24 self-drawn heights tween together from wherever they are; a ControlTheme transition cannot animate self-drawn geometry.</remarks>
internal sealed class ActivityTerrainCardBars : Control
{
    private const int Count = ActivityTerrainMath.Hours;
    private const double BarHeight = 22;
    private const double MinimumShare = 0.06;

    private readonly double[] _from = new double[Count];
    private readonly double[] _to = new double[Count];
    private MotionValue<double>? _progress;
    private int _hour = -1;
    private ActivityTerrainTone _tone;
    private bool _hasDay;

    static ActivityTerrainCardBars()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<ActivityTerrainCardBars>(false);
    }

    public ActivityTerrainCardBars()
    {
        Height = BarHeight;
        ResourcesChanged += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>The share of 22px each bar shows now (tests).</summary>
    internal double ShareAt(int hour)
    {
        var p = _progress?.Value ?? 1;
        return _from[hour] + (_to[hour] - _from[hour]) * p;
    }

    internal int Hour => _hour;

    public void SetDay(double[] values, int hour, ActivityTerrainTone tone)
    {
        var max = 0.0;
        foreach (var value in values)
        {
            max = Math.Max(max, value);
        }
        var p = _progress?.Value ?? 1;
        for (var h = 0; h < Count; h++)
        {
            var current = _from[h] + (_to[h] - _from[h]) * p;
            _from[h] = _hasDay ? current : 0;
            var value = h < values.Length ? values[h] : 0;
            _to[h] = max > 0 ? Math.Max(MinimumShare, value / max) : MinimumShare;
        }
        _hour = hour;
        _tone = tone;
        _progress ??= CreateProgress();
        if (!_hasDay)
        {
            _hasDay = true;
            Array.Copy(_to, _from, Count);
            _progress.Jump(1);
        }
        else
        {
            _progress.Jump(0);
            _ = _progress.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        InvalidateVisual();
    }

    private MotionValue<double> CreateProgress()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    public override void Render(DrawingContext context)
    {
        if (!_hasDay)
        {
            return;
        }
        var width = Bounds.Width;
        const double gap = 1;
        var column = (width - gap * (Count - 1)) / Count;
        if (column <= 0)
        {
            return;
        }
        var rest = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong));
        var current = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, _tone switch
        {
            ActivityTerrainTone.Success => ArcDesignTokenKind.Success,
            ActivityTerrainTone.Warning => ArcDesignTokenKind.Warning,
            ActivityTerrainTone.Danger => ArcDesignTokenKind.Danger,
            ActivityTerrainTone.Neutral => ArcDesignTokenKind.TextSecondary,
            _ => ArcDesignTokenKind.Accent
        }));
        for (var h = 0; h < Count; h++)
        {
            var height = ShareAt(h) * BarHeight;
            var rect = new Rect(h * (column + gap), BarHeight - height, column, height);
            context.DrawRectangle(h == _hour ? current : rest, null, new RoundedRect(rect, 1));
        }
    }
}
