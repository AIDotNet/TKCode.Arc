using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The 32px round badge in front of a transaction: the person's picture, their initials on a colored disc (the color is
/// hashed from the name, so it is stable), or a 16px stroke icon on the muted surface.
/// </summary>
internal sealed class WalletStackBadge : Avalonia.Controls.Control
{
    public const double Diameter = 32;

    private IImage? _image;
    private string? _initials;
    private Geometry? _icon;
    private IBrush? _surface;
    private IBrush? _iconInk;
    private IBrush? _discBrush;
    private FormattedText? _text;
    private Typeface _typeface = Typeface.Default;

    public WalletStackBadge()
    {
        IsHitTestVisible = false;
        Width = Height = Diameter;
    }

    public void Set(WalletStackTransaction transaction, IBrush? surface, IBrush? iconInk, Typeface typeface)
    {
        _image = transaction.Avatar;
        _initials = transaction.Avatar is null ? transaction.AvatarInitials : null;
        _icon = _image is null && string.IsNullOrEmpty(_initials) ? transaction.Icon : null;
        if (_image is null && string.IsNullOrEmpty(_initials) && _icon is null)
        {
            _initials = Initials(transaction.Merchant);
        }
        _surface = surface;
        _iconInk = iconInk;
        _typeface = typeface;
        _text = null;
        _discBrush = string.IsNullOrEmpty(_initials) ? null : Disc(transaction.Merchant);
        InvalidateVisual();
    }

    /// <summary>Up to two initials from a name ("Theo Marsh" → "TM").</summary>
    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }
        var first = char.ToUpperInvariant(parts[0][0]);
        return parts.Length == 1 ? first.ToString() : string.Concat(first, char.ToUpperInvariant(parts[^1][0]));
    }

    // A soft two-hue OKLCH disc whose hue comes from the name.
    private static IBrush Disc(string name)
    {
        var hash = 17;
        foreach (var c in name)
        {
            hash = unchecked(hash * 31 + c);
        }
        var hue = (hash & 0x7FFFFFFF) % 360;
        var stops = new[]
        {
            new ImmutableGradientStop(0, Oklch.ToColor(0.72, 0.11, hue)),
            new ImmutableGradientStop(1, Oklch.ToColor(0.55, 0.13, (hue + 40) % 360)),
        };
        return new ImmutableLinearGradientBrush(stops, 1, null, null, GradientSpreadMethod.Pad, RelativePoint.TopLeft, RelativePoint.BottomRight);
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(0, 0, Diameter, Diameter);
        var center = rect.Center;
        if (_image is { } image)
        {
            using (context.PushGeometryClip(new EllipseGeometry(rect)))
            {
                var size = image.Size;
                var scale = Math.Max(Diameter / Math.Max(1, size.Width), Diameter / Math.Max(1, size.Height));
                var crop = new Size(Diameter / scale, Diameter / scale);
                context.DrawImage(image, new Rect((size.Width - crop.Width) / 2, (size.Height - crop.Height) / 2, crop.Width, crop.Height), rect);
            }
            return;
        }
        if (_discBrush is { } disc && !string.IsNullOrEmpty(_initials))
        {
            context.DrawEllipse(disc, null, center, Diameter / 2, Diameter / 2);
            _text ??= new FormattedText(_initials, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(_typeface.FontFamily, FontStyle.Normal, FontWeight.SemiBold), 12, Brushes.White);
            context.DrawText(_text, new Point(center.X - _text.Width / 2, center.Y - _text.Height / 2));
            return;
        }
        context.DrawEllipse(_surface, null, center, Diameter / 2, Diameter / 2);
        if (_icon is { } icon && _iconInk is { } ink)
        {
            const double iconSize = 16;
            var s = iconSize / 24;
            using (context.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(center.X - iconSize / 2, center.Y - iconSize / 2)))
            {
                context.DrawGeometry(null, new Pen(ink, 1.75, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), icon);
            }
        }
    }
}
