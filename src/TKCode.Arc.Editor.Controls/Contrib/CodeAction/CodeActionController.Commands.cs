// Ported from vscode (src/vs/editor/contrib/codeAction/browser/codeActionCommands.ts: QuickFixAction, CodeActionCommand,
// RefactorAction, SourceAction, OrganizeImportsAction, FixAllAction, AutoFixAction and
// triggerCodeActionsForEditorSelection; the list keybindings of src/vs/platform/actionWidget/browser/actionWidget.ts;
// commit b57d5c9e), MIT license.
// Differences from upstream (and a note: static fields here must not read static fields of the other partial file,
// whose initializers may not have run yet): menus and command metadata are not ported; previewSelectedCodeAction accepts (there is no
// refactor preview); the action widget's keybindings weigh EditorContrib + 1000, over the cursor commands.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.CodeAction;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

internal sealed partial class CodeActionController
{
    private static readonly ContextKeyExpression? s_writableWithProvider =
        ContextKeyExpr.And(EditorContextKeys.Writable, EditorContextKeys.HasCodeActionsProvider);

    /// <summary>Quick Fix... (Ctrl+.).</summary>
    public static readonly EditorAction QuickFixAction = EditorAction.Create(
        CodeActionService.QuickFixCommandId, "Quick Fix...", s_writableWithProvider,
        static (editor, _) => TriggerCodeActionsForEditorSelection(editor, "No code actions available", null, null, CodeActionTriggerSource.QuickFix),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)KeyCode.Period) { When = EditorContextKeys.TextInputFocus });

    /// <summary>Trigger a code action of the kind in the arguments (no keybinding).</summary>
    public static readonly EditorCommand CodeActionCommand = EditorCommand.Create(
        CodeActionService.CodeActionCommandId, s_writableWithProvider,
        static (editor, userArgs) =>
        {
            var args = CodeActionCommandArgs.FromUser(userArgs, HierarchicalKind.Empty, CodeActionAutoApply.IfSingle);
            var kind = CodeActionCommandArgs.GetUserKind(userArgs);
            var message = kind is not null
                ? args.Preferred ? $"No preferred code actions for '{kind}' available" : $"No code actions for '{kind}' available"
                : args.Preferred ? "No preferred code actions available" : "No code actions available";
            TriggerCodeActionsForEditorSelection(editor, message,
                new CodeActionFilter { Include = args.Kind, IncludeSourceActions = true, OnlyIncludePreferredActions = args.Preferred },
                args.Apply);
        });

    /// <summary>Refactor... (Ctrl+Shift+R).</summary>
    public static readonly EditorAction RefactorAction = EditorAction.Create(
        CodeActionService.RefactorCommandId, "Refactor...", s_writableWithProvider,
        static (editor, userArgs) =>
        {
            var args = CodeActionCommandArgs.FromUser(userArgs, CodeActionKind.Refactor, CodeActionAutoApply.Never);
            var kind = CodeActionCommandArgs.GetUserKind(userArgs);
            var message = kind is not null
                ? args.Preferred ? $"No preferred refactorings for '{kind}' available" : $"No refactorings for '{kind}' available"
                : args.Preferred ? "No preferred refactorings available" : "No refactorings available";
            TriggerCodeActionsForEditorSelection(editor, message,
                new CodeActionFilter
                {
                    Include = CodeActionKind.Refactor.Contains(args.Kind) ? args.Kind : HierarchicalKind.None,
                    OnlyIncludePreferredActions = args.Preferred,
                },
                args.Apply, CodeActionTriggerSource.Refactor);
        },
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyR)
        {
            When = EditorContextKeys.TextInputFocus,
            Mac = new PlatformKeybindings { Primary = KeyMod.WinCtrl | KeyMod.Shift | (int)KeyCode.KeyR },
        });

    /// <summary>Source Action... (no keybinding).</summary>
    public static readonly EditorAction SourceAction = EditorAction.Create(
        CodeActionService.SourceActionCommandId, "Source Action...", s_writableWithProvider,
        static (editor, userArgs) =>
        {
            var args = CodeActionCommandArgs.FromUser(userArgs, CodeActionKind.Source, CodeActionAutoApply.Never);
            var kind = CodeActionCommandArgs.GetUserKind(userArgs);
            var message = kind is not null
                ? args.Preferred ? $"No preferred source actions for '{kind}' available" : $"No source actions for '{kind}' available"
                : args.Preferred ? "No preferred source actions available" : "No source actions available";
            TriggerCodeActionsForEditorSelection(editor, message,
                new CodeActionFilter
                {
                    Include = CodeActionKind.Source.Contains(args.Kind) ? args.Kind : HierarchicalKind.None,
                    IncludeSourceActions = true,
                    OnlyIncludePreferredActions = args.Preferred,
                },
                args.Apply, CodeActionTriggerSource.SourceAction);
        });

    /// <summary>Organize Imports (Shift+Alt+O).</summary>
    public static readonly EditorAction OrganizeImportsAction = EditorAction.Create(
        CodeActionService.OrganizeImportsCommandId, "Organize Imports",
        ContextKeyExpr.And(EditorContextKeys.Writable, ContextKeyForSupportedActions(CodeActionKind.SourceOrganizeImports)),
        static (editor, _) => TriggerCodeActionsForEditorSelection(editor, "No organize imports action available",
            new CodeActionFilter { Include = CodeActionKind.SourceOrganizeImports, IncludeSourceActions = true },
            CodeActionAutoApply.IfSingle, CodeActionTriggerSource.OrganizeImports),
        new EditorKeybinding(KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyO) { When = EditorContextKeys.TextInputFocus });

    /// <summary>Fix All (no keybinding).</summary>
    public static readonly EditorAction FixAllAction = EditorAction.Create(
        CodeActionService.FixAllCommandId, "Fix All",
        ContextKeyExpr.And(EditorContextKeys.Writable, ContextKeyForSupportedActions(CodeActionKind.SourceFixAll)),
        static (editor, _) => TriggerCodeActionsForEditorSelection(editor, "No fix all action available",
            new CodeActionFilter { Include = CodeActionKind.SourceFixAll, IncludeSourceActions = true },
            CodeActionAutoApply.IfSingle, CodeActionTriggerSource.FixAll));

    /// <summary>Auto Fix... (Shift+Alt+.).</summary>
    public static readonly EditorAction AutoFixAction = EditorAction.Create(
        CodeActionService.AutoFixCommandId, "Auto Fix...",
        ContextKeyExpr.And(EditorContextKeys.Writable, ContextKeyForSupportedActions(CodeActionKind.QuickFix)),
        static (editor, _) => TriggerCodeActionsForEditorSelection(editor, "No auto fixes available",
            new CodeActionFilter { Include = CodeActionKind.QuickFix, OnlyIncludePreferredActions = true },
            CodeActionAutoApply.IfSingle, CodeActionTriggerSource.AutoFix),
        new EditorKeybinding(KeyMod.Alt | KeyMod.Shift | (int)KeyCode.Period)
        {
            When = EditorContextKeys.TextInputFocus,
            Mac = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | KeyMod.Alt | (int)KeyCode.Period },
        });

    /// <summary>Escape: hides the menu.</summary>
    public static readonly EditorCommand HideCodeActionWidgetCommand = EditorCommand.BindToContribution<CodeActionController>(
        Id, "hideCodeActionWidget", CodeEditorContextKeys.CodeActionMenuVisible, static (controller, _) => controller.HideCodeActions(),
        new EditorKeybinding((int)KeyCode.Escape) { Secondary = [KeyMod.Shift | (int)KeyCode.Escape], Weight = KeybindingWeight.EditorContrib + 1000 });

    /// <summary>Up: the previous action of the menu.</summary>
    public static readonly EditorCommand SelectPrevCodeActionCommand = EditorCommand.BindToContribution<CodeActionController>(
        Id, "selectPrevCodeAction", CodeEditorContextKeys.CodeActionMenuVisible, static (controller, _) => controller.Menu?.FocusPrevious(),
        new EditorKeybinding((int)KeyCode.UpArrow) { Secondary = [KeyMod.CtrlCmd | (int)KeyCode.UpArrow], Weight = KeybindingWeight.EditorContrib + 1000 });

    /// <summary>Down: the next action of the menu.</summary>
    public static readonly EditorCommand SelectNextCodeActionCommand = EditorCommand.BindToContribution<CodeActionController>(
        Id, "selectNextCodeAction", CodeEditorContextKeys.CodeActionMenuVisible, static (controller, _) => controller.Menu?.FocusNext(),
        new EditorKeybinding((int)KeyCode.DownArrow) { Secondary = [KeyMod.CtrlCmd | (int)KeyCode.DownArrow], Weight = KeybindingWeight.EditorContrib + 1000 });

    /// <summary>Enter: applies the focused action.</summary>
    public static readonly EditorCommand AcceptSelectedCodeActionCommand = EditorCommand.BindToContribution<CodeActionController>(
        Id, "acceptSelectedCodeAction", CodeEditorContextKeys.CodeActionMenuVisible, static (controller, _) => controller.Menu?.AcceptSelected(),
        new EditorKeybinding((int)KeyCode.Enter) { Weight = KeybindingWeight.EditorContrib + 1000 });

    /// <summary>Ctrl+Enter: upstream previews the focused action's edit; here it applies it.</summary>
    public static readonly EditorCommand PreviewSelectedCodeActionCommand = EditorCommand.BindToContribution<CodeActionController>(
        Id, "previewSelectedCodeAction", CodeEditorContextKeys.CodeActionMenuVisible, static (controller, _) => controller.Menu?.AcceptSelected(),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)KeyCode.Enter) { Weight = KeybindingWeight.EditorContrib + 1000 });

    /// <summary>Upstream's triggerCodeActionsForEditorSelection.</summary>
    internal static void TriggerCodeActionsForEditorSelection(
        CodeEditor editor,
        string notAvailableMessage,
        CodeActionFilter? filter,
        CodeActionAutoApply? autoApply,
        CodeActionTriggerSource triggerAction = CodeActionTriggerSource.Default)
    {
        Get(editor)?.ManualTriggerAtCurrentPosition(notAvailableMessage, triggerAction, filter, autoApply);
    }

    // upstream's contextKeyForSupportedActions: the kind is one of the space-separated supportedCodeAction kinds
    private static ContextKeyExpression ContextKeyForSupportedActions(HierarchicalKind kind) =>
        ContextKeyExpr.Regex(CodeActionModel.SupportedCodeActions.Key, new JsRegExp("(\\s|^)" + System.Text.RegularExpressions.Regex.Escape(kind.Value) + "\\b"));
}
