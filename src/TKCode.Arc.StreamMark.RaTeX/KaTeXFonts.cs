using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace StreamMark.RaTeX;

/// <summary>解析后的字形：字体 + 字形索引 + 用于 GlyphRun 的字符。</summary>
internal readonly record struct ResolvedGlyph(GlyphTypeface Typeface, ushort GlyphIndex, string Text);

/// <summary>
/// KaTeX 字体加载与字符 → 字形映射。嵌入的 TTF 通过一个私有的 <see cref="FontCollectionBase"/>
/// （不注册到 <see cref="FontManager"/>）各加载一次；需要 Avalonia 平台已初始化（首次绘制时在界面线程调用）。
/// KaTeX 字体缺字（CJK、emoji 等，RaTeX 以 <c>CJK-Regular</c> / <c>CJK-Fallback</c> / <c>Emoji-Fallback</c> 报告）
/// 时回退到系统字体。
/// </summary>
internal static class KaTeXFonts
{
    private const string ResourcePrefix = "StreamMark.RaTeX.Fonts.KaTeX_";

    private static readonly object Gate = new();
    private static KaTeXFontCollection? _collection;
    private static readonly Dictionary<string, GlyphTypeface?> Typefaces = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<int, ResolvedGlyph?> SystemFallback = new();

    /// <summary>按 RaTeX 字体名与码点解析字形；找不到时返回 null（跳过绘制，避免豆腐块）。</summary>
    public static ResolvedGlyph? Resolve(string? font, uint charCode)
    {
        var codePoint = (int)charCode;
        var fontId = font ?? "Main-Regular";
        if (!IsSystemFallbackFont(fontId))
        {
            var typeface = GetKaTeX(fontId) ?? GetKaTeX("Main-Regular");
            if (typeface is not null)
            {
                var mapped = (int)MathAlphanumeric.KaTeXGlyphCodePoint(fontId, charCode);
                if (TryGlyph(typeface, mapped, out var resolved))
                {
                    return resolved;
                }
            }
            if (fontId != "Main-Regular" && GetKaTeX("Main-Regular") is { } main && TryGlyph(main, codePoint, out var fromMain))
            {
                return fromMain;
            }
        }
        return ResolveSystem(codePoint);
    }

    private static bool IsSystemFallbackFont(string fontId) =>
        fontId is "CJK-Regular" or "CJK-Fallback" or "Emoji-Fallback";

    private static bool TryGlyph(GlyphTypeface typeface, int codePoint, out ResolvedGlyph glyph)
    {
        if (typeface.CharacterToGlyphMap.TryGetGlyph(codePoint, out var index) && index != 0)
        {
            glyph = new ResolvedGlyph(typeface, index, char.ConvertFromUtf32(codePoint));
            return true;
        }
        glyph = default;
        return false;
    }

    private static ResolvedGlyph? ResolveSystem(int codePoint)
    {
        if (codePoint is < 0 or > 0x10FFFF or (>= 0xD800 and <= 0xDFFF))
        {
            return null;
        }
        return SystemFallback.GetOrAdd(codePoint, static cp =>
        {
            var fontManager = FontManager.Current;
            if (fontManager.TryGetGlyphTypeface(new Typeface(FontFamily.Default), out var defaultFace) && TryGlyph(defaultFace, cp, out var glyph))
            {
                return glyph;
            }
            if (fontManager.TryMatchCharacter(cp, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, null, null, out var matched)
                && TryGlyph(matched.GlyphTypeface, cp, out glyph))
            {
                return glyph;
            }
            return null;
        });
    }

    /// <summary>取 KaTeX 字体（如 <c>Main-Regular</c> → KaTeX_Main-Regular.ttf）；未知名称返回 null。</summary>
    public static GlyphTypeface? GetKaTeX(string fontId)
    {
        lock (Gate)
        {
            if (Typefaces.TryGetValue(fontId, out var cached))
            {
                return cached;
            }
            GlyphTypeface? typeface = null;
            using (var stream = typeof(KaTeXFonts).Assembly.GetManifestResourceStream(ResourcePrefix + fontId + ".ttf"))
            {
                if (stream is not null)
                {
                    // Skia 的 SKTypeface.FromStream 可能延迟读取，复制到独立的内存流并保持存活
                    var memory = new MemoryStream(checked((int)stream.Length));
                    stream.CopyTo(memory);
                    memory.Position = 0;
                    _collection ??= new KaTeXFontCollection();
                    // 返回 false 也可能只是集合内键冲突，字体本身已创建成功
                    _collection.TryAddGlyphTypeface(memory, out typeface);
                }
            }
            Typefaces[fontId] = typeface;
            return typeface;
        }
    }

    private sealed class KaTeXFontCollection : FontCollectionBase
    {
        private static readonly Uri CollectionKey = new("fonts:StreamMark.RaTeX.KaTeX");

        public override Uri Key => CollectionKey;
    }
}

/// <summary>
/// Unicode 数学字母数字符号（U+1D400–U+1D7FF）在 KaTeX TTF 中位于 ASCII 槽位，
/// 与 ratex-font <c>katex_ttf_glyph_char</c> 一致。
/// </summary>
internal static class MathAlphanumeric
{
    private static readonly (uint Base, string Font)[] Letters =
    [
        (0x1D400, "Main-Bold"),
        (0x1D434, "Math-Italic"),
        (0x1D468, "Math-BoldItalic"),
        (0x1D504, "Fraktur-Regular"),
        (0x1D56C, "Fraktur-Bold"),
        (0x1D5A0, "SansSerif-Regular"),
        (0x1D5D4, "SansSerif-Bold"),
        (0x1D608, "SansSerif-Italic"),
        (0x1D670, "Typewriter-Regular"),
    ];

    private static readonly (uint Base, string Font)[] Digits =
    [
        (0x1D7CE, "Main-Bold"),
        (0x1D7E2, "SansSerif-Regular"),
        (0x1D7EC, "SansSerif-Bold"),
        (0x1D7F6, "Typewriter-Regular"),
    ];

    public static uint KaTeXGlyphCodePoint(string fontId, uint cp)
    {
        if (cp < 0x1D400 || cp > 0x1D7FF)
        {
            return cp;
        }
        return Map(cp) is { } m && m.Font == fontId ? m.Metric : cp;
    }

    private static (string Font, uint Metric)? Map(uint cp)
    {
        foreach (var (b, font) in Letters)
        {
            if (cp >= b && cp < b + 52)
            {
                var i = cp - b;
                return (font, i < 26 ? 0x41 + i : 0x61 + (i - 26));
            }
        }
        if (cp >= 0x1D538 && cp < 0x1D538 + 26)
        {
            return ("AMS-Regular", 0x41 + (cp - 0x1D538));
        }
        if (cp >= 0x1D49C && cp < 0x1D49C + 26)
        {
            return ("Script-Regular", 0x41 + (cp - 0x1D49C));
        }
        if (cp == 0x1D55C)
        {
            return ("AMS-Regular", 'k');
        }
        foreach (var (b, font) in Digits)
        {
            if (cp >= b && cp < b + 10)
            {
                return (font, 0x30 + (cp - b));
            }
        }
        return null;
    }
}
