// Adaptation of vscode (src/vs/editor/common/model.ts: IModelDecoration, commit b57d5c9e), MIT license.
// Upstream hands out the interval tree's nodes themselves, whose range a later edit resets; here a decoration is a copy
// of the node at the time it was asked for, so that it stays what it was.

namespace TKCode.Arc.Editor.Model;

/// <summary>A decoration as a model reports it.</summary>
internal sealed record ModelDecoration(string Id, int OwnerId, TextRange Range, ModelDecorationOptions Options) : IModelDecoration;
