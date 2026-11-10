using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="KnowledgeBase"/> shows or reads out, with English defaults. Set a new object with the
/// properties you want to change to translate or reword; texts with numbers or names are functions so the word order
/// can change.
/// </summary>
public sealed class KnowledgeBaseLabels
{
    /// <summary>The English labels.</summary>
    public static KnowledgeBaseLabels Default { get; } = new();

    /// <summary>The card's title ("Knowledge base").</summary>
    public string Title { get; init; } = "Knowledge base";

    /// <summary>The line under the title; the argument is the assistant's name, or null.</summary>
    public Func<string?, string> Subtitle { get; init; } = static assistant => assistant is null
        ? "Documents the assistant can search and quote"
        : $"Documents {assistant} can search and quote";

    /// <summary>The totals line; the arguments are documents, chunks and the formatted size ("6 documents · 214 chunks · 3.1 MB").</summary>
    public Func<int, int, string, string> Totals { get; init; } = static (documents, chunks, size) =>
        string.Format(CultureInfo.InvariantCulture, "{0} {1} · {2:N0} {3} · {4}", documents, documents == 1 ? "document" : "documents",
            chunks, chunks == 1 ? "chunk" : "chunks", size);

    /// <summary>The indexing note beside the totals; the argument is the number of documents in the queue or under way.</summary>
    public Func<int, string> IndexingCount { get; init; } = static count => count == 1 ? "Indexing 1 document" : $"Indexing {count} documents";

    /// <summary>The add button ("Add files").</summary>
    public string AddFiles { get; init; } = "Add files";

    /// <summary>The drop zone's sentence.</summary>
    public string DropHint { get; init; } = "Drop files here to add them";

    /// <summary>The drop zone's sentence while files are dragged over the card.</summary>
    public string DropActive { get; init; } = "Release to add the files";

    /// <summary>The small print in the drop zone; the argument is the formatted size limit ("PDF, Markdown, text and more, up to 20 MB each").</summary>
    public Func<string, string> DropNote { get; init; } = static limit => $"PDF, Markdown, text and more, up to {limit} each";

    /// <summary>The note after files were refused; the arguments are the count and the first name.</summary>
    public Func<int, string, string> SkippedFiles { get; init; } = static (count, name) =>
        count == 1 ? $"Skipped {name}: type not supported or too large" : $"Skipped {count} files: type not supported or too large";

    /// <summary>The heading of the list ("Documents").</summary>
    public string DocumentsHeading { get; init; } = "Documents";

    /// <summary>The empty list's title.</summary>
    public string EmptyTitle { get; init; } = "No documents yet";

    /// <summary>The empty list's sentence.</summary>
    public string EmptyText { get; init; } = "Add handbooks, policies or notes. The assistant answers from what you add here.";

    /// <summary>The name of a stage, shown in a row ("Queued", "Uploading", "Parsing", "Embedding", "Ready", "Failed").</summary>
    public Func<KnowledgeDocumentStatus, string> StatusName { get; init; } = static status => status switch
    {
        KnowledgeDocumentStatus.Queued => "Queued",
        KnowledgeDocumentStatus.Uploading => "Uploading",
        KnowledgeDocumentStatus.Parsing => "Parsing",
        KnowledgeDocumentStatus.Embedding => "Embedding",
        KnowledgeDocumentStatus.Ready => "Ready",
        _ => "Failed"
    };

    /// <summary>The short type label of a kind ("PDF", "MD", "TXT").</summary>
    public Func<KnowledgeDocumentKind, string> KindName { get; init; } = static kind => kind switch
    {
        KnowledgeDocumentKind.Pdf => "PDF",
        KnowledgeDocumentKind.Markdown => "MD",
        KnowledgeDocumentKind.Document => "DOC",
        KnowledgeDocumentKind.Spreadsheet => "CSV",
        KnowledgeDocumentKind.Code => "CODE",
        KnowledgeDocumentKind.Web => "WEB",
        _ => "TXT"
    };

    /// <summary>A chunk count in a row ("128 chunks").</summary>
    public Func<int, string> Chunks { get; init; } = static count => count == 1 ? "1 chunk" : string.Format(CultureInfo.InvariantCulture, "{0:N0} chunks", count);

    /// <summary>The date in a row; the argument is the date already formatted short ("Updated Nov 3").</summary>
    public Func<string, string> Updated { get; init; } = static date => $"Updated {date}";

    /// <summary>The Re-index button's accessible name and tooltip; the argument is the document name.</summary>
    public Func<string, string> Reindex { get; init; } = static name => $"Re-index {name}";

    /// <summary>The Delete button's accessible name and tooltip; the argument is the document name.</summary>
    public Func<string, string> Delete { get; init; } = static name => $"Delete {name}";

    /// <summary>The Retry button of a failed row ("Retry").</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The question of the delete confirmation; the arguments are the name and the chunk count.</summary>
    public Func<string, int, string> ConfirmDelete { get; init; } = static (name, chunks) => chunks > 0
        ? $"Delete {name}? Its {chunks:N0} chunks leave the index."
        : $"Delete {name}?";

    /// <summary>The confirming button ("Delete").</summary>
    public string ConfirmDeleteButton { get; init; } = "Delete";

    /// <summary>The cancelling button ("Cancel").</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The error shown when the built-in pipeline has no text to read; the argument is the type label.</summary>
    public Func<string, string> NoText { get; init; } = static kind => $"No readable text in this {kind} file";

    /// <summary>The search pane's heading ("Search preview").</summary>
    public string SearchHeading { get; init; } = "Search preview";

    /// <summary>The sentence under the search heading.</summary>
    public string SearchHint { get; init; } = "See which passages the assistant would read for a question.";

    /// <summary>The search box's placeholder.</summary>
    public string SearchPlaceholder { get; init; } = "Try a question people ask";

    /// <summary>The search box's accessible name.</summary>
    public string SearchName { get; init; } = "Search the knowledge base";

    /// <summary>The line before a search ("Type to preview matching passages").</summary>
    public string SearchIdle { get; init; } = "Type to preview matching passages";

    /// <summary>The line while a search runs.</summary>
    public string Searching { get; init; } = "Searching…";

    /// <summary>The line after a search; the argument is the number of passages.</summary>
    public Func<int, string> PassageCount { get; init; } = static count => count == 1 ? "1 matching passage" : $"{count} matching passages";

    /// <summary>The line when nothing matched; the argument is the query.</summary>
    public Func<string, string> NoPassages { get; init; } = static query => $"No passages match “{query}”";

    /// <summary>The line when the search failed; the argument is the message.</summary>
    public Func<string, string> SearchFailed { get; init; } = static message => $"Search failed: {message}";

    /// <summary>A passage's place in its document; the argument is its number from 1 ("Chunk 12").</summary>
    public Func<int, string> ChunkNumber { get; init; } = static number => $"Chunk {number}";

    /// <summary>A passage's accessible name; the arguments are the rank, the document, the score text and the passage.</summary>
    public Func<int, string, string, string, string> PassageName { get; init; } = static (rank, document, score, text) =>
        $"Result {rank}, {document}, relevance {score}: {text}";

    /// <summary>Said when files were added; the argument is the count.</summary>
    public Func<int, string> AddedAnnouncement { get; init; } = static count => count == 1 ? "Added 1 file to the queue" : $"Added {count} files to the queue";

    /// <summary>Said when a document is ready; the arguments are the name and the chunk count.</summary>
    public Func<string, int, string> ReadyAnnouncement { get; init; } = static (name, chunks) => $"{name} is ready, {chunks:N0} chunks";

    /// <summary>Said when indexing failed; the arguments are the name and the message.</summary>
    public Func<string, string, string> FailedAnnouncement { get; init; } = static (name, message) => $"{name} failed: {message}";

    /// <summary>Said when a document was deleted; the argument is the name.</summary>
    public Func<string, string> DeletedAnnouncement { get; init; } = static name => $"Deleted {name}";

    /// <summary>Said when re-indexing starts; the argument is the name.</summary>
    public Func<string, string> ReindexAnnouncement { get; init; } = static name => $"Re-indexing {name}";
}
