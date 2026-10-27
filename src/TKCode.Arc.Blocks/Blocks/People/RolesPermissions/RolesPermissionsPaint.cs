using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The colors and pens every self-drawn part of the matrix shares, read from the grid's theme-bound brushes. One instance
/// per grid; parts hold no colors of their own except the ones that animate (they keep mutable brushes they recolor).
/// </summary>
internal sealed class RolesPermissionsPaint
{
    public Color Foreground;
    public Color TextMuted;
    public Color Border;
    public Color BorderSubtle;
    public Color Surface;
    public Color SurfaceMuted;
    public Color Warning;
    public Color FocusRing;
    public Color BoxOffBorder;
    public Color BoxHoverFill;
    public Color BoxHoverBorder;
    public Color BoxOnFill;
    public Color BoxOnGlyph;
    public Color BoxMixedFill;
    public Color BoxLockedFill;
    public Color BandFill;
    public Color RowHoverFill;

    public IImmutableSolidColorBrush ForegroundBrush = new ImmutableSolidColorBrush(Colors.Black);
    public IImmutableSolidColorBrush TextMutedBrush = new ImmutableSolidColorBrush(Colors.Gray);
    public IImmutableSolidColorBrush SurfaceBrush = new ImmutableSolidColorBrush(Colors.White);
    public IImmutableSolidColorBrush SurfaceMutedBrush = new ImmutableSolidColorBrush(Colors.WhiteSmoke);
    public IImmutableSolidColorBrush WarningBrush = new ImmutableSolidColorBrush(Colors.Orange);
    public IImmutableSolidColorBrush BandBrush = new ImmutableSolidColorBrush(Colors.Transparent);
    public IImmutableSolidColorBrush RowHoverBrush = new ImmutableSolidColorBrush(Colors.Transparent);
    public ImmutablePen BorderPen = new(Brushes.LightGray.ToImmutable());
    public ImmutablePen BorderSubtlePen = new(Brushes.LightGray.ToImmutable());
    public ImmutablePen FocusPen = new(Brushes.Black.ToImmutable(), 2);
    public ImmutablePen MutedIconPen = new(Brushes.Gray.ToImmutable(), 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    /// <summary>Bumped on every refresh so parts can drop what they cached against the old colors.</summary>
    public int Version;

    public void Refresh(RolesPermissionsGrid grid)
    {
        Foreground = ColorOf(grid.Foreground, Colors.Black);
        TextMuted = ColorOf(grid.MutedBrush, Colors.Gray);
        Border = ColorOf(grid.BorderBrush, Colors.LightGray);
        BorderSubtle = ColorOf(grid.SubtleBorderBrush, Colors.LightGray);
        Surface = ColorOf(grid.SurfaceBrush, Colors.White);
        SurfaceMuted = ColorOf(grid.SurfaceMutedBrush, Colors.WhiteSmoke);
        Warning = ColorOf(grid.WarningBrush, Colors.Orange);
        FocusRing = ColorOf(grid.FocusRingBrush, Colors.Black);
        BoxOffBorder = ColorOf(grid.BoxOffBorderBrush, Colors.Gray);
        BoxHoverFill = ColorOf(grid.BoxHoverFillBrush, Colors.WhiteSmoke);
        BoxHoverBorder = ColorOf(grid.BoxHoverBorderBrush, Colors.Gray);
        BoxOnFill = ColorOf(grid.BoxOnFillBrush, Colors.Black);
        BoxOnGlyph = ColorOf(grid.BoxOnGlyphBrush, Colors.White);
        BoxMixedFill = ColorOf(grid.BoxMixedFillBrush, Colors.LightGray);
        BoxLockedFill = ColorOf(grid.BoxLockedFillBrush, Colors.Gray);
        BandFill = ColorOf(grid.BandFillBrush, Colors.Transparent);
        RowHoverFill = ColorOf(grid.RowHoverFillBrush, Colors.Transparent);

        ForegroundBrush = new ImmutableSolidColorBrush(Foreground);
        TextMutedBrush = new ImmutableSolidColorBrush(TextMuted);
        SurfaceBrush = new ImmutableSolidColorBrush(Surface);
        SurfaceMutedBrush = new ImmutableSolidColorBrush(SurfaceMuted);
        WarningBrush = new ImmutableSolidColorBrush(Warning);
        BandBrush = new ImmutableSolidColorBrush(BandFill);
        RowHoverBrush = new ImmutableSolidColorBrush(RowHoverFill);
        BorderPen = new ImmutablePen(new ImmutableSolidColorBrush(Border), 1);
        BorderSubtlePen = new ImmutablePen(new ImmutableSolidColorBrush(BorderSubtle), 1);
        FocusPen = new ImmutablePen(new ImmutableSolidColorBrush(FocusRing), 2);
        MutedIconPen = new ImmutablePen(TextMutedBrush, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        Version++;
    }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

    /// <summary>Linear blend in sRGB (enough for the short color tweens between neighbouring grays).</summary>
    public static Color Lerp(Color a, Color b, double t)
    {
        if (t <= 0)
        {
            return a;
        }
        if (t >= 1)
        {
            return b;
        }
        return Color.FromArgb(
            (byte)Math.Round(a.A + (b.A - a.A) * t),
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    /// <summary>Alpha-scales a color.</summary>
    public static Color Fade(Color color, double opacity)
        => Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);
}

/// <summary>
/// Base of the matrix's self-drawn interactive parts (cells, role headers, group buttons): the grid that owns them, the
/// clip it sets while a group folds, and the motion helpers they share.
/// </summary>
internal abstract class RolesPermissionsPart : Control
{
    private RectangleGeometry? _clip;

    static RolesPermissionsPart()
    {
        // The parts draw their own focus ring.
        FocusAdornerProperty.OverrideDefaultValue<RolesPermissionsPart>(null);
    }

    public RolesPermissionsGrid? Grid { get; set; }

    /// <summary>Shows only <paramref name="rect"/> (in the part's own coordinates) or, with null, everything.</summary>
    public void SetClip(Rect? rect)
    {
        if (rect is not { } r)
        {
            if (Clip is not null)
            {
                Clip = null;
            }
            return;
        }
        _clip ??= new RectangleGeometry();
        if (_clip.Rect != r || !ReferenceEquals(Clip, _clip))
        {
            _clip.Rect = r;
            Clip = _clip;
            InvalidateVisual();
        }
    }

    /// <summary>Whether this part should animate now (Full motion and attached; Reduced and Off jump, as Arc's reduced mode does).</summary>
    protected bool Animates => ArcMotion.IsFull(this) && (TopLevel.GetTopLevel(this) is not null);

    protected bool IsFocusVisible => IsFocused && Classes.Contains(":focus-visible");

    protected override void OnGotFocus(Avalonia.Input.FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(Avalonia.Input.FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    /// <summary>Draws the 2px focus ring 2px outside <paramref name="rect"/> with a radius one step larger.</summary>
    protected void DrawFocusRing(DrawingContext context, Rect rect, double radius)
    {
        if (Grid is null)
        {
            return;
        }
        var ring = rect.Inflate(3);
        context.DrawRectangle(null, Grid.Paint.FocusPen, ring, radius + 3, radius + 3);
    }
}
