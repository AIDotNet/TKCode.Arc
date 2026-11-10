---
title: Knowledge base
summary: The documents an assistant can search: add files by picker or drop, watch each one upload, parse and embed with its chunk count, re-index or delete it, and preview which passages a question finds with the matching words marked.
---

<Demo name="Preview" />

## When to use

A knowledge base is the page where people manage the documents an assistant reads before it answers: handbooks,
policies, product guides, notes. People need to know three things there: what is in it, whether each file has made it
into the index, and what the assistant will actually find for a given question. `KnowledgeBase` answers all three on one
card. The header counts documents, chunks and size and says how many files are still indexing. Files come in from Add
files or a drop on the card and join a queue. Each row then moves through Uploading, Parsing and Embedding with a
progress ring, and ends Ready with its chunk count and date, or Failed with the reason and a Retry button. Re-index and
Delete (with an inline confirmation) sit at the end of each row. Beside the list, a search preview ranks the indexed
passages for a question, shows a relevance score for each and marks the words that matched. A press on a result takes
you to its document.

- **The sources page of an assistant or a support bot**: people upload help-center articles and see when each one can
  be found.
- **Debugging answers**: when the assistant answered wrongly, the search preview shows which passages it read.
- **An admin view of a retrieval pipeline**: the stages, failures and chunk counts come from your own indexer through
  `IndexHandler`.

## When not to use

- **General file storage**: there are no folders, sharing or versions. Use `FileUpload` with your own list.
- **One attachment in a chat**: use `AiComposer`, which uploads files into the message.
- **Editing the documents**: the card lists and indexes files; it does not open or change them.
- **A full search page**: the preview shows a few passages to check retrieval. Use `SearchResults` for a search people browse.

## Usage

`KnowledgeBase` derives from Avalonia's `TemplatedControl`. A document is a `KnowledgeDocument`, an `AvaloniaObject`
with an id, a name, a kind, a size, a status, a progress, a chunk count, a date, an error and, for the built-in
pipeline, its text and chunks. With no documents of your own the card shows an invented library (`KnowledgeBaseSamples`).
Set `IsSampleData="False"` for a real, empty knowledge base.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:KnowledgeBase AssistantName="Pip" DocumentsSource="{Binding Documents}" />
</UserControl>
```

### Anatomy

- **Header**: the title, a subtitle that names the assistant, the totals ("6 documents · 13 chunks · 2.6 MB"), the
  indexing note with a spinner, and Add files.
- **Drop zone**: a strip that lights up while files are dragged over the card, with the accepted types and the size
  limit, or a note about skipped files.
- **Document list**: one row per file. The kind icon, the name over its facts (type, size, chunks, date) or its error,
  the stage with a ring, then Retry, Re-index and Delete.
- **Search preview**: a search box, a status line, and up to `MaxPassages` results. Each shows its rank, the document
  and chunk, a score with a bar, and the passage with the matching words marked.

### Adding documents

Add files opens the platform's file picker, filtered by `AcceptedExtensions`. You can also drop files anywhere on the
card. Files of another type or larger than `MaxFileSize` (20 MB) are skipped, and the drop zone says so. The others go to
the top of the list as Queued. Up to `MaxConcurrentIndexing` (2) documents index at the same time; the rest wait in
arrival order. In code, call `AddDocument`, `AddFiles` (name, size and text) or `AddFilesAsync` (storage files).

<Demo name="Empty" />

### Indexing

Set `IndexHandler` to send a document to your backend. Report the stage and how far the run is through the progress
argument; the row's label and ring follow it. Return the chunks when the document is in the index, or throw to fail it
with the exception's message.

```csharp
knowledge.IndexHandler = async (document, progress, cancellation) =>
{
    progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Uploading, 0.1));
    var id = await api.UploadAsync(document.SourceFile, cancellation);
    progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Embedding, 0.5));
    return await api.WaitForChunksAsync(id, cancellation);
};
```

Without a handler the card runs a simulated pipeline: a picked file uploads in four steps, its text is split into chunks
of about 480 characters (paragraphs kept together), and embedding takes a step per chunk. The text of plain-text files
(.txt, .md, .csv, .json, .html) is read when they are added; other files need your handler, and the simulation fails
them with "No readable text". `KnowledgeBase.ChunkText` splits text the same way, so you can chunk on a server too.

<Demo name="Pipeline" />

### Re-index, retry and delete

Re-index sends a ready document through the pipeline again; Retry does the same for a failed one. Delete swaps the row
for a question ("Delete Returns and refunds.pdf? Its 5 chunks leave the index.") with Cancel and Delete. Set
`DeleteHandler` to remove the document from your storage first; if it throws, the document stays and the message is
announced. `Reindex`, `ReindexAll` and `RemoveAsync` do the same from code.

### Search preview

Type a question and, after a short pause, the preview ranks the chunks of the ready documents. The built-in search uses
BM25 over words, ignores common words such as "the" and "how", and also finds plural and longer forms ("refund" finds
"refunds" and "refunded"). Scores run from 0 to 1. Set `SearchHandler` to ask your vector index instead; the preview
still marks the query's words in what you return. Enter searches at once and Escape clears the box. A press on a result
scrolls the list to its document, tints its row and raises `PassageInvoked`.

`SearchText` is two-way, and `Passages` holds the results.

### Read only

`IsReadOnly` hides Add files, the drop zone and the row buttons, and keeps the list and the search preview. Use it for
people who may see the sources but not change them.

<Demo name="ReadOnly" />

### Wording

Every word comes from `Labels` (`KnowledgeBaseLabels`). Texts with counts or names are functions, so translations can
order the words their own way. `Title` on the control wins over the labels.

### Content guidelines

- **Keep the file names people uploaded**: they recognize their own names, and the search results show them.
- **Explain failures in a sentence**: "The workbook is password protected" tells people what to fix; "Error 500" does not.
- **Preview with real questions**: type what customers ask, not keywords, to see what the assistant will read.

## API reference

<!-- api: KnowledgeBase -->

<!-- api: KnowledgeDocument -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `DocumentAdded` | A document joined the list | `Document` |
| `DocumentDeleted` | A document left the list | `Document` |
| `IndexingCompleted` | A document is ready | `Document`, `ChunkCount`, `IsReindex` |
| `IndexingFailed` | Indexing a document failed | `Document`, `Exception`, `Message` |
| `PassageInvoked` | A search result was pressed | `Passage` |

## Accessibility

- The card is a region. The title is a level 2 heading; "Documents" and "Search preview" are level 3 headings.
- Every row has one name that reads its file, stage, facts and error ("Battery care.txt, Embedding 64%, TXT · 6 KB").
  The Re-index, Delete and Retry buttons are named with the file ("Delete Shipping FAQ.md") and have tooltips.
- A polite live region announces added files, each document that becomes ready or fails, deletions, re-indexing and
  the number of passages found.
- Delete never acts at once: the question takes the focus on Cancel, and focus goes back to Delete after Cancel or to the
  next row after a deletion.
- Status never relies on color alone: Ready has a check, Failed an alert icon and its reason, and running stages a name
  and a percentage.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Add files, then each row's buttons, the search box and the results |
| `ArrowUp` / `ArrowDown` on a row button | The same button in the previous or next row |
| `Home` / `End` on a row button | The first or last row |
| `Escape` in the delete question | Cancels and returns to Delete |
| `Enter` in the search box | Searches at once |
| `Escape` in the search box | Clears it |
| `ArrowDown` in the search box | Moves to the first result; `ArrowUp` / `ArrowDown` move between results |
| `Enter` or `Space` on a result | Opens its document in the list |

## Motion

- The progress ring follows each reported step smoothly instead of jumping.
- The drop zone's tint and border change in 160 ms when files are dragged over the card.
- Rows and results change their tint in 160 ms on hover. The delete question fades in over 240 ms.
- Status lines in the drop zone and the search preview crossfade, rising 2 px as they come in.
- Reduced motion keeps the fades short and draws the ring at its value at once. Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Progress ring | Follows the value | Jumps | Jumps |
| Drop zone and row tint | 160 ms | Immediate | Immediate |
| Delete question | Fades in 240 ms | Immediate | Immediate |
| Status lines | Crossfade, rise 2 px | 120 ms crossfade | Immediate |

## Responsive behavior

- `KnowledgeBase` changes at its own width, not the window's. From 760 px (`WideWidth`, `:wide`) the search preview is
  a 340 px column beside the list; below, it follows the list.
- Below 480 px (`CompactWidth`, `:compact`) the paddings tighten, rows hide their dates and the stage label (the ring
  and icon stay).
- The card is capped at 960 px and centred. The list scrolls inside the card above 420 px and is virtualized, so
  thousands of documents stay fast.
- Names and facts end with an ellipsis; errors and passages wrap.

## Related

- `FileUpload` for uploading files without an index.
- `AiChat` for the chat that uses these documents.
- `AgentMemory` for what an assistant remembers about a person.

| Component | Choose it when |
|---|---|
| `FileUpload` | Files are stored, not indexed for an assistant |
| `SearchResults` | People browse search results |
| `AgentMemory` | The assistant keeps short facts about a person |
| `AiAnswer` | You show an answer with its sources |
