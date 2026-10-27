using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// An incident's updates, newest first, over the line about its lead (notes 3.5 and 6.6). Each entry has a padding of
/// (20, 0, 0, 12), a dot of 8 at (0, 6) (the newest is filled in its tone, older ones are hollow with a BorderStrong edge),
/// a 1px rail at x 3.5 from 18 to the entry's end less 2, the stage word (14, 500) with the time after it (12, tabular,
/// TextMuted) and the body under them (14, TextSecondary, wrapped). The lead row is a 28 avatar and 12px text. A new entry
/// grows from zero height on a spring (k 180, c 27) and fades in over 200ms (Enter).
/// </summary>
/// <remarks>Why code-driven: the entries grow and fade as they arrive inside one drawing with wrapped text of measured height.</remarks>
internal sealed class UptimeTimeline : Control
{
    private static readonly SpringSpec s_grow = SpringSpec.FromPhysical(180, 27);

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(256);
    private readonly List<Entry> _entries = [];
    private readonly LiveLoop _loop;
    private UptimeIncident? _incident;
    private UptimeStatusLabels _labels = new();
    private Func<DateTimeOffset, string> _time = t => t.UtcDateTime.ToString("MMM d, HH:mm", AnalyticsFormat.DefaultCulture);
    private bool _hasData;

    public UptimeTimeline()
    {
        IsHitTestVisible = false;
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Reduced };
        ActualThemeVariantChanged += (_, _) => RefreshPalette();
        ResourcesChanged += (_, _) => RefreshPalette();
    }

    /// <summary>The number of entries.</summary>
    public int EntryCount => _entries.Count;

    /// <summary>The phase words, newest first (tests).</summary>
    internal IReadOnlyList<string> Phases => _entries.Select(e => e.Update.Phase).ToArray();

    /// <summary>The share of each entry's height shown, newest first (tests).</summary>
    internal IReadOnlyList<double> Growth => _entries.Select(e => e.Grow.Value).ToArray();

    internal IReadOnlyList<double> FullHeights => _entries.Select(e => e.FullHeight).ToArray();

    /// <summary>The spoken text of the whole timeline.</summary>
    public string SpokenText { get; private set; } = string.Empty;

    /// <summary>
    /// Shows an incident's updates. Entries seen before keep their place; new ones grow in when <paramref name="animate"/>.
    /// </summary>
    public void SetIncident(UptimeIncident incident, UptimeStatusLabels labels, Func<DateTimeOffset, string> time, bool animate)
    {
        animate &= _hasData && this.IsAttachedToVisualTree() && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        var full = ArcMotion.IsFull(this);
        _incident = incident;
        _labels = labels;
        _time = time;
        var ordered = incident.Updates.OrderByDescending(u => u.Time).ToList();
        var next = new List<Entry>(ordered.Count);
        foreach (var update in ordered)
        {
            var existing = _entries.FirstOrDefault(e => ReferenceEquals(e.Update, update));
            if (existing is not null)
            {
                next.Add(existing);
                continue;
            }
            var entry = new Entry(update);
            if (animate)
            {
                entry.Grow.Jump(full ? 0 : 1);
                entry.Opacity.Jump(0);
                if (full)
                {
                    entry.Grow.SpringTo(1, s_grow);
                }
                entry.Opacity.Start(1, ArcMotionPresets.Standard, AnalyticsEasings.Enter);
            }
            next.Add(entry);
        }
        _entries.Clear();
        _entries.AddRange(next);
        _hasData = true;
        SpokenText = string.Join(" ", ordered.Select(u => $"{u.Phase}, {_time(u.Time)}. {u.Body}"));
        InvalidateMeasure();
        InvalidateVisual();
        _loop.Start();
    }

    private bool Advance(double dt)
    {
        var moving = false;
        foreach (var entry in _entries)
        {
            moving |= entry.Grow.Step(dt);
            moving |= entry.Opacity.Advance(dt);
        }
        InvalidateMeasure();
        InvalidateVisual();
        return moving;
    }

    private void RefreshPalette()
    {
        _text.Clear();
        if (_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private double EntryHeight(Entry entry, double width)
    {
        if (!_palette.IsReady && this.IsAttachedToVisualTree())
        {
            _palette.Refresh(this);
        }
        var h = 19.6 + 12;
        if (!string.IsNullOrEmpty(entry.Update.Body) && _palette.IsReady)
        {
            var body = _text.GetWrapped(entry.Update.Body!, new Typeface(_palette.BodyFont), 14, Math.Max(40, width - 20), 12);
            h += 2 + Math.Max(19.6, body.Lines.Count * 19.6);
        }
        else if (!string.IsNullOrEmpty(entry.Update.Body))
        {
            h += 2 + 19.6;
        }
        entry.FullHeight = h;
        return h;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        var height = 0.0;
        foreach (var entry in _entries)
        {
            height += EntryHeight(entry, width) * Math.Clamp(entry.Grow.Value, 0, 1.05);
        }
        if (_incident?.Lead is not null)
        {
            height += 44;
        }
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            _palette.Refresh(this);
        }
        var p = _palette;
        var width = Bounds.Width;
        var body = new Typeface(p.BodyFont);
        var medium = new Typeface(p.BodyFont, FontStyle.Normal, p.Medium);
        var y = 0.0;
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            var full = EntryHeight(entry, width);
            var shown = full * Math.Clamp(entry.Grow.Value, 0, 1.05);
            using (context.PushClip(new Rect(0, y, width, Math.Max(0, shown))))
            using (context.PushOpacity(Math.Clamp(entry.Opacity.Value, 0, 1)))
            {
                DrawEntry(context, entry, i == 0, y, full, width, body, medium);
            }
            y += shown;
        }
        if (_incident?.Lead is { } lead)
        {
            DrawLead(context, lead, y, width, medium);
        }
    }

    private void DrawEntry(DrawingContext context, Entry entry, bool newest, double top, double height, double width, Typeface body, Typeface medium)
    {
        var p = _palette;
        // The rail runs through the entry (also the last one, down to the lead row).
        context.DrawLine(p.Pen(p.Border, 1, cap: PenLineCap.Flat), new Point(3.5, top + 18), new Point(3.5, top + height - 2));
        var dot = new Rect(0, top + 6, 8, 8);
        if (newest)
        {
            var tone = UptimeColors.Tone(p, entry.Update.Tone);
            context.DrawEllipse(p.Brush(tone), p.Pen(tone, 1), dot.Center, 3.5, 3.5);
        }
        else
        {
            context.DrawEllipse(p.Brush(p.Surface), p.Pen(p.BorderStrong, 1), dot.Center, 3.5, 3.5);
        }
        var phase = _text.Get(entry.Update.Phase, medium, 14);
        phase.Draw(context, p.Brush(p.Foreground), new Point(20, top + (19.6 - phase.Height) / 2));
        var time = _text.Get(_time(entry.Update.Time), body, 12, true);
        time.Draw(context, p.Brush(p.TextMuted), new Point(20 + phase.Width + 8, top + (19.6 - time.Height) / 2 + 1));
        if (!string.IsNullOrEmpty(entry.Update.Body))
        {
            var text = _text.GetWrapped(entry.Update.Body!, body, 14, Math.Max(40, width - 20), 12);
            text.Draw(context, p.Brush(p.TextSecondary), new Point(20, top + 19.6 + 2));
        }
    }

    private void DrawLead(DrawingContext context, UptimePerson lead, double top, double width, Typeface medium)
    {
        var p = _palette;
        var center = new Point(20 + 14, top + 14);
        context.DrawEllipse(p.Brush(p.SurfaceMuted), p.Pen(p.Border, 1), center, 13.5, 13.5);
        if (lead.Image is { } image)
        {
            using (context.PushGeometryClip(new EllipseGeometry(new Rect(center.X - 13, center.Y - 13, 26, 26))))
            {
                context.DrawImage(image, new Rect(center.X - 13, center.Y - 13, 26, 26));
            }
        }
        else
        {
            var initials = _text.Get(lead.AvatarText, medium, 11);
            initials.Draw(context, p.Brush(p.TextSecondary), new Point(center.X - initials.Width / 2, center.Y - initials.Height / 2));
        }
        var format = _incident is { IsResolved: true } ? _labels.LedFormat : _labels.LeadingFormat;
        var line = string.Format(AnalyticsFormat.DefaultCulture, format, lead.Name);
        var text = _text.GetTrimmed(line, new Typeface(p.BodyFont), 12, Math.Max(20, width - 20 - 28 - 8));
        text.Draw(context, p.Brush(p.TextSecondary), new Point(20 + 28 + 8, top + (28 - text.Height) / 2));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ListPeer(this);

    private sealed class Entry(IncidentUpdate update)
    {
        public readonly IncidentUpdate Update = update;
        public AnalyticsSpringChannel Grow = new(1, 0.001, 0.01);
        public AnalyticsTween Opacity = new(1);
        public double FullHeight;
    }

    private sealed class ListPeer(UptimeTimeline owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

        protected override string? GetNameCore() => owner._labels.TimelineText + ". " + owner.SpokenText;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
