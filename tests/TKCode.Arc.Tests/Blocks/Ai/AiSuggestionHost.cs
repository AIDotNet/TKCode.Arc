using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// A window with a stand-in composer (the anchor: 100 high, a text box inside that keeps focus) and an
/// <see cref="AiSuggestionPopup"/>, plus input helpers. The default trigger sits at the start of the first line, as in
/// Arc's composer: 19px in (border 1, padding 10, text padding 8), the line 24 high.
/// </summary>
internal sealed class AiSuggestionHost : IDisposable
{
    public static readonly Rect LineStart = new(19, 19, 6, 24);

    private AiSuggestionHost(Window window, Border anchor, TextBox box, AiSuggestionPopup popup)
    {
        Window = window;
        Anchor = anchor;
        Box = box;
        Popup = popup;
    }

    public Window Window { get; }

    public Border Anchor { get; }

    public TextBox Box { get; }

    public AiSuggestionPopup Popup { get; }

    public AiSuggestionPop Pop => Popup.Pop;

    public List<AiSuggestionAcceptedEventArgs> Accepted { get; } = new();

    /// <summary>
    /// Shows the stand-in composer (<paramref name="anchorWidth"/> wide) at the bottom of a window (or at the top, for the
    /// flip below the line), 32px from the edges.
    /// </summary>
    public static AiSuggestionHost Show(double width = 760, double height = 560, double anchorWidth = 695,
                                        VerticalAlignment anchorAt = VerticalAlignment.Bottom, ArcMotionLevel level = ArcMotionLevel.Off,
                                        double anchorHeight = 100)
    {
        var box = new TextBox { Margin = new Thickness(11), VerticalAlignment = VerticalAlignment.Top, Text = "/" };
        var anchor = new Border
        {
            Width = anchorWidth,
            Height = anchorHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = anchorAt,
            Margin = new Thickness(32),
            Child = box
        };
        var window = TestHelpers.Show(new Panel { Children = { anchor } }, level);
        window.Width = width;
        window.Height = height;
        var popup = new AiSuggestionPopup();
        popup.Pop.CommandListName = "Commands";
        popup.Pop.MentionListName = "Files and docs";
        var host = new AiSuggestionHost(window, anchor, box, popup);
        popup.Pop.Accepted += (_, e) => host.Accepted.Add(e);
        host.Pump();
        box.Focus(NavigationMethod.Tab);
        host.Pump();
        return host;
    }

    public void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Shows <paramref name="rows"/> and opens the list for the trigger at <paramref name="caret"/> (line start by default).</summary>
    public bool Open(AiSuggestionKind kind, IReadOnlyList<AiSuggestionRow> rows, Rect? caret = null)
    {
        if (!Pop.Show(kind, rows))
        {
            Pump();
            return false;
        }
        var shown = Popup.Open(Anchor, caret ?? LineStart);
        Pump();
        return shown;
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    public Point Center(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window)!.Value;

    public Rect BoundsOf(Visual visual) => new(visual.TranslatePoint(default, Window)!.Value, visual.Bounds.Size);

    public void Move(Point point)
    {
        Window.MouseMove(point);
        Pump();
    }

    public void Move(Visual visual) => Move(Center(visual));

    public void Click(Point point)
    {
        Window.MouseMove(point);
        Pump();
        Window.MouseDown(point, MouseButton.Left);
        Window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    public void Click(Visual visual) => Click(Center(visual));

    public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

    public AiSuggestionList List => Pop.List!;

    public ScrollViewer Scroll => Pop.Scroll!;

    public AiSuggestionRowView RowView(int index) => List.RowViews[index];

    /// <summary>The row's rectangle in the scroll viewport's coordinates (visible when inside 0 … viewport height).</summary>
    public Rect InViewport(int index)
    {
        var view = RowView(index);
        var origin = view.TranslatePoint(default, Scroll)!.Value;
        return new Rect(origin, view.Bounds.Size);
    }

    public void Dispose()
    {
        Popup.Dispose();
        Window.Close();
    }
}
