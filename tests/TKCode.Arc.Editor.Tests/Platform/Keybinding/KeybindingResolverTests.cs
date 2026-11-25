// Ported from vscode (src/vs/platform/keybinding/test/common/keybindingResolver.test.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
// - assert.deepStrictEqual on keybinding items compares command, arguments, chords, bubble, default flag, extension
//   and the when clause (ContextKeyExpression.Equals) in AssertItems.
// - The context is TestKeybindingContext, a dictionary (upstream's createContext).

using Shouldly;
using TKCode.Arc.Editor.Platform;
using Xunit;
using static TKCode.Arc.Editor.KeyCodes;

namespace TKCode.Arc.Editor.Tests;

public sealed class KeybindingResolverTests
{
    private static readonly OperatingSystemKind OS = OperatingSystemKinds.OS;

    private static TestKeybindingContext CreateContext(params (string Key, object? Value)[] values) =>
        new(values.ToDictionary(v => v.Key, v => v.Value));

    private static ResolvedKeybindingItem KbItem(int keybinding, string command, object? commandArgs, ContextKeyExpression? when, bool isDefault) =>
        new(KeybindingsTestUtils.CreateUSLayoutResolvedKeybinding(keybinding, OS), command, commandArgs, when, isDefault, null, false);

    private static ResolvedKeybindingItem KbItem(int[] keybinding, string command, object? commandArgs, ContextKeyExpression? when, bool isDefault) =>
        new(KeybindingsTestUtils.CreateUSLayoutResolvedKeybinding(keybinding, OS), command, commandArgs, when, isDefault, null, false);

    private static string GetDispatchStr(KeyCodeChord chord) => USLayoutResolvedKeybinding.GetDispatchStr(chord)!;

    private static void AssertItems(IReadOnlyList<ResolvedKeybindingItem> actual, params ResolvedKeybindingItem[] expected)
    {
        actual.Count.ShouldBe(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            var a = actual[i];
            var e = expected[i];
            a.Command.ShouldBe(e.Command);
            a.CommandArgs.ShouldBe(e.CommandArgs);
            a.Chords.ShouldBe(e.Chords);
            a.Bubble.ShouldBe(e.Bubble);
            a.IsDefault.ShouldBe(e.IsDefault);
            a.ExtensionId.ShouldBe(e.ExtensionId);
            a.IsBuiltinExtension.ShouldBe(e.IsBuiltinExtension);
            (a.When is null ? e.When is null : e.When is not null && a.When.Equals(e.When)).ShouldBeTrue($"when {a.When?.Serialize()} != {e.When?.Serialize()}");
            a.ResolvedKeybinding?.GetUserSettingsLabel().ShouldBe(e.ResolvedKeybinding?.GetUserSettingsLabel());
        }
    }

    private static IReadOnlyList<ResolvedKeybindingItem> Concat(ResolvedKeybindingItem[] defaults, ResolvedKeybindingItem[] overrides) =>
        [.. defaults, .. overrides];

    private static void Noop(string _)
    {
    }

    [Fact]
    public void Resolve_Key()
    {
        var keybinding = KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyZ;
        var runtimeKeybinding = Keybindings.CreateSimpleKeybinding(keybinding, OS);
        var contextRules = ContextKeyExpr.Equals("bar", "baz");
        var keybindingItem = KbItem(keybinding, "yes", null, contextRules, true);

        contextRules.Evaluate(CreateContext(("bar", "baz"))).ShouldBeTrue();
        contextRules.Evaluate(CreateContext(("bar", "bz"))).ShouldBeFalse();

        var resolver = new KeybindingResolver([keybindingItem], [], Noop);

        var r1 = resolver.Resolve(CreateContext(("bar", "baz")), [], GetDispatchStr(runtimeKeybinding));
        r1.Kind.ShouldBe(ResultKind.KbFound);
        r1.CommandId.ShouldBe("yes");

        var r2 = resolver.Resolve(CreateContext(("bar", "bz")), [], GetDispatchStr(runtimeKeybinding));
        r2.Kind.ShouldBe(ResultKind.NoMatchingKb);
    }

    [Fact]
    public void Resolve_Key_With_Arguments()
    {
        var commandArgs = new { text = "no" };
        var keybinding = KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyZ;
        var runtimeKeybinding = Keybindings.CreateSimpleKeybinding(keybinding, OS);
        var contextRules = ContextKeyExpr.Equals("bar", "baz");
        var keybindingItem = KbItem(keybinding, "yes", commandArgs, contextRules, true);

        var resolver = new KeybindingResolver([keybindingItem], [], Noop);

        var r = resolver.Resolve(CreateContext(("bar", "baz")), [], GetDispatchStr(runtimeKeybinding));
        r.Kind.ShouldBe(ResultKind.KbFound);
        r.CommandArgs.ShouldBeSameAs(commandArgs);
    }

    // suite('handle keybinding removals')

    [Fact]
    public void Removals_Simple_1()
    {
        ResolvedKeybindingItem[] defaults = [KbItem((int)KeyCode.KeyA, "yes1", null, ContextKeyExpr.Equals("1", "a"), true)];
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(
            actual,
            KbItem((int)KeyCode.KeyA, "yes1", null, ContextKeyExpr.Equals("1", "a"), true),
            KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), false));
    }

    [Fact]
    public void Removals_Simple_2()
    {
        ResolvedKeybindingItem[] defaults =
        [
            KbItem((int)KeyCode.KeyA, "yes1", null, ContextKeyExpr.Equals("1", "a"), true),
            KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true),
        ];
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyC, "yes3", null, ContextKeyExpr.Equals("3", "c"), false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(
            actual,
            KbItem((int)KeyCode.KeyA, "yes1", null, ContextKeyExpr.Equals("1", "a"), true),
            KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true),
            KbItem((int)KeyCode.KeyC, "yes3", null, ContextKeyExpr.Equals("3", "c"), false));
    }

    private static ResolvedKeybindingItem[] TwoDefaults() =>
    [
        KbItem((int)KeyCode.KeyA, "yes1", null, ContextKeyExpr.Equals("1", "a"), true),
        KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true),
    ];

    [Fact]
    public void Removal_With_Not_Matching_When()
    {
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyA, "-yes1", null, ContextKeyExpr.Equals("1", "b"), false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(TwoDefaults(), overrides));
        AssertItems(actual, TwoDefaults());
    }

    [Fact]
    public void Removal_With_Not_Matching_Keybinding()
    {
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyB, "-yes1", null, ContextKeyExpr.Equals("1", "a"), false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(TwoDefaults(), overrides));
        AssertItems(actual, TwoDefaults());
    }

    [Fact]
    public void Removal_With_Matching_Keybinding_And_When()
    {
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyA, "-yes1", null, ContextKeyExpr.Equals("1", "a"), false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(TwoDefaults(), overrides));
        AssertItems(actual, KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true));
    }

    [Fact]
    public void Removal_With_Unspecified_Keybinding()
    {
        ResolvedKeybindingItem[] overrides = [KbItem(0, "-yes1", null, ContextKeyExpr.Equals("1", "a"), false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(TwoDefaults(), overrides));
        AssertItems(actual, KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true));
    }

    [Fact]
    public void Removal_With_Unspecified_When()
    {
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyA, "-yes1", null, null, false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(TwoDefaults(), overrides));
        AssertItems(actual, KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true));
    }

    [Fact]
    public void Removal_With_Unspecified_When_And_Unspecified_Keybinding()
    {
        ResolvedKeybindingItem[] overrides = [KbItem(0, "-yes1", null, null, false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(TwoDefaults(), overrides));
        AssertItems(actual, KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true));
    }

    [Fact]
    public void Issue_138997_Removal_In_Default_List()
    {
        ResolvedKeybindingItem[] defaults =
        [
            KbItem((int)KeyCode.KeyA, "yes1", null, null, true),
            KbItem((int)KeyCode.KeyB, "yes2", null, null, true),
            KbItem(0, "-yes1", null, null, false),
        ];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, []));
        AssertItems(actual, KbItem((int)KeyCode.KeyB, "yes2", null, null, true));
    }

    [Fact]
    public void Issue_612_Cannot_Remove_Keybindings_For_Commands_With_Caret()
    {
        ResolvedKeybindingItem[] defaults =
        [
            KbItem((int)KeyCode.KeyA, "^yes1", null, ContextKeyExpr.Equals("1", "a"), true),
            KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true),
        ];
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyA, "-yes1", null, null, false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(actual, KbItem((int)KeyCode.KeyB, "yes2", null, ContextKeyExpr.Equals("2", "b"), true));
    }

    [Fact]
    public void Issue_140884_Unable_To_Reassign_F1_As_Keybinding_For_Show_All_Commands()
    {
        ResolvedKeybindingItem[] defaults = [KbItem((int)KeyCode.KeyA, "command1", null, null, true)];
        ResolvedKeybindingItem[] overrides =
        [
            KbItem((int)KeyCode.KeyA, "-command1", null, null, false),
            KbItem((int)KeyCode.KeyA, "command1", null, null, false),
        ];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(actual, KbItem((int)KeyCode.KeyA, "command1", null, null, false));
    }

    [Fact]
    public void Issue_141638_Change_When_Expression_Might_Actually_Remove_Keybinding()
    {
        ResolvedKeybindingItem[] defaults = [KbItem((int)KeyCode.KeyA, "command1", null, null, true)];
        ResolvedKeybindingItem[] overrides =
        [
            KbItem((int)KeyCode.KeyA, "command1", null, ContextKeyExpr.Equals("a", "1"), false),
            KbItem((int)KeyCode.KeyA, "-command1", null, null, false),
        ];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(actual, KbItem((int)KeyCode.KeyA, "command1", null, ContextKeyExpr.Equals("a", "1"), false));
    }

    [Fact]
    public void Issue_157751_Auto_Quoting_Of_Context_Keys_Prevents_Removal_Of_Keybindings_Via_UI()
    {
        ResolvedKeybindingItem[] defaults =
        [
            KbItem((int)KeyCode.KeyA, "command1", null, ContextKeyExpr.Deserialize("editorTextFocus && activeEditor != workbench.editor.notebook && editorLangId in julia.supportedLanguageIds"), true),
        ];
        ResolvedKeybindingItem[] overrides =
        [
            KbItem((int)KeyCode.KeyA, "-command1", null, ContextKeyExpr.Deserialize("editorTextFocus && activeEditor != 'workbench.editor.notebook' && editorLangId in 'julia.supportedLanguageIds'"), false),
        ];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(actual);
    }

    [Fact]
    public void Issue_293802_Removal_Still_Matches_When_Default_When_Clause_Becomes_More_Specific()
    {
        ResolvedKeybindingItem[] defaults =
        [
            KbItem((int)KeyCode.KeyA, "command1", null, ContextKeyExpr.And(ContextKeyExpr.Has("inChatInput"), ContextKeyExpr.Not("withinEditSessionDiff")), true),
        ];
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyA, "-command1", null, ContextKeyExpr.Has("inChatInput"), false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(actual);
    }

    [Fact]
    public void Issue_338834_Removing_The_Chat_Steer_Or_Queue_Enter_Keybinding_From_The_Keyboard_Shortcuts_Editor_Works()
    {
        // The default `when` clauses as chatQueueActions.ts builds them, merged with the action precondition
        var editingQueue = ContextKeyExpr.Equals("chatEditingSentRequest", "q");
        var editingSteer = ContextKeyExpr.Equals("chatEditingSentRequest", "st");
        var editingQueueOrSteer = ContextKeyExpr.Or(editingQueue, editingSteer)!;
        var queuingActionsPresent = ContextKeyExpr.And(
            ContextKeyExpr.Not("chatTranscriptProgressActive"),
            ContextKeyExpr.Or(ContextKeyExpr.Has("chatSessionRequestInProgress"), editingQueueOrSteer),
            ContextKeyExpr.NotEquals("chatEditingSentRequest", "s"));
        var steerIsDefault = ContextKeyExpr.Equals("config.chat.requestQueuing.defaultAction", "steer");
        var effectiveDefaultIsQueue = ContextKeyExpr.Or(ContextKeyExpr.And(steerIsDefault.Negate(), editingQueueOrSteer.Negate()), editingQueue);
        var effectiveDefaultIsSteer = ContextKeyExpr.Or(ContextKeyExpr.And(steerIsDefault, editingQueueOrSteer.Negate()), editingSteer);
        var precondition = ContextKeyExpr.And(ContextKeyExpr.Has("chatInputHasText"), ContextKeyExpr.Not("chatTranscriptProgressActive"), ContextKeyExpr.Not("chatInputBlocked"));

        ResolvedKeybindingItem[] defaults =
        [
            KbItem((int)KeyCode.Enter, "workbench.action.chat.steerWithMessage", null, ContextKeyExpr.And(precondition, ContextKeyExpr.And(ContextKeyExpr.Has("inChatInput"), queuingActionsPresent, effectiveDefaultIsSteer)), true),
            KbItem((int)KeyCode.Enter, "workbench.action.chat.queueMessage", null, ContextKeyExpr.And(precondition, ContextKeyExpr.And(ContextKeyExpr.Has("inChatInput"), queuingActionsPresent, effectiveDefaultIsQueue)), true),
        ];
        // "Remove Keybinding" writes the serialized default `when`, which is parsed when keybindings.json is read
        var overrides = defaults.Select(kb => KbItem((int)KeyCode.Enter, "-" + kb.Command, null, ContextKeyExpr.Deserialize(kb.When!.Serialize()), false)).ToArray();

        for (var i = 0; i < defaults.Length; i++)
        {
            KeybindingResolver.HandleRemovals([defaults[i], overrides[i]]).Select(kb => kb.Command).ShouldBeEmpty();
        }
    }

    [Fact]
    public void Removal_With_More_Specific_When_Clause_Does_Not_Match_Broader_Default()
    {
        ResolvedKeybindingItem[] defaults = [KbItem((int)KeyCode.KeyA, "command1", null, ContextKeyExpr.Has("inChatInput"), true)];
        ResolvedKeybindingItem[] overrides =
        [
            KbItem((int)KeyCode.KeyA, "-command1", null, ContextKeyExpr.And(ContextKeyExpr.Has("inChatInput"), ContextKeyExpr.Not("withinEditSessionDiff")), false),
        ];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(actual, KbItem((int)KeyCode.KeyA, "command1", null, ContextKeyExpr.Has("inChatInput"), true));
    }

    [Fact]
    public void Issue_160604_Remove_Keybindings_With_When_Clause_Does_Not_Work()
    {
        ResolvedKeybindingItem[] defaults = [KbItem((int)KeyCode.KeyA, "command1", null, null, true)];
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyA, "-command1", null, ContextKeyExpr.True(), false)];
        var actual = KeybindingResolver.HandleRemovals(Concat(defaults, overrides));
        AssertItems(actual);
    }

    [Fact]
    public void ContextIsEntirelyIncluded()
    {
        static ContextKeyExpression? ToExpr(object? expr) => expr switch
        {
            null => null,
            string s => ContextKeyExpr.Deserialize(s),
            _ => (ContextKeyExpression)expr,
        };
        void AssertIsIncluded(object? a, object? b) =>
            KeybindingResolver.WhenIsEntirelyIncluded(ToExpr(a), ToExpr(b)).ShouldBeTrue($"{a} => {b}");
        void AssertIsNotIncluded(object? a, object? b) =>
            KeybindingResolver.WhenIsEntirelyIncluded(ToExpr(a), ToExpr(b)).ShouldBeFalse($"{a} => {b}");

        AssertIsIncluded(null, null);
        AssertIsIncluded(null, ContextKeyExpr.True());
        AssertIsIncluded(ContextKeyExpr.True(), null);
        AssertIsIncluded(ContextKeyExpr.True(), ContextKeyExpr.True());
        AssertIsIncluded("key1", null);
        AssertIsIncluded("key1", "");
        AssertIsIncluded("key1", "key1");
        AssertIsIncluded("key1", ContextKeyExpr.True());
        AssertIsIncluded("!key1", "");
        AssertIsIncluded("!key1", "!key1");
        AssertIsIncluded("key2", "");
        AssertIsIncluded("key2", "key2");
        AssertIsIncluded("key1 && key1 && key2 && key2", "key2");
        AssertIsIncluded("key1 && key2", "key2");
        AssertIsIncluded("key1 && key2", "key1");
        AssertIsIncluded("key1 && key2", "");
        AssertIsIncluded("key1", "key1 || key2");
        AssertIsIncluded("key1 || !key1", "key2 || !key2");
        AssertIsIncluded("key1", "key1 || key2 && key3");

        AssertIsNotIncluded("key1", "!key1");
        AssertIsNotIncluded("!key1", "key1");
        AssertIsNotIncluded("key1 && key2", "key3");
        AssertIsNotIncluded("key1 && key2", "key4");
        AssertIsNotIncluded("key1", "key2");
        AssertIsNotIncluded("key1 || key2", "key2");
        AssertIsNotIncluded("", "key2");
        AssertIsNotIncluded(null, "key2");
    }

    // suite('keybinding aliases')

    private static readonly KeybindingAlias s_alias = new("sessions", "toggleChat", ContextKeyExpr.Has("agentsWindow"));

    private static readonly ContextKeyExpression s_toggleChatWhen = ContextKeyExpr.Has("chatViewVisible");

    private static (string?[] AgentsWindow, string?[] EditorWindow) ResolveCommands(
        ResolvedKeybindingItem[] defaults,
        ResolvedKeybindingItem[] overrides,
        int[] keybindings)
    {
        var aliased = KeybindingResolver.ApplyAliases(defaults, overrides, [s_alias]);
        var resolver = new KeybindingResolver(aliased.Defaults, aliased.Overrides, Noop);
        string?[] Resolve(TestKeybindingContext context) => keybindings.Select(keybinding =>
        {
            var result = resolver.Resolve(context, [], GetDispatchStr(Keybindings.CreateSimpleKeybinding(keybinding, OS)));
            return result.Kind == ResultKind.KbFound ? result.CommandId : null;
        }).ToArray();
        return (Resolve(CreateContext(("agentsWindow", true))), Resolve(CreateContext(("chatViewVisible", true))));
    }

    [Fact]
    public void Aliases_Bind_The_Alias_To_The_Keybindings_Of_Its_Source_Including_The_Users_Outranking_The_Source()
    {
        ResolvedKeybindingItem[] defaults = [KbItem((int)KeyCode.KeyA, "toggleChat", null, s_toggleChatWhen, true)];
        ResolvedKeybindingItem[] overrides = [KbItem((int)KeyCode.KeyB, "toggleChat", null, null, false)];

        var (agentsWindow, editorWindow) = ResolveCommands(defaults, overrides, [(int)KeyCode.KeyA, (int)KeyCode.KeyB]);
        agentsWindow.ShouldBe(["sessions", "sessions"]);
        editorWindow.ShouldBe(["toggleChat", "toggleChat"]);
    }

    [Fact]
    public void Aliases_Remove_The_Keybindings_Removed_From_Its_Source_And_Let_The_User_Remove_The_Keybindings_It_Inherits()
    {
        ResolvedKeybindingItem[] defaults =
        [
            KbItem((int)KeyCode.KeyA, "openChat", null, null, true),
            KbItem((int)KeyCode.KeyA, "sessions", null, s_alias.When, true),
        ];
        ResolvedKeybindingItem[] overrides =
        [
            KbItem((int)KeyCode.KeyA, "-toggleChat", null, s_toggleChatWhen, false),
            KbItem((int)KeyCode.KeyB, "toggleChat", null, s_toggleChatWhen, false),
            KbItem((int)KeyCode.KeyC, "toggleChat", null, s_toggleChatWhen, false),
            KbItem((int)KeyCode.KeyC, "-sessions", null, null, false),
        ];

        var (agentsWindow, editorWindow) = ResolveCommands(defaults, overrides, [(int)KeyCode.KeyA, (int)KeyCode.KeyB, (int)KeyCode.KeyC]);
        agentsWindow.ShouldBe(["openChat", "sessions", null]);
        editorWindow.ShouldBe(["openChat", "toggleChat", "toggleChat"]);
    }

    [Fact]
    public void Aliases_Keep_The_Keybindings_Inherited_From_The_User_When_The_Removed_Keybindings_Of_Its_Source_Share_Their_Key()
    {
        ResolvedKeybindingItem[] defaults = [KbItem((int)KeyCode.KeyA, "sessions", null, s_alias.When, true)];
        // Changing only the when clause in the Keyboard Shortcuts editor removes the default at the same key.
        ResolvedKeybindingItem[] sameKey =
        [
            KbItem((int)KeyCode.KeyA, "toggleChat", null, ContextKeyExpr.Has("other"), false),
            KbItem((int)KeyCode.KeyA, "-toggleChat", null, s_toggleChatWhen, false),
        ];
        ResolvedKeybindingItem[] anyKey =
        [
            KbItem((int)KeyCode.KeyB, "toggleChat", null, null, false),
            new ResolvedKeybindingItem(null, "-toggleChat", null, null, false, null, false),
        ];

        ResolveCommands(defaults, sameKey, [(int)KeyCode.KeyA]).AgentsWindow.ShouldBe(["sessions"]);
        ResolveCommands(defaults, anyKey, [(int)KeyCode.KeyA, (int)KeyCode.KeyB]).AgentsWindow.ShouldBe([null, "sessions"]);
    }

    // suite('resolve command')

    private static ResolvedKeybindingItem ResolveItem(int keybinding, string command, ContextKeyExpression? when) =>
        KbItem(keybinding, command, null, when, true);

    private static ResolvedKeybindingItem ResolveItem(int[] keybinding, string command, ContextKeyExpression? when) =>
        KbItem(keybinding, command, null, when, true);

    private static readonly KeybindingResolver s_resolver = new(
        [
            // This one will never match because its "when" is always overwritten by another one
            ResolveItem((int)KeyCode.KeyX, "first", ContextKeyExpr.And(ContextKeyExpr.Equals("key1", true), ContextKeyExpr.NotEquals("key2", false))),
            // This one always overwrites first
            ResolveItem((int)KeyCode.KeyX, "second", ContextKeyExpr.Equals("key2", true)),
            // This one is a secondary mapping for `second`
            ResolveItem((int)KeyCode.KeyZ, "second", null),
            // This one sometimes overwrites first
            ResolveItem((int)KeyCode.KeyX, "third", ContextKeyExpr.Equals("key3", true)),
            // This one is always overwritten by another one
            ResolveItem(KeyMod.CtrlCmd | (int)KeyCode.KeyY, "fourth", ContextKeyExpr.Equals("key4", true)),
            // This one overwrites with a chord the previous one
            ResolveItem(KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyY, (int)KeyCode.KeyZ), "fifth", null),
            // This one has no keybinding
            ResolveItem(0, "sixth", null),
            ResolveItem(KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyU), "seventh", null),
            ResolveItem(KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyK), "seventh", null),
            ResolveItem(KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyU), "uncomment lines", null),
            ResolveItem(KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyC), "comment lines", null), // cmd+k cmd+c
            ResolveItem(KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyG, KeyMod.CtrlCmd | (int)KeyCode.KeyC), "unreachablechord", null), // cmd+g cmd+c
            ResolveItem(KeyMod.CtrlCmd | (int)KeyCode.KeyG, "eleven", null), // cmd+g
            ResolveItem([KeyMod.CtrlCmd | (int)KeyCode.KeyK, (int)KeyCode.KeyA, (int)KeyCode.KeyB], "long multi chord", null), // cmd+k a b
            ResolveItem([KeyMod.CtrlCmd | (int)KeyCode.KeyB, KeyMod.CtrlCmd | (int)KeyCode.KeyC], "shadowed by long-multi-chord-2", null), // cmd+b cmd+c
            ResolveItem([KeyMod.CtrlCmd | (int)KeyCode.KeyB, KeyMod.CtrlCmd | (int)KeyCode.KeyC, (int)KeyCode.KeyI], "long-multi-chord-2", null), // cmd+b cmd+c i
        ],
        [],
        Noop);

    private static void TestKbLookupByCommand(string commandId, params object[] expectedKeys)
    {
        // Test lookup
        var lookupResult = s_resolver.LookupKeybindings(commandId);
        lookupResult.Count.ShouldBe(expectedKeys.Length, "Length mismatch @ commandId " + commandId);
        for (var i = 0; i < lookupResult.Count; i++)
        {
            var expected = (expectedKeys[i] is int[] chords
                ? KeybindingsTestUtils.CreateUSLayoutResolvedKeybinding(chords, OS)
                : KeybindingsTestUtils.CreateUSLayoutResolvedKeybinding((int)expectedKeys[i], OS))!;
            lookupResult[i].ResolvedKeybinding!.GetUserSettingsLabel().ShouldBe(expected.GetUserSettingsLabel(), "value mismatch @ commandId " + commandId);
        }
    }

    private static void TestResolve(IContext ctx, int[] expectedKey, string commandId) =>
        TestResolve(ctx, Keybindings.DecodeKeybinding(expectedKey, OS)!, commandId);

    private static void TestResolve(IContext ctx, int expectedKey, string commandId) =>
        TestResolve(ctx, Keybindings.DecodeKeybinding(expectedKey, OS)!, commandId);

    private static void TestResolve(IContext ctx, Keybinding expectedKeybinding, string commandId)
    {
        var previousChord = new List<string>();

        for (var i = 0; i < expectedKeybinding.Chords.Count; i++)
        {
            var chord = GetDispatchStr((KeyCodeChord)expectedKeybinding.Chords[i]);

            var result = s_resolver.Resolve(ctx, previousChord, chord);

            if (i == expectedKeybinding.Chords.Count - 1)
            {
                // if it's the final chord, then we should find a valid command,
                // and there should not be a chord.
                result.Kind.ShouldBe(ResultKind.KbFound, $"Enters multi chord for {commandId} at chord {i}");
                result.CommandId.ShouldBe(commandId, $"Enters multi chord for {commandId} at chord {i}");
            }
            else if (i > 0)
            {
                // if this is an intermediate chord, we should not find a valid command,
                // and there should be an open chord we continue.
                result.Kind.ShouldBe(ResultKind.MoreChordsNeeded, $"Continues multi chord for {commandId} at chord {i}");
            }
            else
            {
                // if it's not the final chord and not an intermediate, then we should not
                // find a valid command, and we should enter a chord.
                result.Kind.ShouldBe(ResultKind.MoreChordsNeeded, $"Enters multi chord for {commandId} at chord {i}");
            }

            previousChord.Add(chord);
        }
    }

    [Fact]
    public void Resolve_Command_1()
    {
        TestKbLookupByCommand("first");
    }

    [Fact]
    public void Resolve_Command_2()
    {
        TestKbLookupByCommand("second", (int)KeyCode.KeyZ, (int)KeyCode.KeyX);
        TestResolve(CreateContext(("key2", true)), (int)KeyCode.KeyX, "second");
        TestResolve(CreateContext(), (int)KeyCode.KeyZ, "second");
    }

    [Fact]
    public void Resolve_Command_3()
    {
        TestKbLookupByCommand("third", (int)KeyCode.KeyX);
        TestResolve(CreateContext(("key3", true)), (int)KeyCode.KeyX, "third");
    }

    [Fact]
    public void Resolve_Command_4()
    {
        TestKbLookupByCommand("fourth");
    }

    [Fact]
    public void Resolve_Command_5()
    {
        var kb = KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyY, (int)KeyCode.KeyZ);
        TestKbLookupByCommand("fifth", kb);
        TestResolve(CreateContext(), kb, "fifth");
    }

    [Fact]
    public void Resolve_Command_6()
    {
        var kb = KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyK);
        TestKbLookupByCommand("seventh", kb);
        TestResolve(CreateContext(), kb, "seventh");
    }

    [Fact]
    public void Resolve_Command_7()
    {
        var kb = KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyU);
        TestKbLookupByCommand("uncomment lines", kb);
        TestResolve(CreateContext(), kb, "uncomment lines");
    }

    [Fact]
    public void Resolve_Command_8()
    {
        var kb = KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyC);
        TestKbLookupByCommand("comment lines", kb);
        TestResolve(CreateContext(), kb, "comment lines");
    }

    [Fact]
    public void Resolve_Command_9()
    {
        TestKbLookupByCommand("unreachablechord");
    }

    [Fact]
    public void Resolve_Command_10()
    {
        TestKbLookupByCommand("eleven", KeyMod.CtrlCmd | (int)KeyCode.KeyG);
        TestResolve(CreateContext(), KeyMod.CtrlCmd | (int)KeyCode.KeyG, "eleven");
    }

    [Fact]
    public void Resolve_Command_11()
    {
        TestKbLookupByCommand("sixth");
    }

    [Fact]
    public void Resolve_Command_12()
    {
        int[] kb = [KeyMod.CtrlCmd | (int)KeyCode.KeyK, (int)KeyCode.KeyA, (int)KeyCode.KeyB];
        TestKbLookupByCommand("long multi chord", (object)kb);
        TestResolve(CreateContext(), kb, "long multi chord");
    }

    [Fact]
    public void KBs_Having_Common_Prefix_The_One_Defined_Later_Is_Returned()
    {
        TestResolve(TestKeybindingContext.Empty, [KeyMod.CtrlCmd | (int)KeyCode.KeyB, KeyMod.CtrlCmd | (int)KeyCode.KeyC, (int)KeyCode.KeyI], "long-multi-chord-2");
    }
}
