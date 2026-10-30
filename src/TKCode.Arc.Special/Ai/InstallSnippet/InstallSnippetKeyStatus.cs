using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The status line under step 2's code: a glyph and one line of text that crossfades (muted at rest, a success check once a
/// key is in, an alert in the danger color when the key generator failed, so an error is never color alone).
/// </summary>
[TemplatePart(GlyphPart, typeof(ArcGlyph))]
[TemplatePart(TextPart, typeof(ArcContentSwap))]
[PseudoClasses(ReadyPseudoClass, FailedPseudoClass)]
internal sealed class InstallSnippetKeyStatus : TemplatedControl
{
    internal const string GlyphPart = "PART_Glyph";
    internal const string TextPart = "PART_Text";
    internal const string ReadyPseudoClass = ":ready";
    internal const string FailedPseudoClass = ":failed";

    private ArcGlyph? _glyph;
    private ArcContentSwap? _text;
    private Geometry? _shownGlyph;
    private string? _shownText;

    /// <summary>The line on screen.</summary>
    internal string? Text => _shownText;

    /// <summary>Shows a glyph and a line; <paramref name="state"/> picks the colors.</summary>
    public void Show(Geometry glyph, string text, InstallSnippetKeyState state)
    {
        _shownGlyph = glyph;
        _shownText = text;
        PseudoClasses.Set(ReadyPseudoClass, state == InstallSnippetKeyState.Ready);
        PseudoClasses.Set(FailedPseudoClass, state == InstallSnippetKeyState.Failed);
        Apply();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _glyph = e.NameScope.Find<ArcGlyph>(GlyphPart);
        _text = e.NameScope.Find<ArcContentSwap>(TextPart);
        Apply();
    }

    private void Apply()
    {
        if (_glyph is not null)
        {
            _glyph.Data = _shownGlyph;
        }
        if (_text is not null && !Equals(_text.Content, _shownText))
        {
            _text.Content = _shownText;
        }
    }
}
