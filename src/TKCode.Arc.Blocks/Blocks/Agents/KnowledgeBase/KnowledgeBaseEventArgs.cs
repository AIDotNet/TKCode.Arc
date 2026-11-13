using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="KnowledgeBase.DocumentAdded"/> and <see cref="KnowledgeBase.DocumentDeleted"/>.</summary>
public sealed class KnowledgeBaseDocumentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public KnowledgeBaseDocumentEventArgs(RoutedEvent routedEvent, object source, KnowledgeDocument document)
        : base(routedEvent, source)
    {
        Document = document;
    }

    /// <summary>The document that was added or deleted.</summary>
    public KnowledgeDocument Document { get; }
}

/// <summary>Arguments of <see cref="KnowledgeBase.IndexingCompleted"/>.</summary>
public sealed class KnowledgeBaseIndexedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public KnowledgeBaseIndexedEventArgs(RoutedEvent routedEvent, object source, KnowledgeDocument document, int chunkCount, bool isReindex)
        : base(routedEvent, source)
    {
        Document = document;
        ChunkCount = chunkCount;
        IsReindex = isReindex;
    }

    /// <summary>The document that is now ready.</summary>
    public KnowledgeDocument Document { get; }

    /// <summary>How many chunks it has in the index.</summary>
    public int ChunkCount { get; }

    /// <summary>True when the run was a re-index of a document that was ready or failed before.</summary>
    public bool IsReindex { get; }
}

/// <summary>Arguments of <see cref="KnowledgeBase.IndexingFailed"/>.</summary>
public sealed class KnowledgeBaseIndexFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public KnowledgeBaseIndexFailedEventArgs(RoutedEvent routedEvent, object source, KnowledgeDocument document, Exception? exception, string message)
        : base(routedEvent, source)
    {
        Document = document;
        Exception = exception;
        Message = message;
    }

    /// <summary>The document that failed.</summary>
    public KnowledgeDocument Document { get; }

    /// <summary>What the pipeline threw; null when the built-in pipeline found no text.</summary>
    public Exception? Exception { get; }

    /// <summary>The message shown in the row.</summary>
    public string Message { get; }
}

/// <summary>Arguments of <see cref="KnowledgeBase.PassageInvoked"/>.</summary>
public sealed class KnowledgeBasePassageEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public KnowledgeBasePassageEventArgs(RoutedEvent routedEvent, object source, KnowledgePassage passage)
        : base(routedEvent, source)
    {
        Passage = passage;
    }

    /// <summary>The passage that was pressed.</summary>
    public KnowledgePassage Passage { get; }
}
