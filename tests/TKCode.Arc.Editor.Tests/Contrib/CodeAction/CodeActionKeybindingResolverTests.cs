// Ported from vscode (src/vs/editor/contrib/codeAction/test/browser/codeActionKeybindingResolver.test.ts, commit
// b57d5c9e), MIT license. All 3 upstream tests; the mock keybinding service is the function the resolver takes.

using Shouldly;
using TKCode.Arc.Editor.Contrib.CodeAction;
using TKCode.Arc.Editor.Platform;
using Xunit;
using CodeAction = TKCode.Arc.Editor.Languages.CodeAction;

namespace TKCode.Arc.Editor.Tests;

public sealed class CodeActionKeybindingResolverTests
{
    private static readonly ResolvedKeybindingItem s_refactorKeybinding = CreateCodeActionKeybinding(
        KeyCode.KeyA, CodeActionService.RefactorCommandId, new CodeActionUserArgs(CodeActionKind.Refactor.Value));

    private static readonly ResolvedKeybindingItem s_refactorExtractKeybinding = CreateCodeActionKeybinding(
        KeyCode.KeyB, CodeActionService.RefactorCommandId, new CodeActionUserArgs(CodeActionKind.Refactor.Append("extract").Value));

    private static readonly ResolvedKeybindingItem s_organizeImportsKeybinding = CreateCodeActionKeybinding(
        KeyCode.KeyC, CodeActionService.OrganizeImportsCommandId, null);

    private static ResolvedKeybindingItem CreateCodeActionKeybinding(KeyCode keycode, string command, object? commandArgs) =>
        new(new USLayoutResolvedKeybinding([new KeyCodeChord(false, true, false, false, keycode)], OperatingSystemKind.Linux),
            command, commandArgs, null, false, null, false);

    private static Func<CodeAction, ResolvedKeybinding?> Resolver(params ResolvedKeybindingItem[] items) =>
        new CodeActionKeybindingResolver(() => items).GetResolver();

    [Fact]
    public void Should_match_refactor_keybindings()
    {
        var resolver = Resolver(s_refactorKeybinding);

        resolver(new CodeAction(string.Empty)).ShouldBeNull();
        resolver(new CodeAction(string.Empty) { Kind = CodeActionKind.Refactor.Value }).ShouldBeSameAs(s_refactorKeybinding.ResolvedKeybinding);
        resolver(new CodeAction(string.Empty) { Kind = CodeActionKind.Refactor.Append("extract").Value }).ShouldBeSameAs(s_refactorKeybinding.ResolvedKeybinding);
        resolver(new CodeAction(string.Empty) { Kind = CodeActionKind.QuickFix.Value }).ShouldBeNull();
    }

    [Fact]
    public void Should_prefer_most_specific_keybinding()
    {
        var resolver = Resolver(s_refactorKeybinding, s_refactorExtractKeybinding, s_organizeImportsKeybinding);

        resolver(new CodeAction(string.Empty) { Kind = CodeActionKind.Refactor.Value }).ShouldBeSameAs(s_refactorKeybinding.ResolvedKeybinding);
        resolver(new CodeAction(string.Empty) { Kind = CodeActionKind.Refactor.Append("extract").Value }).ShouldBeSameAs(s_refactorExtractKeybinding.ResolvedKeybinding);
    }

    [Fact]
    public void Organize_imports_should_still_return_a_keybinding_even_though_it_does_not_have_args()
    {
        var resolver = Resolver(s_refactorKeybinding, s_refactorExtractKeybinding, s_organizeImportsKeybinding);

        resolver(new CodeAction(string.Empty) { Kind = CodeActionKind.SourceOrganizeImports.Value }).ShouldBeSameAs(s_organizeImportsKeybinding.ResolvedKeybinding);
    }
}
