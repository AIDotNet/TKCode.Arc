// The diff's decorations (upstream's DiffEditorDecorations in
// src/vs/editor/browser/widget/diffEditor/components/diffEditorDecorations.ts and the decoration options of
// registrations.contribution.ts, commit b57d5c9e; MIT license): line backgrounds of changed lines with + / − signs and
// gutter backgrounds, character highlights of the inner changes, whole-line highlights of inserted / removed lines.
// Differences: the CSS classes are decoration styles of each editor (CodeEditorDecorationStyles) with the brushes of
// the control theme; the char highlight is the success / danger brush at CharHighlightOpacity over the line
// background; moved blocks are drawn by the strip between the editors (DiffEditorMovedBlocksStrip) instead of block
// decorations; showEmptyDecorations and useTrueInlineDiffRendering are not ported.

using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc;

public sealed partial class DiffEditor
{
    internal const string LineInsertClassName = "diff-line-insert";
    internal const string LineDeleteClassName = "diff-line-delete";
    internal const string CharInsertClassName = "diff-char-insert";
    internal const string CharDeleteClassName = "diff-char-delete";
    internal const string InsertSignClassName = "diff-insert-sign";
    internal const string DeleteSignClassName = "diff-delete-sign";
    internal const string GutterInsertClassName = "diff-gutter-insert";
    internal const string GutterDeleteClassName = "diff-gutter-delete";

    /// <summary>A plus sign in a 16 × 16 box (upstream's codicon diff-insert).</summary>
    internal static readonly Geometry PlusGlyph = Geometry.Parse("M4,7.4 H12 V8.6 H4 Z M7.4,4 H8.6 V12 H7.4 Z");

    /// <summary>A minus sign in a 16 × 16 box (upstream's codicon diff-remove).</summary>
    internal static readonly Geometry MinusGlyph = Geometry.Parse("M4,7.4 H12 V8.6 H4 Z");

    private static readonly ModelDecorationOptions s_lineInsert = new("line-insert")
    {
        ClassName = LineInsertClassName,
        IsWholeLine = true,
        LinesDecorationsClassName = InsertSignClassName,
        MarginClassName = GutterInsertClassName,
    };

    private static readonly ModelDecorationOptions s_lineDelete = new("line-delete")
    {
        ClassName = LineDeleteClassName,
        IsWholeLine = true,
        LinesDecorationsClassName = DeleteSignClassName,
        MarginClassName = GutterDeleteClassName,
    };

    private static readonly ModelDecorationOptions s_charInsert = new("char-insert") { ClassName = CharInsertClassName, ShouldFillLineOnLineBreak = true };

    private static readonly ModelDecorationOptions s_wholeLineInsert = new("char-insert") { ClassName = CharInsertClassName, IsWholeLine = true };

    private static readonly ModelDecorationOptions s_charDelete = new("char-delete") { ClassName = CharDeleteClassName, ShouldFillLineOnLineBreak = true };

    private static readonly ModelDecorationOptions s_wholeLineDelete = new("char-delete") { ClassName = CharDeleteClassName, IsWholeLine = true };

    private readonly List<IDisposable> _styleRegistrations = [];
    private string[] _originalDecorationIds = [];
    private string[] _modifiedDecorationIds = [];

    /// <summary>The background of changed characters: the success / danger brush at the token's opacity.</summary>
    internal static IBrush? WithOpacity(IBrush? brush, double opacity) => brush switch
    {
        null => null,
        ISolidColorBrush solid => new ImmutableSolidColorBrush(ArcColor.WithOpacity(solid.Color, solid.Opacity * opacity)),
        _ => brush,
    };

    private void RegisterDecorationStyles()
    {
        DisposeDecorationStyles();
        foreach (var editor in new[] { _originalEditor, _modifiedEditor })
        {
            if (editor is null)
            {
                continue;
            }

            var styles = editor.DecorationStyles;
            Register(styles, LineInsertClassName, InsertedLineBrush, b => new CodeEditorDecorationStyle { Background = DecorationBrush.From(b) });
            Register(styles, LineDeleteClassName, RemovedLineBrush, b => new CodeEditorDecorationStyle { Background = DecorationBrush.From(b) });
            Register(styles, GutterInsertClassName, InsertedLineBrush, b => new CodeEditorDecorationStyle { MarginBackground = DecorationBrush.From(b) });
            Register(styles, GutterDeleteClassName, RemovedLineBrush, b => new CodeEditorDecorationStyle { MarginBackground = DecorationBrush.From(b) });
            Register(styles, CharInsertClassName, WithOpacity(InsertedTextBrush, CharHighlightOpacity), b => new CodeEditorDecorationStyle { Background = DecorationBrush.From(b) });
            Register(styles, CharDeleteClassName, WithOpacity(RemovedTextBrush, CharHighlightOpacity), b => new CodeEditorDecorationStyle { Background = DecorationBrush.From(b) });
            Register(styles, InsertSignClassName, InsertedTextBrush, b => new CodeEditorDecorationStyle { Glyph = PlusGlyph, GlyphBrush = DecorationBrush.From(b) });
            Register(styles, DeleteSignClassName, RemovedTextBrush, b => new CodeEditorDecorationStyle { Glyph = MinusGlyph, GlyphBrush = DecorationBrush.From(b) });
        }

        void Register(CodeEditorDecorationStyles styles, string className, IBrush? brush, Func<IBrush, CodeEditorDecorationStyle> create)
        {
            if (brush is not null)
            {
                _styleRegistrations.Add(styles.Register(className, create(brush)));
            }
        }
    }

    private void DisposeDecorationStyles()
    {
        foreach (var registration in _styleRegistrations)
        {
            registration.Dispose();
        }

        _styleRegistrations.Clear();
    }

    /// <summary>The decorations of the diff for each side (upstream's DiffEditorDecorations._decorations).</summary>
    internal static (List<ModelDeltaDecoration> Original, List<ModelDeltaDecoration> Modified) ComputeDecorations(DocumentDiff diff)
    {
        var originalDecorations = new List<ModelDeltaDecoration>();
        var modifiedDecorations = new List<ModelDeltaDecoration>();
        foreach (var m in diff.Changes)
        {
            if (m.Original.ToInclusiveRange() is { } originalRange)
            {
                originalDecorations.Add(new ModelDeltaDecoration(originalRange, s_lineDelete));
            }

            if (m.Modified.ToInclusiveRange() is { } modifiedRange)
            {
                modifiedDecorations.Add(new ModelDeltaDecoration(modifiedRange, s_lineInsert));
            }

            if (m.Modified.IsEmpty || m.Original.IsEmpty)
            {
                if (m.Original.ToInclusiveRange() is { } o)
                {
                    originalDecorations.Add(new ModelDeltaDecoration(o, s_wholeLineDelete));
                }

                if (m.Modified.ToInclusiveRange() is { } mo)
                {
                    modifiedDecorations.Add(new ModelDeltaDecoration(mo, s_wholeLineInsert));
                }
            }
            else
            {
                foreach (var i in m.InnerChanges ?? [])
                {
                    // Don't show empty markers outside the line range
                    if (m.Original.Contains(i.OriginalRange.StartLineNumber) && !i.OriginalRange.IsEmpty)
                    {
                        originalDecorations.Add(new ModelDeltaDecoration(i.OriginalRange, s_charDelete));
                    }

                    if (m.Modified.Contains(i.ModifiedRange.StartLineNumber) && !i.ModifiedRange.IsEmpty)
                    {
                        modifiedDecorations.Add(new ModelDeltaDecoration(i.ModifiedRange, s_charInsert));
                    }
                }
            }
        }

        return (originalDecorations, modifiedDecorations);
    }

    private void ApplyDecorations()
    {
        if (_originalEditor is not { } original || _modifiedEditor is not { } modified || _diff is not { } diff)
        {
            return;
        }

        var (originalDecorations, modifiedDecorations) = ComputeDecorations(diff);
        _originalDecorationIds = original.Model.DeltaDecorations(_originalDecorationIds, originalDecorations);
        _modifiedDecorationIds = modified.Model.DeltaDecorations(_modifiedDecorationIds, modifiedDecorations);
    }

    private void ClearDecorations()
    {
        if (_originalEditor is { } original && _originalDecorationIds.Length > 0)
        {
            original.Model.DeltaDecorations(_originalDecorationIds, []);
        }

        if (_modifiedEditor is { } modified && _modifiedDecorationIds.Length > 0)
        {
            modified.Model.DeltaDecorations(_modifiedDecorationIds, []);
        }

        _originalDecorationIds = [];
        _modifiedDecorationIds = [];
    }

    /// <summary>The decorations of the diff in the original editor (tests).</summary>
    internal IReadOnlyList<string> OriginalDecorationIds => _originalDecorationIds;

    /// <summary>The decorations of the diff in the modified editor (tests).</summary>
    internal IReadOnlyList<string> ModifiedDecorationIds => _modifiedDecorationIds;
}
