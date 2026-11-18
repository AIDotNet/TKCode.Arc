using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia.Automation.Provider;

namespace TKCode.Arc;

/// <summary>
/// The colors and fonts the drawn diff rows use, read once from the tokens of the card (and again when the theme or its
/// resources change) instead of by every row on every frame. Brushes are immutable and shared.
/// </summary>
internal sealed class CodeReviewPalette
{
    public bool IsDark;
    public Color Surface;
    public Color Foreground;
    public Color TextSecondary;
    public Color TextMuted;
    public Color HunkFill;
    public Color AddedLine;
    public Color AddedGutter;
    public Color DeletedLine;
    public Color DeletedGutter;
    public Color FreshLine;
    public Color FreshGutter;
    public Color ActiveLine;
    public Color ActiveGutter;
    public Color Success;
    public Color Danger;
    public Color Accent;
    public Color AccentForeground;
    public Color FocusInset;
    public FontFamily Mono = FontFamily.Default;
    public Typeface MonoFace;
    public Typeface MonoItalic;
    public double CharWidth = 7.2;

    // Geometry from CodeReviewToken (defaults are the token's values, used before the card is in a themed tree).
    public double CodeFontSize = 12;
    public double LineHeight = 22;
    public double HunkHeight = 32;
    public double NumberWidth = 32.5;
    public Avalonia.Thickness GutterPadding = new(12, 0, 6, 0);
    public Avalonia.Thickness CompactGutterPadding = new(4, 0, 6, 0);
    public double SignWidth = 21.7;
    public double SignInset = 4;
    public double MarkSize = 18;
    public double MarkRestScale = 0.6;
    public double SidePadding = 24;
    public double CompactSidePadding = 16;

    /// <summary>Raised after a refresh (rows re-measure: the geometry may have changed).</summary>
    public event Action? Changed;

    /// <summary>Bumped on every refresh, so rows rebuild cached text.</summary>
    public int Version;

    public void Refresh(Control owner)
    {
        IsDark = CodeReviewUi.IsDark(owner);
        Surface = ArcTokens.GetColor(owner, ArcDesignTokenKind.Surface);
        Foreground = ArcTokens.GetColor(owner, ArcDesignTokenKind.Foreground);
        TextSecondary = ArcTokens.GetColor(owner, ArcDesignTokenKind.TextSecondary);
        TextMuted = ArcTokens.GetColor(owner, ArcDesignTokenKind.TextMuted);
        HunkFill = ArcTokens.GetColor(owner, ArcDesignTokenKind.SurfaceMuted);
        Success = ArcTokens.GetColor(owner, ArcDesignTokenKind.Success);
        Danger = ArcTokens.GetColor(owner, ArcDesignTokenKind.Danger);
        Accent = ArcTokens.GetColor(owner, ArcDesignTokenKind.Accent);
        AccentForeground = ArcTokens.GetColor(owner, ArcDesignTokenKind.AccentForeground);
        AddedLine = CodeReviewUi.Color(owner, CodeReviewTokenKind.AddedLine);
        AddedGutter = CodeReviewUi.Color(owner, CodeReviewTokenKind.AddedGutter);
        DeletedLine = CodeReviewUi.Color(owner, CodeReviewTokenKind.DeletedLine);
        DeletedGutter = CodeReviewUi.Color(owner, CodeReviewTokenKind.DeletedGutter);
        FreshLine = CodeReviewUi.Color(owner, CodeReviewTokenKind.FreshLine);
        FreshGutter = CodeReviewUi.Color(owner, CodeReviewTokenKind.FreshGutter);
        ActiveLine = CodeReviewUi.Color(owner, CodeReviewTokenKind.ActiveLine);
        ActiveGutter = CodeReviewUi.Color(owner, CodeReviewTokenKind.ActiveGutter);
        FocusInset = CodeReviewUi.Color(owner, CodeReviewTokenKind.FocusInset);
        CodeFontSize = CodeReviewUi.Number(owner, CodeReviewTokenKind.SmallFontSize, CodeFontSize);
        LineHeight = CodeReviewUi.Number(owner, CodeReviewTokenKind.LineHeight, LineHeight);
        HunkHeight = CodeReviewUi.Number(owner, CodeReviewTokenKind.HunkHeight, HunkHeight);
        NumberWidth = CodeReviewUi.Number(owner, CodeReviewTokenKind.NumberWidth, NumberWidth);
        GutterPadding = CodeReviewUi.Thickness(owner, CodeReviewTokenKind.GutterPadding, GutterPadding);
        CompactGutterPadding = CodeReviewUi.Thickness(owner, CodeReviewTokenKind.CompactGutterPadding, CompactGutterPadding);
        SignWidth = CodeReviewUi.Number(owner, CodeReviewTokenKind.SignWidth, SignWidth);
        SignInset = CodeReviewUi.Number(owner, CodeReviewTokenKind.SignInset, SignInset);
        MarkSize = CodeReviewUi.Number(owner, CodeReviewTokenKind.MarkSize, MarkSize);
        MarkRestScale = CodeReviewUi.Number(owner, CodeReviewTokenKind.MarkRestScale, MarkRestScale);
        SidePadding = CodeReviewUi.Number(owner, CodeReviewTokenKind.SidePadding, SidePadding);
        CompactSidePadding = CodeReviewUi.Number(owner, CodeReviewTokenKind.CompactSidePadding, CompactSidePadding);
        if (owner.TryFindResource(ArcDesignTokenKind.MonoFontFamily, owner.ActualThemeVariant, out var font) && font is FontFamily family)
        {
            Mono = family;
        }
        MonoFace = new Typeface(Mono);
        MonoItalic = new Typeface(Mono, FontStyle.Italic);
        using (var probe = new Avalonia.Media.TextFormatting.TextLayout("0000000000", MonoFace, CodeFontSize, null))
        {
            CharWidth = probe.WidthIncludingTrailingWhitespace / 10;
        }
        Version++;
        Changed?.Invoke();
        _brushes.Clear();
    }

    private readonly Dictionary<Color, IImmutableSolidColorBrush> _brushes = new();

    /// <summary>A shared immutable brush for <paramref name="color"/>.</summary>
    public IImmutableSolidColorBrush Brush(Color color)
    {
        if (!_brushes.TryGetValue(color, out var brush))
        {
            if (_brushes.Count > 256)
            {
                _brushes.Clear();
            }
            brush = new ImmutableSolidColorBrush(color);
            _brushes[color] = brush;
        }
        return brush;
    }
}
