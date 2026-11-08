using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Pdf;

/// <summary>Search, selection and links of <see cref="PdfViewer"/> through real pointer and key input.</summary>
public class PdfViewerInteractionTests
{
    private sealed class Scene : IDisposable
    {
        public Scene(PdfDocument document, double width = 900, double height = 700, Action<PdfViewer>? setup = null)
        {
            Viewer = new PdfViewer { Width = width, Height = height, Document = document, FitMode = PageFitMode.None, Zoom = 1 };
            setup?.Invoke(Viewer);
            Window = TestHelpers.Show(Viewer, ArcMotionLevel.Off);
            Window.Width = Math.Max(1000, width + 50);
            Window.Height = Math.Max(800, height + 50);
            Pump();
            Presenter = Viewer.GetVisualDescendants().OfType<PageViewerPresenter>().Single();
        }

        public PdfViewer Viewer { get; }

        public Window Window { get; }

        internal PageViewerPresenter Presenter { get; }

        /// <summary>A point of a page as a point of the window.</summary>
        public Point At(int page, Point inPage)
        {
            var viewport = Presenter.ViewportSize;
            var content = Viewer.Layout.ToContent(page, inPage, viewport);
            return Presenter.TranslatePoint(content - Presenter.Offset, Window)!.Value;
        }

        public void Move(int page, Point inPage, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.MouseMove(At(page, inPage), modifiers);
            Dispatcher.UIThread.RunJobs();
        }

        public void Press(int page, Point inPage, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.MouseMove(At(page, inPage), modifiers);
            Window.MouseDown(At(page, inPage), MouseButton.Left, modifiers);
            Dispatcher.UIThread.RunJobs();
        }

        public void Drag(int page, Point inPage, RawInputModifiers modifiers = RawInputModifiers.None) =>
            Move(page, inPage, modifiers | RawInputModifiers.LeftMouseButton);

        public void Release(int page, Point inPage, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.MouseUp(At(page, inPage), MouseButton.Left, modifiers);
            Dispatcher.UIThread.RunJobs();
        }

        public void Click(int page, Point inPage, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Press(page, inPage, modifiers);
            Release(page, inPage, modifiers);
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Puts the keyboard on the pages, as a click there does.</summary>
        public void FocusPages()
        {
            Presenter.Focus();
            Dispatcher.UIThread.RunJobs();
        }

        public void Pump()
        {
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }

    private static void Until(Func<bool> condition) => TestHelpers.PumpUntil(condition).ShouldBeTrue();

    /// <summary>Lets the text of a page arrive by pointing at it.</summary>
    private static void Enter(Scene scene, int page = 0)
    {
        scene.Move(page, new Point(10, 10));
        Until(() => scene.Viewer.TryGetText(page) is not null);
    }

    /// <summary>Whether a result is inside the viewport.</summary>
    private static bool IsInView(Scene scene, PdfSearchMatch match)
    {
        var viewport = scene.Presenter.ViewportSize;
        var top = scene.Viewer.Layout.ToContent(match.PageIndex, match.Bounds.TopLeft, viewport).Y - scene.Presenter.Offset.Y;
        var bottom = scene.Viewer.Layout.ToContent(match.PageIndex, match.Bounds.BottomLeft, viewport).Y - scene.Presenter.Offset.Y;
        return top >= 0 && bottom <= viewport.Height;
    }

    private static Point Right(Point point) => new(point.X + ScriptedPdfDocument.CharWidth / 2 - 0.5, point.Y);

    private static Point Left(Point point) => new(point.X - ScriptedPdfDocument.CharWidth / 2 + 0.5, point.Y);

    #region Search

    private static string[] FourPages() =>
    [
        "apple one\nbanana",
        "nothing here",
        "apple two\nand apple three",
        "the last apple"
    ];

    [AvaloniaFact]
    public void Search_Waits_For_The_Typing_To_Stop_And_Then_Counts()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        viewer.SearchText = "a";
        viewer.SearchText = "ap";
        viewer.SearchText = "apple";
        viewer.IsSearchPending.ShouldBeTrue();
        viewer.IsSearching.ShouldBeFalse();
        Dispatcher.UIThread.RunJobs();
        document.TextReads.ShouldBeEmpty();

        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        viewer.CurrentSearchMatch.ShouldBe(0);
        document.TextCallsOnThreadPool.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void First_Hit_Moves_The_View_There()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document, height: 500);
        var viewer = scene.Viewer;
        viewer.SearchText = "three";
        Until(() => viewer.SearchMatchCount == 1 && !viewer.IsSearching);
        viewer.CurrentSearchMatch.ShouldBe(0);
        viewer.CurrentMatch!.PageIndex.ShouldBe(2);
        IsInView(scene, viewer.CurrentMatch).ShouldBeTrue();
        scene.Presenter.Offset.Y.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void After_A_Jump_To_A_Page_The_Spring_To_A_Result_Still_Updates_The_Current_Page()
    {
        using var document = new ScriptedPdfDocument("one", "two", "three", "four", "five", "six apple");
        var viewer = new PdfViewer { Width = 900, Height = 500, Document = document, FitMode = PageFitMode.None, Zoom = 1 };
        var window = TestHelpers.Show(viewer, ArcMotionLevel.Full);
        window.Width = 1000;
        window.Height = 800;
        try
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            viewer.GoToPage(1, animate: false);
            Dispatcher.UIThread.RunJobs();
            viewer.SearchText = "apple";
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (viewer.CurrentPageIndex < 4 && DateTime.UtcNow < deadline)
            {
                TestHelpers.PumpUntil(() => false, 20);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            viewer.SearchMatchCount.ShouldBe(1);
            viewer.CurrentPageIndex.ShouldBeGreaterThanOrEqualTo(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Next_And_Previous_Go_Around()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        viewer.SearchNext();
        viewer.CurrentSearchMatch.ShouldBe(1);
        viewer.SearchNext();
        viewer.SearchNext();
        viewer.CurrentSearchMatch.ShouldBe(3);
        viewer.SearchNext();
        viewer.CurrentSearchMatch.ShouldBe(0);
        viewer.SearchPrevious();
        viewer.CurrentSearchMatch.ShouldBe(3);
        viewer.CurrentMatch!.PageIndex.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Setting_The_Current_Match_Scrolls_To_It_And_Is_Clamped()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document, height: 500);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        IsInView(scene, viewer.CurrentMatch!).ShouldBeTrue();
        viewer.CurrentSearchMatch = 3;
        viewer.CurrentMatch!.PageIndex.ShouldBe(3);
        IsInView(scene, viewer.CurrentMatch).ShouldBeTrue();
        viewer.CurrentSearchMatch = 99;
        viewer.CurrentSearchMatch.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Scanning_Shows_A_Growing_Count_With_A_Plus()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        document.HoldText = true;
        using var scene = new Scene(document, setup: v => v.IsSearchOpen = true);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => viewer.IsSearching && document.TextReads.Count == 1);
        viewer.SearchBar!.CountText.ShouldBe("0 / 0+");
        document.Release(0);
        Until(() => viewer.SearchMatchCount == 1);
        viewer.IsSearching.ShouldBeTrue();
        viewer.SearchBar.CountText.ShouldBe("1 / 1+");
        document.ReleaseAll();
        Until(() => document.TextReads.Count >= 2);
        document.Release(1);
        Until(() => document.TextReads.Count >= 3);
        document.Release(2);
        Until(() => document.TextReads.Count >= 4);
        document.Release(3);
        Until(() => !viewer.IsSearching);
        viewer.SearchMatchCount.ShouldBe(4);
        viewer.SearchBar.CountText.ShouldBe("1 / 4");
    }

    [AvaloniaFact]
    public void Numbers_Follow_The_Document_Though_The_Scan_Starts_Later()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document, height: 500);
        var viewer = scene.Viewer;
        viewer.GoToPage(2, animate: false);
        scene.Pump();
        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        // The first find was on page 2, and it stays the current one after page 0 was added ahead of it.
        viewer.CurrentMatch!.PageIndex.ShouldBe(2);
        viewer.CurrentSearchMatch.ShouldBe(1);
        document.TextReads.ShouldBe([2, 3, 0, 1]);
        viewer.SearchPrevious();
        viewer.CurrentMatch!.PageIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public void New_Input_Replaces_The_Scan_In_Progress()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        document.HoldText = true;
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => document.TextReads.Count == 1);
        viewer.SearchText = "banana";
        Until(() => !viewer.IsSearchPending);
        document.ReleaseAll();
        Until(() => !viewer.IsSearching);
        viewer.SearchMatchCount.ShouldBe(1);
        viewer.CurrentMatch!.Start.ShouldBe(10);
    }

    [AvaloniaFact]
    public void Options_Search_Again_At_Once()
    {
        using var document = new ScriptedPdfDocument("Apple apple pineapple");
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 3 && !viewer.IsSearching);
        viewer.IsMatchCase = true;
        viewer.IsSearching.ShouldBeTrue();
        Until(() => viewer.SearchMatchCount == 2 && !viewer.IsSearching);
        viewer.IsWholeWord = true;
        Until(() => viewer.SearchMatchCount == 1 && !viewer.IsSearching);
        viewer.CurrentMatch!.Start.ShouldBe(6);
    }

    [AvaloniaFact]
    public void Clear_Search_Empties_Everything()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document, setup: v => v.IsSearchOpen = true);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 4);
        viewer.ClearSearch();
        viewer.SearchText.ShouldBe(string.Empty);
        viewer.SearchMatchCount.ShouldBe(0);
        viewer.CurrentSearchMatch.ShouldBe(-1);
        viewer.IsSearching.ShouldBeFalse();
        viewer.IsSearchOpen.ShouldBeTrue();
        viewer.SearchBar!.CountText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Closing_The_Bar_Takes_The_Highlights_Away_And_Opening_Brings_Them_Back()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document, setup: v => v.IsSearchOpen = true);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        viewer.IsSearchOpen = false;
        viewer.SearchMatchCount.ShouldBe(0);
        viewer.SearchText.ShouldBe("apple");
        viewer.IsSearchOpen = true;
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
    }

    [AvaloniaFact]
    public void A_New_Document_Is_Searched_And_A_Closed_One_Stops_The_Scan()
    {
        var first = new ScriptedPdfDocument(FourPages()) { HoldText = true };
        using var second = new ScriptedPdfDocument("apple", "apple");
        using var scene = new Scene(first);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => first.TextReads.Count == 1);
        viewer.Document = second;
        first.Dispose();
        Until(() => viewer.SearchMatchCount == 2 && !viewer.IsSearching);
        viewer.Document = null;
        Dispatcher.UIThread.RunJobs();
        viewer.SearchMatchCount.ShouldBe(0);
        viewer.IsSearching.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Disposing_The_Document_During_A_Scan_Does_Not_Throw()
    {
        var document = new ScriptedPdfDocument(FourPages()) { HoldText = true };
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => document.TextReads.Count == 1);
        document.Dispose();
        Until(() => !viewer.IsSearching);
        viewer.SearchMatchCount.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Toolbar_Toggle_Opens_The_Bar_And_Needs_Pages()
    {
        var viewer = new PdfViewer { Width = 900, Height = 600 };
        var window = TestHelpers.Show(viewer);
        try
        {
            var toggle = (PdfToolbarToggle)viewer.ToolbarEnd!;
            toggle.IsEnabled.ShouldBeFalse();
            using var document = new SimulatedPdfDocument(2);
            viewer.Document = document;
            toggle.IsEnabled.ShouldBeTrue();
            viewer.SearchBar.ShouldBeNull();
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            viewer.IsSearchOpen.ShouldBeTrue();
            viewer.Classes.Contains(":search-open").ShouldBeTrue();
            toggle.IsActive.ShouldBeTrue();
            viewer.SearchBar.ShouldNotBeNull();
            AutomationProperties.GetName(toggle).ShouldBe("Search");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_In_The_Bar_Searches_And_Its_Buttons_Work()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document, setup: v => v.IsSearchOpen = true);
        var viewer = scene.Viewer;
        scene.Pump();
        var bar = viewer.SearchBar!;
        bar.InputField!.Text = "APPLE";
        Until(() => viewer.SearchText == "APPLE");
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        bar.CountText.ShouldBe("1 / 4");

        Button Part(string name) => bar.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);
        void Press(string name) => Part(name).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Press(PdfSearchBar.NextPart);
        viewer.CurrentSearchMatch.ShouldBe(1);
        bar.CountText.ShouldBe("2 / 4");
        Press(PdfSearchBar.PreviousPart);
        viewer.CurrentSearchMatch.ShouldBe(0);
        Press(PdfSearchBar.MatchCasePart);
        viewer.IsMatchCase.ShouldBeTrue();
        Until(() => viewer.SearchMatchCount == 0 && !viewer.IsSearching);
        bar.CountText.ShouldBe("No results");
        Part(PdfSearchBar.NextPart).IsEnabled.ShouldBeFalse();
        Press(PdfSearchBar.WholeWordPart);
        viewer.IsWholeWord.ShouldBeTrue();
        Press(PdfSearchBar.ClosePart);
        viewer.IsSearchOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Result_Count_Is_Announced_Politely_And_Not_On_Every_Page()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        document.HoldText = true;
        using var scene = new Scene(document, setup: v => v.IsSearchOpen = true);
        var viewer = scene.Viewer;
        scene.Pump();
        var bar = viewer.SearchBar!;
        var count = bar.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == PdfSearchBar.CountPart);
        AutomationProperties.GetLiveSetting(count).ShouldBe(AutomationLiveSetting.Polite);
        viewer.SearchText = "apple";
        Until(() => document.TextReads.Count == 1);
        document.Release(0);
        Until(() => viewer.SearchMatchCount == 1);
        bar.Announcement.ShouldBeNullOrEmpty();
        document.HoldText = false;
        document.ReleaseAll();
        Until(() => !viewer.IsSearching);
        bar.Announcement.ShouldBe("1 of 4");
        viewer.SearchNext();
        bar.Announcement.ShouldBe("2 of 4");
        viewer.IsMatchCase = true;
        viewer.SearchText = "nothing like it";
        Until(() => !viewer.IsSearchPending && !viewer.IsSearching);
        bar.Announcement.ShouldBe("No results");
    }

    [AvaloniaFact]
    public void Narrow_Viewer_Stretches_The_Bar_Inside_Itself()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document, width: 320, setup: v => v.IsSearchOpen = true);
        var viewer = scene.Viewer;
        scene.Pump();
        var bar = viewer.SearchBar!;
        bar.Classes.Contains(":narrow").ShouldBeTrue();
        var corner = bar.TranslatePoint(new Point(0, 0), viewer)!.Value;
        corner.X.ShouldBeGreaterThanOrEqualTo(0);
        bar.Bounds.Width.ShouldBe(320 - 16, 1);
        bar.InputField!.Bounds.Width.ShouldBeGreaterThan(50);
        foreach (var button in bar.GetVisualDescendants().OfType<Button>())
        {
            var right = button.TranslatePoint(new Point(button.Bounds.Width, 0), viewer)!.Value.X;
            right.ShouldBeLessThanOrEqualTo(320);
        }
    }

    [AvaloniaFact]
    public void Wide_Viewer_Keeps_The_Bar_At_Its_Width_At_The_Top_Right()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document, setup: v => v.IsSearchOpen = true);
        var viewer = scene.Viewer;
        scene.Pump();
        var bar = viewer.SearchBar!;
        bar.Classes.Contains(":narrow").ShouldBeFalse();
        bar.Bounds.Width.ShouldBe(360);
        bar.TranslatePoint(new Point(bar.Bounds.Width, 0), viewer)!.Value.X.ShouldBe(900 - 16, 1);
    }

    [AvaloniaFact]
    public void Keys_Open_Step_And_Close_The_Search()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        scene.FocusPages();
        scene.Key(PhysicalKey.F, RawInputModifiers.Control);
        viewer.IsSearchOpen.ShouldBeTrue();
        scene.Pump();
        viewer.SearchBar!.InputField!.IsFocused.ShouldBeTrue();

        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        // In the field: Enter and Shift+Enter.
        scene.Key(PhysicalKey.Enter);
        viewer.CurrentSearchMatch.ShouldBe(1);
        scene.Key(PhysicalKey.Enter, RawInputModifiers.Shift);
        viewer.CurrentSearchMatch.ShouldBe(0);
        // Anywhere: F3 and Shift+F3.
        scene.Key(PhysicalKey.F3);
        viewer.CurrentSearchMatch.ShouldBe(1);
        scene.Key(PhysicalKey.F3, RawInputModifiers.Shift);
        viewer.CurrentSearchMatch.ShouldBe(0);
        // Cmd works for Ctrl, and asking again goes back to the field.
        scene.FocusPages();
        scene.Key(PhysicalKey.Enter);
        viewer.CurrentSearchMatch.ShouldBe(1);
        scene.Key(PhysicalKey.F, RawInputModifiers.Meta);
        scene.Pump();
        viewer.SearchBar.InputField.IsFocused.ShouldBeTrue();
        // Escape closes it and gives the keyboard back to the pages.
        scene.Key(PhysicalKey.Escape);
        viewer.IsSearchOpen.ShouldBeFalse();
        scene.Presenter.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Enter_In_The_Page_Box_Is_Not_A_Search_Key()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        viewer.IsSearchOpen = true;
        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        var box = viewer.GetVisualDescendants().OfType<Input>().First(i => i.Name == PageViewerToolbar.PageBoxPart);
        box.Focus();
        Dispatcher.UIThread.RunJobs();
        scene.Key(PhysicalKey.Enter);
        viewer.CurrentSearchMatch.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Search_Text_Works_With_The_Bar_Closed()
    {
        using var document = new ScriptedPdfDocument(FourPages());
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        viewer.SearchText = "apple";
        Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        viewer.SearchBar.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Viewer_Leaving_The_Tree_Pauses_The_Scan()
    {
        using var document = new ScriptedPdfDocument(FourPages()) { HoldText = true };
        var viewer = new PdfViewer { Width = 900, Height = 600, Document = document };
        var host = new Border { Child = viewer };
        var window = TestHelpers.Show(host);
        try
        {
            viewer.SearchText = "apple";
            Until(() => document.TextReads.Count == 1);
            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            viewer.IsSearching.ShouldBeFalse();
            document.HoldText = false;
            document.ReleaseAll();
            host.Child = viewer;
            Until(() => viewer.SearchMatchCount == 4 && !viewer.IsSearching);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Selection

    private const string Line1 = "the quick brown fox";
    private const string Line2 = "jumps over the lazy dog";

    [AvaloniaFact]
    public void Text_Is_Read_Only_When_The_Pointer_Enters_A_Page()
    {
        using var document = new ScriptedPdfDocument(Line1, Line2);
        using var scene = new Scene(document);
        scene.Pump();
        document.TextReads.ShouldBeEmpty();
        scene.Move(0, new Point(10, 10));
        scene.Move(0, new Point(20, 10));
        document.TextReads.ShouldBe([0]);
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Drag_Selects_What_It_Passes()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        Enter(scene);
        var q = document.PointOf(0, "quick");
        var k = document.PointOf(0, "k brown");
        scene.Press(0, Left(q));
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Drag(0, Right(k));
        scene.Viewer.SelectedText.ShouldBe("quick");
        scene.Release(0, Right(k));
        scene.Viewer.SelectedText.ShouldBe("quick");
    }

    [AvaloniaFact]
    public void Drag_Backwards_Selects_The_Same()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        Enter(scene);
        var q = document.PointOf(0, "quick");
        var k = document.PointOf(0, "k brown");
        scene.Press(0, Right(k));
        scene.Drag(0, Left(q));
        scene.Release(0, Left(q));
        scene.Viewer.SelectedText.ShouldBe("quick");
    }

    [AvaloniaFact]
    public void A_Plain_Click_Clears_And_Selects_Nothing()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        Enter(scene);
        scene.Viewer.SelectAll();
        scene.Viewer.SelectedText.ShouldBe(Line1);
        scene.Click(0, document.PointOf(0, "brown"));
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Viewer.SelectionLayer.HasSelection.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Press_On_The_Canvas_Clears_The_Selection()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document, width: 1400);
        Enter(scene);
        scene.Viewer.SelectAll();
        scene.Viewer.SelectedText.ShouldBe(Line1);
        var left = scene.Presenter.TranslatePoint(new Point(5, 200), scene.Window)!.Value;
        scene.Window.MouseMove(left);
        scene.Window.MouseDown(left, MouseButton.Left);
        scene.Window.MouseUp(left, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Double_Click_Selects_A_Word_And_Triple_Click_A_Line()
    {
        using var document = new ScriptedPdfDocument(Line1 + "\n" + Line2);
        using var scene = new Scene(document);
        Enter(scene);
        var point = document.PointOf(0, "over");
        scene.Click(0, point);
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Click(0, point);
        scene.Viewer.SelectedText.ShouldBe("over");
        scene.Click(0, point);
        scene.Viewer.SelectedText.ShouldBe(Line2);
    }

    [AvaloniaFact]
    public void Shift_Click_Extends_From_Where_The_Selection_Began()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        Enter(scene);
        var q = document.PointOf(0, "quick");
        scene.Click(0, Left(q));
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        // Far enough not to count as a double click's second press.
        scene.Window.MouseMove(scene.At(0, new Point(500, 500)));
        var n = document.PointOf(0, "n fox");
        scene.Click(0, Right(n), RawInputModifiers.Shift);
        scene.Viewer.SelectedText.ShouldBe("quick brown");
        var w = document.PointOf(0, "quick");
        scene.Click(0, Right(new Point(w.X + ScriptedPdfDocument.CharWidth * 4, w.Y)), RawInputModifiers.Shift);
        scene.Viewer.SelectedText.ShouldBe("quick");
    }

    [AvaloniaFact]
    public void Selection_Runs_Across_Pages_In_Order_With_A_Break_Between_Them()
    {
        using var document = new ScriptedPdfDocument(Line1, "middle page", Line2);
        using var scene = new Scene(document, height: 500);
        Enter(scene);
        var from = document.PointOf(0, "brown");
        scene.Press(0, Left(from));
        scene.Drag(0, new Point(from.X, from.Y + 1));
        scene.Move(0, Left(from), RawInputModifiers.LeftMouseButton);
        // Over two pages in one drag, though the pointer stays in the window: the layer takes the nearest page.
        var to = document.PointOf(2, "over");
        scene.Viewer.SelectionLayer.IsDragging.ShouldBeTrue();
        scene.Presenter.ScrollTo(new Vector(0, scene.Viewer.Layout.GetPageRect(2, scene.Presenter.ViewportSize).Y), animate: false);
        scene.Pump();
        scene.Drag(2, Right(new Point(to.X + ScriptedPdfDocument.CharWidth * 3, to.Y)));
        Until(() => scene.Viewer.SelectedText.Length > 0 && !document.TextReads.Contains(99) && scene.Viewer.SelectionLayer.WhenComplete.IsCompleted);
        scene.Viewer.SelectedText.ShouldBe("brown fox\nmiddle page\njumps over");
        scene.Release(2, to);
        document.TextReads.Distinct().Order().ShouldBe([0, 1, 2]);
    }

    [AvaloniaFact]
    public void Dragging_Back_Forgets_The_Pages_Left_Behind()
    {
        using var document = new ScriptedPdfDocument(Line1, Line2);
        using var scene = new Scene(document, height: 1400);
        Enter(scene);
        Enter(scene, 1);
        var from = document.PointOf(0, "fox");
        scene.Press(0, Left(from));
        var to = document.PointOf(1, "over");
        scene.Drag(1, Left(to));
        scene.Viewer.SelectedText.ShouldBe("fox\njumps ");
        scene.Drag(0, Right(from));
        scene.Viewer.SelectedText.ShouldBe("f");
    }

    [AvaloniaFact]
    public void A_Drag_Off_The_Bottom_Of_A_Page_Takes_The_Rest_Of_It()
    {
        using var document = new ScriptedPdfDocument(Line1 + "\n" + Line2, "next");
        using var scene = new Scene(document, height: 1400);
        Enter(scene);
        var from = document.PointOf(0, "quick");
        scene.Press(0, Left(from));
        var size = document.GetPageSize(0);
        // The gap under the page: the nearest page is still this one, with a point below its bottom.
        var gap = scene.At(0, new Point(from.X, size.Height + 4));
        scene.Window.MouseMove(gap, RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        scene.Viewer.SelectedText.ShouldBe("quick brown fox\n" + Line2);
    }

    [AvaloniaFact]
    public void A_Press_Before_The_Text_Has_Arrived_Waits_And_Then_Selects_Rightly()
    {
        using var document = new ScriptedPdfDocument(Line1) { HoldText = true };
        using var scene = new Scene(document);
        var q = document.PointOf(0, "quick");
        var k = document.PointOf(0, "k brown");

        scene.Press(0, Left(q));
        scene.Drag(0, Right(k));
        // Nothing to go by yet: nothing is selected, nothing throws, and the cursor stays out of it until there is text.
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Release(0, Right(k));
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        document.TextReads.ShouldBe([0]);

        document.Release(0);
        Until(() => scene.Viewer.SelectedText.Length > 0);
        scene.Viewer.SelectedText.ShouldBe("quick");
    }

    [AvaloniaFact]
    public void A_Click_And_A_Double_Click_On_A_Page_Still_Loading_Do_No_Harm()
    {
        using var document = new ScriptedPdfDocument(Line1) { HoldText = true };
        using var scene = new Scene(document);
        var q = document.PointOf(0, "quick");
        scene.Click(0, q);
        scene.Click(0, q);
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Viewer.SelectAll();
        scene.Viewer.ClearSelection();
        document.Release(0);
        Dispatcher.UIThread.RunJobs();
        Until(() => scene.Viewer.TryGetText(0) is not null);
        scene.Pump();
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Middle_Pages_Are_Read_In_The_Background_And_Copy_Waits_For_Them()
    {
        using var document = new ScriptedPdfDocument("page zero", "page one", "page two");
        using var scene = new Scene(document, height: 1400, setup: v => v.Zoom = 0.4);
        Enter(scene, 0);
        Enter(scene, 2);
        document.HoldText = true;
        var from = document.PointOf(0, "zero");
        scene.Press(0, Left(from));
        scene.Drag(2, Right(document.PointOf(2, "page")));
        scene.Release(2, Right(document.PointOf(2, "page")));
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Viewer.SelectionLayer.WhenComplete.IsCompleted.ShouldBeFalse();
        var copy = scene.Viewer.CopySelectionAsync();
        copy.IsCompleted.ShouldBeFalse();
        document.Release(1);
        Until(() => copy.IsCompleted);
        copy.Result.ShouldBeTrue();
        scene.Viewer.SelectedText.ShouldBe("zero\npage one\np");
    }

    [AvaloniaFact]
    public void A_Selection_Never_Splits_A_Surrogate_Pair()
    {
        const string text = "ab\U00020BB7cd";
        using var document = new ScriptedPdfDocument(text);
        using var scene = new Scene(document);
        Enter(scene);
        var pair = document.PointOf(0, 2);
        scene.Press(0, Right(pair));
        scene.Drag(0, new Point(ScriptedPdfDocument.Left + 7 * 10, pair.Y));
        scene.Release(0, new Point(ScriptedPdfDocument.Left + 7 * 10, pair.Y));
        scene.Viewer.SelectedText.ShouldBe("cd");

        scene.Viewer.ClearSelection();
        scene.Window.MouseMove(scene.At(0, new Point(500, 500)));
        scene.Press(0, Left(pair));
        scene.Drag(0, Right(pair));
        scene.Release(0, Right(pair));
        scene.Viewer.SelectedText.ShouldBe("\U00020BB7");
    }

    [AvaloniaFact]
    public void Select_All_Takes_The_Current_Page_And_Waits_For_Its_Text()
    {
        using var document = new ScriptedPdfDocument("page zero\n", "page one") { HoldText = true };
        using var scene = new Scene(document);
        scene.Viewer.SelectAll();
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        document.Release(0);
        Until(() => scene.Viewer.SelectedText.Length > 0);
        // The page's final line break is not part of what a reader selected.
        scene.Viewer.SelectedText.ShouldBe("page zero");
    }

    [AvaloniaFact]
    public void Keys_Select_All_Copy_And_Clear()
    {
        using var document = new ScriptedPdfDocument(Line1, Line2);
        using var scene = new Scene(document);
        Enter(scene);
        scene.FocusPages();
        scene.Key(PhysicalKey.A, RawInputModifiers.Control);
        Until(() => scene.Viewer.SelectedText == Line1);
        scene.Key(PhysicalKey.C, RawInputModifiers.Control);
        Until(() => ClipboardText(scene.Window) == Line1);
        scene.Key(PhysicalKey.Escape);
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Key(PhysicalKey.A, RawInputModifiers.Meta);
        Until(() => scene.Viewer.SelectedText == Line1);
    }

    private static string? ClipboardText(Window window)
    {
        var task = Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(window.Clipboard!);
        TestHelpers.PumpUntil(() => task.IsCompleted);
        return task.Result;
    }

    [AvaloniaFact]
    public void Select_All_Key_In_The_Search_Field_Stays_In_The_Field()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document, setup: v => v.IsSearchOpen = true);
        scene.Pump();
        scene.Viewer.SearchBar!.InputField!.IsFocused.ShouldBeTrue();
        scene.Key(PhysicalKey.A, RawInputModifiers.Control);
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Escape_Closes_The_Search_Before_It_Clears_The_Selection()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document, setup: v => v.IsSearchOpen = true);
        Enter(scene);
        scene.Viewer.SelectAll();
        scene.FocusPages();
        scene.Key(PhysicalKey.Escape);
        scene.Viewer.IsSearchOpen.ShouldBeFalse();
        scene.Viewer.SelectedText.ShouldBe(Line1);
        scene.Key(PhysicalKey.Escape);
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Cursor_Is_An_I_Beam_Only_Over_Text()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        var viewer = scene.Viewer;
        var layer = viewer.SelectionLayer;
        var over = document.PointOf(0, "quick");
        viewer.LayerList[1].ShouldBeSameAs(layer);
        // Not known yet: arrow.
        scene.Move(0, over);
        Until(() => viewer.TryGetText(0) is not null);
        scene.Pump();
        scene.Move(0, over);
        scene.Presenter.Cursor.ShouldNotBeNull();
        var ibeam = scene.Presenter.Cursor;
        scene.Move(0, new Point(10, 10));
        scene.Presenter.Cursor.ShouldBeNull();
        scene.Move(0, over);
        scene.Presenter.Cursor.ShouldBeSameAs(ibeam);
    }

    [AvaloniaFact]
    public void Cursor_Updates_When_The_Text_Under_The_Pointer_Arrives()
    {
        using var document = new ScriptedPdfDocument(Line1) { HoldText = true };
        using var scene = new Scene(document);
        var over = document.PointOf(0, "quick");
        scene.Move(0, over);
        scene.Presenter.Cursor.ShouldBeNull();
        document.Release(0);
        Until(() => scene.Presenter.Cursor is not null);
    }

    [AvaloniaFact]
    public void Context_Menu_Copies_And_Selects_All()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        Enter(scene);
        var viewer = scene.Viewer;
        var point = scene.At(0, document.PointOf(0, "quick"));
        scene.Window.MouseMove(point);
        scene.Window.MouseDown(point, MouseButton.Right);
        scene.Window.MouseUp(point, MouseButton.Right);
        scene.Pump();
        var menu = viewer.PageContextMenu;
        menu.ShouldNotBeNull();
        menu.IsOpen.ShouldBeTrue();
        var entries = menu.Items.OfType<MenuEntry>().ToArray();
        entries.Select(e => e.Header).ShouldBe(["Copy", "Select all"]);
        entries.ShouldAllBe(e => e.Icon != null);
        entries[0].IsEnabled.ShouldBeFalse();
        menu.Close(returnFocus: false);
        scene.Pump();

        viewer.SelectAll();
        scene.Window.MouseDown(point, MouseButton.Right);
        scene.Window.MouseUp(point, MouseButton.Right);
        scene.Pump();
        menu.IsOpen.ShouldBeTrue();
        entries[0].IsEnabled.ShouldBeTrue();
        // A right press leaves the selection alone.
        viewer.SelectedText.ShouldBe(Line1);
    }

    [AvaloniaFact]
    public void Context_Menu_Follows_The_Language()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        PdfViewerLangResources.Get(PdfViewerLangResourceKind.Copy).ShouldNotBeNullOrEmpty();
        scene.Viewer.OpenContextMenu(new Point(100, 100));
        scene.Pump();
        scene.Viewer.PageContextMenu!.Items.OfType<MenuEntry>().First().Header.ShouldBe(PdfViewerLangResources.Get(PdfViewerLangResourceKind.Copy));
    }

    [AvaloniaFact]
    public void Pan_Mode_Neither_Selects_Nor_Follows_Links()
    {
        using var document = new ScriptedPdfDocument(Line1, "second");
        var target = new PdfDestination(1);
        var box = ScriptedPdfDocument.Layout(Line1).GetRects(0, 9)[0];
        document.Links[0] = [new PdfLink(box, target, null)];
        using var scene = new Scene(document, setup: v => v.InteractionMode = PageInteractionMode.Pan);
        var activated = 0;
        scene.Viewer.LinkActivated += (_, _) => activated++;
        scene.Pump();
        var q = document.PointOf(0, "quick");
        scene.Move(0, q);
        document.TextReads.ShouldBeEmpty();
        scene.Press(0, Left(q));
        scene.Drag(0, new Point(q.X + 40, q.Y));
        scene.Release(0, new Point(q.X + 40, q.Y));
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Click(0, q);
        activated.ShouldBe(0);
        scene.Viewer.LinkLayer.Hovered.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Disabling_Selection_Leaves_Links_Working_And_Stops_Reading_Text()
    {
        using var document = new ScriptedPdfDocument(Line1);
        var box = ScriptedPdfDocument.Layout(Line1).GetRects(0, 9)[0];
        document.Links[0] = [new PdfLink(box, null, new Uri("https://example.com/"))];
        using var scene = new Scene(document, setup: v => v.IsTextSelectionEnabled = false);
        var opened = new List<Uri>();
        scene.Viewer.UriOpener = uri =>
        {
            opened.Add(uri);
            return Task.FromResult(true);
        };
        scene.Pump();
        Until(() => scene.Viewer.LinkLayer.TryGetLinks(0) is not null);
        var q = document.PointOf(0, "the");
        scene.Move(0, q);
        document.TextReads.ShouldBeEmpty();
        scene.Click(0, q);
        opened.Count.ShouldBe(1);
        scene.Viewer.SelectAll();
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Turning_Selection_Off_Clears_It()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        Enter(scene);
        scene.Viewer.SelectAll();
        scene.Viewer.IsTextSelectionEnabled = false;
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void A_New_Document_Clears_The_Selection()
    {
        using var first = new ScriptedPdfDocument(Line1);
        using var second = new ScriptedPdfDocument(Line2);
        using var scene = new Scene(first);
        Enter(scene);
        scene.Viewer.SelectAll();
        scene.Viewer.Document = second;
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
        scene.Viewer.SelectionLayer.HasSelection.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Drag_Cut_Short_By_Leaving_The_Tree_Does_Not_Go_On()
    {
        using var document = new ScriptedPdfDocument(Line1);
        using var scene = new Scene(document);
        Enter(scene);
        var q = document.PointOf(0, "quick");
        scene.Press(0, Left(q));
        scene.Drag(0, Right(q));
        var selected = scene.Viewer.SelectedText;
        scene.Viewer.SelectionLayer.IsDragging.ShouldBeTrue();
        var content = scene.Window.Content;
        scene.Window.Content = null;
        Dispatcher.UIThread.RunJobs();
        scene.Viewer.SelectionLayer.IsDragging.ShouldBeFalse();
        scene.Window.Content = content;
        scene.Pump();
        scene.Move(0, new Point(q.X + 40, q.Y));
        scene.Viewer.SelectedText.ShouldBe(selected);
    }

    [AvaloniaFact]
    public void A_Page_That_Cannot_Be_Read_Selects_Nothing_Without_Trouble()
    {
        using var document = new ScriptedPdfDocument(Line1);
        document.Unreadable.Add(0);
        using var scene = new Scene(document);
        scene.Move(0, new Point(100, 100));
        Until(() => scene.Viewer.TryGetText(0) is not null);
        scene.Press(0, new Point(100, 100));
        scene.Drag(0, new Point(200, 100));
        scene.Release(0, new Point(200, 100));
        scene.Viewer.SelectedText.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(0xFF, 0x35, 0x35, 0x35)]
    [InlineData(0xFF, 0xBE, 0xBE, 0xBE)]
    [InlineData(0xFF, 0x6B, 0xA3, 0xFF)]
    [InlineData(0xFF, 0xFF, 0xFF, 0xFF)]
    public void Selection_Color_Shows_On_Paper_With_Any_Accent(byte a, byte r, byte g, byte b)
    {
        var color = PdfViewer.SelectionColorFor(Avalonia.Media.Color.FromArgb(a, r, g, b));
        color.A.ShouldBeLessThan((byte)0xC0);
        // Over white, a clear step down from the paper, and light enough to read black text through.
        var over = (color.R * color.A + 255 * (255 - color.A)) / 255.0;
        var overG = (color.G * color.A + 255 * (255 - color.A)) / 255.0;
        Math.Min(over, overG).ShouldBeLessThan(235);
        Math.Max(over, overG).ShouldBeGreaterThan(150);
    }

    #endregion

    #region Links

    private static (ScriptedPdfDocument Document, Rect Internal, Rect External) LinkDocument(Uri? uri = null)
    {
        const string text = "go to page two here\nvisit the site here\nplain words";
        var document = new ScriptedPdfDocument(text, "second page", "third page");
        var layout = ScriptedPdfDocument.Layout(text);
        var inside = layout.GetRects(0, 14)[0];
        var outside = layout.GetRects(20, 14)[0];
        document.Links[0] =
        [
            new PdfLink(inside, new PdfDestination(1, new Point(0, 50)), null),
            new PdfLink(outside, null, uri ?? new Uri("https://example.com/path"))
        ];
        return (document, inside, outside);
    }

    private static Scene LinkScene(PdfDocument document, List<Uri> opened, double height = 500)
    {
        var scene = new Scene(document, height: height);
        scene.Viewer.UriOpener = uri =>
        {
            opened.Add(uri);
            return Task.FromResult(true);
        };
        scene.Pump();
        Until(() => scene.Viewer.LinkLayer.TryGetLinks(0) is not null);
        return scene;
    }

    [AvaloniaFact]
    public void Click_On_An_Internal_Link_Goes_There()
    {
        var (document, inside, _) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            PdfLink? link = null;
            scene.Viewer.LinkActivated += (_, e) => link = e.Link;
            scene.Click(0, inside.Center);
            link.ShouldNotBeNull();
            link.Destination!.PageIndex.ShouldBe(1);
            Until(() => scene.Viewer.CurrentPageIndex == 1);
            opened.ShouldBeEmpty();
        }
    }

    [AvaloniaFact]
    public void Click_On_An_External_Link_Opens_It_And_Handled_Stops_That()
    {
        var (document, _, outside) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            scene.Click(0, outside.Center);
            opened.Select(u => u.AbsoluteUri).ShouldBe(["https://example.com/path"]);

            scene.Viewer.LinkActivated += (_, e) => e.Handled = true;
            scene.Click(0, new Point(outside.Left + 2, outside.Center.Y));
            opened.Count.ShouldBe(1);
        }
    }

    [AvaloniaFact]
    public void Behavior_None_Only_Raises_The_Event()
    {
        var (document, _, outside) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            scene.Viewer.ExternalLinkBehavior = PdfExternalLinkBehavior.None;
            var raised = 0;
            scene.Viewer.LinkActivated += (_, _) => raised++;
            scene.Click(0, outside.Center);
            raised.ShouldBe(1);
            opened.ShouldBeEmpty();
        }
    }

    public static TheoryData<string> DisallowedUris => new()
    {
        "file:///etc/passwd",
        "FILE:///C:/Windows/system32/calc.exe",
        "javascript:alert(1)",
        "JavaScript:alert(1)",
        "data:text/html,<script>alert(1)</script>",
        "ftp://example.com/x",
        "httpx://example.com/",
        "http.evil://example.com/",
        "https+evil://example.com/",
        "shttp://example.com/",
        "smb://server/share",
        "vbscript:msgbox(1)",
        "mailtox:me@example.com",
        "tel:+15551234567",
        "ms-msdt:/id",
        "C:\\Windows\\system32\\calc.exe",
        "/etc/passwd",
    };

    public static TheoryData<string> AllowedUris => new()
    {
        "http://example.com/",
        "https://example.com/a?b=c",
        "HTTP://EXAMPLE.COM/",
        "HtTpS://example.com/",
        "mailto:someone@example.com",
        "MAILTO:someone@example.com",
    };

    [Theory]
    [MemberData(nameof(AllowedUris))]
    public void Policy_Takes_The_Allowed_Schemes_In_Any_Case(string text)
    {
        Uri.TryCreate(text, UriKind.Absolute, out var uri).ShouldBeTrue();
        PdfLinkPolicy.IsAllowed(uri).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(DisallowedUris))]
    public void Policy_Refuses_Every_Other_Scheme(string text)
    {
        Uri.TryCreate(text, UriKind.Absolute, out var uri);
        PdfLinkPolicy.IsAllowed(uri).ShouldBeFalse();
    }

    [Theory]
    [InlineData("not a uri")]
    [InlineData("")]
    [InlineData("http//missing-colon")]
    [InlineData("://no-scheme")]
    public void Policy_Refuses_What_Is_Not_A_Uri(string text)
    {
        Uri.TryCreate(text, UriKind.Absolute, out var uri);
        PdfLinkPolicy.IsAllowed(uri).ShouldBeFalse();
    }

    [Fact]
    public void Policy_Refuses_No_Uri_And_Relative_Ones()
    {
        PdfLinkPolicy.IsAllowed(null).ShouldBeFalse();
        PdfLinkPolicy.IsAllowed(new Uri("relative/path", UriKind.Relative)).ShouldBeFalse();
        PdfLinkPolicy.IsAllowed(new Uri("http://x/rel", UriKind.Absolute)).ShouldBeTrue();
    }

    [AvaloniaTheory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("httpx://example.com/")]
    [InlineData("data:text/html,x")]
    public void A_Click_On_A_Disallowed_Link_Raises_The_Event_And_Opens_Nothing(string text)
    {
        var (document, _, outside) = LinkDocument(new Uri(text));
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            var raised = 0;
            scene.Viewer.LinkActivated += (_, _) => raised++;
            scene.Click(0, outside.Center);
            raised.ShouldBe(1);
            opened.ShouldBeEmpty();
        }
    }

    [AvaloniaFact]
    public void A_Click_On_A_Link_Does_Not_Start_Or_Disturb_A_Selection()
    {
        var (document, _, outside) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            Enter(scene);
            scene.Click(0, outside.Center);
            opened.Count.ShouldBe(1);
            scene.Viewer.SelectedText.ShouldBe(string.Empty);
            scene.Viewer.SelectionLayer.HasSelection.ShouldBeFalse();

            scene.Viewer.SelectAll();
            var selected = scene.Viewer.SelectedText;
            // Away from the first click, or this would be the second half of a double click.
            scene.Click(0, new Point(outside.Left + 2, outside.Center.Y));
            opened.Count.ShouldBe(2);
            scene.Viewer.SelectedText.ShouldBe(selected);
        }
    }

    [AvaloniaFact]
    public void A_Drag_That_Starts_On_A_Link_Selects_And_Does_Not_Activate()
    {
        var (document, inside, _) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            Enter(scene);
            var activated = 0;
            scene.Viewer.LinkActivated += (_, _) => activated++;
            var start = new Point(inside.Left + 1, inside.Center.Y);
            var end = new Point(inside.Left + ScriptedPdfDocument.CharWidth * 5 - 1, inside.Center.Y);
            scene.Press(0, start);
            scene.Viewer.LinkLayer.IsPressingLink.ShouldBeTrue();
            scene.Drag(0, end);
            scene.Viewer.LinkLayer.IsPressingLink.ShouldBeFalse();
            scene.Viewer.SelectedText.ShouldBe("go to");
            scene.Release(0, end);
            activated.ShouldBe(0);
            scene.Viewer.SelectedText.ShouldBe("go to");
        }
    }

    [AvaloniaFact]
    public void A_Tiny_Slip_During_A_Click_Still_Counts_As_A_Click()
    {
        var (document, inside, _) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            var activated = 0;
            scene.Viewer.LinkActivated += (_, _) => activated++;
            var start = inside.Center;
            var slip = new Point(start.X + 2, start.Y);
            scene.Press(0, start);
            scene.Drag(0, slip);
            scene.Release(0, slip);
            activated.ShouldBe(1);
            scene.Viewer.SelectedText.ShouldBe(string.Empty);
        }
    }

    [AvaloniaFact]
    public void Releasing_Off_The_Link_Does_Not_Activate()
    {
        var (document, inside, _) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = new Scene(document);
            scene.Pump();
            Until(() => scene.Viewer.LinkLayer.TryGetLinks(0) is not null);
            var activated = 0;
            scene.Viewer.LinkActivated += (_, _) => activated++;
            scene.Press(0, inside.Center);
            scene.Release(0, new Point(inside.Right + 200, inside.Center.Y));
            activated.ShouldBe(0);
            opened.ShouldBeEmpty();
        }
    }

    [AvaloniaFact]
    public void Shift_Click_On_A_Link_Extends_Instead()
    {
        var (document, inside, outside) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            Enter(scene);
            var activated = 0;
            scene.Viewer.LinkActivated += (_, e) =>
            {
                activated++;
                e.Handled = true;
            };
            scene.Click(0, new Point(inside.Left + 1, inside.Center.Y));
            activated.ShouldBe(1);
            scene.Click(0, outside.Center, RawInputModifiers.Shift);
            activated.ShouldBe(1);
            scene.Viewer.SelectedText.ShouldStartWith("go to page");
        }
    }

    [AvaloniaFact]
    public void A_Double_Click_On_A_Link_Activates_Once_And_Selects_The_Word()
    {
        var (document, inside, _) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            Enter(scene);
            var activated = 0;
            scene.Viewer.LinkActivated += (_, e) =>
            {
                activated++;
                e.Handled = true;
            };
            scene.Click(0, new Point(inside.Left + 1, inside.Center.Y));
            scene.Click(0, new Point(inside.Left + 1, inside.Center.Y));
            activated.ShouldBe(1);
            scene.Viewer.SelectedText.ShouldBe("go");
        }
    }

    [AvaloniaFact]
    public void Hovering_A_Link_Shows_A_Hand_A_Tint_And_Its_Address()
    {
        var (document, _, outside) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            Enter(scene);
            scene.Move(0, outside.Center);
            var layer = scene.Viewer.LinkLayer;
            layer.Hovered.ShouldNotBeNull();
            layer.Hovered!.Uri!.AbsoluteUri.ShouldBe("https://example.com/path");
            scene.Presenter.Cursor.ShouldNotBeNull();
            var hand = scene.Presenter.Cursor;
            var anchor = scene.Viewer.LinkTipAnchor!;
            Tooltip.GetTip(anchor).ShouldBe("https://example.com/path");
            Until(() => Tooltip.GetIsOpen(anchor));
            // The anchor sits over the link.
            scene.Pump();
            var corner = anchor.TranslatePoint(new Point(0, 0), scene.Window)!.Value;
            var expected = scene.At(0, outside.TopLeft);
            corner.X.ShouldBe(expected.X, 1);
            corner.Y.ShouldBe(expected.Y, 1);
            anchor.Bounds.Width.ShouldBe(outside.Width, 1);

            // On the same text but off the link: an I-beam; then away.
            scene.Move(0, new Point(ScriptedPdfDocument.Left + ScriptedPdfDocument.CharWidth, ScriptedPdfDocument.Top + 2 * ScriptedPdfDocument.LineHeight + 5));
            layer.Hovered.ShouldBeNull();
            Tooltip.GetIsOpen(anchor).ShouldBeFalse();
            scene.Presenter.Cursor.ShouldNotBeNull();
            scene.Presenter.Cursor.ShouldNotBeSameAs(hand);
        }
    }

    [AvaloniaFact]
    public void Link_Tip_Shows_The_Address_Encoded_And_Cut_To_A_Sane_Length()
    {
        var spoof = new PdfLink(default, null, new Uri("https://example.com/a b\u202Ecod.exe"));
        PdfViewer.LinkTipText(spoof).ShouldNotBeNull();
        PdfViewer.LinkTipText(spoof)!.ShouldNotContain("\u202E");
        var longOne = new PdfLink(default, null, new Uri("https://example.com/" + new string('a', 5000)));
        PdfViewer.LinkTipText(longOne)!.Length.ShouldBe(201);
        PdfViewer.LinkTipText(new PdfLink(default, new PdfDestination(2), null)).ShouldBe(PdfViewerLangResources.Format(PdfViewerLangResourceKind.PageNumber, 3));
        PdfViewer.LinkTipText(new PdfLink(default, null, null)).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Hover_Ends_With_The_Wheel_And_The_Links_Of_A_Page_Are_Read_When_It_Shows()
    {
        var (document, _, outside) = LinkDocument();
        using (document)
        {
            var opened = new List<Uri>();
            using var scene = LinkScene(document, opened);
            scene.Move(0, outside.Center);
            scene.Viewer.LinkLayer.Hovered.ShouldNotBeNull();
            scene.Window.MouseWheel(scene.At(0, outside.Center), new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            scene.Viewer.LinkLayer.Hovered.ShouldBeNull();
        }
    }

    [AvaloniaFact]
    public void Failing_To_Read_Links_Counts_As_None()
    {
        using var document = new BrokenLinksDocument();
        using var scene = new Scene(document);
        scene.Pump();
        Until(() => scene.Viewer.LinkLayer.TryGetLinks(0) is { Count: 0 });
    }

    private sealed class BrokenLinksDocument : PdfDocument
    {
        private readonly SimulatedPdfDocument _inner = new(1);

        public override int PageCount => 1;

        public override PdfDocumentInfo Info => _inner.Info;

        public override Size GetPageSize(int pageIndex) => _inner.GetPageSize(pageIndex);

        public override ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken) => _inner.RenderAsync(request, cancellationToken);

        public override string? GetPageLabel(int pageIndex) => null;

        public override Task<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken) => _inner.GetOutlineAsync(cancellationToken);

        public override Task<PdfPageText> GetPageTextAsync(int pageIndex, CancellationToken cancellationToken) => _inner.GetPageTextAsync(pageIndex, cancellationToken);

        public override Task<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<PdfLink>>(new InvalidOperationException("broken"));

        public override void Dispose() => _inner.Dispose();
    }

    [AvaloniaFact]
    public void Simulated_Document_Works_With_All_Of_It()
    {
        using var document = new SimulatedPdfDocument(3);
        using var scene = new Scene(document, height: 500);
        var viewer = scene.Viewer;
        viewer.SearchText = "lorem";
        Until(() => viewer.SearchMatchCount > 0 && !viewer.IsSearching);
        viewer.CurrentMatch.ShouldNotBeNull();
        viewer.CurrentMatch!.Rects.ShouldNotBeEmpty();

        Until(() => viewer.LinkLayer.TryGetLinks(viewer.CurrentPageIndex) is not null);
        viewer.GoToPage(0, animate: false);
        scene.Pump();
        Until(() => viewer.LinkLayer.TryGetLinks(0) is { Count: 2 });
        var external = viewer.LinkLayer.TryGetLinks(0)![1];
        var opened = new List<Uri>();
        viewer.UriOpener = uri =>
        {
            opened.Add(uri);
            return Task.FromResult(true);
        };
        scene.Click(0, external.Bounds.Center);
        opened.ShouldHaveSingleItem().AbsoluteUri.ShouldBe("https://example.com/");

        viewer.SelectAll();
        Until(() => viewer.SelectedText.Length > 0);
        viewer.SelectedText.ShouldStartWith("Page 1\nGo to chapter two");
    }

    #endregion

    #region Page labels

    [AvaloniaFact]
    public void Thumbnails_Show_Labels_That_Arrive_Later()
    {
        using var document = new ScriptedPdfDocument("a", "b", "c");
        using var scene = new Scene(document, setup: v => v.IsSidebarOpen = true);
        scene.Pump();
        var thumbnail = scene.Viewer.GetVisualDescendants().OfType<PdfThumbnail>().First(t => t.PageIndex == 1);
        AutomationProperties.GetName(thumbnail).ShouldEndWith("2");
        document.Labels[1] = "ii";

        // From any thread, as a backend reading labels in the background would.
        var publish = Task.Run(document.PublishLabels);
        Until(() => publish.IsCompleted && AutomationProperties.GetName(thumbnail)!.EndsWith("ii", StringComparison.Ordinal));
        scene.Viewer.ThumbnailLabel(1).ShouldBe("ii");
        scene.Viewer.ThumbnailLabel(2).ShouldBe("3");
    }

    #endregion
}
