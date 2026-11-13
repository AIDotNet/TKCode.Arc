namespace TKCode.Arc;

/// <summary>One cover of a <see cref="CoverFlow"/>: the picture and the two lines shown under it while it faces forward.</summary>
public sealed class CoverFlowItem
{
    /// <summary>The cover picture.</summary>
    public ImageSource? Source { get; set; }

    /// <summary>The first line under the centred cover, such as an album or a book title.</summary>
    public string? Title { get; set; }

    /// <summary>The second line, such as the artist or the author.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Any object of yours this cover stands for, handed back by <see cref="CoverFlow.ItemInvoked"/>.</summary>
    public object? Tag { get; set; }
}
