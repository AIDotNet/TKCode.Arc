namespace TKCode.Arc;

/// <summary>Toggle items: which blocks a collapsed toggle hides, and which toggle hides a block.</summary>
internal static class RichTextEditorToggles
{
    /// <summary>The blocks nested under a collapsed toggle (true at their indices), or null when nothing is folded away.</summary>
    public static bool[]? HiddenMask(IReadOnlyList<RichTextEditorBlock> blocks)
    {
        bool[]? hidden = null;
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (!block.IsToggle || !block.IsCollapsed || hidden is not null && hidden[i])
            {
                continue;
            }
            var end = i + 1;
            while (end < blocks.Count && blocks[end].IsList && blocks[end].Level > block.Level)
            {
                (hidden ??= new bool[blocks.Count])[end] = true;
                end++;
            }
            i = end - 1;
        }
        return hidden;
    }

    /// <summary>The shown, collapsed toggle that hides the block at <paramref name="index"/> (its outermost folded ancestor), or -1.</summary>
    public static int Owner(IReadOnlyList<RichTextEditorBlock> blocks, bool[]? hidden, int index)
    {
        if (hidden is null || index < 0 || index >= blocks.Count || !hidden[index])
        {
            return -1;
        }
        for (var j = index - 1; j >= 0; j--)
        {
            if (!hidden[j])
            {
                return blocks[j].IsToggle ? j : -1;
            }
        }
        return -1;
    }
}
