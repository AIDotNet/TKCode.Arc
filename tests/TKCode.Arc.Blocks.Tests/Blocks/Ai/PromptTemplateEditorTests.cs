using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The prompt studio's template editor: chips from the text, tones and states, atomic editing, the "{{" list, "}}"
/// typing, paste normalization, copy, undo grouping, submit, placeholder, read-only and growth. Sample data is our own
/// (prompt studio spec, section 10: Northwind Kites).
/// </summary>
public class PromptTemplateEditorTests
{
    internal const string System =
        "You answer refund questions for Northwind Kites.\n\nApply this rule: {{refund_rule}}\n\n- Address {{buyer_name}} once by first name.\n- Mention ticket {{ticket_id}}.\n- Sign as {{agent_sign}}.";

    internal static readonly string[] Variables = ["buyer_name", "ticket_id", "refund_rule", "agent_sign"];

    private sealed class Command : ICommand
    {
        public int Runs { get; private set; }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => Runs++;
    }

    private sealed class Rig : IDisposable
    {
        private readonly IDisposable? _clock;

        public Rig(string text = System, ArcMotionLevel level = ArcMotionLevel.Off, double width = 460, ManualFrameClock? frames = null,
                   Action<PromptTemplateEditor>? configure = null)
        {
            _clock = frames?.Install();
            Editor = new PromptTemplateEditor
            {
                Width = width,
                Text = text,
                Variables = new List<string>(Variables),
                AccessibleName = "System prompt",
                ListName = "Variables",
                Placeholder = "Describe the assistant's role and rules",
                AddVariableText = name => $"Add {name}",
                AnnounceCount = count => $"{count} variables",
                SubmitCommand = Submit
            };
            configure?.Invoke(Editor);
            Editor.VariableAdded += (_, e) => Added.Add(e.Name);
            Editor.ChipInvoked += (_, e) => Invoked.Add(e.Name);
            Window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { Editor } }, level);
            Window.Width = 640;
            Window.Height = 700;
            Editor.History.Clock = () => Now;
            Pump();
        }

        public PromptTemplateEditor Editor { get; }

        public Window Window { get; }

        public TokenTextEditor Inner => Editor.Editor!;

        public Command Submit { get; } = new();

        public List<string> Added { get; } = new();

        public List<string> Invoked { get; } = new();

        public TimeSpan Now { get; set; } = TimeSpan.FromSeconds(10);

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        public void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Pump();
        }

        /// <summary>Types <paramref name="text"/> one character at a time, <paramref name="pause"/> ms apart.</summary>
        public void Type(string text, int pause = 100)
        {
            foreach (var c in text)
            {
                Now += TimeSpan.FromMilliseconds(pause);
                Window.KeyTextInput(c.ToString());
                Pump();
            }
        }

        public void Caret(int index)
        {
            Inner.Focus();
            Pump();
            Inner.Select(index, index);
            Pump();
        }

        public void CaretAtEnd() => Caret(Inner.Text.Length);

        public RawInputModifiers CommandKey()
        {
            var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
            return hotkeys?.CommandModifiers == KeyModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control;
        }

        /// <summary>The window point of <paramref name="local"/> in the inner TextBox's text.</summary>
        public Point TextPoint(Point local) => Inner.Box!.Presenter!.TranslatePoint(local, Window)!.Value;

        public Rect ChipBox(string name)
        {
            var chip = Inner.Tokens.First(t => t.Id == name);
            return Inner.Box!.Presenter!.TextLayout.HitTestTextRange(chip.Start, chip.Length).Single();
        }

        public void Click(Point point)
        {
            Window.MouseMove(point);
            Window.MouseDown(point, MouseButton.Left);
            Window.MouseUp(point, MouseButton.Left);
            Pump();
        }

        public PromptVariablePop Pop => Editor.Pop;

        public void Dispose()
        {
            Window.Close();
            _clock?.Dispose();
        }
    }

    private static List<GeometryDrawing> Paint(Visual visual)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            visual.Render(context);
        }
        var shapes = new List<GeometryDrawing>();
        Collect(group, shapes);
        return shapes;

        static void Collect(DrawingGroup group, List<GeometryDrawing> into)
        {
            foreach (var child in group.Children)
            {
                if (child is DrawingGroup nested)
                {
                    Collect(nested, into);
                }
                else if (child is GeometryDrawing shape)
                {
                    into.Add(shape);
                }
            }
        }
    }

    #region Chips

    [AvaloniaFact]
    public void Markers_Are_Atomic_Chips_That_Show_Only_The_Name()
    {
        using var rig = new Rig();
        var inner = rig.Inner;
        inner.Tokens.Select(t => t.Id).ShouldBe(["refund_rule", "buyer_name", "ticket_id", "agent_sign"]);
        inner.Tokens.ShouldAllBe(t => t.Kind == PromptTemplateText.VariableKind);
        inner.Box!.Presenter!.InlineTokens.Count.ShouldBe(4);
        rig.Editor.Text.ShouldBe(System);
        // A chip is as wide as its name at 0.93em plus 5 + 1 on each side, not as its "{{name}}".
        var label = new Avalonia.Media.TextFormatting.TextLayout("ticket_id", new Typeface(inner.Box.FontFamily), 14 * 0.93, Brushes.Black,
            letterSpacing: -0.14);
        rig.ChipBox("ticket_id").Width.ShouldBe(label.WidthIncludingTrailingWhitespace + 12, 0.01);
        // The editor's text: Inter 14 / 23.8, padding 2 12 6, Enter adds lines.
        inner.FontSize.ShouldBe(14);
        inner.LineHeight.ShouldBe(23.8, 0.001);
        inner.Padding.ShouldBe(new Thickness(12, 2, 12, 6));
        inner.SubmitOnEnter.ShouldBeFalse();
        AutomationProperties.GetName(inner.Box).ShouldBe("System prompt");
        // The hint line describes the text box (Arc's aria-describedby).
        AutomationProperties.SetHelpText(rig.Editor, "Type {{ to add a variable");
        AutomationProperties.GetHelpText(inner.Box).ShouldBe("Type {{ to add a variable");
    }

    [AvaloniaFact]
    public void Tones_Follow_The_Variables_And_Cycle_Through_Four()
    {
        using var rig = new Rig("{{buyer_name}} {{ticket_id}} {{refund_rule}} {{agent_sign}} {{tracking_url}}");
        var editor = rig.Editor;
        Variables.Select(editor.ToneOf).ShouldBe([1, 2, 3, 4]);
        // A name only in the text follows the listed ones: the fifth takes tone 1 again.
        editor.ToneOf("tracking_url").ShouldBe(1);
        editor.ToneSelector = name => name == "ticket_id" ? 4 : 2;
        editor.ToneOf("ticket_id").ShouldBe(4);
        editor.ToneOf("buyer_name").ShouldBe(2);
        editor.ToneSelector = null;
        // Text: the series color mixed 68% into Foreground; the series colors come from the theme.
        var series = ArcTokens.GetColor(editor, DesignTokens.ArcDesignTokenKind.Series2);
        editor.SeriesColorOf(2).ShouldBe(series);
        var foreground = TestHelpers.ColorOf(editor.Foreground);
        editor.ChipTextColor("ticket_id").ShouldBe(PromptTone.Mix(foreground, series, 0.68));
    }

    [AvaloniaFact]
    public void Chips_Paint_Their_Normal_Active_And_Missing_States()
    {
        using var rig = new Rig("Mention ticket {{ticket_id}}.");
        var mirror = rig.Inner.Box!.Mirror!;
        var series = rig.Editor.SeriesColorOf(2);

        static byte Alpha(IBrush? brush) => TestHelpers.ColorOf(brush).A;

        var shapes = Paint(mirror);
        var fill = shapes.First(s => s.Brush is not null);
        TestHelpers.ColorOf(fill.Brush).ShouldBe(PromptTone.WithOpacity(series, 0.12), "fill: series 12%");
        var ring = shapes.First(s => s.Pen is not null);
        Alpha(ring.Pen!.Brush).ShouldBe((byte)56); // 22%
        ring.Pen.Thickness.ShouldBe(1);
        // The box: the content area of the 13.02px name plus 1 above and below (18 with Inter; the test font differs).
        var metrics = new Typeface(rig.Inner.Box!.FontFamily).GlyphTypeface.Metrics;
        var content = (metrics.Descent - metrics.Ascent) * 14 * 0.93 / metrics.DesignEmHeight;
        fill.Geometry!.Bounds.Height.ShouldBe(content + 2, 1); // snapped to whole pixels

        // Active: 24% / 45% (motion off: at once).
        rig.Editor.ActiveVariable = "ticket_id";
        shapes = Paint(mirror);
        Alpha(shapes.First(s => s.Brush is not null).Brush).ShouldBe((byte)61);
        Alpha(shapes.First(s => s.Pen is not null).Pen!.Brush).ShouldBe((byte)115);
        rig.Editor.ActiveVariable = null;

        // Missing: no fill, no ring, a dashed 60% outline.
        rig.Editor.MissingVariables = ["ticket_id"];
        shapes = Paint(mirror);
        shapes.ShouldNotContain(s => s.Brush != null);
        var dashed = shapes.Single(s => s.Pen is not null).Pen!;
        dashed.DashStyle.ShouldNotBeNull();
        Alpha(dashed.Brush).ShouldBe((byte)153);
        // A live collection is followed.
        var missing = new Avalonia.Collections.AvaloniaList<string>();
        rig.Editor.MissingVariables = missing;
        Paint(mirror).ShouldContain(s => s.Brush != null);
        missing.Add("ticket_id");
        Paint(mirror).ShouldNotContain(s => s.Brush != null);
    }

    [AvaloniaFact]
    public void Active_Eases_In_160ms_And_A_Flash_Pulses_From_40_Percent_For_700ms()
    {
        var frames = new ManualFrameClock();
        using var rig = new Rig("{{ticket_id}} and {{buyer_name}}", ArcMotionLevel.Full, frames: frames);
        var renderer = rig.Editor.Renderer;
        rig.Editor.ActiveVariable = "ticket_id";
        renderer.ActiveAmount("ticket_id").ShouldBeLessThan(0.2);
        frames.Advance(TimeSpan.FromMilliseconds(80));
        renderer.ActiveAmount("ticket_id").ShouldBeInRange(0.3, 0.99);
        frames.Advance(TimeSpan.FromMilliseconds(120));
        renderer.ActiveAmount("ticket_id").ShouldBe(1, 0.001);

        rig.Editor.FlashVariable("ticket_id");
        renderer.FlashAmount("ticket_id").ShouldBe(1);
        var fill = Paint(rig.Inner.Box!.Mirror!).First(s => s.Brush is not null);
        TestHelpers.ColorOf(fill.Brush).A.ShouldBe((byte)102); // 40%
        frames.Advance(TimeSpan.FromMilliseconds(350));
        renderer.FlashAmount("ticket_id").ShouldBeInRange(0.01, 0.5);
        frames.Advance(TimeSpan.FromMilliseconds(400));
        renderer.FlashAmount("ticket_id").ShouldBe(0, 0.001);
        // Each call flashes again; other names stay still.
        rig.Editor.FlashVariable("ticket_id");
        renderer.FlashAmount("ticket_id").ShouldBe(1);
        renderer.FlashAmount("buyer_name").ShouldBe(0);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_Active_In_120ms_And_Does_Not_Flash()
    {
        var frames = new ManualFrameClock();
        using var rig = new Rig("{{ticket_id}}", ArcMotionLevel.Reduced, frames: frames);
        var renderer = rig.Editor.Renderer;
        rig.Editor.ActiveVariable = "ticket_id";
        frames.Advance(TimeSpan.FromMilliseconds(60));
        renderer.ActiveAmount("ticket_id").ShouldBeInRange(0.3, 0.7);
        frames.Advance(TimeSpan.FromMilliseconds(80));
        renderer.ActiveAmount("ticket_id").ShouldBe(1, 0.001);
        rig.Editor.FlashVariable("ticket_id");
        renderer.FlashAmount("ticket_id").ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_Click_On_A_Chip_Raises_ChipInvoked_With_Its_Name()
    {
        using var rig = new Rig();
        var box = rig.ChipBox("buyer_name");
        rig.Click(rig.TextPoint(box.Center));
        rig.Invoked.ShouldBe(["buyer_name"]);
        rig.Click(rig.TextPoint(new Point(box.Right + 30, box.Center.Y)));
        rig.Invoked.Count.ShouldBe(1);
    }

    #endregion

    #region Atomic editing

    [AvaloniaFact]
    public void The_Caret_Never_Rests_Inside_A_Chip()
    {
        using var rig = new Rig("Hi {{buyer_name}}, thanks");
        var chip = rig.Inner.Tokens.Single();
        rig.Caret(chip.Start);
        rig.Press(PhysicalKey.ArrowRight);
        rig.Inner.CaretIndex.ShouldBe(chip.End);
        rig.Press(PhysicalKey.ArrowLeft);
        rig.Inner.CaretIndex.ShouldBe(chip.Start);
        // Code and clicks inside a chip land on its nearer edge.
        rig.Editor.Select(chip.Start + 3, chip.Start + 3);
        rig.Pump();
        rig.Inner.CaretIndex.ShouldBe(chip.Start);
        rig.Editor.Select(chip.End - 2, chip.End - 2);
        rig.Pump();
        rig.Inner.CaretIndex.ShouldBe(chip.End);
        var box = rig.ChipBox("buyer_name");
        rig.Click(rig.TextPoint(new Point(box.X + 3, box.Center.Y)));
        rig.Inner.CaretIndex.ShouldBe(chip.Start);
        // Shift+arrows select a chip whole, never half of it.
        rig.Caret(chip.Start);
        rig.Press(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        (rig.Inner.SelectionStart, rig.Inner.SelectionEnd).ShouldBe((chip.Start, chip.End));
    }

    [AvaloniaFact]
    public void Backspace_And_Delete_Remove_A_Chip_Whole_And_Alt_Backspace_Counts_It_As_A_Word()
    {
        using var rig = new Rig("Mention ticket {{ticket_id}} now");
        var chip = rig.Inner.Tokens.Single();
        rig.Caret(chip.End);
        rig.Press(PhysicalKey.Backspace);
        rig.Editor.Text.ShouldBe("Mention ticket  now");
        rig.Editor.Text = "Mention ticket {{ticket_id}} now";
        rig.Caret(chip.Start);
        rig.Press(PhysicalKey.Delete);
        rig.Editor.Text.ShouldBe("Mention ticket  now");

        // Alt+Backspace after "{{ticket_id}} ": the chip and the space; again: "ticket ".
        rig.Editor.Text = "Mention ticket {{ticket_id}} now";
        rig.Caret(chip.End + 1);
        rig.Press(PhysicalKey.Backspace, RawInputModifiers.Alt);
        rig.Editor.Text.ShouldBe("Mention ticket now");
        rig.Press(PhysicalKey.Backspace, RawInputModifiers.Alt);
        rig.Editor.Text.ShouldBe("Mention now");
        // Alt+Delete before a chip removes it whole.
        rig.Editor.Text = "Mention {{ticket_id}} now";
        rig.Caret("Mention ".Length);
        rig.Press(PhysicalKey.Delete, RawInputModifiers.Alt);
        rig.Editor.Text.ShouldBe("Mention  now");
    }

    [AvaloniaFact]
    public void Copy_Writes_The_Markers()
    {
        using var rig = new Rig("Mention ticket {{ticket_id}} now");
        var chip = rig.Inner.Tokens.Single();
        rig.Caret(chip.Start);
        rig.Inner.Select(chip.Start - 7, chip.End);
        rig.Pump();
        rig.Inner.Box!.SelectedText.ShouldBe("ticket {{ticket_id}}");
        rig.Inner.Box.Copy();
        rig.Pump();
        var clipboard = TopLevel.GetTopLevel(rig.Window)!.Clipboard!;
        var task = clipboard.TryGetTextAsync();
        while (!task.IsCompleted)
        {
            rig.Pump();
        }
        task.Result.ShouldBe("ticket {{ticket_id}}");
    }

    #endregion

    #region The "{{" list

    private static void OpenList(Rig rig, string typed = "{{")
    {
        rig.CaretAtEnd();
        rig.Type(" " + typed);
    }

    [AvaloniaFact]
    public void Typing_Two_Braces_Opens_The_Variables_Below_The_Caret()
    {
        using var rig = new Rig("Sign as");
        rig.CaretAtEnd();
        rig.Type(" {");
        rig.Editor.IsListOpen.ShouldBeFalse(); // one brace is text
        rig.Type("{");
        rig.Editor.IsListOpen.ShouldBeTrue();
        rig.Pop.Rows.Select(r => r.Name).ShouldBe(Variables);
        rig.Pop.ActiveIndex.ShouldBe(0);
        rig.Pop.LastAnnouncement.ShouldBe("4 variables");
        AutomationProperties.GetItemStatus(rig.Inner).ShouldBe("buyer_name");
        rig.Inner.SuggestionsExpanded.ShouldBeTrue();
        AutomationProperties.GetName(rig.Pop).ShouldBe("Variables");
        // 240 wide, 34px rows; its left edge 10 left of the caret, its top 6 below the caret's line.
        rig.Pop.Bounds.Width.ShouldBe(240);
        rig.Pop.RowViews[0].Bounds.Height.ShouldBe(34);
        var caret = rig.Inner.TriggerState!.CaretRect;
        var anchor = rig.Inner.TranslatePoint(new Point(caret.X - 10, caret.Bottom + 6), rig.Window)!.Value;
        var origin = rig.Pop.TranslatePoint(default, rig.Window)!.Value;
        origin.X.ShouldBe(anchor.X, 1);
        origin.Y.ShouldBe(anchor.Y, 1);
    }

    [AvaloniaFact]
    public void Typing_Filters_And_A_New_Name_Gets_An_Add_Row()
    {
        using var rig = new Rig("Sign as");
        OpenList(rig, "{{ti");
        rig.Pop.Rows.ShouldBe([new PromptVariableRow("ticket_id", false), new PromptVariableRow("ti", true)]);
        rig.Pop.RowText(rig.Pop.Rows[1]).ShouldBe("Add ti");
        rig.Pop.LastAnnouncement.ShouldBe("2 variables");
        // An invalid name empties the list.
        rig.Type("-");
        rig.Editor.IsListOpen.ShouldBeFalse();
        rig.Press(PhysicalKey.Backspace);
        rig.Editor.IsListOpen.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Arrows_Wrap_And_Enter_Inserts_The_Active_Variable()
    {
        using var rig = new Rig("Sign as");
        OpenList(rig);
        rig.Press(PhysicalKey.ArrowUp);
        rig.Pop.ActiveIndex.ShouldBe(3);
        rig.Press(PhysicalKey.ArrowDown);
        rig.Pop.ActiveIndex.ShouldBe(0);
        rig.Press(PhysicalKey.ArrowDown);
        AutomationProperties.GetItemStatus(rig.Inner).ShouldBe("ticket_id");
        rig.Press(PhysicalKey.Enter);
        rig.Editor.Text.ShouldBe("Sign as {{ticket_id}}");
        rig.Inner.CaretIndex.ShouldBe(rig.Editor.Text.Length);
        rig.Editor.IsListOpen.ShouldBeFalse();
        rig.Added.ShouldBeEmpty();
        // Tab inserts too.
        rig.Type(" {{ref");
        rig.Press(PhysicalKey.Tab);
        rig.Editor.Text.ShouldBe("Sign as {{ticket_id}} {{refund_rule}}");
    }

    [AvaloniaFact]
    public void Escape_Closes_The_List_And_Keeps_What_Was_Typed()
    {
        using var rig = new Rig("Sign as");
        OpenList(rig, "{{ag");
        rig.Press(PhysicalKey.Escape);
        rig.Editor.IsListOpen.ShouldBeFalse();
        rig.Editor.Text.ShouldBe("Sign as {{ag");
        rig.Inner.SuggestionsExpanded.ShouldBeFalse();
        // Typing on does not reopen the dismissed query.
        rig.Type("e");
        rig.Editor.IsListOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Add_Inserts_A_New_Variable_And_Raises_VariableAdded()
    {
        using var rig = new Rig("Sign as");
        OpenList(rig, "{{agent_name");
        rig.Pop.Rows[^1].ShouldBe(new PromptVariableRow("agent_name", true));
        rig.Pop.SetActive(rig.Pop.Rows.Count - 1);
        rig.Press(PhysicalKey.Enter);
        rig.Editor.Text.ShouldBe("Sign as {{agent_name}}");
        rig.Added.ShouldBe(["agent_name"]);
    }

    [AvaloniaFact]
    public void A_Click_On_A_Row_Inserts_It_And_The_Editor_Keeps_Focus()
    {
        using var rig = new Rig("Sign as");
        OpenList(rig);
        var row = rig.Pop.RowViews[2];
        var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), rig.Window)!.Value;
        rig.Window.MouseMove(center);
        rig.Pump();
        rig.Pop.ActiveIndex.ShouldBe(2);
        rig.Click(center);
        rig.Editor.Text.ShouldBe("Sign as {{refund_rule}}");
        rig.Inner.Box!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Closing_Braces_Turn_A_Typed_Name_Into_A_Chip()
    {
        using var rig = new Rig("Sign as");
        rig.CaretAtEnd();
        rig.Type(" {{agent_name}");
        rig.Inner.Tokens.ShouldBeEmpty();
        rig.Added.ShouldBeEmpty();
        rig.Type("}");
        rig.Inner.Tokens.Single().Id.ShouldBe("agent_name");
        rig.Added.ShouldBe(["agent_name"]);
        rig.Editor.IsListOpen.ShouldBeFalse();
        // A known name adds nothing.
        rig.Type(" {{ticket_id}}");
        rig.Added.ShouldBe(["agent_name"]);
    }

    [AvaloniaFact]
    public void The_List_Leaves_The_Keys_Alone_During_An_IME_Composition()
    {
        using var rig = new Rig("Sign as");
        OpenList(rig);
        rig.Inner.Box!.Presenter!.PreeditText = "ni";
        rig.Press(PhysicalKey.ArrowDown);
        rig.Pop.ActiveIndex.ShouldBe(0);
        rig.Inner.Box.Presenter.PreeditText = null;
        rig.Press(PhysicalKey.ArrowDown);
        rig.Pop.ActiveIndex.ShouldBe(1);
    }

    #endregion

    #region Paste, undo, submit

    [AvaloniaFact]
    public async Task Paste_Normalizes_Markers_And_Adds_New_Names()
    {
        using var rig = new Rig("Thanks");
        rig.CaretAtEnd();
        var clipboard = TopLevel.GetTopLevel(rig.Window)!.Clipboard!;
        await clipboard.SetTextAsync(", {{ agent_sign }} and {{ tracking_url }} for {{buyer_name}}");
        rig.Inner.Box!.Paste();
        for (var i = 0; i < 50 && !rig.Editor.Text.Contains("for", StringComparison.Ordinal); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            rig.Pump();
        }
        rig.Pump();
        rig.Editor.Text.ShouldBe("Thanks, {{agent_sign}} and {{tracking_url}} for {{buyer_name}}");
        rig.Inner.CaretIndex.ShouldBe(rig.Editor.Text.Length);
        rig.Inner.Tokens.Count.ShouldBe(3);
        rig.Added.ShouldBe(["tracking_url"]); // agent_sign and buyer_name are variables already
        // One undo takes the whole paste back.
        rig.Press(PhysicalKey.Z, rig.CommandKey());
        rig.Editor.Text.ShouldBe("Thanks");
    }

    [AvaloniaFact]
    public void A_Text_From_The_Owner_Is_Normalized_And_Starts_The_Undo_Over()
    {
        using var rig = new Rig("Hi");
        rig.Editor.Text = "Hi {{ buyer_name }}";
        rig.Editor.Text.ShouldBe("Hi {{buyer_name}}");
        rig.Inner.Tokens.Single().Id.ShouldBe("buyer_name");
        rig.Editor.History.CanUndo.ShouldBeFalse();
        rig.Added.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Undo_Groups_Typing_By_Pauses_And_Spaces_And_Brings_Chips_Back()
    {
        using var rig = new Rig("Hi");
        rig.CaretAtEnd();
        rig.Type(" there friend");
        var command = rig.CommandKey();
        rig.Press(PhysicalKey.Z, command);
        rig.Editor.Text.ShouldBe("Hi there");
        rig.Inner.CaretIndex.ShouldBe(8);
        rig.Press(PhysicalKey.Z, command);
        rig.Editor.Text.ShouldBe("Hi");
        rig.Press(PhysicalKey.Z, command | RawInputModifiers.Shift);
        rig.Editor.Text.ShouldBe("Hi there");
        rig.Press(PhysicalKey.Z, command | RawInputModifiers.Shift);
        rig.Editor.Text.ShouldBe("Hi there friend");

        // A pause longer than the delay splits a word.
        rig.Type("s");
        rig.Now += TimeSpan.FromMilliseconds(1200);
        rig.Type("hip");
        rig.Press(PhysicalKey.Z, command);
        rig.Editor.Text.ShouldBe("Hi there friends");
        // With a longer delay the same pause does not.
        rig.Editor.UndoGroupDelay = TimeSpan.FromSeconds(2);
        rig.Type("!");
        rig.Now += TimeSpan.FromMilliseconds(1200);
        rig.Type("?");
        rig.Press(PhysicalKey.Z, command);
        rig.Editor.Text.ShouldBe("Hi there friends");

        // A deleted chip comes back as a chip.
        rig.Editor.Text = "Mention {{ticket_id}}";
        rig.CaretAtEnd();
        rig.Press(PhysicalKey.Backspace);
        rig.Editor.Text.ShouldBe("Mention ");
        rig.Press(PhysicalKey.Z, command);
        rig.Editor.Text.ShouldBe("Mention {{ticket_id}}");
        rig.Inner.Tokens.Single().Id.ShouldBe("ticket_id");
        rig.Inner.CaretIndex.ShouldBe(rig.Editor.Text.Length);
    }

    [AvaloniaFact]
    public void Ctrl_Or_Cmd_Enter_Submits_Without_A_Line_Break()
    {
        using var rig = new Rig("Hi");
        rig.CaretAtEnd();
        rig.Press(PhysicalKey.Enter, RawInputModifiers.Control);
        rig.Press(PhysicalKey.Enter, RawInputModifiers.Meta);
        rig.Submit.Runs.ShouldBe(2);
        rig.Editor.Text.ShouldBe("Hi");
        // Enter alone adds a line.
        rig.Press(PhysicalKey.Enter);
        rig.Editor.Text.ShouldBe("Hi\n");
        // Not while composing.
        rig.Inner.Box!.Presenter!.PreeditText = "ni";
        rig.Press(PhysicalKey.Enter, RawInputModifiers.Control);
        rig.Submit.Runs.ShouldBe(2);
    }

    #endregion

    #region Placeholder, read-only, growth

    [AvaloniaFact]
    public void An_Empty_Editor_Shows_The_Placeholder()
    {
        using var rig = new Rig("");
        var placeholder = rig.Inner.Box!.PlaceholderBlock!;
        placeholder.IsVisible.ShouldBeTrue();
        placeholder.Text.ShouldBe("Describe the assistant's role and rules");
        rig.Editor.Text = "x";
        rig.Pump();
        placeholder.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Read_Only_Editor_Can_Be_Selected_And_Copied_But_Not_Changed()
    {
        using var rig = new Rig("Hi {{buyer_name}}", configure: e => e.IsReadOnly = true);
        rig.CaretAtEnd();
        rig.Type("{{");
        rig.Editor.Text.ShouldBe("Hi {{buyer_name}}");
        rig.Editor.IsListOpen.ShouldBeFalse();
        rig.Press(PhysicalKey.Backspace);
        rig.Editor.Text.ShouldBe("Hi {{buyer_name}}");
        rig.Inner.Select(0, 2);
        rig.Pump();
        rig.Inner.Box!.SelectedText.ShouldBe("Hi");
        // Chips still invoke.
        rig.Click(rig.TextPoint(rig.ChipBox("buyer_name").Center));
        rig.Invoked.ShouldBe(["buyer_name"]);
    }

    [AvaloniaFact]
    public void The_Editor_Grows_With_Its_Lines_Up_To_MaxLines()
    {
        using var rig = new Rig("one", configure: e => e.MaxLines = 3);
        rig.Editor.Bounds.Height.ShouldBe(23.8 + 8, 0.6);
        rig.Editor.Text = "one\ntwo";
        rig.Pump();
        rig.Editor.Bounds.Height.ShouldBe(2 * 23.8 + 8, 0.6);
        rig.Editor.Text = "one\ntwo\nthree\nfour\nfive";
        rig.Pump();
        rig.Editor.Bounds.Height.ShouldBe(3 * 23.8 + 8, 0.6);
        // A wrapped paragraph with chips grows too.
        rig.Editor.MaxLines = 16;
        rig.Editor.Text = System;
        rig.Pump();
        rig.Editor.Bounds.Height.ShouldBeGreaterThan(7 * 23.8);
    }

    #endregion
}
