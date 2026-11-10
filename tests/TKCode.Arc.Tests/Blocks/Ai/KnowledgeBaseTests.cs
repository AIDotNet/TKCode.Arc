using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="KnowledgeBase"/> on the invented Kestrel Lane samples: the list and totals, the queue and the built-in
/// pipeline (with a manual clock), failures and retry, delete with its question, the search preview, the layout breakpoints,
/// read only and the keyboard.
/// </summary>
public class KnowledgeBaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Delays that finish only when the test says so (or at once with <see cref="Instant"/>).</summary>
    private sealed class ManualDelays
    {
        private readonly List<TaskCompletionSource> _waiting = new();

        public bool Instant { get; set; }

        public int Pending => _waiting.Count;

        public Task Delay(TimeSpan time, CancellationToken token)
        {
            if (Instant)
            {
                return Task.CompletedTask;
            }
            var source = new TaskCompletionSource();
            token.Register(() => source.TrySetCanceled(token));
            _waiting.Add(source);
            return source.Task;
        }

        /// <summary>Finishes every delay that is waiting now, then runs the UI jobs.</summary>
        public void Step()
        {
            var now = _waiting.ToArray();
            _waiting.Clear();
            foreach (var source in now)
            {
                source.TrySetResult();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Drain(int max = 200)
        {
            for (var i = 0; i < max && _waiting.Count > 0; i++)
            {
                Step();
            }
        }
    }

    private static (KnowledgeBase Knowledge, Window Window, ManualDelays Delays) Show(Action<KnowledgeBase>? setup = null, double width = 900)
    {
        var delays = new ManualDelays();
        var knowledge = new KnowledgeBase { Width = width, Delay = delays.Delay, Clock = () => Now };
        setup?.Invoke(knowledge);
        var window = TestHelpers.Show(knowledge);
        Dispatcher.UIThread.RunJobs();
        return (knowledge, window, delays);
    }

    private static KnowledgeBaseDocumentRow Row(KnowledgeBase knowledge, string id)
    {
        Dispatcher.UIThread.RunJobs();
        var document = knowledge.Items.Single(d => d.Id == id);
        // The test window is 300 high, so the list realizes only the rows in view: bring this one in first.
        knowledge.List!.ScrollIntoView(knowledge.Items.IndexOf(document));
        Dispatcher.UIThread.RunJobs();
        return knowledge.RowOf(document).ShouldNotBeNull();
    }

    private static void Click(AvaloniaButton? button)
    {
        button.ShouldNotBeNull();
        button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    #region List and totals

    [AvaloniaFact]
    public void Shows_The_Samples_With_Totals_And_Starts_The_Queue()
    {
        var (knowledge, window, _) = Show();
        knowledge.Items.Select(d => d.Id).ShouldBe(["returns", "warranty", "assembly", "shipping", "battery", "dealers"]);
        // The queued sample starts at once: it has no file, so the built-in pipeline begins with parsing.
        knowledge.Items.Single(d => d.Id == "battery").Status.ShouldBe(KnowledgeDocumentStatus.Parsing);
        knowledge.PendingCount.ShouldBe(1);
        knowledge.Classes.ShouldContain(":indexing");
        var ready = knowledge.Items.Where(d => d.Status == KnowledgeDocumentStatus.Ready).Sum(d => d.ChunkCount);
        knowledge.TotalChunks.ShouldBe(ready);
        knowledge.TotalChunks.ShouldBeGreaterThan(8);
        knowledge.TotalsText!.Text.ShouldBe($"6 documents · {ready} chunks · 2.6 MB");
        window.Close();
    }

    [AvaloniaFact]
    public void Rows_Show_Kind_Size_Chunks_Date_And_State()
    {
        var (knowledge, window, _) = Show();
        var returns = Row(knowledge, "returns");
        returns.NameText!.Text.ShouldBe("Returns and refunds.pdf");
        var chunks = knowledge.Items.Single(d => d.Id == "returns").ChunkCount;
        returns.MetaText!.Text.ShouldBe($"PDF · 184 KB · {chunks} chunks · Updated Nov 8");
        returns.StatusText!.Text.ShouldBe("Ready");
        returns.Classes.ShouldContain(":ready");
        AutomationProperties.GetName(returns.DeleteButton!).ShouldBe("Delete Returns and refunds.pdf");
        AutomationProperties.GetName(returns).ShouldStartWith("Returns and refunds.pdf, Ready, PDF");

        var dealers = Row(knowledge, "dealers");
        dealers.Classes.ShouldContain(":failed");
        dealers.ErrorText!.Text.ShouldBe("The workbook is password protected");
        dealers.ErrorText.IsEffectivelyVisible.ShouldBeTrue();
        dealers.RetryButton!.IsEffectivelyVisible.ShouldBeTrue();
        dealers.ReindexButton!.IsEffectivelyVisible.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void No_Samples_Shows_The_Empty_State()
    {
        var (knowledge, window, _) = Show(k => k.IsSampleData = false);
        knowledge.Items.ShouldBeEmpty();
        knowledge.Classes.ShouldContain(":empty");
        knowledge.TotalsText!.Text.ShouldBe("0 documents · 0 chunks · 0 B");
        window.Close();
    }

    [AvaloniaFact]
    public void Inline_Documents_Replace_The_Samples()
    {
        var (knowledge, window, _) = Show(k => k.Documents.Add(new KnowledgeDocument
        {
            Id = "a", Name = "Notes.md", Status = KnowledgeDocumentStatus.Ready, ChunkCount = 3, SizeBytes = 2048
        }));
        knowledge.Items.Count.ShouldBe(1);
        knowledge.TotalChunks.ShouldBe(3);
        window.Close();
    }

    #endregion

    #region Pipeline

    [AvaloniaFact]
    public void The_Built_In_Pipeline_Parses_Embeds_And_Raises_Completed()
    {
        var (knowledge, window, delays) = Show();
        var battery = knowledge.Items.Single(d => d.Id == "battery");
        KnowledgeBaseIndexedEventArgs? completed = null;
        knowledge.IndexingCompleted += (_, e) => completed = e;
        delays.Step();
        battery.Status.ShouldBe(KnowledgeDocumentStatus.Embedding);
        battery.Progress.ShouldBeGreaterThan(0.39);
        var row = Row(knowledge, "battery");
        row.Classes.ShouldContain(":busy");
        row.Ring!.IsEffectivelyVisible.ShouldBeTrue();
        row.StatusText!.Text!.ShouldStartWith("Embedding ");
        delays.Drain();
        battery.Status.ShouldBe(KnowledgeDocumentStatus.Ready);
        battery.Progress.ShouldBe(1);
        battery.UpdatedAt.ShouldBe(Now);
        battery.ChunkCount.ShouldBe(KnowledgeBase.ChunkText("battery", battery.Text).Count);
        completed.ShouldNotBeNull();
        completed.ChunkCount.ShouldBe(battery.ChunkCount);
        completed.IsReindex.ShouldBeFalse();
        knowledge.PendingCount.ShouldBe(0);
        knowledge.LastAnnouncement.ShouldBe($"Battery care.txt is ready, {battery.ChunkCount} chunks");
        window.Close();
    }

    [AvaloniaFact]
    public void Added_Files_Queue_And_Run_Two_At_A_Time()
    {
        var (knowledge, window, delays) = Show(k => k.IsSampleData = false);
        var added = new List<string>();
        knowledge.DocumentAdded += (_, e) => added.Add(e.Document.Name);
        var documents = knowledge.AddFiles(
        [
            ("One.md", 100, "First file text about bikes."),
            ("Two.txt", 100, "Second file text about wheels."),
            ("Three.md", 100, "Third file text about brakes.")
        ]);
        documents.Count.ShouldBe(3);
        knowledge.Items.Select(d => d.Name).ShouldBe(["One.md", "Two.txt", "Three.md"]);
        added.ShouldBe(["Three.md", "Two.txt", "One.md"]);
        knowledge.Running.Count.ShouldBe(2);
        knowledge.Items[2].Status.ShouldBe(KnowledgeDocumentStatus.Queued);
        knowledge.LastAnnouncement.ShouldBe("Added 3 files to the queue");
        delays.Drain();
        knowledge.Items.ShouldAllBe(d => d.Status == KnowledgeDocumentStatus.Ready);
        window.Close();
    }

    [AvaloniaFact]
    public void Files_Of_Other_Types_Or_Too_Large_Are_Skipped_With_A_Note()
    {
        var (knowledge, window, _) = Show(k => k.IsSampleData = false);
        knowledge.MaxFileSize = 1000;
        var added = knowledge.AddFiles([("Tool.exe", 10, null), ("Big.pdf", 5000, null)]);
        added.ShouldBeEmpty();
        knowledge.DropNoteText.ShouldBe("Skipped 2 files: type not supported or too large");
        knowledge.AddFiles([("Ok.md", 10, "Fine.")]).Count.ShouldBe(1);
        knowledge.DropNoteText.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void A_File_Without_Text_Fails_And_Retry_Uses_The_Handler()
    {
        var (knowledge, window, delays) = Show(k => k.IsSampleData = false);
        delays.Instant = true;
        KnowledgeBaseIndexFailedEventArgs? failed = null;
        knowledge.IndexingFailed += (_, e) => failed = e;
        var document = knowledge.AddFiles([("Scan.pdf", 1000, null)]).Single();
        document.Status.ShouldBe(KnowledgeDocumentStatus.Failed);
        document.Error.ShouldBe("No readable text in this PDF file");
        failed.ShouldNotBeNull();
        failed.Exception.ShouldBeNull();

        knowledge.IndexHandler = (doc, progress, _) =>
        {
            progress.Report(new KnowledgeIndexProgress(KnowledgeDocumentStatus.Embedding, 0.5));
            return Task.FromResult<IReadOnlyList<KnowledgeChunk>>([new KnowledgeChunk(doc.Id, 0, "OCR text")]);
        };
        var row = Row(knowledge, document.Id);
        Click(row.RetryButton);
        document.Status.ShouldBe(KnowledgeDocumentStatus.Ready);
        document.ChunkCount.ShouldBe(1);
        document.Error.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void A_Handler_That_Throws_Fails_The_Document_With_Its_Message()
    {
        var (knowledge, window, _) = Show(k =>
        {
            k.IsSampleData = false;
            k.IndexHandler = (_, _, _) => throw new InvalidOperationException("Quota exceeded");
        });
        var document = knowledge.AddFiles([("A.md", 1, "x")]).Single();
        document.Status.ShouldBe(KnowledgeDocumentStatus.Failed);
        document.Error.ShouldBe("Quota exceeded");
        knowledge.LastAnnouncement.ShouldBe("A.md failed: Quota exceeded");
        window.Close();
    }

    [AvaloniaFact]
    public void Reindex_Runs_Again_And_Reports_A_Reindex()
    {
        var (knowledge, window, delays) = Show();
        delays.Drain();
        var returns = knowledge.Items.Single(d => d.Id == "returns");
        var reindexed = false;
        knowledge.IndexingCompleted += (_, e) => reindexed = e.IsReindex && ReferenceEquals(e.Document, returns);
        Click(Row(knowledge, "returns").ReindexButton);
        returns.Status.ShouldBe(KnowledgeDocumentStatus.Parsing);
        knowledge.LastAnnouncement.ShouldBe("Re-indexing Returns and refunds.pdf");
        delays.Drain();
        returns.Status.ShouldBe(KnowledgeDocumentStatus.Ready);
        reindexed.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Stops_Runs_And_They_Resume_On_Return()
    {
        var (knowledge, window, delays) = Show();
        var battery = knowledge.Items.Single(d => d.Id == "battery");
        battery.IsBusy.ShouldBeTrue();
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        battery.Status.ShouldBe(KnowledgeDocumentStatus.Queued);
        knowledge.Running.ShouldBeEmpty();
        window.Content = knowledge;
        Dispatcher.UIThread.RunJobs();
        battery.IsBusy.ShouldBeTrue();
        delays.Drain();
        battery.Status.ShouldBe(KnowledgeDocumentStatus.Ready);
        window.Close();
    }

    #endregion

    #region Delete

    [AvaloniaFact]
    public void Delete_Asks_First_And_Escape_Cancels()
    {
        var (knowledge, window, _) = Show();
        Click(Row(knowledge, "shipping").DeleteButton);
        knowledge.ConfirmingDocument!.Id.ShouldBe("shipping");
        // The question changes the row's height, so the list may hand the document to another row: look it up again.
        var row = Row(knowledge, "shipping");
        row.IsConfirming.ShouldBeTrue();
        row.Classes.ShouldContain(":confirming");
        var chunks = knowledge.Items.Single(d => d.Id == "shipping").ChunkCount;
        row.ConfirmText!.Text.ShouldBe($"Delete Shipping FAQ.md? Its {chunks} chunks leave the index.");
        row.CancelButton!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        knowledge.ConfirmingDocument.ShouldBeNull();
        row = Row(knowledge, "shipping");
        row.IsConfirming.ShouldBeFalse();
        row.DeleteButton!.IsFocused.ShouldBeTrue();
        knowledge.Items.Count.ShouldBe(6);
        window.Close();
    }

    [AvaloniaFact]
    public void Confirming_Deletes_And_Moves_Focus_To_The_Next_Row()
    {
        var (knowledge, window, _) = Show();
        KnowledgeDocument? deleted = null;
        knowledge.DocumentDeleted += (_, e) => deleted = e.Document;
        Click(Row(knowledge, "warranty").DeleteButton);
        Click(Row(knowledge, "warranty").ConfirmDeleteButton);
        Dispatcher.UIThread.RunJobs();
        knowledge.Items.Select(d => d.Id).ShouldNotContain("warranty");
        deleted!.Id.ShouldBe("warranty");
        knowledge.LastAnnouncement.ShouldBe("Deleted Warranty terms.md");
        var next = Row(knowledge, "assembly");
        next.DeleteButton!.IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_Failing_Delete_Handler_Keeps_The_Document()
    {
        var (knowledge, window, _) = Show(k => k.DeleteHandler = (_, _) => throw new InvalidOperationException("Storage is offline"));
        var shipping = knowledge.Items.Single(d => d.Id == "shipping");
        (await knowledge.RemoveAsync(shipping)).ShouldBeFalse();
        knowledge.Items.ShouldContain(shipping);
        knowledge.LastAnnouncement.ShouldBe("Shipping FAQ.md failed: Storage is offline");
        window.Close();
    }

    [AvaloniaFact]
    public void Deleting_A_Running_Document_Cancels_Its_Run()
    {
        var (knowledge, window, delays) = Show();
        var battery = knowledge.Items.Single(d => d.Id == "battery");
        knowledge.RemoveAsync(battery).IsCompletedSuccessfully.ShouldBeTrue();
        knowledge.Running.ShouldBeEmpty();
        delays.Drain();
        battery.Status.ShouldNotBe(KnowledgeDocumentStatus.Ready);
        knowledge.PendingCount.ShouldBe(0);
        window.Close();
    }

    #endregion

    #region Search

    [AvaloniaFact]
    public void Search_Waits_For_Typing_Then_Ranks_And_Marks_Passages()
    {
        var (knowledge, window, delays) = Show();
        knowledge.SearchText = "damaged delivery refund";
        knowledge.IsSearching.ShouldBeTrue();
        knowledge.Passages.ShouldBeEmpty();
        knowledge.SearchStatusText.ShouldBe("Searching…");
        delays.Step();
        knowledge.IsSearching.ShouldBeFalse();
        knowledge.Passages.Count.ShouldBeGreaterThan(0);
        knowledge.Passages.Count.ShouldBeLessThanOrEqualTo(4);
        knowledge.Passages[0].Document.Id.ShouldBe("returns");
        knowledge.Passages.Select(p => p.Score).ShouldBeInOrder(SortDirection.Descending);
        knowledge.Passages.ShouldAllBe(p => p.Score > 0 && p.Score <= 1);
        knowledge.SearchStatusText.ShouldBe($"{knowledge.Passages.Count} matching passages");
        var view = knowledge.PassageViews[0];
        view.IsVisible.ShouldBeTrue();
        view.Ranges.Count.ShouldBeGreaterThan(1);
        foreach (var range in view.Ranges)
        {
            var word = view.PreviewText.Substring(range.Start, range.Length).ToLowerInvariant();
            (word.StartsWith("damage") || word.StartsWith("deliver") || word.StartsWith("refund")).ShouldBeTrue(word);
        }
        AutomationProperties.GetName(view).ShouldStartWith("Result 1, Returns and refunds.pdf, relevance ");
        window.Close();
    }

    [AvaloniaFact]
    public void No_Match_Says_So_And_Escape_Clears()
    {
        var (knowledge, window, delays) = Show();
        knowledge.SearchText = "zeppelin";
        delays.Step();
        knowledge.Passages.ShouldBeEmpty();
        knowledge.SearchStatusText.ShouldBe("No passages match “zeppelin”");
        knowledge.LastAnnouncement.ShouldBe("No passages match “zeppelin”");
        knowledge.SearchBox!.Focus();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        knowledge.SearchText.ShouldBeNull();
        knowledge.SearchStatusText.ShouldBe("Type to preview matching passages");
        window.Close();
    }

    [AvaloniaFact]
    public void A_Search_Handler_Replaces_The_Built_In_Search()
    {
        var (knowledge, window, _) = Show();
        knowledge.SearchHandler = (query, limit, _) => Task.FromResult<IReadOnlyList<KnowledgePassage>>(
        [
            new KnowledgePassage(knowledge.Items[0], 2, $"Vector hit for {query} with limit {limit}", 0.91)
        ]);
        knowledge.SearchText = "tyres";
        knowledge.RefreshSearchAsync().IsCompleted.ShouldBeTrue();
        knowledge.Passages.Single().Score.ShouldBe(0.91);
        knowledge.PassageViews[0].PreviewText.ShouldBe("Vector hit for tyres with limit 4");
        knowledge.PassageViews[0].Ranges.Single().ShouldBe(new KnowledgeTextRange(15, 5));
        window.Close();
    }

    [AvaloniaFact]
    public void Pressing_A_Passage_Locates_Its_Document()
    {
        var (knowledge, window, delays) = Show();
        knowledge.SearchText = "warranty battery";
        delays.Step();
        KnowledgePassage? invoked = null;
        knowledge.PassageInvoked += (_, e) => invoked = e.Passage;
        Click(knowledge.PassageViews[0]);
        invoked.ShouldNotBeNull();
        knowledge.LocatedDocument.ShouldBe(invoked.Document);
        Row(knowledge, invoked.Document.Id).Classes.ShouldContain(":located");
        window.Close();
    }

    [AvaloniaFact]
    public void A_Document_That_Becomes_Ready_Joins_The_Results()
    {
        var (knowledge, window, delays) = Show();
        knowledge.SearchText = "swells smells";
        delays.Step();
        knowledge.Passages.ShouldBeEmpty();
        delays.Drain();
        knowledge.Passages.ShouldNotBeEmpty();
        knowledge.Passages[0].Document.Id.ShouldBe("battery");
        window.Close();
    }

    #endregion

    #region Layout, read only, keyboard

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Card_Width()
    {
        var (knowledge, window, _) = Show(width: 900);
        knowledge.Classes.ShouldContain(":wide");
        knowledge.Classes.ShouldNotContain(":compact");
        knowledge.Width = 600;
        Dispatcher.UIThread.RunJobs();
        knowledge.Classes.ShouldNotContain(":wide");
        knowledge.Width = 420;
        Dispatcher.UIThread.RunJobs();
        knowledge.Classes.ShouldContain(":compact");
        // Compact rows drop the date.
        Row(knowledge, "returns").MetaText!.Text!.ShouldNotContain("Updated");
        window.Close();
    }

    [AvaloniaFact]
    public void Read_Only_Hides_Adding_And_Row_Actions()
    {
        var (knowledge, window, _) = Show(k =>
        {
            k.IsReadOnly = true;
            k.DocumentsSource = new ObservableCollection<KnowledgeDocument>(KnowledgeBaseSamples.IndexedDocuments());
        });
        knowledge.AddButton!.IsEffectivelyVisible.ShouldBeFalse();
        Row(knowledge, "returns").DeleteButton!.IsEffectivelyVisible.ShouldBeFalse();
        knowledge.PendingCount.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Rows_In_The_Same_Column()
    {
        var (knowledge, window, _) = Show();
        var first = Row(knowledge, "returns");
        first.ReindexButton!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Row(knowledge, "warranty").ReindexButton!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        // The last row failed: Re-index is hidden there, so Retry takes the focus.
        Row(knowledge, "dealers").RetryButton!.IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (knowledge, window, _) = Show(k => k.Labels = new KnowledgeBaseLabels
        {
            Title = "Wissensbasis",
            StatusName = s => s == KnowledgeDocumentStatus.Ready ? "Bereit" : s.ToString()
        });
        window.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.Text == "Wissensbasis");
        Row(knowledge, "returns").StatusText!.Text.ShouldBe("Bereit");
        window.Close();
    }

    #endregion
}
