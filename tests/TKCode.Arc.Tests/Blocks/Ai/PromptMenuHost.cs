using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A window with a header pill and its wired popover (as the studio wires them), and input helpers.</summary>
internal sealed class PromptMenuHost : IDisposable
{
    private PromptMenuHost(Window window, PromptHeaderPill pill, PromptPopoverMenu menu)
    {
        Window = window;
        Pill = pill;
        Menu = menu;
    }

    public Window Window { get; }

    public PromptHeaderPill Pill { get; }

    public PromptPopoverMenu Menu { get; }

    public PromptVersionMenu VersionMenu => (PromptVersionMenu)Menu;

    public PromptModelMenu ModelMenu => (PromptModelMenu)Menu;

    public static PromptMenuHost Show(PromptHeaderPill pill, PromptPopoverMenu menu, ArcMotionLevel level = ArcMotionLevel.Off,
                                      HorizontalAlignment align = HorizontalAlignment.Left, double width = 760, double height = 600)
    {
        PromptMenuSamples.Wire(pill, menu);
        pill.HorizontalAlignment = align;
        var root = new StackPanel { Margin = new Thickness(24), Children = { pill, menu } };
        var window = TestHelpers.Show(root, level);
        window.Width = width;
        window.Height = height;
        var host = new PromptMenuHost(window, pill, menu);
        host.Pump();
        return host;
    }

    public static PromptMenuHost ShowVersions(PromptVersionMenu? menu = null, ArcMotionLevel level = ArcMotionLevel.Off) =>
        Show(PromptMenuSamples.VersionPill(), menu ?? PromptMenuSamples.VersionMenu(), level);

    /// <summary>Runs layout, render ticks and posted jobs a few times.</summary>
    public void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        Window.UpdateLayout();
    }

    public Point CenterOf(Visual visual) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), Window)!.Value;

    /// <summary>The box of <paramref name="visual"/> in window coordinates.</summary>
    public Rect BoundsOf(Visual visual) =>
        new(visual.TranslatePoint(default, Window)!.Value, visual.Bounds.Size);

    public void Click(Visual visual) => Click(CenterOf(visual));

    public void Click(Point point)
    {
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Pump(1);
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump();
    }

    public void Move(Visual visual)
    {
        Window.MouseMove(CenterOf(visual), RawInputModifiers.None);
        Pump(1);
    }

    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    /// <summary>Opens the popover with the keys, as a person would (focus the pill, press Enter).</summary>
    public void OpenWithKeys()
    {
        Pill.Focus(NavigationMethod.Tab);
        Key(PhysicalKey.Enter);
    }

    /// <summary>The focused element, if any.</summary>
    public IInputElement? Focused => TopLevel.GetTopLevel(Window)?.FocusManager?.GetFocusedElement();

    public bool HasFocusRing(Visual visual) => visual is Control control && control.Classes.Contains(":focus-visible");

    public void Dispose() => Window.Close();
}
