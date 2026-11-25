namespace TKCode.Arc.Editor;

/// <summary>The line ending a new or empty text buffer uses (Monaco's <c>DefaultEndOfLine</c>).</summary>
public enum DefaultEndOfLine
{
    /// <summary>Line feed (<c>\n</c>).</summary>
    LF = 1,

    /// <summary>Carriage return and line feed (<c>\r\n</c>).</summary>
    CRLF = 2
}

/// <summary>Which line ending a read of the text returns (Monaco's <c>EndOfLinePreference</c>).</summary>
public enum EndOfLinePreference
{
    /// <summary>The line ending of the text buffer.</summary>
    TextDefined = 0,

    /// <summary>Line feed (<c>\n</c>).</summary>
    LF = 1,

    /// <summary>Carriage return and line feed (<c>\r\n</c>).</summary>
    CRLF = 2
}

/// <summary>The line ending of a text buffer (Monaco's <c>EndOfLineSequence</c>).</summary>
public enum EndOfLineSequence
{
    /// <summary>Line feed (<c>\n</c>).</summary>
    LF = 0,

    /// <summary>Carriage return and line feed (<c>\r\n</c>).</summary>
    CRLF = 1
}
