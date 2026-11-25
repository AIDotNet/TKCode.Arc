// Ported from vscode (src/vs/editor/test/common/modes/supports/onEnterRules.ts, commit b57d5c9e), MIT license.
// The regular expression literals are JsRegExp.Of(source, flags).

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Tests;

internal static class OnEnterRules
{
    public static readonly IReadOnlyList<OnEnterRule> JavascriptOnEnterRules =
    [
        // e.g. /** | */
        new OnEnterRule(
            JsRegExp.Of(@"^\s*\/\*\*(?!\/)([^\*]|\*(?!\/))*$"),
            new EnterAction(IndentAction.IndentOutdent) { AppendText = " * " })
        {
            AfterText = JsRegExp.Of(@"^\s*\*\/$"),
        },
        // e.g. /** ...|
        new OnEnterRule(
            JsRegExp.Of(@"^\s*\/\*\*(?!\/)([^\*]|\*(?!\/))*$"),
            new EnterAction(IndentAction.None) { AppendText = " * " }),
        // e.g.  * ...|
        new OnEnterRule(
            JsRegExp.Of(@"^(\t|[ ])*[ ]\*([ ]([^\*]|\*(?!\/))*)?$"),
            new EnterAction(IndentAction.None) { AppendText = "* " })
        {
            PreviousLineText = JsRegExp.Of(@"(?=^(\s*(\/\*\*|\*)).*)(?=(?!(\s*\*\/)))"),
        },
        // e.g.  */|
        new OnEnterRule(
            JsRegExp.Of(@"^(\t|[ ])*[ ]\*\/\s*$"),
            new EnterAction(IndentAction.None) { RemoveText = 1 }),
        // e.g.  *-----*/|
        new OnEnterRule(
            JsRegExp.Of(@"^(\t|[ ])*[ ]\*[^/]*\*\/\s*$"),
            new EnterAction(IndentAction.None) { RemoveText = 1 }),
        new OnEnterRule(
            JsRegExp.Of(@"^\s*(\bcase\s.+:|\bdefault:)$"),
            new EnterAction(IndentAction.Indent))
        {
            AfterText = JsRegExp.Of(@"^(?!\s*(\bcase\b|\bdefault\b))"),
        },
        new OnEnterRule(
            JsRegExp.Of(@"^\s+([^{i\s]|i(?!f\b))"),
            new EnterAction(IndentAction.Outdent))
        {
            PreviousLineText = JsRegExp.Of(@"^\s*(((else ?)?if|for|while)\s*\(.*\)\s*|else\s*)$"),
        },
        // Indent when pressing enter from inside ()
        new OnEnterRule(
            JsRegExp.Of(@"^.*\([^\)]*$"),
            new EnterAction(IndentAction.IndentOutdent) { AppendText = "\t" })
        {
            AfterText = JsRegExp.Of(@"^\s*\).*$"),
        },
        // Indent when pressing enter from inside {}
        new OnEnterRule(
            JsRegExp.Of(@"^.*\{[^\}]*$"),
            new EnterAction(IndentAction.IndentOutdent) { AppendText = "\t" })
        {
            AfterText = JsRegExp.Of(@"^\s*\}.*$"),
        },
        // Indent when pressing enter from inside []
        new OnEnterRule(
            JsRegExp.Of(@"^.*\[[^\]]*$"),
            new EnterAction(IndentAction.IndentOutdent) { AppendText = "\t" })
        {
            AfterText = JsRegExp.Of(@"^\s*\].*$"),
        },
    ];

    public static readonly IReadOnlyList<OnEnterRule> PhpOnEnterRules =
    [
        new OnEnterRule(
            JsRegExp.Of(@"^\s*\/\*\*(?!\/)([^\*]|\*(?!\/))*$"),
            new EnterAction(IndentAction.IndentOutdent) { AppendText = " * " })
        {
            AfterText = JsRegExp.Of(@"^\s*\*\/$"),
        },
        new OnEnterRule(
            JsRegExp.Of(@"^\s*\/\*\*(?!\/)([^\*]|\*(?!\/))*$"),
            new EnterAction(IndentAction.None) { AppendText = " * " }),
        new OnEnterRule(
            JsRegExp.Of(@"^(\t|(\ \ ))*\ \*(\ ([^\*]|\*(?!\/))*)?$"),
            new EnterAction(IndentAction.None) { AppendText = "* " }),
        new OnEnterRule(
            JsRegExp.Of(@"^(\t|(\ \ ))*\ \*\/\s*$"),
            new EnterAction(IndentAction.None) { RemoveText = 1 }),
        new OnEnterRule(
            JsRegExp.Of(@"^(\t|(\ \ ))*\ \*[^/]*\*\/\s*$"),
            new EnterAction(IndentAction.None) { RemoveText = 1 }),
        new OnEnterRule(
            JsRegExp.Of(@"^\s+([^{i\s]|i(?!f\b))"),
            new EnterAction(IndentAction.Outdent))
        {
            PreviousLineText = JsRegExp.Of(@"^\s*(((else ?)?if|for(each)?|while)\s*\(.*\)\s*|else\s*)$"),
        },
    ];

    public static readonly IReadOnlyList<OnEnterRule> CppOnEnterRules =
    [
        new OnEnterRule(
            JsRegExp.Of(@"^\s+([^{i\s]|i(?!f\b))"),
            new EnterAction(IndentAction.Outdent))
        {
            PreviousLineText = JsRegExp.Of(@"^\s*(((else ?)?if|for|while)\s*\(.*\)\s*|else\s*)$"),
        },
    ];

    public static readonly IReadOnlyList<OnEnterRule> HtmlOnEnterRules =
    [
        new OnEnterRule(
            JsRegExp.Of(@"<(?!(?:area|base|br|col|embed|hr|img|input|keygen|link|menuitem|meta|param|source|track|wbr))([_:\w][_:\w\-.\d]*)(?:(?:[^'""/>]|""[^""]*""|'[^']*')*?(?!\/)>)[^<]*$", "i"),
            new EnterAction(IndentAction.IndentOutdent))
        {
            AfterText = JsRegExp.Of(@"^<\/([_:\w][_:\w\-.\d]*)\s*>", "i"),
        },
        new OnEnterRule(
            JsRegExp.Of(@"<(?!(?:area|base|br|col|embed|hr|img|input|keygen|link|menuitem|meta|param|source|track|wbr))([_:\w][_:\w\-.\d]*)(?:(?:[^'""/>]|""[^""]*""|'[^']*')*?(?!\/)>)[^<]*$", "i"),
            new EnterAction(IndentAction.Indent)),
    ];

    public static readonly IReadOnlyList<OnEnterRule> VbOnEnterRules =
    [
        // Prevent indent after End statements and block terminators (but NOT ElseIf...Then or Else which should indent)
        new OnEnterRule(
            JsRegExp.Of(@"^\s*((End\s+(If|Sub|Function|Class|Module|Enum|Structure|Interface|Namespace|With|Select|Try|While|For|Property|Get|Set|SyncLock|Using|AddHandler|RaiseEvent|RemoveHandler|Event|Operator))|Loop|Next|Wend|Until)\b.*$", "i"),
            new EnterAction(IndentAction.None)),
    ];
}
