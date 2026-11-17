using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A window with an anchor (standing in for the pill) and a model menu, plus input helpers for the menu tests.</summary>
internal sealed class ModelMenuHost : IDisposable
{
    private ModelMenuHost(Window window, ModelMenu menu, Button anchor)
    {
        Window = window;
        Menu = menu;
        Anchor = anchor;
    }

    public Window Window { get; }

    public ModelMenu Menu { get; }

    public Button Anchor { get; }

    public ModelMenuPanel Main => Menu.MainPanel!;

    public ModelMenuPanel? Effort => Menu.EffortPresence?.Panel;

    public ModelMenuPanel? More => Menu.MorePresence?.Panel;

    /// <summary>
    /// Shows <paramref name="menu"/> in a window of the given size; the anchor sits 40px from the top, at the left (room for
    /// submenus on the right), centered or at the right.
    /// </summary>
    public static ModelMenuHost Show(ModelMenu? menu = null, double width = 900, double height = 720,
                                     ArcMotionLevel level = ArcMotionLevel.Off, HorizontalAlignment anchorAt = HorizontalAlignment.Left,
                                     VerticalAlignment anchorV = VerticalAlignment.Top)
    {
        menu ??= ModelMenuSamples.Menu();
        var anchor = new Button
        {
            Content = "Model",
            Width = 220,
            Height = 40,
            HorizontalAlignment = anchorAt,
            VerticalAlignment = anchorV,
            Margin = anchorV == VerticalAlignment.Bottom ? new Thickness(40, 0, 40, 40) : new Thickness(40, 40, 40, 0)
        };
        var root = new Panel { Children = { anchor, menu } };
        var window = TestHelpers.Show(root, level);
        window.Width = width;
        window.Height = height;
        var host = new ModelMenuHost(window, menu, anchor);
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

    public void Open(ModelMenuOpenFocus focus = ModelMenuOpenFocus.Surface)
    {
        Menu.Open(Anchor, focus);
        Pump();
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

    /// <summary>The focused element.</summary>
    public IInputElement? Focused => Window.FocusManager?.GetFocusedElement();

    public T Row<T>(ModelMenuPanel panel, Func<T, bool> where) where T : ModelMenuItem => panel.Rows.OfType<T>().First(where);

    public ModelMenuModelRow ModelRow(ModelMenuPanel panel, string id) => Row<ModelMenuModelRow>(panel, r => r.Model.Id == id);

    public ModelMenuActionRow ActionRow(ModelMenuPanel panel, ModelMenuRowKind kind) => Row<ModelMenuActionRow>(panel, r => r.Kind == kind);

    public ModelMenuEffortRow LevelRow(ModelMenuPanel panel, ModelEffort level) => Row<ModelMenuEffortRow>(panel, r => r.Level == level);

    public IEnumerable<T> InPanel<T>(ModelMenuPanel panel) where T : Visual => panel.GetVisualDescendants().OfType<T>();

    public void Dispose() => Window.Close();
}
