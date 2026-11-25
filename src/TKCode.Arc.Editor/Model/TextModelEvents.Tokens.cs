// Ported from vscode (src/vs/editor/common/textModelEvents.ts: IModelLanguageConfigurationChangedEvent and
// IModelTokensChangedEvent, commit b57d5c9e), MIT license.
// Differences from upstream: the two interfaces are classes, and a range of IModelTokensChangedEvent is the record
// struct ModelTokensChangedRange. The text model does not raise them yet; the view model's outgoing events wrap them.

namespace TKCode.Arc.Editor.Model;

/// <summary>The language configuration of a model's language changed; it carries nothing (as upstream).</summary>
internal sealed class ModelLanguageConfigurationChangedEvent
{
}

/// <summary>A range of model lines whose tokens changed, both ends inclusive.</summary>
internal readonly record struct ModelTokensChangedRange(int FromLineNumber, int ToLineNumber);

/// <summary>The tokens of a model changed.</summary>
internal sealed class ModelTokensChangedEvent
{
    public ModelTokensChangedEvent(bool semanticTokensApplied, IReadOnlyList<ModelTokensChangedRange> ranges)
    {
        SemanticTokensApplied = semanticTokensApplied;
        Ranges = ranges;
    }

    public bool SemanticTokensApplied { get; }

    public IReadOnlyList<ModelTokensChangedRange> Ranges { get; }
}
