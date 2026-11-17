using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using PdfAttached = global::TKCode.Arc.Pdf;

namespace TKCode.Arc.Tests.Pdf;

public class PdfViewerTests
{
    private static PdfSource Source(string name = "a.pdf") => PdfSource.FromFile(name);

    private static PdfViewer Viewer(PdfSource? source = null) => new() { Width = 900, Height = 600, Source = source };

    /// <summary>In a window that fits the viewer: the default test window would clip the sidebar away, and no thumbnail would be in view.</summary>
    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(content, level);
        window.Width = 1000;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle(Func<bool> condition) => TestHelpers.PumpUntil(condition).ShouldBeTrue();

    #region Theme

    [AvaloniaFact]
    public void Theme_Reuses_The_PageViewer_Template_Under_Its_Own_Key()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        var window = Show(viewer);
        try
        {
            viewer.Theme.ShouldBeNull();
            viewer.TryFindResource(typeof(PdfViewer), out var theme).ShouldBeTrue();
            ((Avalonia.Styling.ControlTheme)theme!).BasedOn.ShouldNotBeNull();
            TestHelpers.Descendant<ScrollViewer>(viewer, PageViewer.ScrollPart).ShouldNotBeNull();
            viewer.GetVisualDescendants().OfType<Control>().Single(c => c.Name == PageViewer.ToolbarPart).ShouldNotBeNull();
            viewer.ToolbarStart.ShouldBeOfType<PdfToolbarToggle>();
            ((Control)viewer.ToolbarStart!).Bounds.Width.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shows_Pages_In_The_Base_Presenter()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        var window = Show(viewer);
        try
        {
            Pump();
            viewer.PageCount.ShouldBe(12);
            // The base style for the presenter reached it through BasedOn.
            var presenter = viewer.GetVisualDescendants().OfType<Control>().Single(c => c.Name == PageViewer.PresenterPart);
            presenter.Bounds.Width.ShouldBeGreaterThan(0);
            viewer.Zoom.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region State machine

    [AvaloniaFact]
    public void Starts_Empty()
    {
        var viewer = Viewer();
        var window = Show(viewer);
        try
        {
            viewer.Status.ShouldBe(PdfViewerStatus.Empty);
            viewer.Error.ShouldBeNull();
            viewer.OpenedDocument.ShouldBeNull();
            viewer.StateLayer.ShouldBeNull();
            viewer.Sidebar.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Goes_Through_Loading_To_Ready()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source("report.pdf"));
        PdfDocument? opened = null;
        viewer.DocumentOpened += (_, e) => opened = e.Document;
        var window = Show(viewer);
        try
        {
            viewer.Status.ShouldBe(PdfViewerStatus.Loading);
            viewer.StateLayer!.IsVisible.ShouldBeTrue();
            viewer.StateLayer.Classes.Contains(PdfStateLayer.LoadingPseudoClass).ShouldBeTrue();
            Pump();
            TestHelpers.Descendant<ArcSpinner>(viewer.StateLayer).IsEffectivelyVisible.ShouldBeTrue();

            var document = backend.NewDocument();
            backend.Calls.Single().Result.SetResult(document);
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            viewer.OpenedDocument.ShouldBeSameAs(document);
            viewer.PageSource.ShouldBeSameAs(document);
            viewer.PageCount.ShouldBe(12);
            viewer.Title.ShouldBe("Simulated document");
            opened.ShouldBeSameAs(document);
            viewer.StateLayer.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Title_Falls_Back_To_The_Display_Name()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source("report.pdf"));
        var window = Show(viewer);
        try
        {
            backend.Calls.Single().Result.SetResult(new CountingPdfDocument(2));
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            viewer.Title.ShouldBe("report.pdf");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Backend_It_Fails_With_The_Hint_And_Shows_No_Fake_Pages()
    {
        var viewer = Viewer(Source());
        PdfException? failed = null;
        viewer.DocumentFailed += (_, e) => failed = e.Error;
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.Failed);
            viewer.Error!.Kind.ShouldBe(PdfErrorKind.BackendUnavailable);
            failed.ShouldBeSameAs(viewer.Error);
            viewer.PageCount.ShouldBe(0);
            viewer.Classes.Contains(PdfViewer.FailedPseudoClass).ShouldBeTrue();
            viewer.StateLayer!.Detail.ShouldNotBeNull().ShouldContain("ArcPdfNative.Use()");
            viewer.StateLayer.Message.ShouldBe("No PDF renderer is set up");
            Pump();
            viewer.StateLayer.IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(PdfErrorKind.NotFound, "The file was not found")]
    [InlineData(PdfErrorKind.Corrupt, "The file is damaged")]
    [InlineData(PdfErrorKind.Network, null)]
    public void Failure_Text_Follows_The_Kind(PdfErrorKind kind, string? detail)
    {
        using var scope = new BackendScope(new FakePdfBackend { Failure = new PdfException(kind) });
        var viewer = Viewer(Source());
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.Failed);
            viewer.StateLayer!.Message.ShouldBe("Could not open this document");
            viewer.StateLayer.Detail.ShouldBe(detail);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Failure_Text_Follows_The_Language()
    {
        using var scope = new BackendScope(new FakePdfBackend { Failure = new PdfException(PdfErrorKind.Corrupt) });
        var viewer = Viewer(Source());
        var window = Show(viewer);
        var languages = Localization.ArcLanguageManager.Current!;
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.Failed);
            languages.Set(Localization.LanguageTags.ZhCN);
            Settle(() => viewer.StateLayer!.Detail == "文件已损坏");
        }
        finally
        {
            languages.Set(Localization.LanguageTags.EnUS);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Other_Exceptions_From_A_Backend_Become_Pdf_Failures()
    {
        using var scope = new BackendScope(new FakePdfBackend { Failure = new FileNotFoundException("gone") });
        var viewer = Viewer(Source());
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.Failed);
            viewer.Error!.Kind.ShouldBe(PdfErrorKind.NotFound);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Source_Null_Goes_Back_To_Empty()
    {
        using var scope = new BackendScope(new FakePdfBackend());
        var viewer = Viewer(Source());
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            viewer.Source = null;
            viewer.Status.ShouldBe(PdfViewerStatus.Empty);
            viewer.OpenedDocument.ShouldBeNull();
            viewer.PageCount.ShouldBe(0);
            viewer.StateLayer!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Ownership and superseded opens

    [AvaloniaFact]
    public void Changing_Source_Disposes_The_Document_It_Opened()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source("a.pdf"));
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            var first = backend.Documents.Single();
            viewer.Source = Source("b.pdf");
            first.IsDisposed.ShouldBeTrue();
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            backend.Documents.Count.ShouldBe(2);
            viewer.OpenedDocument.ShouldBeSameAs(backend.Documents[1]);
            backend.Documents[1].IsDisposed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
        Dispatcher.UIThread.RunJobs();
        backend.Documents[1].IsDisposed.ShouldBeTrue("leaving the tree closes what the viewer opened");
    }

    [AvaloniaFact]
    public void A_Document_From_The_Caller_Is_Never_Disposed()
    {
        using var document = new SimulatedPdfDocument();
        using var other = new SimulatedPdfDocument(3);
        var viewer = Viewer();
        viewer.Document = document;
        var window = Show(viewer);
        try
        {
            viewer.Status.ShouldBe(PdfViewerStatus.Ready);
            viewer.OpenedDocument.ShouldBeSameAs(document);
            viewer.Document = other;
            document.IsDisposed.ShouldBeFalse();
            viewer.PageCount.ShouldBe(3);
            viewer.Document = null;
            other.IsDisposed.ShouldBeFalse();
            viewer.Status.ShouldBe(PdfViewerStatus.Empty);
            viewer.Document = other;
        }
        finally
        {
            window.Close();
        }
        other.IsDisposed.ShouldBeFalse();
        viewer.Status.ShouldBe(PdfViewerStatus.Ready);
    }

    [AvaloniaFact]
    public void Source_Wins_Over_Document()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        using var document = new SimulatedPdfDocument(3);
        using var ignored = new SimulatedPdfDocument(4);
        var viewer = Viewer();
        viewer.Document = document;
        var window = Show(viewer);
        try
        {
            viewer.Source = Source();
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            viewer.OpenedDocument.ShouldBeSameAs(backend.Documents.Single());
            document.IsDisposed.ShouldBeFalse();
            viewer.Document = ignored;
            viewer.Document = document;
            backend.Calls.Count.ShouldBe(1, "a change of document does not reopen the source");
            backend.Documents.Single().IsDisposed.ShouldBeFalse();
            viewer.Source = null;
            viewer.OpenedDocument.ShouldBeSameAs(document);
            backend.Documents.Single().IsDisposed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Superseded_Open_Is_Cancelled_And_What_It_Brings_Is_Disposed()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source("old.pdf"));
        var opened = 0;
        var failed = 0;
        viewer.DocumentOpened += (_, _) => opened++;
        viewer.DocumentFailed += (_, _) => failed++;
        var window = Show(viewer);
        try
        {
            var old = backend.Calls.Single();
            viewer.Source = Source("new.pdf");
            old.Token.IsCancellationRequested.ShouldBeTrue();
            var current = backend.Calls[1];
            current.Token.IsCancellationRequested.ShouldBeFalse();

            // A backend may finish anyway: the late document is closed and never shown.
            var late = backend.NewDocument();
            old.Result.SetResult(late);
            Settle(() => late.IsDisposed);
            viewer.Status.ShouldBe(PdfViewerStatus.Loading);
            opened.ShouldBe(0);

            var fresh = backend.NewDocument();
            current.Result.SetResult(fresh);
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            viewer.OpenedDocument.ShouldBeSameAs(fresh);
            opened.ShouldBe(1);
            failed.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Superseded_Open_That_Fails_Changes_Nothing()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source("old.pdf"));
        var window = Show(viewer);
        try
        {
            var old = backend.Calls.Single();
            viewer.Source = Source("new.pdf");
            old.Result.SetException(new PdfException(PdfErrorKind.Corrupt));
            Pump();
            viewer.Status.ShouldBe(PdfViewerStatus.Loading);
            viewer.Error.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Stops_The_Open_And_Coming_Back_Opens_Again()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        var host = new Panel { Children = { viewer } };
        var window = Show(host);
        try
        {
            var first = backend.Calls.Single();
            host.Children.Clear();
            Dispatcher.UIThread.RunJobs();
            first.Token.IsCancellationRequested.ShouldBeTrue();
            viewer.Status.ShouldBe(PdfViewerStatus.Empty);
            host.Children.Add(viewer);
            backend.Calls.Count.ShouldBe(2);
            viewer.Status.ShouldBe(PdfViewerStatus.Loading);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Source_Set_Before_The_Viewer_Enters_The_Tree_Opens_Once()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        backend.Calls.ShouldBeEmpty();
        var window = Show(viewer);
        try
        {
            Pump();
            backend.Calls.Count.ShouldBe(1);
            viewer.Status.ShouldBe(PdfViewerStatus.Ready);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Source_Set_In_The_Tree_Opens_Once()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var viewer = Viewer();
        var window = Show(viewer);
        try
        {
            viewer.Source = Source();
            Pump();
            backend.Calls.Count.ShouldBe(1);
            viewer.Status.ShouldBe(PdfViewerStatus.Ready);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Source_Set_While_A_Parent_Enters_The_Tree_Opens_Once()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var viewer = Viewer();
        var host = new Panel { Children = { viewer } };
        host.AttachedToVisualTree += (_, _) => viewer.Source = Source();
        var window = Show(host);
        try
        {
            Pump();
            backend.Calls.Count.ShouldBe(1);
            viewer.Status.ShouldBe(PdfViewerStatus.Ready);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>What the browser host does at startup: the first view leaves and a new one takes it, right after the app set the source.</summary>
    [AvaloniaFact]
    public void Moving_To_Another_Parent_In_One_Step_Does_Not_Open_Again()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer();
        var first = new Panel { Children = { viewer } };
        var second = new Panel();
        var window = Show(new Panel { Children = { first, second } });
        try
        {
            viewer.Source = Source();
            var call = backend.Calls.Single();
            first.Children.Clear();
            second.Children.Add(viewer);
            Pump();
            backend.Calls.Count.ShouldBe(1);
            call.Token.IsCancellationRequested.ShouldBeFalse();
            viewer.Status.ShouldBe(PdfViewerStatus.Loading);
            call.Result.SetResult(backend.NewDocument());
            Settle(() => viewer.Status == PdfViewerStatus.Ready);

            // A document on show stays open through a move too.
            var opened = viewer.OpenedDocument;
            second.Children.Clear();
            first.Children.Add(viewer);
            Pump();
            backend.Calls.Count.ShouldBe(1);
            viewer.OpenedDocument.ShouldBeSameAs(opened);
            viewer.Status.ShouldBe(PdfViewerStatus.Ready);
            viewer.PageCount.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Moving_A_Locked_Document_Does_Not_Ask_Again()
    {
        var backend = new FakePdfBackend { RequiredPassword = "secret" };
        using var scope = new BackendScope(backend);
        var asked = 0;
        var viewer = Viewer();
        viewer.PasswordRequested += (_, e) =>
        {
            asked++;
            e.Password = "secret";
        };
        var first = new Panel { Children = { viewer } };
        var second = new Panel();
        var window = Show(new Panel { Children = { first, second } });
        try
        {
            viewer.Source = Source();
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            asked.ShouldBe(1);
            first.Children.Clear();
            second.Children.Add(viewer);
            Pump();
            asked.ShouldBe(1);
            viewer.Status.ShouldBe(PdfViewerStatus.Ready);

            // Leaving for good forgets the password: coming back asks again.
            second.Children.Clear();
            Pump();
            viewer.Status.ShouldBe(PdfViewerStatus.Empty);
            first.Children.Add(viewer);
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            asked.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Source_While_Moving_Opens_The_New_One()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source("a.pdf"));
        var host = new Panel { Children = { viewer } };
        var window = Show(host);
        try
        {
            var first = backend.Calls.Single();
            host.Children.Clear();
            viewer.Source = Source("b.pdf");
            first.Token.IsCancellationRequested.ShouldBeTrue();
            host.Children.Add(viewer);
            Pump();
            backend.Calls.Count.ShouldBe(2);
            backend.Calls[1].Token.IsCancellationRequested.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Password

    [AvaloniaFact]
    public void Without_A_Handler_The_Form_Asks_And_A_Right_Password_Opens()
    {
        var backend = new FakePdfBackend { RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.PasswordRequired && viewer.StateLayer?.Form is not null);
            viewer.Classes.Contains(PdfViewer.PasswordPseudoClass).ShouldBeTrue();
            Pump();
            var form = viewer.StateLayer!.Form!;
            form.IsEffectivelyVisible.ShouldBeTrue();
            form.Field!.IsInvalid.ShouldBeFalse();
            backend.Calls.Single().Password.ShouldBeNull();

            form.Field.Text = "sesame";
            form.Submit();
            form.Field.Text.ShouldBeNullOrEmpty();
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            backend.Calls[1].Password.ShouldBe("sesame");
            viewer.StateLayer.Form.ShouldBeNull();
            viewer.StateLayer.IsVisible.ShouldBeFalse();
            viewer.Classes.Contains(PdfViewer.PasswordPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Wrong_Password_Says_So_Shakes_And_Can_Be_Retried()
    {
        var backend = new FakePdfBackend { RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        var window = Show(viewer, ArcMotionLevel.Full);
        try
        {
            Settle(() => viewer.StateLayer?.Form is not null);
            Pump();
            var form = viewer.StateLayer!.Form!;
            form.Field!.Text = "wrong";
            form.Submit();
            Settle(() => form.Field.IsInvalid);
            viewer.Status.ShouldBe(PdfViewerStatus.PasswordRequired);
            form.Field.ErrorMessage.ShouldBe("Incorrect password");
            form.ShakeCount.ShouldBe(1);
            form.Field.Text.ShouldBeNullOrEmpty();
            form.IsBusy.ShouldBeFalse();
            form.Field.IsEnabled.ShouldBeTrue();
            viewer.Error.ShouldBeNull();

            form.Field.Text = "sesame";
            form.Submit();
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            backend.Calls.Select(c => c.Password).ShouldBe([null, "wrong", "sesame"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_Reduced_Motion_A_Wrong_Password_Does_Not_Move_The_Form()
    {
        var backend = new FakePdfBackend { RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        var window = Show(viewer, ArcMotionLevel.Reduced);
        try
        {
            Settle(() => viewer.StateLayer?.Form is not null);
            Pump();
            var form = viewer.StateLayer!.Form!;
            form.Field!.Text = "wrong";
            form.Submit();
            Settle(() => form.Field.IsInvalid);
            form.ShakeCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cancelling_The_Form_Fails_With_PasswordRequired()
    {
        using var scope = new BackendScope(new FakePdfBackend { RequiredPassword = "sesame" });
        var viewer = Viewer(Source());
        PdfException? failed = null;
        viewer.DocumentFailed += (_, e) => failed = e.Error;
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.StateLayer?.Form is not null);
            Pump();
            viewer.StateLayer!.Form!.Cancel();
            Settle(() => viewer.Status == PdfViewerStatus.Failed);
            viewer.Error!.Kind.ShouldBe(PdfErrorKind.PasswordRequired);
            failed.ShouldBeSameAs(viewer.Error);
            viewer.StateLayer.Form.ShouldBeNull();
            viewer.StateLayer.Detail.ShouldBe("Password required");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handler_Can_Answer_And_Is_Told_About_A_Retry()
    {
        var backend = new FakePdfBackend { RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        var retries = new List<bool>();
        viewer.PasswordRequested += (_, e) =>
        {
            retries.Add(e.IsRetry);
            e.Password = e.IsRetry ? "sesame" : "wrong";
        };
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            retries.ShouldBe([false, true]);
            viewer.StateLayer!.Form.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handler_Can_Answer_Later_With_A_Deferral()
    {
        var backend = new FakePdfBackend { RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        PdfPasswordEventArgs? args = null;
        PdfPasswordDeferral? deferral = null;
        viewer.PasswordRequested += (_, e) =>
        {
            args = e;
            deferral = e.GetDeferral();
        };
        var window = Show(viewer);
        try
        {
            Settle(() => args is not null);
            Pump();
            viewer.Status.ShouldBe(PdfViewerStatus.PasswordRequired);
            viewer.StateLayer!.Form.ShouldBeNull();
            backend.Calls.Count.ShouldBe(1);

            args!.Password = "sesame";
            deferral!.Complete();
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            args.Password.ShouldBeNull("the viewer does not leave the password in the arguments");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handler_Can_Cancel()
    {
        using var scope = new BackendScope(new FakePdfBackend { RequiredPassword = "sesame" });
        var viewer = Viewer(Source());
        viewer.PasswordRequested += (_, e) => e.Cancel = true;
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Status == PdfViewerStatus.Failed);
            viewer.Error!.Kind.ShouldBe(PdfErrorKind.PasswordRequired);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Handler_That_Keeps_Giving_A_Wrong_Password_Ends_At_The_Form()
    {
        var backend = new FakePdfBackend { RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        viewer.PasswordRequested += (_, e) => e.Password = "wrong";
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.StateLayer?.Form is not null);
            viewer.Status.ShouldBe(PdfViewerStatus.PasswordRequired);
            backend.Calls.Count.ShouldBeLessThan(10);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_Source_While_The_Form_Is_Up_Drops_It()
    {
        var backend = new FakePdfBackend { RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source("locked.pdf"));
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.StateLayer?.Form is not null);
            backend.RequiredPassword = null;
            viewer.Source = Source("open.pdf");
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            viewer.StateLayer!.Form.ShouldBeNull();
            backend.Calls.Last().Password.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Password_Stays_Out_Of_Errors_And_State()
    {
        const string secret = "correct-horse-battery";
        var backend = new FakePdfBackend { Failure = null, RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.StateLayer?.Form is not null);
            Pump();
            var form = viewer.StateLayer!.Form!;
            form.Field!.Text = secret;
            form.Submit();
            Settle(() => form.Field.IsInvalid);
            form.Field.Text.ShouldBeNullOrEmpty();
            form.Cancel();
            Settle(() => viewer.Status == PdfViewerStatus.Failed);
            viewer.Error!.ToString().ShouldNotContain(secret);
            viewer.Error.Message.ShouldNotContain(secret);
            viewer.StateLayer.Message!.ShouldNotContain(secret);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Sidebar

    [AvaloniaFact]
    public void The_Toolbar_Button_Opens_The_Sidebar_And_It_Takes_Width_From_The_Pages()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        var window = Show(viewer);
        try
        {
            Pump();
            var toggle = (PdfToolbarToggle)viewer.ToolbarStart!;
            toggle.IsEnabled.ShouldBeTrue();
            var scroll = TestHelpers.Descendant<ScrollViewer>(viewer, PageViewer.ScrollPart);
            var before = scroll.Bounds.Width;
            viewer.Sidebar.ShouldBeNull("nothing is built until it is opened");

            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            viewer.IsSidebarOpen.ShouldBeTrue();
            viewer.Classes.Contains(PdfViewer.SidebarOpenPseudoClass).ShouldBeTrue();
            toggle.IsActive.ShouldBeTrue();
            Pump();
            viewer.Sidebar!.Bounds.Width.ShouldBe(224);
            scroll.Bounds.Width.ShouldBe(before - 224, 1);

            viewer.IsSidebarOpen = false;
            Pump();
            toggle.IsActive.ShouldBeFalse();
            viewer.Sidebar.IsVisible.ShouldBeFalse();
            scroll.Bounds.Width.ShouldBe(before, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Under_Full_Motion_The_Sidebar_Width_Moves_And_Hides_When_Done()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        var window = Show(viewer, ArcMotionLevel.Full);
        try
        {
            Pump();
            var scroll = TestHelpers.Descendant<ScrollViewer>(viewer, PageViewer.ScrollPart);
            var full = scroll.Bounds.Width;
            viewer.IsSidebarOpen = true;
            var moving = false;
            TestHelpers.PumpUntil(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                moving |= scroll.Bounds.Width > full - 224 + 1 && scroll.Bounds.Width < full - 1;
                return Math.Abs(scroll.Bounds.Width - (full - 224)) < 1;
            }).ShouldBeTrue();
            moving.ShouldBeTrue("the width goes through values in between");

            viewer.IsSidebarOpen = false;
            viewer.Sidebar!.IsVisible.ShouldBeTrue("it stays until the exit has played");
            TestHelpers.PumpUntil(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return !viewer.Sidebar.IsVisible;
            }).ShouldBeTrue();
            scroll.Bounds.Width.ShouldBe(full, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PresenceHost_Slide_Comes_In_From_The_Left()
    {
        var host = new PresenceHost { Mode = PresenceMode.Slide, Offset = 200, IsPresent = false, Child = new Border { Width = 50, Height = 50 } };
        var window = Show(host, ArcMotionLevel.Full);
        try
        {
            Pump();
            host.RenderTransform!.Value.M31.ShouldBe(-200, 0.01);
            host.IsPresent = true;
            var between = false;
            TestHelpers.PumpUntil(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var x = host.RenderTransform!.Value.M31;
                between |= x < -1 && x > -199;
                return Math.Abs(x) < 0.01;
            }).ShouldBeTrue();
            between.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Toolbar_Button_Is_Off_Without_Pages()
    {
        var viewer = Viewer();
        var window = Show(viewer);
        try
        {
            ((PdfToolbarToggle)viewer.ToolbarStart!).IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void When_Narrow_The_Sidebar_Floats_And_The_Pages_Keep_Their_Width()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = new PdfViewer { Width = 500, Height = 600, Document = document };
        var window = Show(viewer);
        try
        {
            Pump();
            viewer.Classes.Contains(PdfViewer.NarrowPseudoClass).ShouldBeTrue();
            var scroll = TestHelpers.Descendant<ScrollViewer>(viewer, PageViewer.ScrollPart);
            var before = scroll.Bounds.Width;

            viewer.IsSidebarOpen = true;
            Pump();
            viewer.Sidebar!.IsEffectivelyVisible.ShouldBeTrue();
            viewer.Sidebar.Bounds.Width.ShouldBe(224);
            viewer.Sidebar.Classes.Contains(PdfSidebar.FloatingPseudoClass).ShouldBeTrue();
            scroll.Bounds.Width.ShouldBe(before, 1);

            // Wide again: docked, beside the pages.
            viewer.Width = 900;
            Pump();
            viewer.Classes.Contains(PdfViewer.NarrowPseudoClass).ShouldBeFalse();
            viewer.Sidebar.Classes.Contains(PdfSidebar.FloatingPseudoClass).ShouldBeFalse();
            viewer.Sidebar.IsEffectivelyVisible.ShouldBeTrue();
            scroll.Bounds.Width.ShouldBe(900 - 224, 1);

            // Narrow and closed stays closed, and Escape closes the floating one.
            viewer.Width = 500;
            Pump();
            viewer.Sidebar.IsEffectivelyVisible.ShouldBeTrue();
            viewer.Focus();
            var presenter = viewer.GetVisualDescendants().OfType<Control>().Single(c => c.Name == PageViewer.PresenterPart);
            presenter.Focus();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            viewer.IsSidebarOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Outline_Tab_Hides_Without_An_Outline()
    {
        using var plain = new CountingPdfDocument(4);
        using var rich = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = plain;
        viewer.SidebarMode = PdfSidebarMode.Outline;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Pump();
            var sidebar = viewer.Sidebar!;
            sidebar.Tabs!.IsVisible.ShouldBeFalse();
            sidebar.EffectiveMode.ShouldBe(PdfSidebarMode.Thumbnails);
            sidebar.OutlineTree!.IsVisible.ShouldBeFalse();
            viewer.SidebarMode.ShouldBe(PdfSidebarMode.Outline, "what the caller asked for stays");

            viewer.Document = rich;
            Settle(() => sidebar.Tabs.IsVisible);
            sidebar.EffectiveMode.ShouldBe(PdfSidebarMode.Outline);
            sidebar.Tabs.SelectedIndex.ShouldBe(1);
            sidebar.OutlineTree.IsVisible.ShouldBeTrue();

            sidebar.Tabs.SelectedIndex = 0;
            viewer.SidebarMode.ShouldBe(PdfSidebarMode.Thumbnails);
            sidebar.OutlineTree.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Broken_Outline_Is_No_Outline()
    {
        using var document = new BrokenOutlineDocument();
        var viewer = Viewer();
        viewer.Document = document;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Pump();
            viewer.Status.ShouldBe(PdfViewerStatus.Ready);
            viewer.Sidebar!.Tabs!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_An_Outline_Entry_Jumps()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        viewer.SidebarMode = PdfSidebarMode.Outline;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.Sidebar?.OutlineTree?.IsVisible == true);
            Pump();
            var tree = viewer.Sidebar!.OutlineTree!;
            var outline = (IReadOnlyList<PdfOutlineItem>)tree.ItemsSource!;
            tree.RaiseEvent(new TreeViewItemEventArgs(TreeView.ItemInvokedEvent, outline[1].Children[1]));
            viewer.CurrentPageIndex.ShouldBe(2);
            // A group without a destination goes nowhere.
            tree.RaiseEvent(new TreeViewItemEventArgs(TreeView.ItemInvokedEvent, outline[2]));
            viewer.CurrentPageIndex.ShouldBe(2);
            // A point of a page ends on screen, and moves the current page there.
            tree.RaiseEvent(new TreeViewItemEventArgs(TreeView.ItemInvokedEvent, outline[1].Children[0]));
            Pump();
            viewer.PageHitTest(new Point(viewer.Bounds.Width - 100, viewer.Bounds.Height / 2), out var page, out _).ShouldBeTrue();
            page.ShouldBe(1);
            viewer.CurrentPageIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Thumbnails

    [AvaloniaFact]
    public void Thumbnails_Are_Virtualized_And_Only_Those_On_Screen_Render()
    {
        using var document = new CountingPdfDocument(2000);
        var viewer = Viewer();
        viewer.Document = document;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Pump();
            Settle(() => document.Requests.Any(r => r.TargetSize.Width == 120));
            var list = viewer.Sidebar!.Thumbnails!;
            list.ItemCount.ShouldBe(2000);
            var realized = list.Realized.ToList();
            realized.ShouldNotBeEmpty();
            realized.Count.ShouldBeLessThan(30);
            realized.Max(t => t.PageIndex).ShouldBeLessThan(30);
            Pump();
            document.Requests.Where(r => r.TargetSize.Width == 120).Select(r => r.PageIndex).Distinct().Count().ShouldBeLessThan(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumbnails_Draw_From_The_Shared_Cache_Without_Asking_Twice()
    {
        using var document = new CountingPdfDocument(40);
        var viewer = Viewer();
        viewer.Document = document;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Settle(() => document.Requests.Any(r => r.TargetSize.Width == 120));
            Pump();
            var thumbnail = viewer.Sidebar!.Thumbnails!.Realized.First(t => t.PageIndex == 0);
            Settle(() => viewer.PeekThumbnail(0, thumbnail.TargetSize) is not null);
            for (var i = 0; i < 5; i++)
            {
                thumbnail.InvalidateVisual();
                Pump();
            }
            document.Requests.Count(r => r.PageIndex == 0 && r.TargetSize == thumbnail.TargetSize).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Closed_Sidebar_Asks_For_Nothing()
    {
        using var document = new CountingPdfDocument(40);
        var viewer = Viewer();
        viewer.Document = document;
        var window = Show(viewer);
        try
        {
            Pump();
            document.Requests.ShouldNotContain(r => r.TargetSize.Width == 120);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrolling_The_Thumbnails_Takes_Back_What_Has_Not_Started()
    {
        var source = new global::TKCode.Arc.Tests.Media.FakePageSource(500, new Size(816, 1056)) { Hold = true };
        var viewer = Viewer();
        viewer.PageSource = source;
        viewer.IsSidebarOpen = true;
        viewer.Height = 400;
        var window = Show(viewer);
        try
        {
            Pump();
            var list = viewer.Sidebar!.Thumbnails!;
            var first = list.Realized.First(t => t.PageIndex == 0);
            Settle(() => source.WaitingCount > 0);
            var size = first.TargetSize;
            Pump();
            list.ScrollIntoView(400);
            Pump();
            list.Realized.ShouldNotContain(t => t.PageIndex == 0);
            var askedEarly = source.Requests.Count(r => r.TargetSize == size && r.PageIndex < 20);
            // Everything finishes: what was scrolled away and had not started never reaches the source.
            source.Hold = false;
            source.Release();
            Settle(() => source.WaitingCount == 0);
            Pump();
            var after = source.Requests.Where(r => r.TargetSize == size).Select(r => r.PageIndex).ToList();
            after.Count(p => p < 20).ShouldBe(askedEarly);
            after.ShouldContain(p => p >= 390);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Current_Page_Is_Highlighted_And_Followed()
    {
        using var document = new SimulatedPdfDocument(300);
        var viewer = Viewer();
        viewer.Document = document;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Pump();
            var list = viewer.Sidebar!.Thumbnails!;
            list.Realized.Single(t => t.IsCurrent).PageIndex.ShouldBe(0);

            viewer.GoToPage(200, animate: false);
            Pump();
            list.Realized.Single(t => t.IsCurrent).PageIndex.ShouldBe(200);
            var ring = list.Realized.Single(t => t.IsCurrent);
            ring.TranslatePoint(default, list.GetVisualAncestors().OfType<ScrollViewer>().First())!.Value.Y.ShouldBeGreaterThanOrEqualTo(-1, "brought into view");
            list.Realized.Single(t => t.IsCurrent).Classes.Contains(":current").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Activating_A_Thumbnail_Goes_To_Its_Page()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Pump();
            viewer.Sidebar!.Thumbnails!.Realized.First(t => t.PageIndex == 2).Activate();
            viewer.CurrentPageIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Thumbnail_Click_And_The_Keyboard_Work()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Pump();
            var list = viewer.Sidebar!.Thumbnails!;
            var thumbnail = list.Realized.First(t => t.PageIndex == 1);
            var center = thumbnail.TranslatePoint(new Point(thumbnail.Bounds.Width / 2, thumbnail.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Pump();
            viewer.CurrentPageIndex.ShouldBe(1);

            thumbnail.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Pump();
            var focused = (PdfThumbnail)window.FocusManager!.GetFocusedElement()!;
            focused.PageIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            viewer.CurrentPageIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Thumbnails_Follow_A_New_Document_And_A_Change_Of_Pages()
    {
        var source = new global::TKCode.Arc.Tests.Media.FakePageSource(10, new Size(100, 100));
        using var document = new SimulatedPdfDocument(3);
        var viewer = Viewer();
        viewer.PageSource = source;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        try
        {
            Pump();
            var list = viewer.Sidebar!.Thumbnails!;
            list.ItemCount.ShouldBe(10);
            source.SetPages(4, new Size(100, 100));
            Pump();
            list.ItemCount.ShouldBe(4);
            viewer.PageSource = null;
            viewer.Document = document;
            Pump();
            list.ItemCount.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Toolbar without a document

    private static Control ToolbarTools(PageViewer viewer) =>
        viewer.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_Tools");

    [AvaloniaFact]
    public void Without_A_Document_The_Toolbar_Keeps_Nothing_That_Needs_One()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer();
        var window = Show(viewer);
        try
        {
            void ShouldBeBare(string state)
            {
                viewer.PageCount.ShouldBe(0, state);
                ToolbarTools(viewer).IsEffectivelyVisible.ShouldBeFalse(state);
                ((Control)viewer.ToolbarStart!).IsVisible.ShouldBeFalse(state);
                ((Control)viewer.ToolbarEnd!).IsVisible.ShouldBeFalse(state);
                // Out of reach of the keyboard as well.
                viewer.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Where(b => b.IsEffectivelyVisible).ShouldNotContain(b => b.IsEffectivelyEnabled, state);
            }

            ShouldBeBare("empty");

            viewer.Source = Source();
            viewer.Status.ShouldBe(PdfViewerStatus.Loading);
            Pump();
            ShouldBeBare("loading");

            backend.Calls.Single().Result.SetException(new PdfException(PdfErrorKind.PasswordRequired));
            Settle(() => viewer.Status == PdfViewerStatus.PasswordRequired);
            Pump();
            viewer.PageCount.ShouldBe(0);
            ToolbarTools(viewer).IsEffectivelyVisible.ShouldBeFalse();
            ((Control)viewer.ToolbarEnd!).IsVisible.ShouldBeFalse();

            viewer.OnFormCancelled();
            Settle(() => viewer.Status == PdfViewerStatus.Failed);
            Pump();
            ShouldBeBare("failed");

            viewer.Source = Source("b.pdf");
            backend.Calls.Last().Result.SetResult(backend.NewDocument());
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            Pump();
            ToolbarTools(viewer).IsEffectivelyVisible.ShouldBeTrue();
            ((Control)viewer.ToolbarStart!).IsVisible.ShouldBeTrue();
            ((Control)viewer.ToolbarEnd!).IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Open_Sidebar_And_Search_Bar_Wait_For_The_Document()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = Viewer(Source());
        viewer.IsSidebarOpen = true;
        viewer.IsSearchOpen = true;
        var window = Show(viewer);
        try
        {
            Pump();
            viewer.Status.ShouldBe(PdfViewerStatus.Loading);
            viewer.IsSidebarOpen.ShouldBeTrue();
            viewer.IsSearchOpen.ShouldBeTrue();
            viewer.GetVisualDescendants().OfType<PdfSidebar>().Where(s => s.IsEffectivelyVisible).ShouldBeEmpty();
            viewer.GetVisualDescendants().OfType<PdfSearchBar>().Where(s => s.IsEffectivelyVisible).ShouldBeEmpty();

            backend.Calls.Single().Result.SetResult(backend.NewDocument());
            Settle(() => viewer.Status == PdfViewerStatus.Ready);
            Pump();
            viewer.GetVisualDescendants().OfType<PdfSidebar>().Single().IsEffectivelyVisible.ShouldBeTrue();
            viewer.GetVisualDescendants().OfType<PdfSearchBar>().Single().IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Attached property

    [AvaloniaFact]
    public void Pdf_Source_Opens_Once_And_Goes_On_Through_A_Move()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = new PageViewer { Width = 900, Height = 600 };
        PdfAttached.SetSource(viewer, Source());
        var first = new Panel { Children = { viewer } };
        var second = new Panel();
        var window = Show(new Panel { Children = { first, second } });
        try
        {
            var call = backend.Calls.Single();
            first.Children.Clear();
            second.Children.Add(viewer);
            Pump();
            backend.Calls.Count.ShouldBe(1);
            call.Token.IsCancellationRequested.ShouldBeFalse();

            second.Children.Clear();
            Pump();
            call.Token.IsCancellationRequested.ShouldBeTrue();
            first.Children.Add(viewer);
            backend.Calls.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pdf_Source_Opens_In_A_Plain_PageViewer_And_Closes_On_Change()
    {
        var backend = new FakePdfBackend();
        using var scope = new BackendScope(backend);
        var viewer = new PageViewer { Width = 800, Height = 600 };
        PdfAttached.SetSource(viewer, Source("a.pdf"));
        backend.Calls.ShouldBeEmpty("nothing opens outside a tree");
        var window = Show(viewer);
        try
        {
            Settle(() => viewer.PageCount == 12);
            var first = backend.Documents.Single();
            viewer.PageSource.ShouldBeSameAs(first);

            PdfAttached.SetSource(viewer, Source("b.pdf"));
            first.IsDisposed.ShouldBeTrue();
            Settle(() => backend.Documents.Count == 2 && viewer.PageSource == backend.Documents[1]);

            PdfAttached.SetSource(viewer, null);
            backend.Documents[1].IsDisposed.ShouldBeTrue();
            viewer.PageSource.ShouldBeNull();
            viewer.PageCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pdf_Source_Closes_With_The_Viewer_And_Cancels_A_Superseded_Open()
    {
        var backend = new FakePdfBackend { Hold = true };
        using var scope = new BackendScope(backend);
        var viewer = new PageViewer { Width = 800, Height = 600 };
        PdfAttached.SetSource(viewer, Source("a.pdf"));
        var window = Show(viewer);
        SimulatedPdfDocument? shown = null;
        try
        {
            var old = backend.Calls.Single();
            PdfAttached.SetSource(viewer, Source("b.pdf"));
            old.Token.IsCancellationRequested.ShouldBeTrue();
            var late = backend.NewDocument();
            old.Result.SetResult(late);
            Settle(() => late.IsDisposed);
            viewer.PageSource.ShouldBeNull();

            shown = backend.NewDocument();
            backend.Calls[1].Result.SetResult(shown);
            Settle(() => viewer.PageSource == shown);
        }
        finally
        {
            window.Close();
        }
        Dispatcher.UIThread.RunJobs();
        shown!.IsDisposed.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Pdf_Source_Reports_Failures()
    {
        var viewer = new PageViewer { Width = 800, Height = 600 };
        PdfAttached.SetSource(viewer, Source());
        var window = Show(viewer);
        try
        {
            Settle(() => PdfAttached.GetError(viewer) is not null);
            PdfAttached.GetError(viewer)!.Kind.ShouldBe(PdfErrorKind.BackendUnavailable);
            viewer.PageCount.ShouldBe(0);
            PdfAttached.SetSource(viewer, null);
            PdfAttached.GetError(viewer).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Accessibility and languages

    [AvaloniaFact]
    public async Task The_Automation_Peer_Reads_The_Current_Page()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        var window = Show(viewer);
        try
        {
            Pump();
            var peer = ControlAutomationPeer.CreatePeerForElement(viewer).ShouldBeOfType<PdfViewerAutomationPeer>();
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Document);
            peer.GetName().ShouldBe("Page 1 of 12");
            var first = (await document.GetPageTextAsync(0, default)).Text;
            Settle(() => peer.GetHelpText() == first);

            viewer.GoToPage(1, animate: false);
            var second = (await document.GetPageTextAsync(1, default)).Text;
            Settle(() => peer.GetHelpText() == second);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Localized_Text_Is_There_In_Every_Language()
    {
        var languages = Localization.ArcLanguageManager.Current!;
        try
        {
            foreach (var (tag, word) in new[] { (Localization.LanguageTags.ZhCN, "缩略图"), (Localization.LanguageTags.ZhTW, "縮圖"), (Localization.LanguageTags.EnUS, "Thumbnails") })
            {
                languages.Set(tag);
                Localization.PdfViewerLangResources.Get(Localization.PdfViewerLangResourceKind.Thumbnails).ShouldBe(word);
            }
        }
        finally
        {
            languages.Set(Localization.LanguageTags.EnUS);
        }
    }

    [AvaloniaFact]
    public void Sidebar_Tab_Labels_Follow_The_Language()
    {
        using var document = new SimulatedPdfDocument();
        var viewer = Viewer();
        viewer.Document = document;
        viewer.IsSidebarOpen = true;
        var window = Show(viewer);
        var languages = Localization.ArcLanguageManager.Current!;
        try
        {
            Settle(() => viewer.Sidebar?.Tabs?.IsVisible == true);
            Pump();
            var tabs = viewer.Sidebar!.Tabs!;
            ((SegmentedOption)tabs.Items[0]!).Label.ShouldBe("Thumbnails");
            languages.Set(Localization.LanguageTags.ZhCN);
            Pump();
            ((SegmentedOption)tabs.Items[1]!).Label.ShouldBe("目录");
        }
        finally
        {
            languages.Set(Localization.LanguageTags.EnUS);
            window.Close();
        }
    }

    #endregion

    #region Light and dark

    [AvaloniaFact]
    public async Task Renders_In_Both_Appearances_In_Every_State()
    {
        var backend = new FakePdfBackend { RequiredPassword = "sesame" };
        using var scope = new BackendScope(backend);
        foreach (var dark in new[] { false, true })
        {
            await TestHelpers.ApplyAsync(dark ? TestHelpers.Dark : null);
            using var document = new SimulatedPdfDocument();
            var viewer = Viewer();
            viewer.Document = document;
            viewer.IsSidebarOpen = true;
            var window = Show(viewer);
            try
            {
                Pump();
                window.CaptureRenderedFrame()?.Dispose();
                viewer.Source = Source();
                Settle(() => viewer.StateLayer?.Form is not null);
                Pump();
                window.CaptureRenderedFrame()?.Dispose();
                viewer.StateLayer!.Form!.Cancel();
                Settle(() => viewer.Status == PdfViewerStatus.Failed);
                Pump();
                window.CaptureRenderedFrame()?.Dispose();
            }
            finally
            {
                window.Close();
            }
        }
        await TestHelpers.ApplyAsync(null);
    }

    #endregion

    private sealed class BrokenOutlineDocument : PdfDocument
    {
        private readonly SimulatedPdfDocument _inner = new(2);

        public override int PageCount => _inner.PageCount;

        public override PdfDocumentInfo Info => _inner.Info;

        public override Size GetPageSize(int pageIndex) => _inner.GetPageSize(pageIndex);

        public override ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken) => _inner.RenderAsync(request, cancellationToken);

        public override string? GetPageLabel(int pageIndex) => null;

        public override Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Broken.");

        public override Task<PdfPageText> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken) => _inner.GetPageTextAsync(pageIndex, cancellationToken);

        public override Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken) => _inner.GetLinksAsync(pageIndex, cancellationToken);

        public override void Dispose() => _inner.Dispose();
    }
}
