using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Pdf.Native.Tests;

/// <summary>The queue of the PDFium thread, calls from many threads, closing while work waits.</summary>
[Collection(nameof(PdfiumThreadCollection))]
public sealed class PdfiumThreadingTests
{
    [Fact]
    public async Task Work_runs_on_one_thread_by_priority_then_order_of_arrival()
    {
        Pdfium.RequireBackend();
        var order = new List<string>();
        var threads = new HashSet<int>();
        Task<bool> Add(string name, PdfiumPriority priority) => PdfiumThread.Instance.RunAsync(() =>
        {
            order.Add(name);
            threads.Add(Environment.CurrentManagedThreadId);
            return true;
        }, priority, CancellationToken.None);

        Task[] all;
        using (new ThreadBlocker())
        {
            all =
            [
                Add("background 1", PdfiumPriority.Background),
                Add("render 1", PdfiumPriority.Render),
                Add("background 2", PdfiumPriority.Background),
                Add("interactive", PdfiumPriority.Interactive),
                Add("render 2", PdfiumPriority.Render),
                Add("lifetime", PdfiumPriority.Lifetime)
            ];
        }
        await Task.WhenAll(all).WaitAsync(Pdfium.Timeout);
        order.ShouldBe(["lifetime", "interactive", "render 1", "render 2", "background 1", "background 2"]);
        threads.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Cancelled_work_ends_the_wait_at_once_and_is_skipped_when_dequeued()
    {
        Pdfium.RequireBackend();
        using var cancellation = new CancellationTokenSource();
        var ran = false;
        using (new ThreadBlocker())
        {
            var work = PdfiumThread.Instance.RunAsync(() =>
            {
                ran = true;
                return true;
            }, PdfiumPriority.Render, cancellation.Token);
            cancellation.Cancel();
            // The thread is still blocked: the wait ended without it.
            await Should.ThrowAsync<OperationCanceledException>(work.WaitAsync(Pdfium.Timeout));
        }
        await PdfiumThread.Instance.RunAsync(() => true, PdfiumPriority.Background, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        ran.ShouldBeFalse();
    }

    [Fact]
    public async Task A_result_nobody_takes_goes_to_the_abandoned_callback_on_the_thread()
    {
        Pdfium.RequireBackend();
        using var cancellation = new CancellationTokenSource();
        using var running = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        var abandoned = new TaskCompletionSource<(int Value, bool OnThread)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = PdfiumThread.Instance.RunAsync(() =>
        {
            running.Set();
            finish.Wait();
            return 42;
        }, PdfiumPriority.Render, cancellation.Token, value => abandoned.SetResult((value, PdfiumThread.Instance.IsCurrent)));
        running.Wait(Pdfium.Timeout).ShouldBeTrue();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(work.WaitAsync(Pdfium.Timeout));
        finish.Set();
        (await abandoned.Task.WaitAsync(Pdfium.Timeout)).ShouldBe((42, true));
    }

    [Fact]
    public async Task Failures_reach_the_caller_and_the_thread_goes_on()
    {
        Pdfium.RequireBackend();
        await Should.ThrowAsync<InvalidOperationException>(PdfiumThread.Instance.RunAsync<bool>(() => throw new InvalidOperationException("no"), PdfiumPriority.Render, CancellationToken.None).WaitAsync(Pdfium.Timeout));
        (await PdfiumThread.Instance.RunAsync(() => 1, PdfiumPriority.Render, CancellationToken.None).WaitAsync(Pdfium.Timeout)).ShouldBe(1);
    }

    [Fact]
    public async Task Continuations_do_not_run_on_the_pdfium_thread()
    {
        Pdfium.RequireBackend();
        var onThread = await PdfiumThread.Instance.RunAsync(() => true, PdfiumPriority.Render, CancellationToken.None)
                                         .ContinueWith(_ => PdfiumThread.Instance.IsCurrent, TaskContinuationOptions.ExecuteSynchronously);
        onThread.ShouldBeFalse();
    }

    [Fact]
    public async Task Calls_from_many_threads_give_the_same_results_as_calls_one_by_one()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        using var other = await Pdfium.OpenAsync(PdfSource.FromBytes(await File.ReadAllBytesAsync(Pdfium.Sample)));
        var expectedText = new string[document.PageCount];
        var expectedLinks = new int[document.PageCount];
        for (var i = 0; i < document.PageCount; i++)
        {
            expectedText[i] = (await document.GetPageTextAsync(i, CancellationToken.None)).Text;
            expectedLinks[i] = (await document.GetLinksAsync(i, CancellationToken.None)).Count;
        }
        var expectedInk = (await Pdfium.RenderAsync(document, 2, scale: 0.5)).CountInk(new PixelRect(0, 0, 10_000, 10_000));
        var outlineCount = (await document.GetOutlineAsync(CancellationToken.None)).Count;

        var workers = Enumerable.Range(0, 32).Select(worker => Task.Run(async () =>
        {
            for (var round = 0; round < 12; round++)
            {
                var page = (worker + round) % document.PageCount;
                var target = worker % 2 == 0 ? document : other;
                (await target.GetPageTextAsync(page, CancellationToken.None)).Text.ShouldBe(expectedText[page]);
                (await target.GetLinksAsync(page, CancellationToken.None)).Count.ShouldBe(expectedLinks[page]);
                (await Pdfium.RenderAsync(target, 2, scale: 0.5)).CountInk(new PixelRect(0, 0, 10_000, 10_000)).ShouldBe(expectedInk);
                target.GetPageLabel(page).ShouldNotBeNull();
                if (round % 4 == 0)
                {
                    (await target.GetOutlineAsync(CancellationToken.None)).Count.ShouldBe(outlineCount);
                }
            }
        })).ToArray();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task Disposing_cancels_what_waits_and_closes_the_document_afterwards()
    {
        var source = await File.ReadAllBytesAsync(Pdfium.Sample);
        var document = await Pdfium.OpenAsync(PdfSource.FromBytes(source));
        var target = new PixelSize(400, 560);
        var calls = new List<Task>();
        using (new ThreadBlocker())
        {
            for (var i = 0; i < 40; i++)
            {
                calls.Add(document.RenderAsync(new PageRenderRequest(i % 12, target, null), CancellationToken.None).AsTask());
                calls.Add(document.GetPageTextAsync(i % 12, CancellationToken.None));
                calls.Add(document.GetLinksAsync(i % 12, CancellationToken.None));
            }
            calls.Add(document.GetOutlineAsync(CancellationToken.None));
            document.Dispose();
            // All of them end while the thread is still blocked: nobody waits for a close.
            foreach (var call in calls)
            {
                await Should.ThrowAsync<OperationCanceledException>(call.WaitAsync(Pdfium.Timeout));
            }
        }

        // Later calls.
        await Should.ThrowAsync<OperationCanceledException>(document.RenderAsync(new PageRenderRequest(0, target, null), CancellationToken.None).AsTask());
        await Should.ThrowAsync<OperationCanceledException>(document.GetPageTextAsync(0, CancellationToken.None));
        await Should.ThrowAsync<OperationCanceledException>(document.GetLinksAsync(0, CancellationToken.None));
        await Should.ThrowAsync<OperationCanceledException>(document.GetOutlineAsync(CancellationToken.None));
        Should.Throw<ObjectDisposedException>(() => document.GetPageLabel(0));
        document.Dispose();

        // What does not need PDFium is still answered, which keeps a viewer that has not let go yet from crashing.
        document.PageCount.ShouldBe(12);
        document.GetPageSize(0).Width.ShouldBeGreaterThan(0);

        // The thread survived and others are not disturbed.
        using var next = await Pdfium.OpenAsync(PdfSource.FromBytes(source));
        (await Pdfium.RenderAsync(next, 0)).InkBounds().Width.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_page_being_rendered_during_dispose_is_safe()
    {
        var document = await Pdfium.OpenAsync(Pdfium.Sample);
        // Big enough to take a moment, so that dispose lands while it runs (or just after); neither order may crash.
        var render = document.RenderAsync(new PageRenderRequest(0, new PixelSize(3000, 4200), null), CancellationToken.None).AsTask();
        await Task.Delay(2);
        document.Dispose();
        try
        {
            var image = await render.WaitAsync(Pdfium.Timeout);
            Pixels.Of(image!.Bitmap).Width.ShouldBe(3000);
        }
        catch (OperationCanceledException)
        {
            // Ended by Dispose, as documented.
        }
        await PdfiumThread.Instance.RunAsync(() => true, PdfiumPriority.Background, CancellationToken.None).WaitAsync(Pdfium.Timeout);
    }

    [Fact]
    public async Task A_bitmap_outlives_its_document()
    {
        var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var image = await document.RenderAsync(new PageRenderRequest(2, new PixelSize(400, 560), null), CancellationToken.None);
        document.Dispose();
        await PdfiumThread.Instance.RunAsync(() => true, PdfiumPriority.Background, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        Pixels.Of(image!.Bitmap).InkBounds().Width.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task A_stopped_render_cancels_only_itself()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        using var cancellation = new CancellationTokenSource();
        Task<PageImage?> stopped;
        Task<PageImage?> kept;
        using (new ThreadBlocker())
        {
            stopped = document.RenderAsync(new PageRenderRequest(0, new PixelSize(100, 140), null), cancellation.Token).AsTask();
            kept = document.RenderAsync(new PageRenderRequest(1, new PixelSize(100, 140), null), CancellationToken.None).AsTask();
            cancellation.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(stopped.WaitAsync(Pdfium.Timeout));
        }
        var image = await kept.WaitAsync(Pdfium.Timeout);
        image.ShouldNotBeNull();
    }
}

/// <summary>Those tests stop the one PDFium thread, so they do not run in parallel with each other; the timeouts of other classes are generous enough.</summary>
[CollectionDefinition(nameof(PdfiumThreadCollection), DisableParallelization = true)]
public sealed class PdfiumThreadCollection;
