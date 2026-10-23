using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.SpecialInputs;

/// <summary>
/// The shared token editor (extracted from MentionInput): triggers, token insertion, atomic tokens, reconciliation,
/// submit, autosize and the mirror. Sample data is our own (AI composer spec, section 10).
/// </summary>
public class TokenTextEditorTests
{
    private const string Command = "Command";
    private const string Mention = "Mention";

    private static TokenTrigger SlashTrigger() => new('/', Command) { Position = TokenTriggerPosition.LineStart };

    private static TokenTrigger AtTrigger() => new('@', Mention) { Position = TokenTriggerPosition.AfterWhitespaceOrLineStart };

    private static TokenTrigger[] ComposerTriggers() => [SlashTrigger(), AtTrigger()];

    private static TokenTextEditor Create(Action<TokenTextEditor>? configure = null)
    {
        var editor = new TokenTextEditor { Width = 360 };
        editor.Triggers.Add(SlashTrigger());
        editor.Triggers.Add(AtTrigger());
        configure?.Invoke(editor);
        return editor;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { control } }, level);
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

    private static void Type(Window window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    private static TokenTextEditor Focused(TokenTextEditor editor)
    {
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    private static RawInputModifiers CommandKey()
    {
        var hotkeys = Application.Current!.PlatformSettings?.HotkeyConfiguration;
        return hotkeys?.CommandModifiers == KeyModifiers.Meta ? RawInputModifiers.Meta : RawInputModifiers.Control;
    }

    private static TokenSpan Span(string text, string kind, string id, string tokenText, string label)
    {
        var start = text.IndexOf(tokenText, StringComparison.Ordinal);
        return new TokenSpan(kind, id, label, start, start + tokenText.Length);
    }

    #region Engine

    [Fact]
    public void Slash_Opens_Only_At_The_Start_Of_A_Line_With_A_Query_Without_Spaces()
    {
        var triggers = ComposerTriggers();
        var match = TokenTextEngine.FindTrigger("/re", 3, [], triggers)!.Value;
        match.Trigger.Kind.ShouldBe(Command);
        match.Start.ShouldBe(0);
        match.Query.ShouldBe("re");
        TokenTextEngine.FindTrigger("/", 1, [], triggers)!.Value.Query.ShouldBe(string.Empty);
        TokenTextEngine.FindTrigger("x /re", 5, [], triggers).ShouldBeNull(); // not at the line start
        TokenTextEngine.FindTrigger("  /re", 5, [], triggers).ShouldBeNull(); // indentation is not the line start
        TokenTextEngine.FindTrigger("notes\n/re", 9, [], triggers)!.Value.Start.ShouldBe(6); // after a line break
        TokenTextEngine.FindTrigger("/re do", 6, [], triggers).ShouldBeNull(); // the query never holds a space
        TokenTextEngine.FindTrigger("/re", 1, [], triggers)!.Value.Query.ShouldBe(string.Empty); // up to the caret only
    }

    [Fact]
    public void At_Opens_At_A_Line_Start_Or_After_Whitespace_But_Never_After_A_Bracket()
    {
        var triggers = ComposerTriggers();
        TokenTextEngine.FindTrigger("@exp", 4, [], triggers)!.Value.Query.ShouldBe("exp");
        TokenTextEngine.FindTrigger("see @exp", 8, [], triggers)!.Value.Start.ShouldBe(4);
        TokenTextEngine.FindTrigger("a\n@exp", 6, [], triggers)!.Value.Start.ShouldBe(2);
        TokenTextEngine.FindTrigger("see (@exp", 9, [], triggers).ShouldBeNull(); // not after "("
        TokenTextEngine.FindTrigger("mail@exp", 8, [], triggers).ShouldBeNull(); // not inside a word
        TokenTextEngine.FindTrigger("@exp ort", 8, [], triggers).ShouldBeNull(); // no spaces
        TokenTextEngine.FindTrigger("@exp\nx", 6, [], triggers).ShouldBeNull(); // never across a line
        // With brackets and one space allowed (MentionInput's person rule).
        var people = new TokenTrigger('@', "Person") { AllowAfterOpeningBracket = true, AllowSpacesInQuery = true };
        TokenTextEngine.FindTrigger("(@ma", 4, [], [people])!.Value.Start.ShouldBe(1);
        TokenTextEngine.FindTrigger("@maya chen", 10, [], [people])!.Value.Query.ShouldBe("maya chen");
        TokenTextEngine.FindTrigger("@a b c", 6, [], [people]).ShouldBeNull(); // one space at most by default
        people.MaxQuerySpaces = 2;
        TokenTextEngine.FindTrigger("@a b c", 6, [], [people]).ShouldNotBeNull();
        var anywhere = new TokenTrigger('#', "Tag") { Position = TokenTriggerPosition.Anywhere };
        TokenTextEngine.FindTrigger("issue#12", 8, [], [anywhere])!.Value.Start.ShouldBe(5);
    }

    [Fact]
    public void A_Trigger_Inside_A_Token_Or_Of_A_Disabled_Kind_Does_Not_Open()
    {
        var triggers = ComposerTriggers();
        var token = new TokenSpan(Mention, "f1", "export-worker.ts", 0, 17);
        TokenTextEngine.FindTrigger("@export-worker.ts", 17, [token], triggers).ShouldBeNull();
        // A disabled trigger still claims its symbol: the scan stops there (MentionInput's "kind off").
        var people = new TokenTrigger('@', "Person") { IsEnabled = false };
        var channels = new TokenTrigger('#', "Channel");
        TokenTextEngine.FindTrigger("#d @x", 5, [], [people, channels]).ShouldBeNull();
        TokenTextEngine.FindTrigger("#d", 2, [], [people, channels]).ShouldNotBeNull();
        // Another trigger's symbol ends a query.
        TokenTextEngine.FindTrigger("@ma#", 4, [], [new TokenTrigger('@', "Person"), channels]).ShouldBeNull();
        // At most 48 characters back.
        TokenTextEngine.FindTrigger("@" + new string('a', 47), 48, [], triggers).ShouldNotBeNull();
        TokenTextEngine.FindTrigger("@" + new string('a', 48), 49, [], triggers).ShouldBeNull();
    }

    [Fact]
    public void Reconcile_Moves_Tokens_With_Edits_And_Drops_Touched_Ones()
    {
        const string text = "/recap @Sprint 15 review today";
        TokenSpan[] tokens =
        [
            Span(text, Command, "recap", "/recap", "recap"),
            Span(text, Mention, "doc1", "@Sprint 15 review", "Sprint 15 review")
        ];
        // An edit after both keeps them.
        TokenTextEngine.Reconcile(text, tokens, text + "!", text.Length).ShouldBe(tokens);
        // An edit between them moves only the second.
        var moved = TokenTextEngine.Reconcile(text, tokens, "/recap the @Sprint 15 review today", 7);
        moved.Select(t => t.Start).ShouldBe([0, 11]);
        moved[1].End.ShouldBe(28);
        // An edit inside the first dissolves it.
        var touched = TokenTextEngine.Reconcile(text, tokens, "/reXcap @Sprint 15 review today", 3);
        touched.Single().Id.ShouldBe("doc1");
        touched[0].Start.ShouldBe(8);
        // Repeated characters settle with the caret hint.
        TokenSpan[] spaced = [new TokenSpan(Mention, "f", "x", 2, 4)];
        TokenTextEngine.Reconcile("a  @x", spaced, "a @x", 1).Single().Start.ShouldBe(1);
    }

    #endregion

    [AvaloniaFact]
    public void Defaults_Are_The_Composer_Text_Area()
    {
        var editor = Create();
        var window = Show(editor);
        try
        {
            editor.Text.ShouldBe(string.Empty);
            editor.Tokens.ShouldBeEmpty();
            editor.MinLines.ShouldBe(1);
            editor.MaxLines.ShouldBe(8);
            editor.SubmitOnEnter.ShouldBeTrue();
            editor.ExpandSelectionToTokens.ShouldBeFalse();
            editor.TokenDecorationBreak.ShouldBe(TokenDecorationBreak.Slice);
            editor.TriggerState.ShouldBeNull();
            TokenTextEditor.TextProperty.GetMetadata(typeof(TokenTextEditor)).DefaultBindingMode.ShouldBe(Avalonia.Data.BindingMode.TwoWay);
            editor.Bounds.Height.ShouldBe(40, 0.5); // one 24px line + 8px padding twice
            var box = editor.Box!;
            box.AcceptsReturn.ShouldBeTrue();
            box.TextWrapping.ShouldBe(TextWrapping.Wrap);
            box.FontSize.ShouldBe(16);
            box.LineHeight.ShouldBe(24);
            box.LetterSpacing.ShouldBe(-0.16, 0.001);
            box.Padding.ShouldBe(new Thickness(8));
            TestHelpers.ColorOf(box.Foreground).A.ShouldBe((byte)0); // the mirror paints the text
            editor.TokenCornerRadius.ShouldBe(6);
            editor.TokenOutset.ShouldBe(2);
            editor.TokenPadding.ShouldBe(default);
            TestHelpers.ColorOf(editor.TokenForeground).ShouldBe(ArcTokens.GetColor(editor, DesignTokens.ArcDesignTokenKind.AccentStrong));
            var accent = ArcTokens.GetColor(editor, DesignTokens.ArcDesignTokenKind.Accent);
            var fill = TestHelpers.ColorOf(editor.Box!.Mirror!.ResolveFill(Command));
            ((int)fill.A).ShouldBeInRange(30, 31); // Arc: color-mix(accent 12%)
            (fill.R, fill.G, fill.B).ShouldBe((accent.R, accent.G, accent.B));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_A_Trigger_Reports_Its_Kind_Query_And_Geometry()
    {
        var editor = Create();
        var states = new List<TokenTriggerState?>();
        editor.TriggerChanged += (_, e) => states.Add(e.State);
        var window = Show(editor);
        try
        {
            Focused(editor);
            Type(window, "/re");
            var state = editor.TriggerState.ShouldNotBeNull();
            state.Trigger.Kind.ShouldBe(Command);
            state.Kind.ShouldBe(Command);
            state.Start.ShouldBe(0);
            state.Query.ShouldBe("re");
            states.Last().ShouldBe(state);
            // Geometry in the editor's coordinates: the trigger character's line box and the caret.
            state.TriggerRect.X.ShouldBe(8, 0.5);
            state.TriggerRect.Y.ShouldBe(8, 0.5);
            state.TriggerRect.Height.ShouldBe(24, 0.5);
            state.CaretRect.X.ShouldBeGreaterThan(state.TriggerRect.X);
            state.CaretRect.Y.ShouldBe(8, 0.5);
            var count = states.Count;
            Type(window, " ");
            editor.TriggerState.ShouldBeNull(); // a space ends the query
            states.Count.ShouldBe(count + 1);
            states.Last().ShouldBeNull();
            Type(window, "/");
            editor.TriggerState.ShouldBeNull(); // not at the line start
            Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
            Type(window, "/");
            editor.TriggerState!.Start.ShouldBe(6);
            editor.TriggerState.TriggerRect.Y.ShouldBe(8 + 24, 0.5); // second line
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_Never_Opens_After_A_Bracket_And_Closes_On_A_Space()
    {
        var editor = Create();
        var window = Show(editor);
        try
        {
            Focused(editor);
            Type(window, "see (@ex");
            editor.TriggerState.ShouldBeNull();
            Type(window, " and @ex");
            editor.TriggerState!.Kind.ShouldBe(Mention);
            editor.TriggerState.Query.ShouldBe("ex");
            Type(window, "p ");
            editor.TriggerState.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Trigger_Follows_Focus_And_The_Caret()
    {
        var other = new TextBox();
        var editor = Create();
        var window = Show(new StackPanel { Children = { editor, other } });
        try
        {
            Focused(editor);
            Type(window, "hi @sp");
            editor.TriggerState.ShouldNotBeNull();
            other.Focus();
            Dispatcher.UIThread.RunJobs();
            editor.TriggerState.ShouldBeNull();
            Focused(editor);
            editor.TriggerState.ShouldNotBeNull();
            Press(window, PhysicalKey.Home);
            editor.TriggerState.ShouldBeNull(); // the caret left the query
            Press(window, PhysicalKey.End);
            editor.TriggerState!.Query.ShouldBe("sp");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_List_Owner_Handling_Keys_On_The_Editor_Sees_The_Current_Trigger()
    {
        var editor = Create(e => e.SetTextWithTokens("@exp and more", []));
        var seen = new List<TokenTriggerState?>();
        editor.AddHandler(InputElement.KeyDownEvent, (_, _) => seen.Add(editor.TriggerState), Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var window = Show(editor);
        try
        {
            Focused(editor);
            editor.TriggerState.ShouldBeNull(); // the caret is at the end, past a space
            editor.Select(4, 4); // queues a refresh
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None); // before the queue runs
            seen.Single()!.Query.ShouldBe("exp");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Trigger_Geometry_Is_Reported_Again_When_The_Text_Reflows()
    {
        var editor = Create();
        var states = new List<TokenTriggerState?>();
        editor.TriggerChanged += (_, e) => states.Add(e.State);
        var window = Show(editor);
        try
        {
            Focused(editor);
            Type(window, "Compare the drop-off with @exp");
            var before = editor.TriggerState!.TriggerRect;
            var count = states.Count;
            editor.Width = 180; // the query wraps onto the next line
            Dispatcher.UIThread.RunJobs();
            states.Count.ShouldBe(count + 1);
            var after = states.Last()!;
            after.Query.ShouldBe("exp");
            after.TriggerRect.Y.ShouldBeGreaterThan(before.Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CancelTrigger_Closes_Until_Another_Trigger_Starts()
    {
        var editor = Create();
        var window = Show(editor);
        try
        {
            Focused(editor);
            Type(window, "@sp");
            editor.CancelTrigger();
            editor.TriggerState.ShouldBeNull();
            Type(window, "r");
            editor.TriggerState.ShouldBeNull(); // the same trigger stays dismissed
            Type(window, " @ex");
            editor.TriggerState!.Start.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InsertToken_Replaces_The_Trigger_And_Undo_And_Redo_Restore_Tokens()
    {
        var editor = Create();
        var inserted = new List<TokenSpan>();
        var changes = new List<TokenTextChangedEventArgs>();
        editor.TokenInserted += (_, e) => inserted.Add(e.Token);
        editor.TextChanged += (_, e) => changes.Add(e);
        var window = Show(editor);
        try
        {
            Focused(editor);
            Type(window, "/re");
            var token = editor.InsertToken(Command, "recap", "recap");
            Dispatcher.UIThread.RunJobs();
            var expected = new TokenSpan(Command, "recap", "recap", 0, 6);
            token.ShouldBe(expected);
            editor.Text.ShouldBe("/recap ");
            editor.Tokens.Single().ShouldBe(expected);
            editor.Box!.CaretIndex.ShouldBe(7);
            inserted.Single().ShouldBe(expected);
            changes.Last().Text.ShouldBe("/recap ");
            changes.Last().Tokens.Single().ShouldBe(expected);
            changes.Last().OldText.ShouldBe("/re");
            editor.TriggerState.ShouldBeNull();
            // Through the TextBox: undo takes it back, redo brings the token back with its text.
            editor.Box.CanUndo.ShouldBeTrue();
            editor.Undo();
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("/re");
            editor.Tokens.ShouldBeEmpty();
            editor.Redo();
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("/recap ");
            editor.Tokens.Single().ShouldBe(expected);
            inserted.Count.ShouldBe(1); // redo is not a new insertion
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InsertToken_Without_An_Active_Trigger_Does_Nothing_And_A_Following_Space_Is_Not_Doubled()
    {
        var editor = Create();
        var window = Show(editor);
        try
        {
            Focused(editor);
            editor.InsertToken(Mention, "f1", "export-worker.ts").ShouldBeNull();
            editor.Text.ShouldBe(string.Empty);
            editor.SetTextWithTokens("@exp now", []);
            editor.Select(4, 4);
            Dispatcher.UIThread.RunJobs();
            editor.TriggerState!.Query.ShouldBe("exp");
            editor.InsertToken(Mention, "f1", "export-worker.ts");
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("@export-worker.ts now");
            editor.Tokens.Single().End.ShouldBe(17);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Undo_Brings_Back_A_Deleted_Token()
    {
        const string text = "Ask @Sprint 15 review now";
        var editor = Create(e => e.SetTextWithTokens(text, [Span(text, Mention, "doc1", "@Sprint 15 review", "Sprint 15 review")]));
        var window = Show(editor);
        try
        {
            Focused(editor);
            editor.Select(21, 21);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Backspace);
            editor.Text.ShouldBe("Ask  now");
            editor.Tokens.ShouldBeEmpty();
            Press(window, PhysicalKey.Z, CommandKey());
            editor.Text.ShouldBe(text);
            editor.Tokens.Single().Id.ShouldBe("doc1");
            editor.Redo();
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("Ask  now");
            editor.Tokens.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Retyping_A_Dissolved_Token_Does_Not_Bring_It_Back()
    {
        const string text = "/recap notes";
        var editor = Create(e => e.SetTextWithTokens(text, [new TokenSpan(Command, "recap", "recap", 0, 6)]));
        var window = Show(editor);
        try
        {
            Focused(editor);
            editor.TypeText("X", 3, 3); // a programmatic edit inside the token
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("/reXcap notes");
            editor.Tokens.ShouldBeEmpty();
            editor.Select(4, 4);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Backspace);
            editor.Text.ShouldBe(text);
            editor.Tokens.ShouldBeEmpty(); // only undo and redo restore tokens
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void InsertTokenAtCaret_Adds_The_Kind_Symbol_And_A_Space()
    {
        var editor = Create(e => e.SetTextWithTokens("Look at ", []));
        var window = Show(editor);
        try
        {
            Focused(editor);
            var token = editor.InsertTokenAtCaret(Mention, "f1", "export-worker.ts");
            Dispatcher.UIThread.RunJobs();
            token.ShouldBe(new TokenSpan(Mention, "f1", "export-worker.ts", 8, 25));
            editor.Text.ShouldBe("Look at @export-worker.ts ");
            editor.Box!.CaretIndex.ShouldBe(26);
            // A kind without a trigger is inserted as its label.
            editor.InsertTokenAtCaret("Tag", "t1", "urgent");
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("Look at @export-worker.ts urgent ");
            editor.Tokens.Select(t => t.Kind).ShouldBe([Mention, "Tag"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SetTextWithTokens_Keeps_Valid_Spans_And_Moves_The_Caret_To_The_End()
    {
        var editor = Create();
        var window = Show(editor);
        try
        {
            const string text = "/debug @export-worker.ts stalls";
            editor.SetTextWithTokens(text,
            [
                new TokenSpan(Mention, "f1", "export-worker.ts", 7, 24),
                new TokenSpan(Command, "debug", "debug", 0, 6),
                new TokenSpan(Mention, "bad", "overlap", 5, 9), // overlaps both
                new TokenSpan(Mention, "out", "outside", 28, 40) // past the end
            ]);
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe(text);
            editor.Tokens.Select(t => t.Id).ShouldBe(["debug", "f1"]);
            editor.Box!.CaretIndex.ShouldBe(text.Length);
            editor.Box.Presenter!.Tokens.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Hop_Over_A_Token_And_Shift_Arrows_Select_By_Character()
    {
        const string text = "/recap the notes";
        var editor = Create(e => e.SetTextWithTokens(text, [new TokenSpan(Command, "recap", "recap", 0, 6)]));
        var window = Show(editor);
        try
        {
            Focused(editor);
            var box = editor.Box!;
            editor.Select(6, 6);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowLeft);
            box.CaretIndex.ShouldBe(0); // one step hops over the token
            Press(window, PhysicalKey.ArrowRight);
            box.CaretIndex.ShouldBe(6);
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            Press(window, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            box.SelectionStart.ShouldBe(6);
            box.SelectionEnd.ShouldBe(4); // by character, not the whole token
            // MentionInput's rule: selections grow to whole tokens.
            editor.ExpandSelectionToTokens = true;
            editor.Select(8, 3);
            Dispatcher.UIThread.RunJobs();
            box.SelectionStart.ShouldBe(8);
            box.SelectionEnd.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Caret_Inside_A_Token_Snaps_To_The_Nearest_Edge()
    {
        const string text = "/recap the notes";
        var editor = Create(e => e.SetTextWithTokens(text, [new TokenSpan(Command, "recap", "recap", 0, 6)]));
        var window = Show(editor);
        try
        {
            Focused(editor);
            var box = editor.Box!;
            box.CaretIndex = 2;
            Dispatcher.UIThread.RunJobs();
            box.CaretIndex.ShouldBe(0);
            box.CaretIndex = 4;
            Dispatcher.UIThread.RunJobs();
            box.CaretIndex.ShouldBe(6);
            // A click one character inside the end lands on the end, even right after the caret was there.
            Click(window, editor, 5);
            box.CaretIndex.ShouldBe(6);
            Click(window, editor, 1);
            box.CaretIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_After_And_Delete_Before_A_Token_Remove_It_Whole()
    {
        const string text = "Ask @Sprint 15 review now";
        var span = Span(text, Mention, "doc1", "@Sprint 15 review", "Sprint 15 review");
        var editor = Create(e => e.SetTextWithTokens(text, [span]));
        var window = Show(editor);
        try
        {
            Focused(editor);
            editor.Select(21, 21);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Backspace);
            editor.Text.ShouldBe("Ask  now");
            editor.Tokens.ShouldBeEmpty();
            editor.SetTextWithTokens(text, [span]);
            editor.Select(4, 4);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Delete);
            editor.Text.ShouldBe("Ask  now");
            editor.Box!.CaretIndex.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Edits_Before_A_Token_Move_It_And_Programmatic_Text_Reconciles()
    {
        const string text = "/recap @Sprint 15 review";
        var editor = Create(e => e.SetTextWithTokens(text,
        [
            new TokenSpan(Command, "recap", "recap", 0, 6),
            Span(text, Mention, "doc1", "@Sprint 15 review", "Sprint 15 review")
        ]));
        var window = Show(editor);
        try
        {
            Focused(editor);
            editor.Select(7, 7);
            Dispatcher.UIThread.RunJobs();
            Type(window, "the ");
            editor.Tokens.Select(t => t.Start).ShouldBe([0, 11]);
            // Setting Text keeps the tokens the edit did not touch.
            editor.Text = "/recap the @Sprint 15 review, please";
            Dispatcher.UIThread.RunJobs();
            editor.Tokens.Count.ShouldBe(2);
            editor.Text = "/recap the @Sprint 16 review, please";
            Dispatcher.UIThread.RunJobs();
            editor.Tokens.Single().Kind.ShouldBe(Command); // the edit inside dissolved the mention
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Requests_Submit_And_Shift_Enter_Adds_A_Line()
    {
        var editor = Create();
        var submits = new List<TokenTextSubmitRequestedEventArgs>();
        editor.SubmitRequested += (_, e) => submits.Add(e);
        var window = Show(editor);
        try
        {
            Focused(editor);
            Type(window, "Ship it");
            Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
            editor.Text.ShouldBe("Ship it\n");
            submits.ShouldBeEmpty();
            Type(window, "today");
            Press(window, PhysicalKey.Enter);
            editor.Text.ShouldBe("Ship it\ntoday"); // no line break
            submits.Single().Text.ShouldBe("Ship it\ntoday");
            submits[0].Tokens.ShouldBeEmpty();
            // No submit while composing (IME), while read-only, or with SubmitOnEnter off.
            editor.Box!.Presenter!.PreeditText = "ni";
            Press(window, PhysicalKey.Enter);
            submits.Count.ShouldBe(1);
            editor.Box.Presenter.PreeditText = null;
            editor.IsReadOnly = true;
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.Enter);
            submits.Count.ShouldBe(1);
            editor.IsReadOnly = false;
            editor.SubmitOnEnter = false;
            Dispatcher.UIThread.RunJobs();
            editor.Select(editor.Text.Length, editor.Text.Length);
            Press(window, PhysicalKey.Enter);
            submits.Count.ShouldBe(1);
            editor.Text.ShouldEndWith("\n");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Editor_Is_As_Tall_As_Its_Wrapped_Placeholder()
    {
        // Arc measures the placeholder in its mirror: in a narrow box a two-line hint shows whole, and typing shrinks it.
        var editor = Create(e => e.Placeholder = string.Join(" ", Enumerable.Repeat("hint", 30)));
        var window = Show(editor);
        try
        {
            editor.Bounds.Height.ShouldBeGreaterThan(2 * 24 + 16 - 0.5);
            editor.Bounds.Height.ShouldBeLessThanOrEqualTo(8 * 24 + 16 + 0.5);
            Focused(editor);
            Type(window, "a");
            editor.Bounds.Height.ShouldBe(24 + 16, 0.5);
            editor.Placeholder = "Ask";
            editor.Clear();
            Dispatcher.UIThread.RunJobs();
            editor.Bounds.Height.ShouldBe(24 + 16, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Height_Grows_Line_By_Line_To_MaxLines_Then_Scrolls()
    {
        var editor = Create(e =>
        {
            e.MinLines = 2;
            e.MaxLines = 3;
        });
        var window = Show(editor);
        try
        {
            editor.Bounds.Height.ShouldBe(2 * 24 + 16, 0.5);
            Focused(editor);
            Type(window, "one");
            for (var i = 0; i < 2; i++)
            {
                Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
                Type(window, "more");
            }
            editor.Bounds.Height.ShouldBe(3 * 24 + 16, 0.5);
            Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
            Type(window, "and more");
            editor.TargetHeight.ShouldBe(3 * 24 + 16, 0.5);
            editor.Bounds.Height.ShouldBe(3 * 24 + 16, 0.5);
            var scroller = editor.Box!.Scroller!;
            scroller.Extent.Height.ShouldBeGreaterThan(scroller.Viewport.Height);
            scroller.Offset.Y.ShouldBeGreaterThan(0); // the caret's line scrolled into view
            editor.MaxLines = 5;
            Dispatcher.UIThread.RunJobs();
            editor.Bounds.Height.ShouldBe(4 * 24 + 16, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Eases_The_Height_Over_160ms_And_Reduced_Jumps()
    {
        var clock = new ManualFrameClock();
        using (clock.Install())
        {
            var editor = Create();
            var window = Show(editor, ArcMotionLevel.Full);
            try
            {
                editor.Bounds.Height.ShouldBe(40, 0.5); // the first measure jumps
                Focused(editor);
                Type(window, "a");
                Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
                editor.TargetHeight.ShouldBe(64, 0.5);
                editor.Bounds.Height.ShouldBe(40, 0.5);
                clock.Advance(TimeSpan.FromMilliseconds(17));
                Dispatcher.UIThread.RunJobs();
                editor.Bounds.Height.ShouldBeInRange(44, 58); // standard ease: a fast start (Arc: ~40% after 18ms)
                clock.Advance(TimeSpan.FromMilliseconds(33));
                Dispatcher.UIThread.RunJobs();
                editor.Bounds.Height.ShouldBeInRange(56, 63.5); // ~85% after 51ms
                clock.Advance(TimeSpan.FromMilliseconds(120));
                Dispatcher.UIThread.RunJobs();
                editor.Bounds.Height.ShouldBe(64, 0.5); // settled by 160ms
            }
            finally
            {
                window.Close();
            }

            var reduced = Create();
            window = Show(reduced, ArcMotionLevel.Reduced);
            try
            {
                Focused(reduced);
                Type(window, "a");
                Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
                reduced.Bounds.Height.ShouldBe(64, 0.5);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Kinds_Take_Their_Style_And_The_Mirror_Matches_The_Editor_Layout()
    {
        const string text = "/recap @export-worker.ts";
        var editor = Create(e =>
        {
            e.Width = 640; // one line, even with the headless test font
            e.TokenStyles.Add(new TokenStyle { Kind = Mention, Fill = Brushes.Red });
            e.SetTextWithTokens(text,
            [
                new TokenSpan(Command, "recap", "recap", 0, 6),
                Span(text, Mention, "f1", "@export-worker.ts", "export-worker.ts")
            ]);
        });
        var window = Show(editor);
        try
        {
            var mirror = TestHelpers.Descendant<TokenTextMirror>(editor);
            mirror.ShouldBeSameAs(editor.Box!.Mirror);
            TestHelpers.ColorOf(mirror.ResolveFill(Mention)).ShouldBe(Colors.Red);
            TestHelpers.ColorOf(mirror.ResolveForeground(Mention)).ShouldBe(TestHelpers.ColorOf(editor.TokenForeground)); // falls back
            ((int)TestHelpers.ColorOf(mirror.ResolveFill(Command)).A).ShouldBeInRange(30, 31);
            // A style change repaints without touching the text.
            var style = editor.TokenStyles[0];
            style.Foreground = Brushes.Blue;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.ColorOf(mirror.ResolveForeground(Mention)).ShouldBe(Colors.Blue);
            var layout = mirror.EnsureLayout()!;
            var presenter = editor.Box!.Presenter!;
            layout.TextLines.Count.ShouldBe(presenter.TextLayout.TextLines.Count);
            layout.HitTestTextPosition(10).X.ShouldBe(presenter.TextLayout.HitTestTextPosition(10).X, 0.01);
            // One box per token on one line: the glyph box grown by the 2px outset, radius 6 + 2.
            var boxes = mirror.TokenBoxes();
            boxes.Count.ShouldBe(2);
            var glyphs = presenter.TextLayout.HitTestTextRange(0, 6).Single();
            boxes[0].Rect.X.ShouldBe(glyphs.X - 2, 0.01);
            boxes[0].Rect.Width.ShouldBe(glyphs.Width + 4, 0.01);
            boxes[0].Radius.TopLeft.ShouldBe(8);
            boxes[0].Radius.BottomRight.ShouldBe(8);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Wrapped_Token_Is_Drawn_Per_Line_Sliced_Or_Cloned()
    {
        const string text = "Compare @Onboarding flow v2 notes";
        var editor = Create(e =>
        {
            e.Width = 200;
            e.SetTextWithTokens(text, [Span(text, Mention, "doc3", "@Onboarding flow v2", "Onboarding flow v2")]);
        });
        var window = Show(editor);
        try
        {
            var mirror = editor.Box!.Mirror!;
            var boxes = mirror.TokenBoxes();
            boxes.Count.ShouldBeGreaterThanOrEqualTo(2);
            var first = boxes[0];
            var last = boxes[^1];
            first.Rect.Y.ShouldBeLessThan(last.Rect.Y);
            // Slice (CSS default): only the token's own ends are rounded and outset.
            first.Radius.TopLeft.ShouldBe(8);
            first.Radius.TopRight.ShouldBe(0);
            last.Radius.TopLeft.ShouldBe(0);
            last.Radius.BottomRight.ShouldBe(8);
            editor.TokenDecorationBreak = TokenDecorationBreak.Clone;
            Dispatcher.UIThread.RunJobs();
            boxes = mirror.TokenBoxes();
            boxes[0].Radius.TopRight.ShouldBe(8);
            boxes[^1].Radius.TopLeft.ShouldBe(8);
            boxes[0].Rect.Right.ShouldBeGreaterThan(first.Rect.Right);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_TextBox_Is_A_ComboBox_Whose_Expanded_State_The_Owner_Sets()
    {
        var editor = Create();
        var collapses = 0;
        editor.CollapseRequested += (_, _) => collapses++;
        AutomationProperties.SetName(editor, "Message");
        var window = Show(editor);
        try
        {
            var box = editor.Box!;
            AutomationProperties.GetName(box).ShouldBe("Message");
            var peer = ControlAutomationPeer.CreatePeerForElement(box);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.ComboBox);
            var expand = peer.GetProvider<IExpandCollapseProvider>().ShouldNotBeNull();
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            editor.SuggestionsExpanded = true;
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            expand.Collapse();
            collapses.ShouldBe(1);
            AutomationProperties.SetName(editor, "Prompt");
            AutomationProperties.GetName(box).ShouldBe("Prompt");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OpenTrigger_Types_The_Symbol_Where_The_Trigger_Allows_It()
    {
        var editor = Create(e => e.SetTextWithTokens("Look at", []));
        var window = Show(editor);
        try
        {
            editor.OpenTrigger(Mention).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("Look at @");
            editor.Box!.IsFocused.ShouldBeTrue();
            editor.TriggerState!.Kind.ShouldBe(Mention);
            editor.InsertToken(Mention, "f2", "usage-oct.csv");
            Dispatcher.UIThread.RunJobs();
            editor.OpenTrigger(Command).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("Look at @usage-oct.csv \n/"); // a command starts its own line
            editor.TriggerState!.Kind.ShouldBe(Command);
            editor.OpenTrigger("Unknown").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Paste_Bubbles_Through_The_Editor_So_Its_Owner_Can_Take_It()
    {
        var editor = Create();
        var pastes = 0;
        // The composer turns long pastes and files into attachments: it handles the paste before the TextBox does.
        editor.AddHandler(TextBox.PastingFromClipboardEvent, (_, e) =>
        {
            pastes++;
            e.Handled = true;
        });
        var window = Show(editor);
        try
        {
            Focused(editor);
            editor.Box!.Paste();
            Dispatcher.UIThread.RunJobs();
            pastes.ShouldBe(1);
            editor.Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Only_Takes_No_Typing_But_Programmatic_Tokens_Still_Apply()
    {
        var editor = Create(e => e.IsReadOnly = true);
        var window = Show(editor);
        try
        {
            Focused(editor);
            Type(window, "hello");
            editor.Text.ShouldBe(string.Empty);
            editor.InsertTokenAtCaret(Command, "recap", "recap");
            Dispatcher.UIThread.RunJobs();
            editor.Text.ShouldBe("/recap ");
            editor.Tokens.Single().Id.ShouldBe("recap");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Clicks just inside character <paramref name="index"/>, so the caret lands before it.</summary>
    private static void Click(Window window, TokenTextEditor editor, int index)
    {
        var presenter = editor.Box!.Presenter!;
        var rect = presenter.TextLayout.HitTestTextPosition(index);
        var next = presenter.TextLayout.HitTestTextPosition(index + 1);
        var local = new Point(rect.X + (next.X - rect.X) * 0.3, rect.Y + rect.Height / 2); // nearer its left edge
        var point = presenter.TranslatePoint(local, window)!.Value;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
