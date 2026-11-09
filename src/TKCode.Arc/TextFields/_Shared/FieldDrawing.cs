using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A Core <see cref="LazyFocusRing"/> for a button whose template used to carry a hidden <c>PART_FocusRing</c> Border:
/// the ring is built the first time the button shows <c>:focus-visible</c> and placed last in the template's host panel,
/// where the Border was. Metrics the template gave as literals or template bindings rather than token resources
/// (a fixed margin, another thickness token, a corner radius that follows one of the button's properties) are applied
/// to the ring once it exists, so it looks exactly as the Border did.
/// </summary>
internal sealed class FieldFocusRing
{
    private readonly Control _owner;
    private readonly LazyFocusRing _ring;
    private readonly Thickness? _margin;
    private readonly object? _thicknessKey;
    private readonly StyledProperty<CornerRadius>? _radiusProperty;
    private Border? _applied;

    /// <param name="owner">The focusable button.</param>
    /// <param name="marginKey">Resource key of the ring margin; null when <paramref name="margin"/> gives it.</param>
    /// <param name="radiusKey">Resource key of the ring corner radius; also the fallback while <paramref name="radiusProperty"/> is unset.</param>
    /// <param name="margin">A fixed margin (the template's literal), applied over the key.</param>
    /// <param name="thicknessKey">A border thickness key other than the design token <c>FocusEdgeThickness</c>.</param>
    /// <param name="radiusProperty">A property of the owner the ring's corner radius follows (the template's TemplateBinding).</param>
    public FieldFocusRing(Control owner, object? marginKey, object radiusKey, Thickness? margin = null, object? thicknessKey = null,
        StyledProperty<CornerRadius>? radiusProperty = null)
    {
        _owner = owner;
        _margin = margin;
        _thicknessKey = thicknessKey;
        _radiusProperty = radiusProperty;
        // The helper always binds its margin to a key; a literal margin replaces that value below.
        _ring = new LazyFocusRing(owner, marginKey ?? ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness), radiusKey);
    }

    /// <summary>The ring once it was needed; null before.</summary>
    public Border? Ring => _ring.Ring;

    /// <summary>Places the ring in the new template's host panel (null when the template has none).</summary>
    public void Attach(Panel? host)
    {
        _ring.Attach(host);
        _applied = null;
        Update();
    }

    /// <summary>Shows the ring while the owner is keyboard focused; builds and adjusts it the first time.</summary>
    public void Update()
    {
        _ring.Update();
        if (_ring.Ring is not { } ring || ReferenceEquals(ring, _applied))
        {
            return;
        }
        _applied = ring;
        if (_margin is { } margin)
        {
            ring.Margin = margin;
        }
        if (_thicknessKey is { } thicknessKey)
        {
            ring.Bind(Border.BorderThicknessProperty, ring.GetResourceObservable(thicknessKey));
        }
        if (_radiusProperty is { } radiusProperty)
        {
            ring.Bind(Border.CornerRadiusProperty, _owner.GetObservable(radiusProperty));
        }
    }
}

/// <summary>
/// Shared helpers for the field family: the Arc number test used by message copy. Animated blurs use the Core
/// <see cref="Primitives.ArcBlur"/> cache.
/// </summary>
internal static class FieldDrawing
{
    /// <summary>Arc's number test (<c>/^\d[\d.,/:]*%?$/</c>): counts like "12", "1,204", "3/5", "50%".</summary>
    public static bool IsNumber(string word)
    {
        if (word.Length == 0 || !char.IsAsciiDigit(word[0]))
        {
            return false;
        }
        var end = word.Length;
        if (word[end - 1] == '%')
        {
            end--;
        }
        for (var i = 1; i < end; i++)
        {
            var c = word[i];
            if (!char.IsAsciiDigit(c) && c != '.' && c != ',' && c != '/' && c != ':')
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>JavaScript <c>parseFloat(word.replace(/,/g, ""))</c>: the leading numeric part.</summary>
    public static double LeadingNumber(string word)
    {
        Span<char> buffer = stackalloc char[Math.Min(word.Length, 64)];
        var length = 0;
        var seenDot = false;
        foreach (var c in word)
        {
            if (c == ',')
            {
                continue;
            }
            if (char.IsAsciiDigit(c) || (c == '.' && !seenDot))
            {
                seenDot |= c == '.';
                if (length < buffer.Length)
                {
                    buffer[length++] = c;
                }
                continue;
            }
            break;
        }
        return double.TryParse(buffer[..length], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
    }

    public static double Lerp(double from, double to, double t) => from + (to - from) * t;
}
