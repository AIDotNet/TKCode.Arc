namespace TKCode.Arc.Editor;

/// <summary>
/// A view of a text buffer's content at one moment (Monaco's <c>ITextSnapshot</c>). It stays valid and unchanged while
/// the buffer is edited, and may be read from another thread.
/// </summary>
public interface ITextSnapshot
{
    /// <summary>Returns the next chunk of text, or null when the whole content has been read.</summary>
    string? Read();
}
