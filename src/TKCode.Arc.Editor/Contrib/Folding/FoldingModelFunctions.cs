// Ported from vscode (src/vs/editor/contrib/folding/browser/foldingModel.ts: the module functions toggleCollapseState,
// setCollapseStateLevelsDown, setCollapseStateLevelsUp, setCollapseStateUp, setCollapseStateAtLevel,
// setCollapseStateForRest, setCollapseStateForMatchingLines, setCollapseStateForType, getParentFoldLine,
// getPreviousFoldLine, getNextFoldLine; commit b57d5c9e), MIT license.
// Differences:
//  - static methods of FoldingModelFunctions; Number.MAX_VALUE levels are int.MaxValue;
//  - getNextFoldLine stops at the last region (upstream reads one past the end of its Uint32Array there, which yields
//    line 0 and parent 0, so a region whose parent is 0 could jump to line 0).

using System.Text.RegularExpressions;

namespace TKCode.Arc.Editor.Contrib.Folding;

/// <summary>Operations on a <see cref="FoldingModel"/> (upstream's foldingModel.ts functions).</summary>
internal static class FoldingModelFunctions
{
    /// <summary>
    /// Toggles the regions at the given lines: <paramref name="levels"/> 1 only the region at each line, more also the
    /// regions inside it, to that depth, whose state differs from the new one.
    /// </summary>
    public static void ToggleCollapseState(FoldingModel foldingModel, int levels, IReadOnlyList<int> lineNumbers)
    {
        var toToggle = new List<FoldingRegion>();
        foreach (var lineNumber in lineNumbers)
        {
            if (foldingModel.GetRegionAtLine(lineNumber) is { } region)
            {
                var doCollapse = !region.IsCollapsed;
                toToggle.Add(region);
                if (levels > 1)
                {
                    var regionsInside = foldingModel.GetRegionsInside(region, (r, level) => r.IsCollapsed != doCollapse && level < levels);
                    toToggle.AddRange(regionsInside);
                }
            }
        }

        foldingModel.ToggleCollapseState(toToggle);
    }

    /// <summary>
    /// Collapses or expands the regions at the given lines including their children, <paramref name="levels"/> deep
    /// (int.MaxValue for all); every region of the model when no lines are given.
    /// </summary>
    public static void SetCollapseStateLevelsDown(FoldingModel foldingModel, bool doCollapse, int levels = int.MaxValue, IReadOnlyList<int>? lineNumbers = null)
    {
        var toToggle = new List<FoldingRegion>();
        if (lineNumbers is { Count: > 0 })
        {
            foreach (var lineNumber in lineNumbers)
            {
                if (foldingModel.GetRegionAtLine(lineNumber) is { } region)
                {
                    if (region.IsCollapsed != doCollapse)
                    {
                        toToggle.Add(region);
                    }

                    if (levels > 1)
                    {
                        var regionsInside = foldingModel.GetRegionsInside(region, (r, level) => r.IsCollapsed != doCollapse && level < levels);
                        toToggle.AddRange(regionsInside);
                    }
                }
            }
        }
        else
        {
            var regionsInside = foldingModel.GetRegionsInside(null, (r, level) => r.IsCollapsed != doCollapse && level < levels);
            toToggle.AddRange(regionsInside);
        }

        foldingModel.ToggleCollapseState(toToggle);
    }

    /// <summary>Collapses or expands the regions at the given lines including their parents, <paramref name="levels"/> up.</summary>
    public static void SetCollapseStateLevelsUp(FoldingModel foldingModel, bool doCollapse, int levels, IReadOnlyList<int> lineNumbers)
    {
        var toToggle = new List<FoldingRegion>();
        foreach (var lineNumber in lineNumbers)
        {
            var regions = foldingModel.GetAllRegionsAtLine(lineNumber, (region, level) => region.IsCollapsed != doCollapse && level <= levels);
            toToggle.AddRange(regions);
        }

        foldingModel.ToggleCollapseState(toToggle);
    }

    /// <summary>
    /// Collapses or expands the region at each line; when the innermost one is already in that state, its first parent
    /// that is not.
    /// </summary>
    public static void SetCollapseStateUp(FoldingModel foldingModel, bool doCollapse, IReadOnlyList<int> lineNumbers)
    {
        var toToggle = new List<FoldingRegion>();
        foreach (var lineNumber in lineNumbers)
        {
            var regions = foldingModel.GetAllRegionsAtLine(lineNumber, (region, _) => region.IsCollapsed != doCollapse);
            if (regions.Count > 0)
            {
                toToggle.Add(regions[0]);
            }
        }

        foldingModel.ToggleCollapseState(toToggle);
    }

    /// <summary>Folds or unfolds all regions of level <paramref name="foldLevel"/> (1 is the top), except those that contain a blocked line.</summary>
    public static void SetCollapseStateAtLevel(FoldingModel foldingModel, int foldLevel, bool doCollapse, IReadOnlyList<int> blockedLineNumbers)
    {
        var toToggle = foldingModel.GetRegionsInside(null, (region, level) =>
            level == foldLevel && region.IsCollapsed != doCollapse && !blockedLineNumbers.Any(line => region.ContainsLine(line)));
        foldingModel.ToggleCollapseState(toToggle);
    }

    /// <summary>Folds or unfolds all regions, except those that contain or are contained by the region of a blocked line.</summary>
    public static void SetCollapseStateForRest(FoldingModel foldingModel, bool doCollapse, IReadOnlyList<int> blockedLineNumbers)
    {
        var filteredRegions = new List<FoldingRegion>();
        foreach (var lineNumber in blockedLineNumbers)
        {
            var regions = foldingModel.GetAllRegionsAtLine(lineNumber);
            if (regions.Count > 0)
            {
                filteredRegions.Add(regions[0]);
            }
        }

        var toToggle = foldingModel.GetRegionsInside(null, region =>
            filteredRegions.All(filteredRegion => !filteredRegion.ContainedBy(region) && !region.ContainedBy(filteredRegion)) && region.IsCollapsed != doCollapse);
        foldingModel.ToggleCollapseState(toToggle);
    }

    /// <summary>Folds or unfolds all regions whose start line matches <paramref name="regExp"/>.</summary>
    public static void SetCollapseStateForMatchingLines(FoldingModel foldingModel, Regex regExp, bool doCollapse)
    {
        var editorModel = foldingModel.TextModel;
        var regions = foldingModel.Regions;
        var toToggle = new List<FoldingRegion>();
        for (var i = regions.Length - 1; i >= 0; i--)
        {
            if (doCollapse != regions.IsCollapsed(i))
            {
                var startLineNumber = regions.GetStartLineNumber(i);
                if (regExp.IsMatch(editorModel.GetLineContent(startLineNumber)))
                {
                    toToggle.Add(regions.ToRegion(i));
                }
            }
        }

        foldingModel.ToggleCollapseState(toToggle);
    }

    /// <summary>Folds or unfolds all regions of a type (<c>comment</c>, <c>imports</c>, <c>region</c>).</summary>
    public static void SetCollapseStateForType(FoldingModel foldingModel, string type, bool doCollapse)
    {
        var regions = foldingModel.Regions;
        var toToggle = new List<FoldingRegion>();
        for (var i = regions.Length - 1; i >= 0; i--)
        {
            if (doCollapse != regions.IsCollapsed(i) && type == regions.GetType(i))
            {
                toToggle.Add(regions.ToRegion(i));
            }
        }

        foldingModel.ToggleCollapseState(toToggle);
    }

    /// <summary>The start line of the fold that contains <paramref name="lineNumber"/>, or of its parent when the line starts it; null when there is none.</summary>
    public static int? GetParentFoldLine(int lineNumber, FoldingModel foldingModel)
    {
        int? startLineNumber = null;
        if (foldingModel.GetRegionAtLine(lineNumber) is { } foldingRegion)
        {
            startLineNumber = foldingRegion.StartLineNumber;
            // If current line is not the start of the current fold, go to top line of current fold. If not, go to parent fold
            if (lineNumber == startLineNumber)
            {
                var parentFoldingIdx = foldingRegion.ParentIndex;
                startLineNumber = parentFoldingIdx != -1 ? foldingModel.Regions.GetStartLineNumber(parentFoldingIdx) : null;
            }
        }

        return startLineNumber;
    }

    /// <summary>The start line of the previous fold at the level of <paramref name="lineNumber"/>, or null.</summary>
    public static int? GetPreviousFoldLine(int lineNumber, FoldingModel foldingModel)
    {
        var foldingRegion = foldingModel.GetRegionAtLine(lineNumber);
        // If on the folding range start line, go to previous sibling.
        if (foldingRegion is not null && foldingRegion.StartLineNumber == lineNumber)
        {
            // Find min line number to stay within parent.
            var expectedParentIndex = foldingRegion.ParentIndex;
            var minLineNumber = 0;
            if (expectedParentIndex != -1)
            {
                minLineNumber = foldingModel.Regions.GetStartLineNumber(foldingRegion.ParentIndex);
            }

            // Find fold at same level.
            while (foldingRegion is not null)
            {
                if (foldingRegion.RegionIndex > 0)
                {
                    foldingRegion = foldingModel.Regions.ToRegion(foldingRegion.RegionIndex - 1);

                    // Keep at same level.
                    if (foldingRegion.StartLineNumber <= minLineNumber)
                    {
                        return null;
                    }

                    if (foldingRegion.ParentIndex == expectedParentIndex)
                    {
                        return foldingRegion.StartLineNumber;
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        else
        {
            // Go to last fold that's before the current line.
            if (foldingModel.Regions.Length > 0)
            {
                foldingRegion = foldingModel.Regions.ToRegion(foldingModel.Regions.Length - 1);
                while (foldingRegion is not null)
                {
                    // Found fold before current line.
                    if (foldingRegion.StartLineNumber < lineNumber)
                    {
                        return foldingRegion.StartLineNumber;
                    }

                    foldingRegion = foldingRegion.RegionIndex > 0 ? foldingModel.Regions.ToRegion(foldingRegion.RegionIndex - 1) : null;
                }
            }
        }

        return null;
    }

    /// <summary>The start line of the next fold at the level of <paramref name="lineNumber"/>, or null.</summary>
    public static int? GetNextFoldLine(int lineNumber, FoldingModel foldingModel)
    {
        var foldingRegion = foldingModel.GetRegionAtLine(lineNumber);
        var regions = foldingModel.Regions;
        // If on the folding range start line, go to next sibling.
        if (foldingRegion is not null && foldingRegion.StartLineNumber == lineNumber)
        {
            // Find max line number to stay within parent.
            var expectedParentIndex = foldingRegion.ParentIndex;
            int maxLineNumber;
            if (expectedParentIndex != -1)
            {
                maxLineNumber = regions.GetEndLineNumber(foldingRegion.ParentIndex);
            }
            else if (regions.Length == 0)
            {
                return null;
            }
            else
            {
                maxLineNumber = regions.GetEndLineNumber(regions.Length - 1);
            }

            // Find fold at same level.
            while (foldingRegion is not null)
            {
                if (foldingRegion.RegionIndex + 1 < regions.Length)
                {
                    foldingRegion = regions.ToRegion(foldingRegion.RegionIndex + 1);

                    // Keep at same level.
                    if (foldingRegion.StartLineNumber >= maxLineNumber)
                    {
                        return null;
                    }

                    if (foldingRegion.ParentIndex == expectedParentIndex)
                    {
                        return foldingRegion.StartLineNumber;
                    }
                }
                else
                {
                    return null;
                }
            }
        }
        else
        {
            // Go to first fold that's after the current line.
            if (regions.Length > 0)
            {
                foldingRegion = regions.ToRegion(0);
                while (foldingRegion is not null)
                {
                    // Found fold after current line.
                    if (foldingRegion.StartLineNumber > lineNumber)
                    {
                        return foldingRegion.StartLineNumber;
                    }

                    foldingRegion = foldingRegion.RegionIndex + 1 < regions.Length ? regions.ToRegion(foldingRegion.RegionIndex + 1) : null;
                }
            }
        }

        return null;
    }
}
