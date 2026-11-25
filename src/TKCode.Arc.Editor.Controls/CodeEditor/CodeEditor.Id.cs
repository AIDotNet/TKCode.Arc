namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    /// <summary>
    /// The editor's id (upstream's getId()): the owner id of the decorations it adds to its model, which only this
    /// editor's view draws.
    /// </summary>
    internal int EditorId => _editorId;
}
