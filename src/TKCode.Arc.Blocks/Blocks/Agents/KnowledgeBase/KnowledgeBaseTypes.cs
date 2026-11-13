using Avalonia;

namespace TKCode.Arc;

/// <summary>Where a document of a <see cref="KnowledgeBase"/> stands on its way into the index.</summary>
public enum KnowledgeDocumentStatus
{
    /// <summary>Waiting for a free indexing slot.</summary>
    Queued,

    /// <summary>The file is being sent to storage.</summary>
    Uploading,

    /// <summary>The text is being read out of the file and split into chunks.</summary>
    Parsing,

    /// <summary>The chunks are being turned into vectors and written to the index.</summary>
    Embedding,

    /// <summary>Indexed: the assistant can find its passages.</summary>
    Ready,

    /// <summary>Indexing stopped with an error (<see cref="KnowledgeDocument.Error"/>); Retry starts again.</summary>
    Failed
}

/// <summary>The kind of a document, which picks its icon and its short type label.</summary>
public enum KnowledgeDocumentKind
{
    /// <summary>Plain text (.txt).</summary>
    Text,

    /// <summary>Markdown (.md).</summary>
    Markdown,

    /// <summary>A PDF file.</summary>
    Pdf,

    /// <summary>A word-processor document (.docx).</summary>
    Document,

    /// <summary>A table (.csv, .xlsx).</summary>
    Spreadsheet,

    /// <summary>Source code or structured data (.json, .cs, .py).</summary>
    Code,

    /// <summary>A web page (.html, or a crawled address).</summary>
    Web
}

/// <summary>One passage of a document as it sits in the index: the document, its position and its text.</summary>
/// <param name="DocumentId">The <see cref="KnowledgeDocument.Id"/> it belongs to.</param>
/// <param name="Index">Its position in the document, from 0.</param>
/// <param name="Text">The passage text.</param>
public sealed record KnowledgeChunk(string DocumentId, int Index, string Text);

/// <summary>A search result: a passage of a document and how well it matches the query.</summary>
/// <param name="Document">The document the passage comes from.</param>
/// <param name="ChunkIndex">The passage's position in the document, from 0.</param>
/// <param name="Text">The passage text (the preview cuts a window around the first match).</param>
/// <param name="Score">Relevance from 0 to 1; the best passage of the built-in search scores at most 1.</param>
public sealed record KnowledgePassage(KnowledgeDocument Document, int ChunkIndex, string Text, double Score);

/// <summary>Progress reported by an indexing pipeline: the stage it is in and how far the whole run is (0–1).</summary>
/// <param name="Status">The stage: <see cref="KnowledgeDocumentStatus.Uploading"/>, <see cref="KnowledgeDocumentStatus.Parsing"/> or <see cref="KnowledgeDocumentStatus.Embedding"/>.</param>
/// <param name="Fraction">How far the whole run is, from 0 to 1.</param>
public readonly record struct KnowledgeIndexProgress(KnowledgeDocumentStatus Status, double Fraction);

/// <summary>A range of characters to mark in a passage preview.</summary>
/// <param name="Start">Index of the first character.</param>
/// <param name="Length">Number of characters.</param>
public readonly record struct KnowledgeTextRange(int Start, int Length);

/// <summary>
/// A document a <see cref="KnowledgeBase"/> lists and indexes. Data only: an <see cref="AvaloniaObject"/>, so a change
/// made by the block (status, progress, chunk count) or by your code reaches the row on screen.
/// </summary>
public class KnowledgeDocument : AvaloniaObject
{
    /// <summary>Stable id, unique within the knowledge base.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<KnowledgeDocument, string>(nameof(Id), string.Empty);

    /// <summary>The file name shown in the list, such as "Returns policy.pdf".</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<KnowledgeDocument, string>(nameof(Name), string.Empty);

    /// <summary>The kind, which picks the icon and the type label; <see cref="KnowledgeBase.KindOf"/> guesses it from a file name.</summary>
    public static readonly StyledProperty<KnowledgeDocumentKind> KindProperty =
        AvaloniaProperty.Register<KnowledgeDocument, KnowledgeDocumentKind>(nameof(Kind));

    /// <summary>The file size in bytes.</summary>
    public static readonly StyledProperty<long> SizeBytesProperty =
        AvaloniaProperty.Register<KnowledgeDocument, long>(nameof(SizeBytes));

    /// <summary>Where the document stands; the block moves it along while it indexes.</summary>
    public static readonly StyledProperty<KnowledgeDocumentStatus> StatusProperty =
        AvaloniaProperty.Register<KnowledgeDocument, KnowledgeDocumentStatus>(nameof(Status), KnowledgeDocumentStatus.Queued);

    /// <summary>How far indexing is, from 0 to 1 (the ring in the row).</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<KnowledgeDocument, double>(nameof(Progress),
            coerce: static (_, v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1));

    /// <summary>The number of chunks in the index for this document.</summary>
    public static readonly StyledProperty<int> ChunkCountProperty =
        AvaloniaProperty.Register<KnowledgeDocument, int>(nameof(ChunkCount));

    /// <summary>When the document was last indexed; null shows no date.</summary>
    public static readonly StyledProperty<DateTimeOffset?> UpdatedAtProperty =
        AvaloniaProperty.Register<KnowledgeDocument, DateTimeOffset?>(nameof(UpdatedAt));

    /// <summary>Why indexing failed, shown under the name while <see cref="Status"/> is <see cref="KnowledgeDocumentStatus.Failed"/>.</summary>
    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<KnowledgeDocument, string?>(nameof(Error));

    /// <summary>
    /// The document's text, read by the built-in pipeline (split into chunks) and kept for re-indexing. Files of a
    /// plain-text type get it when they are added; null means only your <see cref="KnowledgeBase.IndexHandler"/> can read it.
    /// </summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<KnowledgeDocument, string?>(nameof(Text));

    /// <summary>The indexed chunks the built-in search reads; set by the pipeline, or by you for documents indexed elsewhere.</summary>
    public static readonly StyledProperty<IReadOnlyList<KnowledgeChunk>?> ChunksProperty =
        AvaloniaProperty.Register<KnowledgeDocument, IReadOnlyList<KnowledgeChunk>?>(nameof(Chunks));

    /// <summary>The file the document was added from, when it came from a picker or a drop; null otherwise.</summary>
    public object? SourceFile { get; set; }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public KnowledgeDocumentKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="SizeBytesProperty"/>
    public long SizeBytes { get => GetValue(SizeBytesProperty); set => SetValue(SizeBytesProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public KnowledgeDocumentStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <inheritdoc cref="ChunkCountProperty"/>
    public int ChunkCount { get => GetValue(ChunkCountProperty); set => SetValue(ChunkCountProperty, value); }

    /// <inheritdoc cref="UpdatedAtProperty"/>
    public DateTimeOffset? UpdatedAt { get => GetValue(UpdatedAtProperty); set => SetValue(UpdatedAtProperty, value); }

    /// <inheritdoc cref="ErrorProperty"/>
    public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="ChunksProperty"/>
    public IReadOnlyList<KnowledgeChunk>? Chunks { get => GetValue(ChunksProperty); set => SetValue(ChunksProperty, value); }

    /// <summary>Whether indexing is under way (uploading, parsing or embedding).</summary>
    public bool IsBusy => Status is KnowledgeDocumentStatus.Uploading or KnowledgeDocumentStatus.Parsing or KnowledgeDocumentStatus.Embedding;
}
