using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.SpecialInputs;

/// <summary>
/// Inline tokens of <see cref="TokenTextEditor"/> (<see cref="TokenRenderer"/>) and parsed tokens
/// (<see cref="TokenTextEditor.TokenParser"/>): a token drawn as one chip in place of its characters.
/// </summary>
public class TokenTextEditorInlineTests
{
    private const string Chip = "Chip";
    private const double ChipWidth = 30;

    /// <summary>Draws every "{{name}}" token as a 30px box and records what it painted.</summary>
    private sealed class BoxRenderer : TokenRenderer
    {
        public List<(TokenSpan Token, Rect Bounds, double Baseline)> Painted { get; } = new();

        public int Measured { get; private set; }

        public double Width { get; set; } = ChipWidth;

        public override TokenInlineSize? Measure(TokenSpan token, in TokenRenderContext context)
        {
            Measured++;
            return token.Kind == Chip ? new TokenInlineSize(Width, context.Ascent, context.Descent) : null;
        }

        public override void Render(DrawingContext drawingContext, TokenSpan token, Rect bounds, double baseline, in TokenRenderContext context)
        {
            Painted.Add((token, bounds, baseline));
            drawingContext.DrawRectangle(Brushes.Orange, null, bounds);
        }

        public void Grow(double width)
        {
            Width = width;
            InvalidateMeasure();
        }

        public void Repaint() => Invalidate();
    }

    private static readonly Regex s_marker = new(@"\{\{(\w+)\}\}", RegexOptions.CultureInvariant);

    private static IEnumerable<TokenSpan> Parse(string text)
    {
        foreach (Match match in s_marker.Matches(text))
        {
            var name = match.Groups[1].Value;
            yield return new TokenSpan(Chip, name, name, match.Index, match.Index + match.Length);
        }
    }

    private static TokenTextEditor Create(string text, BoxRenderer renderer, double width = 360)
    {
        var editor = new TokenTextEditor { Width = width, SubmitOnEnter = false, TokenParser = Parse, TokenRenderer = renderer };
        editor.Text = text;
        return editor;
    }

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } }, ArcMotionLevel.Off);
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static RawInputModifiers WordKey()
    {
        var modifiers = Application.Current!.PlatformSettings?.HotkeyConfiguration.WholeWordTextActionModifiers ?? KeyModifiers.Control;
        return modifiers == KeyModifiers.Alt ? RawInputModifiers.Alt : RawInputModifiers.Control;
    }

    private static TokenTextEditor Focused(TokenTextEditor editor, int caret)
    {
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        editor.Select(caret, caret);
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    private static double X(TokenTextEditor editor, int index) => editor.Box!.Presenter!.TextLayout.HitTestTextPosition(index).X;

    private static void Record(Visual visual)
    {
        var group = new DrawingGroup();
        using var context = group.Open();
        visual.Render(context);
    }

    [AvaloniaFact]
    public void A_Parsed_Token_Takes_The_Measured_Width_Instead_Of_Its_Characters()
    {
        var renderer = new BoxRenderer();
        var editor = Create("ab{{name}}cd", renderer);
        var window = Show(editor);
        try
        {
            editor.Tokens.ShouldHaveSingleItem().ShouldBe(new TokenSpan(Chip, "name", "name", 2, 10));
            (X(editor, 10) - X(editor, 2)).ShouldBe(ChipWidth, 0.01);
            // Text after the chip follows it directly; a caret after a chip at the end of the text sits after its box.
            (X(editor, 11) - X(editor, 10)).ShouldBe(X(editor, 1) - X(editor, 0), 0.01);
            editor.Text = "ab{{name}}";
            Dispatcher.UIThread.RunJobs();
            (X(editor, 10) - X(editor, 2)).ShouldBe(ChipWidth, 0.01);
            editor.Box!.Presenter!.InlineTokens.ShouldHaveSingleItem();
            // The value keeps every character.
            editor.Text.ShouldBe("ab{{name}}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lines_With_And_Without_Chips_Are_Equally_Tall()
    {
        var renderer = new BoxRenderer();
        var editor = Create("plain line\n{{name}} chip line", renderer);
        var window = Show(editor);
        try
        {
            var lines = editor.Box!.Presenter!.TextLayout.TextLines;
            lines.Count.ShouldBe(2);
            lines[1].Height.ShouldBe(lines[0].Height, 0.001);
            lines[1].Baseline.ShouldBe(lines[0].Baseline, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Step_Over_A_Chip_And_Shift_Selects_It_Whole()
    {
        var renderer = new BoxRenderer();
        var editor = Create("ab{{name}}cd", renderer);
        var window = Show(editor);
        try
        {
            Focused(editor, 2);
            Press(window, PhysicalKey.ArrowRight);
            editor.CaretIndex.ShouldBe(10);
            Press(window, PhysicalKey.ArrowLeft);
            editor.CaretIndex.ShouldBe(2);
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            (editor.Box!.SelectionStart, editor.Box.SelectionEnd).ShouldBe((2, 10));
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            (editor.Box.SelectionStart, editor.Box.SelectionEnd).ShouldBe((2, 11));
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            (editor.Box.SelectionStart, editor.Box.SelectionEnd).ShouldBe((2, 2));
            // A chip that ends the text: one step each way, and a selection over it.
            editor.Text = "ab{{name}}";
            Focused(editor, 2);
            Press(window, PhysicalKey.ArrowRight);
            editor.CaretIndex.ShouldBe(10);
            Press(window, PhysicalKey.ArrowLeft);
            editor.CaretIndex.ShouldBe(2);
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            (editor.Box.SelectionStart, editor.Box.SelectionEnd).ShouldBe((2, 10));
            Focused(editor, 10);
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            (editor.Box.SelectionStart, editor.Box.SelectionEnd).ShouldBe((10, 2));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Word_Jumps_Cross_A_Chip_In_Their_Direction()
    {
        var renderer = new BoxRenderer();
        var editor = Create("say {{name}} now", renderer);
        var window = Show(editor);
        try
        {
            Focused(editor, 4);
            Press(window, PhysicalKey.ArrowRight, WordKey());
            editor.CaretIndex.ShouldBeGreaterThanOrEqualTo(12);
            Focused(editor, 12);
            Press(window, PhysicalKey.ArrowLeft, WordKey());
            editor.CaretIndex.ShouldBeLessThanOrEqualTo(4);
            // A programmatic caret inside a chip snaps to its nearer edge.
            editor.Select(6, 6);
            Dispatcher.UIThread.RunJobs();
            editor.CaretIndex.ShouldBe(4);
            editor.Select(11, 11);
            Dispatcher.UIThread.RunJobs();
            editor.CaretIndex.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_And_Delete_Remove_A_Chip_Whole()
    {
        var renderer = new BoxRenderer();
        var editor = Create("ab{{name}}cd", renderer);
        var window = Show(editor);
        try
        {
            Focused(editor, 10);
            Press(window, PhysicalKey.Backspace);
            editor.Text.ShouldBe("abcd");
            editor.Tokens.ShouldBeEmpty();
            editor.CaretIndex.ShouldBe(2);
            editor.Text = "ab{{name}}cd";
            Focused(editor, 2);
            Press(window, PhysicalKey.Delete);
            editor.Text.ShouldBe("abcd");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_On_A_Chip_Lands_On_Its_Nearer_Edge_And_Hit_Tests_The_Token()
    {
        var renderer = new BoxRenderer { Cursor = new Cursor(StandardCursorType.Hand) };
        var editor = Create("ab{{name}}cd", renderer);
        var window = Show(editor);
        try
        {
            editor.Focus();
            Dispatcher.UIThread.RunJobs();
            var presenter = editor.Box!.Presenter!;
            var box = presenter.TextLayout.HitTestTextRange(2, 8).Single();
            var y = box.Center.Y;
            var right = presenter.TranslatePoint(new Point(box.Right - 4, y), window)!.Value;
            var left = presenter.TranslatePoint(new Point(box.Left + 4, y), window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.MouseDown(right, MouseButton.Left);
            window.MouseUp(right, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.CaretIndex.ShouldBe(10);
            window.MouseDown(left, MouseButton.Left);
            window.MouseUp(left, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.CaretIndex.ShouldBe(2);
            var center = presenter.TranslatePoint(box.Center, editor)!.Value;
            editor.HitTestToken(center)!.Id.ShouldBe("name");
            editor.HitTestToken(center + new Point(ChipWidth, 0)).ShouldBeNull();
            // The renderer's cursor over the chip, the text cursor elsewhere.
            window.MouseMove(presenter.TranslatePoint(box.Center, window)!.Value);
            Dispatcher.UIThread.RunJobs();
            editor.Box.Cursor.ShouldBeSameAs(renderer.Cursor);
            window.MouseMove(presenter.TranslatePoint(box.Center + new Point(ChipWidth, 0), window)!.Value);
            Dispatcher.UIThread.RunJobs();
            editor.Box.Cursor.ShouldNotBeSameAs(renderer.Cursor);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Mirror_Asks_The_Renderer_To_Paint_Chips_And_Tints_Only_Text_Tokens()
    {
        var renderer = new BoxRenderer();
        var editor = Create("ab{{name}}cd", renderer);
        var window = Show(editor);
        try
        {
            var mirror = editor.Box!.Mirror!;
            mirror.TokenBoxes().ShouldBeEmpty();
            renderer.Painted.Clear();
            Record(mirror);
            var painted = renderer.Painted.ShouldHaveSingleItem();
            painted.Token.Id.ShouldBe("name");
            painted.Bounds.Width.ShouldBe(ChipWidth, 0.01);
            painted.Bounds.X.ShouldBe(X(editor, 2), 0.01);
            var line = editor.Box.Presenter!.TextLayout.TextLines[0];
            painted.Baseline.ShouldBe(line.Baseline, 0.01);
            // A chip that ends the text is painted once too.
            editor.Text = "ab{{name}}";
            Dispatcher.UIThread.RunJobs();
            renderer.Painted.Clear();
            Record(mirror);
            renderer.Painted.ShouldHaveSingleItem().Bounds.Width.ShouldBe(ChipWidth, 0.01);
            // Invalidate repaints, InvalidateMeasure lays the text out again.
            var repainted = 0;
            renderer.Invalidated += (_, _) => repainted++;
            renderer.Repaint();
            repainted.ShouldBe(1);
            renderer.Grow(50);
            Dispatcher.UIThread.RunJobs();
            (X(editor, 10) - X(editor, 2)).ShouldBe(50, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Chip_That_Does_Not_Fit_Wraps_Whole()
    {
        var renderer = new BoxRenderer { Width = 60 };
        var editor = Create("one two three {{name}}", renderer, width: 140);
        var window = Show(editor);
        try
        {
            var layout = editor.Box!.Presenter!.TextLayout;
            var start = editor.Text.IndexOf("{{", StringComparison.Ordinal);
            var rects = layout.HitTestTextRange(start, editor.Text.Length - start).ToArray();
            rects.Length.ShouldBe(1);
            rects[0].Width.ShouldBe(60, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Parser_Decides_The_Tokens_For_Typing_And_Programmatic_Text()
    {
        var renderer = new BoxRenderer();
        var editor = Create("hi ", renderer);
        var window = Show(editor);
        try
        {
            Focused(editor, 3);
            window.KeyTextInput("{{x}");
            Dispatcher.UIThread.RunJobs();
            editor.Tokens.ShouldBeEmpty();
            window.KeyTextInput("}");
            Dispatcher.UIThread.RunJobs();
            editor.Tokens.ShouldHaveSingleItem().Id.ShouldBe("x");
            // Tokens handed to SetTextWithTokens are replaced by the parser's.
            editor.SetTextWithTokens("{{a}} {{b}}", [new TokenSpan("Other", "z", "z", 0, 1)]);
            editor.Tokens.Select(t => t.Id).ShouldBe(["a", "b"]);
            // Without a parser the old model holds.
            editor.TokenParser = null;
            editor.SetTextWithTokens("plain", [new TokenSpan("Other", "z", "z", 0, 5)]);
            editor.Tokens.ShouldHaveSingleItem().Kind.ShouldBe("Other");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Renderer_Tokens_Stay_Text()
    {
        var editor = new TokenTextEditor { Width = 360, TokenParser = Parse, Text = "ab{{name}}cd" };
        var window = Show(editor);
        try
        {
            editor.Tokens.ShouldHaveSingleItem();
            editor.Box!.Presenter!.InlineTokens.ShouldBeEmpty();
            editor.Box.Mirror!.TokenBoxes().ShouldHaveSingleItem();
            (X(editor, 10) - X(editor, 2)).ShouldBe(8 * (X(editor, 1) - X(editor, 0)), 0.01);
        }
        finally
        {
            window.Close();
        }
    }
}
