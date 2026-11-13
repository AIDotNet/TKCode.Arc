using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The documents an assistant can search and quote, on one card: totals and an Add files button over a drop zone, the
/// document list with each file's stage (queued, uploading, parsing, embedding, ready or failed), a progress ring, its
/// chunk count and date, Re-index and Delete (with an inline confirmation), Retry on a failed row, and a search preview
/// that ranks the indexed passages for a question and marks the words that matched.
/// <para>
/// Without an <see cref="IndexHandler"/> the card runs a simulated pipeline that splits each document's
/// <see cref="KnowledgeDocument.Text"/> into chunks; without a <see cref="SearchHandler"/> it searches those chunks with
/// BM25. Set both to connect your own storage, parser, embeddings and vector search. With no documents of your own and
/// <see cref="IsSampleData"/> left on, the card shows the invented Kestrel Lane library (<see cref="KnowledgeBaseSamples"/>).
/// </para>
/// <code language="xml">
/// &lt;arc:KnowledgeBase AssistantName="Pip" DocumentsSource="{Binding Documents}" /&gt;
/// </code>
/// </summary>
[TemplatePart(AddPart, typeof(Button))]
[TemplatePart(ListPart, typeof(ItemsControl))]
[TemplatePart(SearchPart, typeof(TextBox))]
[TemplatePart(PassagesPart, typeof(StackPanel))]
[PseudoClasses(WidePseudoClass, CompactPseudoClass, DraggingPseudoClass, EmptyPseudoClass, IndexingPseudoClass, ReadOnlyPseudoClass,
    SearchActivePseudoClass)]
public partial class KnowledgeBase : TemplatedControl
{
    public const string CardPart = "PART_Card";
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string TotalsPart = "PART_Totals";
    public const string IndexingPart = "PART_Indexing";
    public const string IndexingTextPart = "PART_IndexingText";
    public const string AddPart = "PART_Add";
    public const string AddTextPart = "PART_AddText";
    public const string DropZonePart = "PART_DropZone";
    public const string DropTextPart = "PART_DropText";
    public const string DropNotePart = "PART_DropNote";
    public const string ListHeadingPart = "PART_ListHeading";
    public const string ListCountPart = "PART_ListCount";
    public const string ListPart = "PART_List";
    public const string EmptyPart = "PART_Empty";
    public const string EmptyTitlePart = "PART_EmptyTitle";
    public const string EmptyTextPart = "PART_EmptyText";
    public const string SearchHeadingPart = "PART_SearchHeading";
    public const string SearchHintPart = "PART_SearchHint";
    public const string SearchPart = "PART_Search";
    public const string SearchStatusPart = "PART_SearchStatus";
    public const string SearchSpinnerPart = "PART_SearchSpinner";
    public const string PassagesPart = "PART_Passages";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the card is at least <c>WideWidth</c> (760) wide: the search preview sits beside the list.</summary>
    public const string WidePseudoClass = ":wide";

    /// <summary>Set while the card is narrower than <c>CompactWidth</c> (480): paddings tighten and rows hide their dates.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while files are dragged over the card.</summary>
    public const string DraggingPseudoClass = ":dragging";

    /// <summary>Set while there are no documents.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while a document is queued or being indexed.</summary>
    public const string IndexingPseudoClass = ":indexing";

    /// <summary>Set while <see cref="IsReadOnly"/> is true.</summary>
    public const string ReadOnlyPseudoClass = ":readonly";

    /// <summary>Set while the search box holds a query.</summary>
    public const string SearchActivePseudoClass = ":search-active";

    #region Properties

    /// <summary>The documents when you give them as a list to follow; the card adds and removes on that list. Wins over <see cref="Documents"/>.</summary>
    public static readonly StyledProperty<IList<KnowledgeDocument>?> DocumentsSourceProperty =
        AvaloniaProperty.Register<KnowledgeBase, IList<KnowledgeDocument>?>(nameof(DocumentsSource));

    /// <summary>
    /// With no <see cref="DocumentsSource"/> and no <see cref="Documents"/> when the card is first shown, fill
    /// <see cref="Documents"/> with the invented samples (default true). Set it to false for a real, empty knowledge base.
    /// </summary>
    public static readonly StyledProperty<bool> IsSampleDataProperty =
        AvaloniaProperty.Register<KnowledgeBase, bool>(nameof(IsSampleData), true);

    /// <summary>The assistant's name, used in the subtitle ("Documents Pip can search and quote"); null keeps it general.</summary>
    public static readonly StyledProperty<string?> AssistantNameProperty =
        AvaloniaProperty.Register<KnowledgeBase, string?>(nameof(AssistantName));

    /// <summary>The title; null uses the title of <see cref="Labels"/> ("Knowledge base").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<KnowledgeBase, string?>(nameof(Title));

    /// <summary>The search preview's query (two-way). Typing waits a moment before it searches; setting it searches the same way.</summary>
    public static readonly StyledProperty<string?> SearchTextProperty =
        AvaloniaProperty.Register<KnowledgeBase, string?>(nameof(SearchText), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many passages the search preview shows at most (4).</summary>
    public static readonly StyledProperty<int> MaxPassagesProperty =
        AvaloniaProperty.Register<KnowledgeBase, int>(nameof(MaxPassages), 4, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>How many documents index at the same time (2); the rest wait as Queued.</summary>
    public static readonly StyledProperty<int> MaxConcurrentIndexingProperty =
        AvaloniaProperty.Register<KnowledgeBase, int>(nameof(MaxConcurrentIndexing), 2, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>The largest file the card accepts, in bytes (20 MB); larger files are skipped with a note.</summary>
    public static readonly StyledProperty<long> MaxFileSizeProperty =
        AvaloniaProperty.Register<KnowledgeBase, long>(nameof(MaxFileSize), 20L * 1024 * 1024);

    /// <summary>The file extensions the card accepts, with their dots (".pdf", ".md", ...); null or empty accepts any file.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> AcceptedExtensionsProperty =
        AvaloniaProperty.Register<KnowledgeBase, IReadOnlyList<string>?>(nameof(AcceptedExtensions),
            new[] { ".pdf", ".docx", ".md", ".txt", ".csv", ".json", ".html" });

    /// <summary>Hides Add files, the drop zone, Re-index, Delete and Retry: a card to look at and search only.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<KnowledgeBase, bool>(nameof(IsReadOnly));

    /// <summary>Culture of numbers, sizes and dates (default "en-US").</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<KnowledgeBase, string>(nameof(CultureName), "en-US");

    /// <summary>Every word the card shows or reads out; replace the object to translate or reword.</summary>
    public static readonly StyledProperty<KnowledgeBaseLabels> LabelsProperty =
        AvaloniaProperty.Register<KnowledgeBase, KnowledgeBaseLabels>(nameof(Labels), KnowledgeBaseLabels.Default);

    /// <summary>The passages the search preview shows, best first (empty without a query).</summary>
    public static readonly DirectProperty<KnowledgeBase, IReadOnlyList<KnowledgePassage>> PassagesProperty =
        AvaloniaProperty.RegisterDirect<KnowledgeBase, IReadOnlyList<KnowledgePassage>>(nameof(Passages), o => o.Passages);

    /// <summary>Whether a search is waiting or running.</summary>
    public static readonly DirectProperty<KnowledgeBase, bool> IsSearchingProperty =
        AvaloniaProperty.RegisterDirect<KnowledgeBase, bool>(nameof(IsSearching), o => o.IsSearching);

    /// <summary>The chunks in the index: the sum of <see cref="KnowledgeDocument.ChunkCount"/> over the ready documents.</summary>
    public static readonly DirectProperty<KnowledgeBase, int> TotalChunksProperty =
        AvaloniaProperty.RegisterDirect<KnowledgeBase, int>(nameof(TotalChunks), o => o.TotalChunks);

    /// <summary>The size of every document, in bytes.</summary>
    public static readonly DirectProperty<KnowledgeBase, long> TotalBytesProperty =
        AvaloniaProperty.RegisterDirect<KnowledgeBase, long>(nameof(TotalBytes), o => o.TotalBytes);

    /// <summary>How many documents are queued or being indexed.</summary>
    public static readonly DirectProperty<KnowledgeBase, int> PendingCountProperty =
        AvaloniaProperty.RegisterDirect<KnowledgeBase, int>(nameof(PendingCount), o => o.PendingCount);

    internal static readonly StyledProperty<double> WideWidthProperty =
        AvaloniaProperty.Register<KnowledgeBase, double>(nameof(WideWidth), 760);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<KnowledgeBase, double>(nameof(CompactWidth), 480);

    internal static readonly StyledProperty<TimeSpan> SearchDelayProperty =
        AvaloniaProperty.Register<KnowledgeBase, TimeSpan>(nameof(SearchDelay), TimeSpan.FromMilliseconds(180));

    internal static readonly StyledProperty<TimeSpan> PipelineStepProperty =
        AvaloniaProperty.Register<KnowledgeBase, TimeSpan>(nameof(PipelineStep), TimeSpan.FromMilliseconds(260));

    internal static readonly StyledProperty<int> PreviewLengthProperty =
        AvaloniaProperty.Register<KnowledgeBase, int>(nameof(PreviewLength), 240);

    /// <summary>Raised when a document joins the list (a picked or dropped file, or <see cref="AddDocument"/>).</summary>
    public static readonly RoutedEvent<KnowledgeBaseDocumentEventArgs> DocumentAddedEvent =
        RoutedEvent.Register<KnowledgeBase, KnowledgeBaseDocumentEventArgs>(nameof(DocumentAdded), RoutingStrategies.Bubble);

    /// <summary>Raised after a document left the list (confirmed in its row, or <see cref="RemoveAsync"/>).</summary>
    public static readonly RoutedEvent<KnowledgeBaseDocumentEventArgs> DocumentDeletedEvent =
        RoutedEvent.Register<KnowledgeBase, KnowledgeBaseDocumentEventArgs>(nameof(DocumentDeleted), RoutingStrategies.Bubble);

    /// <summary>Raised when a document is ready: its first indexing or a re-index finished.</summary>
    public static readonly RoutedEvent<KnowledgeBaseIndexedEventArgs> IndexingCompletedEvent =
        RoutedEvent.Register<KnowledgeBase, KnowledgeBaseIndexedEventArgs>(nameof(IndexingCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when indexing a document failed.</summary>
    public static readonly RoutedEvent<KnowledgeBaseIndexFailedEventArgs> IndexingFailedEvent =
        RoutedEvent.Register<KnowledgeBase, KnowledgeBaseIndexFailedEventArgs>(nameof(IndexingFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when a passage of the search preview is pressed; the card also scrolls to its document.</summary>
    public static readonly RoutedEvent<KnowledgeBasePassageEventArgs> PassageInvokedEvent =
        RoutedEvent.Register<KnowledgeBase, KnowledgeBasePassageEventArgs>(nameof(PassageInvoked), RoutingStrategies.Bubble);

    /// <summary>The documents, declared inline or added in code. <see cref="DocumentsSource"/> replaces them when set.</summary>
    [Content]
    public AvaloniaList<KnowledgeDocument> Documents { get; } = new();

    /// <inheritdoc cref="DocumentsSourceProperty"/>
    public IList<KnowledgeDocument>? DocumentsSource { get => GetValue(DocumentsSourceProperty); set => SetValue(DocumentsSourceProperty, value); }

    /// <inheritdoc cref="IsSampleDataProperty"/>
    public bool IsSampleData { get => GetValue(IsSampleDataProperty); set => SetValue(IsSampleDataProperty, value); }

    /// <inheritdoc cref="AssistantNameProperty"/>
    public string? AssistantName { get => GetValue(AssistantNameProperty); set => SetValue(AssistantNameProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string? SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="MaxPassagesProperty"/>
    public int MaxPassages { get => GetValue(MaxPassagesProperty); set => SetValue(MaxPassagesProperty, value); }

    /// <inheritdoc cref="MaxConcurrentIndexingProperty"/>
    public int MaxConcurrentIndexing { get => GetValue(MaxConcurrentIndexingProperty); set => SetValue(MaxConcurrentIndexingProperty, value); }

    /// <inheritdoc cref="MaxFileSizeProperty"/>
    public long MaxFileSize { get => GetValue(MaxFileSizeProperty); set => SetValue(MaxFileSizeProperty, value); }

    /// <inheritdoc cref="AcceptedExtensionsProperty"/>
    public IReadOnlyList<string>? AcceptedExtensions { get => GetValue(AcceptedExtensionsProperty); set => SetValue(AcceptedExtensionsProperty, value); }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public KnowledgeBaseLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PassagesProperty"/>
    public IReadOnlyList<KnowledgePassage> Passages { get => _passages; private set => SetAndRaise(PassagesProperty, ref _passages, value); }

    /// <inheritdoc cref="IsSearchingProperty"/>
    public bool IsSearching { get => _isSearching; private set => SetAndRaise(IsSearchingProperty, ref _isSearching, value); }

    /// <inheritdoc cref="TotalChunksProperty"/>
    public int TotalChunks { get => _totalChunks; private set => SetAndRaise(TotalChunksProperty, ref _totalChunks, value); }

    /// <inheritdoc cref="TotalBytesProperty"/>
    public long TotalBytes { get => _totalBytes; private set => SetAndRaise(TotalBytesProperty, ref _totalBytes, value); }

    /// <inheritdoc cref="PendingCountProperty"/>
    public int PendingCount { get => _pendingCount; private set => SetAndRaise(PendingCountProperty, ref _pendingCount, value); }

    internal double WideWidth { get => GetValue(WideWidthProperty); set => SetValue(WideWidthProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal TimeSpan SearchDelay { get => GetValue(SearchDelayProperty); set => SetValue(SearchDelayProperty, value); }
    internal TimeSpan PipelineStep { get => GetValue(PipelineStepProperty); set => SetValue(PipelineStepProperty, value); }
    internal int PreviewLength { get => GetValue(PreviewLengthProperty); set => SetValue(PreviewLengthProperty, value); }

    /// <summary>
    /// Indexes one document through your backend: upload it, parse it, embed its chunks, and return the chunks. Report
    /// the stage and how far the run is through the progress (the row's ring and label follow it); throw to fail the
    /// document with the exception's message. The token is cancelled when the document is deleted, re-indexed again or
    /// the card leaves the visual tree. Null (the default) runs the built-in simulated pipeline on
    /// <see cref="KnowledgeDocument.Text"/>.
    /// </summary>
    public Func<KnowledgeDocument, IProgress<KnowledgeIndexProgress>, CancellationToken, Task<IReadOnlyList<KnowledgeChunk>>>? IndexHandler { get; set; }

    /// <summary>
    /// Searches your index for the preview: the query, the most passages to return and a token cancelled when the query
    /// changes. Return them best first with scores from 0 to 1. Null (the default) ranks the chunks of the ready documents
    /// with BM25.
    /// </summary>
    public Func<string, int, CancellationToken, Task<IReadOnlyList<KnowledgePassage>>>? SearchHandler { get; set; }

    /// <summary>
    /// Deletes a document from your storage and index before it leaves the list; throw to keep it (the message is
    /// announced). Null removes it from the list only.
    /// </summary>
    public Func<KnowledgeDocument, CancellationToken, Task>? DeleteHandler { get; set; }

    /// <summary>The waits of the card (search delay, pipeline steps); tests replace it to step time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    /// <summary>The clock for <see cref="KnowledgeDocument.UpdatedAt"/>; tests replace it.</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    /// <inheritdoc cref="DocumentAddedEvent"/>
    public event EventHandler<KnowledgeBaseDocumentEventArgs>? DocumentAdded
    {
        add => AddHandler(DocumentAddedEvent, value);
        remove => RemoveHandler(DocumentAddedEvent, value);
    }

    /// <inheritdoc cref="DocumentDeletedEvent"/>
    public event EventHandler<KnowledgeBaseDocumentEventArgs>? DocumentDeleted
    {
        add => AddHandler(DocumentDeletedEvent, value);
        remove => RemoveHandler(DocumentDeletedEvent, value);
    }

    /// <inheritdoc cref="IndexingCompletedEvent"/>
    public event EventHandler<KnowledgeBaseIndexedEventArgs>? IndexingCompleted
    {
        add => AddHandler(IndexingCompletedEvent, value);
        remove => RemoveHandler(IndexingCompletedEvent, value);
    }

    /// <inheritdoc cref="IndexingFailedEvent"/>
    public event EventHandler<KnowledgeBaseIndexFailedEventArgs>? IndexingFailed
    {
        add => AddHandler(IndexingFailedEvent, value);
        remove => RemoveHandler(IndexingFailedEvent, value);
    }

    /// <inheritdoc cref="PassageInvokedEvent"/>
    public event EventHandler<KnowledgeBasePassageEventArgs>? PassageInvoked
    {
        add => AddHandler(PassageInvokedEvent, value);
        remove => RemoveHandler(PassageInvokedEvent, value);
    }

    #endregion

    /// <summary>The extensions accepted by default: PDF, Word, Markdown, text, CSV, JSON and HTML.</summary>
    public static IReadOnlyList<string> DefaultExtensions => AcceptedExtensionsProperty.GetDefaultValue(typeof(KnowledgeBase))!;

    /// <summary>Guesses a document's kind from its file name (".pdf" is a PDF, ".md" Markdown, ".csv" a spreadsheet, and so on).</summary>
    public static KnowledgeDocumentKind KindOf(string fileName) => KnowledgeBaseText.KindOf(fileName);

    /// <summary>Splits a text into chunks the way the built-in pipeline does (paragraphs kept together up to about 480 characters).</summary>
    public static IReadOnlyList<KnowledgeChunk> ChunkText(string documentId, string? text) => KnowledgeBaseText.Chunk(documentId, text);

    private IReadOnlyList<KnowledgePassage> _passages = Array.Empty<KnowledgePassage>();
    private bool _isSearching;
    private int _totalChunks;
    private long _totalBytes;
    private int _pendingCount;

    private CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }

    static KnowledgeBase()
    {
        FocusableProperty.OverrideDefaultValue<KnowledgeBase>(false);
    }

    /// <summary>Creates an empty card; it fills with the samples when first shown unless you give it documents or turn <see cref="IsSampleData"/> off.</summary>
    public KnowledgeBase()
    {
        Documents.CollectionChanged += OnInlineDocumentsChanged;
    }
}
