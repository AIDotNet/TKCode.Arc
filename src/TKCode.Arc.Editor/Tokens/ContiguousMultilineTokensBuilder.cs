// Ported from vscode (src/vs/editor/common/tokens/contiguousMultilineTokensBuilder.ts, commit b57d5c9e), MIT license.
// Differences from upstream: serialize and deserialize are left out (nothing crosses a process here); finalize is
// FinalizeTokens (a method named Finalize would hide object.Finalize).

namespace TKCode.Arc.Editor.Tokens;

/// <summary>Collects the tokens of lines, joining consecutive lines into one <see cref="ContiguousMultilineTokens"/>.</summary>
internal sealed class ContiguousMultilineTokensBuilder
{
    private readonly List<ContiguousMultilineTokens> _tokens = new();

    public void Add(int lineNumber, uint[] lineTokens)
    {
        if (_tokens.Count > 0)
        {
            var last = _tokens[^1];
            if (last.EndLineNumber + 1 == lineNumber)
            {
                // append
                last.AppendLineTokens(lineTokens);
                return;
            }
        }

        _tokens.Add(new ContiguousMultilineTokens(lineNumber, [lineTokens]));
    }

    public List<ContiguousMultilineTokens> FinalizeTokens() => _tokens;
}
