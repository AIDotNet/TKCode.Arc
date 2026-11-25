using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Content widgets, overlay widgets and view zones in the widget host over the view (upstream's contentWidgets,
/// overlayWidgets and viewZones view parts).
/// </summary>
public class CodeEditorWidgetTests
{
    private static (CodeEditor Editor, CodeEditorView View, Window Window) Show(string text, double height = 200)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = height };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);
        return (editor, TestHelpers.Descendant<CodeEditorView>(editor), window);
    }

    private static void Render(Window window)
    {
        for (var i = 0; i < 2; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static string Lines(int count) => string.Join('\n', Enumerable.Range(1, count).Select(i => "line " + i));

    private sealed class ContentWidget(string id, Control control) : ICodeEditorContentWidget
    {
        public ContentWidgetPosition? Position { get; set; }

        public string Id => id;

        public Control Control => control;

        public ContentWidgetPositionPreference? Rendered { get; private set; }

        public ContentWidgetPosition? GetPosition() => Position;

        public void AfterRender(ContentWidgetPositionPreference? position, Point? coordinate) => Rendered = position;
    }

    private sealed class OverlayWidget(string id, Control control, OverlayWidgetPositionPreference preference) : ICodeEditorOverlayWidget
    {
        public string Id => id;

        public Control Control => control;

        public OverlayWidgetPosition? GetPosition() => new(preference);
    }

    [AvaloniaFact]
    public void A_content_widget_goes_below_its_position_or_above_when_below_has_no_room()
    {
        var (editor, view, window) = Show(Lines(30), height: 200);
        var control = new Border { Width = 120, Height = 60 };
        var widget = new ContentWidget("w", control)
        {
            Position = new ContentWidgetPosition(new TextPosition(2, 3), [ContentWidgetPositionPreference.Below, ContentWidgetPositionPreference.Above]),
        };
        editor.AddContentWidget(widget);
        Render(window);

        var placement = editor.GetContentWidgetPlacement("w").ShouldNotBeNull();
        placement.Preference.ShouldBe(ContentWidgetPositionPreference.Below);
        placement.Bounds.Top.ShouldBe(view.GetY(2) + view.LineHeight, 0.01);
        placement.Bounds.Left.ShouldBe(view.GetX(2, 3), 0.01);
        control.Bounds.ShouldBe(placement.Bounds);
        widget.Rendered.ShouldBe(ContentWidgetPositionPreference.Below);

        // near the bottom there is no room below: above
        var lastVisible = (int)Math.Floor(200 / view.LineHeight);
        widget.Position = widget.Position with { Position = new TextPosition(lastVisible, 1) };
        editor.LayoutContentWidget(widget);
        Render(window);
        placement = editor.GetContentWidgetPlacement("w").ShouldNotBeNull();
        placement.Preference.ShouldBe(ContentWidgetPositionPreference.Above);
        placement.Bounds.Bottom.ShouldBe(view.GetY(lastVisible), 0.01);

        // a widget is kept inside the text area horizontally
        widget.Position = widget.Position with { Position = new TextPosition(3, 7) };
        control.Width = 390;
        editor.LayoutContentWidget(widget);
        Render(window);
        editor.GetContentWidgetPlacement("w")!.Bounds.Left.ShouldBe(view.GutterWidth, 0.01);
    }

    [AvaloniaFact]
    public void A_content_widget_scrolls_with_its_line_and_hides_without_a_position()
    {
        var (editor, view, window) = Show(Lines(100));
        var control = new Border { Width = 50, Height = 20 };
        var widget = new ContentWidget("w", control)
        {
            Position = new ContentWidgetPosition(new TextPosition(3, 1), [ContentWidgetPositionPreference.Exact]),
        };
        editor.AddContentWidget(widget);
        Render(window);
        editor.GetContentWidgetPlacement("w")!.Bounds.Top.ShouldBe(view.GetY(3), 0.01);

        view.ViewModel!.ViewLayout.SetScrollPosition(new NewScrollPosition(null, view.LineHeight), ScrollType.Immediate);
        Render(window);
        editor.GetContentWidgetPlacement("w")!.Bounds.Top.ShouldBe(view.LineHeight, 0.01);

        // scrolled away: not placed
        view.ViewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(null, view.LineHeight * 50), ScrollType.Immediate);
        Render(window);
        editor.GetContentWidgetPlacement("w").ShouldBeNull();

        widget.Position = null;
        editor.LayoutContentWidget(widget);
        Render(window);
        control.IsVisible.ShouldBeFalse();

        editor.RemoveContentWidget(widget);
        editor.WidgetHost!.Children.ShouldNotContain(control);
    }

    [AvaloniaFact]
    public void Overlay_widgets_sit_in_their_corners()
    {
        var (editor, view, window) = Show("abc");
        var topRight = new Border { Width = 100, Height = 30 };
        var bottomRight = new Border { Width = 40, Height = 10 };
        editor.AddOverlayWidget(new OverlayWidget("tr", topRight, OverlayWidgetPositionPreference.TopRightCorner));
        editor.AddOverlayWidget(new OverlayWidget("br", bottomRight, OverlayWidgetPositionPreference.BottomRightCorner));
        Render(window);

        var host = editor.WidgetHost!;
        topRight.Bounds.Top.ShouldBe(0);
        topRight.Bounds.Right.ShouldBeLessThanOrEqualTo(host.Bounds.Width);
        topRight.Bounds.Right.ShouldBeGreaterThan(host.Bounds.Width - 30);
        bottomRight.Bounds.Bottom.ShouldBeLessThanOrEqualTo(host.Bounds.Height);
        bottomRight.Bounds.Bottom.ShouldBeGreaterThan(host.Bounds.Height - 30);
        host.Bounds.Size.ShouldBe(view.Bounds.Size);
    }

    [AvaloniaFact]
    public void Clicks_where_no_widget_is_reach_the_text()
    {
        var (editor, view, window) = Show("abcdef\nghijkl");
        var widget = new Border { Width = 60, Height = 20, Background = Avalonia.Media.Brushes.Red };
        var pressed = 0;
        widget.PointerPressed += (_, e) =>
        {
            pressed++;
            e.Handled = true;
        };
        editor.AddOverlayWidget(new OverlayWidget("tr", widget, OverlayWidgetPositionPreference.TopRightCorner));
        Render(window);

        var origin = view.TranslatePoint(new Point(0, 0), window)!.Value;

        // on the text: the cursor moves
        var onText = origin + new Point(view.GetX(2, 4) + 1, view.GetY(2) + (view.LineHeight / 2));
        window.MouseDown(onText, MouseButton.Left);
        window.MouseUp(onText, MouseButton.Left);
        view.ViewModel!.GetPosition().ShouldBe(new TextPosition(2, 4));

        // on the widget: the widget gets it, the cursor stays
        var onWidget = origin + widget.Bounds.Center;
        window.MouseDown(onWidget, MouseButton.Left);
        window.MouseUp(onWidget, MouseButton.Left);
        pressed.ShouldBe(1);
        view.ViewModel.GetPosition().ShouldBe(new TextPosition(2, 4));
    }

    [AvaloniaFact]
    public void A_view_zone_stays_left_of_the_minimap_and_the_scroll_bar()
    {
        // upstream lays zones out over the content width; a zone over the minimap would put its right edge (a peek
        // widget's close button) under the minimap or the scroll bar
        var editor = new CodeEditor { Text = Lines(200), Width = 900, Height = 200, ShowMinimap = true };
        var window = TestHelpers.Show(editor);
        Render(window);
        var view = TestHelpers.Descendant<CodeEditorView>(editor);
        view.IsMinimapVisible.ShouldBeTrue();
        var control = new Border();
        editor.ChangeViewZones(accessor => accessor.AddZone(new CodeEditorViewZone { AfterLineNumber = 1, HeightInLines = 2, Control = control }));
        Render(window);

        control.Bounds.Width.ShouldBeGreaterThan(0);
        control.Bounds.Right.ShouldBeLessThanOrEqualTo(view.TextAreaRight + 0.01);
        control.Bounds.Right.ShouldBeLessThanOrEqualTo(view.Bounds.Width - view.OverlayRightInset + 0.01);
    }

    [AvaloniaFact]
    public void A_view_zone_makes_room_between_lines_and_hosts_its_control()
    {
        var (editor, view, window) = Show(Lines(10));
        var y3 = view.GetY(3);
        var control = new Border();
        var margin = new Border();
        var heights = new List<double>();
        string zoneId = "";
        editor.ChangeViewZones(accessor => zoneId = accessor.AddZone(new CodeEditorViewZone
        {
            AfterLineNumber = 2,
            HeightInLines = 2,
            Control = control,
            MarginControl = margin,
            OnComputedHeight = heights.Add,
        }));
        Render(window);

        heights.ShouldBe([view.LineHeight * 2]);
        view.GetY(3).ShouldBe(y3 + (view.LineHeight * 2), 0.01);
        var bounds = editor.GetViewZoneBounds(zoneId).ShouldNotBeNull();
        bounds.Top.ShouldBe(y3, 0.01);
        control.Bounds.ShouldBe(new Rect(view.GutterWidth, y3, Math.Min(view.TextAreaRight, view.Bounds.Width - view.OverlayRightInset) - view.GutterWidth, view.LineHeight * 2));
        margin.Bounds.ShouldBe(new Rect(0, y3, view.GutterWidth, view.LineHeight * 2));

        // the zone is not text: a hit there is the zone's
        view.GetMouseTargetAt(new Point(view.GutterWidth + 5, y3 + 2)).ShouldSatisfyAllConditions(
            t => t.Type.ShouldBe(CodeEditorMouseTargetType.ContentViewZone),
            t => t.Detail.ShouldBe(zoneId));

        // the zone moves with its line when text is inserted above
        editor.Model.ApplyEdits([EditOperation.Insert(new TextPosition(1, 1), "new\n")]);
        Render(window);
        editor.GetViewZoneBounds(zoneId)!.Value.Top.ShouldBe(view.GetY(3) + view.LineHeight, 0.01);

        // a new view model (font change) keeps the zone
        editor.FontSize = editor.FontSize + 2;
        Render(window);
        editor.GetViewZoneBounds(zoneId).ShouldNotBeNull().Height.ShouldBe(view.LineHeight * 2, 0.01);

        editor.ChangeViewZones(accessor => accessor.RemoveZone(zoneId));
        Render(window);
        editor.GetViewZoneBounds(zoneId).ShouldBeNull();
        editor.WidgetHost!.Children.ShouldNotContain(control);
        view.GetY(3).ShouldBe(view.LineHeight * 2, 0.01);
    }

    [AvaloniaFact]
    public void A_zone_after_line_zero_is_above_the_first_line_and_changes_height_on_layout()
    {
        var (editor, view, window) = Show(Lines(5));
        var zone = new CodeEditorViewZone { AfterLineNumber = 0, HeightInPx = 30 };
        string id = "";
        editor.ChangeViewZones(accessor => id = accessor.AddZone(zone));
        Render(window);
        view.GetY(1).ShouldBe(30, 0.01);

        zone.HeightInPx = 50;
        editor.ChangeViewZones(accessor => accessor.LayoutZone(id));
        Render(window);
        view.GetY(1).ShouldBe(50, 0.01);
    }
}
