// Ported from vscode (src/vs/editor/common/languages/supports/indentRules.ts, commit b57d5c9e), MIT license.
// Differences from upstream: IndentConsts is a [Flags] enum; a .NET Regex keeps no lastIndex, so resetGlobalRegex has
// no equivalent.

namespace TKCode.Arc.Editor.Languages;

/// <summary>The bits of <see cref="IndentRulesSupport.GetIndentMetadata"/>.</summary>
[Flags]
internal enum IndentConsts
{
    IncreaseMask = 0b00000001,
    DecreaseMask = 0b00000010,
    IndentNextlineMask = 0b00000100,
    UnindentMask = 0b00001000,
}

/// <summary>Tests lines against a language's indentation rules.</summary>
internal sealed class IndentRulesSupport
{
    private readonly IndentationRule _indentationRules;

    public IndentRulesSupport(IndentationRule indentationRules)
    {
        _indentationRules = indentationRules;
    }

    public bool ShouldIncrease(string text)
    {
        if (_indentationRules.IncreaseIndentPattern is { } increaseIndentPattern && increaseIndentPattern.IsMatch(text))
        {
            return true;
        }
        // if (this._indentationRules.indentNextLinePattern && this._indentationRules.indentNextLinePattern.test(text)) {
        // 	return true;
        // }
        return false;
    }

    public bool ShouldDecrease(string text)
    {
        if (_indentationRules.DecreaseIndentPattern is { } decreaseIndentPattern && decreaseIndentPattern.IsMatch(text))
        {
            return true;
        }
        return false;
    }

    public bool ShouldIndentNextLine(string text)
    {
        if (_indentationRules.IndentNextLinePattern is { } indentNextLinePattern && indentNextLinePattern.IsMatch(text))
        {
            return true;
        }

        return false;
    }

    public bool ShouldIgnore(string text)
    {
        // the text matches `unIndentedLinePattern`
        if (_indentationRules.UnIndentedLinePattern is { } unIndentedLinePattern && unIndentedLinePattern.IsMatch(text))
        {
            return true;
        }

        return false;
    }

    public IndentConsts GetIndentMetadata(string text)
    {
        IndentConsts ret = 0;
        if (ShouldIncrease(text))
        {
            ret |= IndentConsts.IncreaseMask;
        }
        if (ShouldDecrease(text))
        {
            ret |= IndentConsts.DecreaseMask;
        }
        if (ShouldIndentNextLine(text))
        {
            ret |= IndentConsts.IndentNextlineMask;
        }
        if (ShouldIgnore(text))
        {
            ret |= IndentConsts.UnindentMask;
        }
        return ret;
    }
}
