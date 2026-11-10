// Ported from Arc's free logo-marquee block (MIT, Copyright (c) 2026 Elia Kuratli): the travelling row of logo-marquee.tsx and .module.css.
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The row of marks. It draws copies of the list itself (a mark and a name per brand) and travels by one list width in
/// <see cref="TravelDuration"/>, linearly, on a <see cref="LiveLoop"/>. Frames only move a transform, so nothing redraws or
/// allocates while it runs. As many copies are drawn as fill the width, at least two. Below full motion it draws one
/// centered list that wraps, 20 apart, and does not move.
/// </summary>
internal sealed class LogoMarqueeTrack : Control
{
    private const double RowGap = 20;
    private const double StaticInset = 24;

    public static readonly StyledProperty<IReadOnlyList<LogoMarqueeBrand>?> BrandsProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, IReadOnlyList<LogoMarqueeBrand>?>(nameof(Brands));

    public static readonly StyledProperty<LogoMarqueeTone> ToneProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, LogoMarqueeTone>(nameof(Tone));

    public static readonly StyledProperty<bool> IsHeldProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, bool>(nameof(IsHeld));

    public static readonly StyledProperty<TimeSpan> TravelDurationProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, TimeSpan>(nameof(TravelDuration), TimeSpan.FromSeconds(39));

    public static readonly StyledProperty<double> ItemGapProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, double>(nameof(ItemGap), 56);

    public static readonly StyledProperty<double> NameGapProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, double>(nameof(NameGap), 10);

    public static readonly StyledProperty<double> MarkSizeProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, double>(nameof(MarkSize), 22);

    public static readonly StyledProperty<double> NameFontSizeProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, double>(nameof(NameFontSize), 18);

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<LogoMarqueeTrack>();

    public static readonly StyledProperty<IBrush?> NameBrushProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, IBrush?>(nameof(NameBrush));

    public static readonly StyledProperty<IBrush?> MarkBrushProperty =
        AvaloniaProperty.Register<LogoMarqueeTrack, IBrush?>(nameof(MarkBrush));

    private readonly List<TextLayout> _names = new();
    private readonly List<double> _itemWidths = new();
    private readonly List<(int Index, Point Position)> _staticItems = new();
    private readonly TranslateTransform _offset = new();
    private readonly LiveLoop _loop;
    private double _listWidth;
    private double _rowHeight;
    private double _phase;
    private bool _static = true;
    private bool _namesDirty = true;

    static LogoMarqueeTrack()
    {
        AffectsMeasure<LogoMarqueeTrack>(BrandsProperty, ItemGapProperty, NameGapProperty, MarkSizeProperty, NameFontSizeProperty, FontFamilyProperty);
        AffectsRender<LogoMarqueeTrack>(ToneProperty, NameBrushProperty, MarkBrushProperty);
    }

    public LogoMarqueeTrack()
    {
        RenderTransform = _offset;
        _loop = new LiveLoop(this, Step);
        _loop.Start();
    }

    public IReadOnlyList<LogoMarqueeBrand>? Brands { get => GetValue(BrandsProperty); set => SetValue(BrandsProperty, value); }

    public LogoMarqueeTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>The row stands still (paused, or the pointer is on it), keeping its place.</summary>
    public bool IsHeld { get => GetValue(IsHeldProperty); set => SetValue(IsHeldProperty, value); }

    public TimeSpan TravelDuration { get => GetValue(TravelDurationProperty); set => SetValue(TravelDurationProperty, value); }

    public double ItemGap { get => GetValue(ItemGapProperty); set => SetValue(ItemGapProperty, value); }

    public double NameGap { get => GetValue(NameGapProperty); set => SetValue(NameGapProperty, value); }

    public double MarkSize { get => GetValue(MarkSizeProperty); set => SetValue(MarkSizeProperty, value); }

    public double NameFontSize { get => GetValue(NameFontSizeProperty); set => SetValue(NameFontSizeProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public IBrush? NameBrush { get => GetValue(NameBrushProperty); set => SetValue(NameBrushProperty, value); }

    public IBrush? MarkBrush { get => GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }

    /// <summary>How far along one list the row is, 0 to 1.</summary>
    public double Phase => _phase;

    /// <summary>The width of one list including the gap after its last mark.</summary>
    public double ListWidth => _listWidth;

    /// <summary>How many copies of the list are drawn.</summary>
    public int CopyCount { get; private set; } = 1;

    /// <summary>True below full motion: one wrapped list that does not move.</summary>
    public bool IsStatic => _static;

    /// <summary>The current travel in px (0 or negative).</summary>
    public double Offset => _offset.X;

    /// <summary>The running loop, for tests.</summary>
    internal LiveLoop Loop => _loop;

    /// <summary>Advances as one frame does; tests call it directly.</summary>
    internal bool Step(double seconds)
    {
        var duration = TravelDuration.TotalSeconds;
        if (duration > 0 && !IsHeld)
        {
            _phase = (_phase + seconds / duration) % 1;
            _offset.X = -_phase * _listWidth;
        }
        return true;
    }

    /// <summary>Re-reads the motion level: only full motion travels (Arc removes the animation and the copy otherwise).</summary>
    public void RefreshLevel()
    {
        var isStatic = !ArcMotion.IsFull(this);
        if (isStatic != _static)
        {
            _static = isStatic;
            InvalidateMeasure();
            InvalidateVisual();
        }
        if (_static)
        {
            _offset.X = 0;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RefreshLevel();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == BrandsProperty || property == NameFontSizeProperty || property == FontFamilyProperty)
        {
            _namesDirty = true;
        }
        if (property == NameBrushProperty)
        {
            _namesDirty = true;
            InvalidateMeasure();
        }
        if (property == BrandsProperty)
        {
            var names = Brands is { } brands ? string.Join(", ", brands.Select(b => b.Name)) : null;
            AutomationProperties.SetHelpText(this, names);
        }
        if (property == ArcMotion.LevelProperty)
        {
            RefreshLevel();
        }
    }

    private void RebuildNames()
    {
        foreach (var layout in _names)
        {
            layout.Dispose();
        }
        _names.Clear();
        _itemWidths.Clear();
        var weight = FontWeight.Medium;
        var typeface = new Typeface(FontFamily, FontStyle.Normal, weight);
        var size = NameFontSize;
        foreach (var brand in Brands ?? [])
        {
            _names.Add(new TextLayout(brand.Name, typeface, size, NameBrush, letterSpacing: size * -0.01));
        }
        _namesDirty = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_namesDirty)
        {
            RebuildNames();
        }
        _itemWidths.Clear();
        _rowHeight = MarkSize;
        var list = 0.0;
        foreach (var name in _names)
        {
            var width = MarkSize + NameGap + name.WidthIncludingTrailingWhitespace;
            _itemWidths.Add(width);
            list += width + ItemGap;
            _rowHeight = Math.Max(_rowHeight, name.Height);
        }
        _listWidth = list;
        _staticItems.Clear();
        var room = double.IsInfinity(availableSize.Width) ? list : availableSize.Width;
        if (_static)
        {
            CopyCount = 1;
            return new Size(room, MeasureStatic(room));
        }
        // Travelling by one list needs one list more than fills the width.
        CopyCount = list > 0 ? Math.Max(2, (int)Math.Ceiling(room / list) + 1) : 0;
        _offset.X = -_phase * _listWidth;
        return new Size(room, _rowHeight);
    }

    /// <summary>Lays out one list in centered rows and returns its height.</summary>
    private double MeasureStatic(double room)
    {
        var inner = Math.Max(0, room - StaticInset * 2);
        var y = 0.0;
        var start = 0;
        while (start < _itemWidths.Count)
        {
            var end = start;
            var used = _itemWidths[start];
            while (end + 1 < _itemWidths.Count && used + ItemGap + _itemWidths[end + 1] <= inner)
            {
                end++;
                used += ItemGap + _itemWidths[end];
            }
            var x = (room - used) / 2;
            for (var i = start; i <= end; i++)
            {
                _staticItems.Add((i, new Point(x, y)));
                x += _itemWidths[i] + ItemGap;
            }
            y += _rowHeight + RowGap;
            start = end + 1;
        }
        return y > 0 ? y - RowGap : 0;
    }

    public override void Render(DrawingContext context)
    {
        var brands = Brands;
        if (brands is null || _names.Count != brands.Count)
        {
            return;
        }
        if (_static)
        {
            foreach (var (index, position) in _staticItems)
            {
                DrawItem(context, brands[index], index, position);
            }
            return;
        }
        var x = 0.0;
        for (var copy = 0; copy < CopyCount; copy++)
        {
            for (var i = 0; i < _names.Count; i++)
            {
                DrawItem(context, brands[i], i, new Point(x, 0));
                x += _itemWidths[i] + ItemGap;
            }
        }
    }

    private void DrawItem(DrawingContext context, LogoMarqueeBrand brand, int index, Point at)
    {
        var mark = new Rect(at.X, at.Y + (_rowHeight - MarkSize) / 2, MarkSize, MarkSize);
        if (Tone == LogoMarqueeTone.Color && brand.ColorIcon is { } image)
        {
            // Drawn at the origin of a pushed offset: a drawing image scales its own offset when it is drawn at another size.
            using (context.PushTransform(Matrix.CreateTranslation(mark.X, mark.Y)))
            {
                context.DrawImage(image, new Rect(image.Size), new Rect(mark.Size));
            }
        }
        else if (brand.Icon is { } icon)
        {
            DrawGeometry(context, brand, icon, mark);
        }
        var name = _names[index];
        name.Draw(context, new Point(at.X + MarkSize + NameGap, at.Y + (_rowHeight - name.Height) / 2));
    }

    private void DrawGeometry(DrawingContext context, LogoMarqueeBrand brand, Geometry icon, Rect mark)
    {
        var brush = Tone == LogoMarqueeTone.Color && brand.Color is { } color ? new ImmutableSolidColorBrush(color) : MarkBrush;
        var bounds = icon.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var scale = Math.Min(mark.Width / bounds.Width, mark.Height / bounds.Height);
        var x = mark.X + (mark.Width - bounds.Width * scale) / 2 - bounds.X * scale;
        var y = mark.Y + (mark.Height - bounds.Height * scale) / 2 - bounds.Y * scale;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            context.DrawGeometry(brush, null, icon);
        }
    }
}
