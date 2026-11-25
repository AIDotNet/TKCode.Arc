// Ported from vscode (src/vs/editor/test/common/modes/supports/indentationRules.ts, commit b57d5c9e), MIT license.
// The regular expression literals are JsRegExp.Of(source, flags).

using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Tests;

internal static class IndentationRules
{
    public static readonly IndentationRule JavascriptIndentationRules = new(
        DecreaseIndentPattern: JsRegExp.Of(@"^((?!.*?\/\*).*\*\/)?\s*[\}\]\)].*$"),
        IncreaseIndentPattern: JsRegExp.Of(@"^((?!\/\/).)*(\{([^}""'`]*|(\t|[ ])*\/\/.*)|\([^)""'`]*|\[[^\]""'`]*)$"))
    {
        // e.g.  * ...| or */| or *-----*/|
        UnIndentedLinePattern = JsRegExp.Of(@"^(\t|[ ])*[ ]\*[^/]*\*\/\s*$|^(\t|[ ])*[ ]\*\/\s*$|^(\t|[ ])*[ ]\*([ ]([^\*]|\*(?!\/))*)?$"),
        IndentNextLinePattern = JsRegExp.Of(@"^((.*=>\s*)|((.*[^\w]+|\s*)(if|while|for)\s*\(.*\)\s*))$"),
    };

    public static readonly IndentationRule RubyIndentationRules = new(
        DecreaseIndentPattern: JsRegExp.Of(@"^\s*([}\]]([,)]?\s*(#|$)|\.[a-zA-Z_]\w*\b)|(end|rescue|ensure|else|elsif)\b|(in|when)\s)"),
        IncreaseIndentPattern: JsRegExp.Of(@"^\s*((begin|class|(private|protected)\s+def|def|else|elsif|ensure|for|if|module|rescue|unless|until|when|in|while|case)|([^#]*\sdo\b)|([^#]*=\s*(case|if|unless)))\b([^#\{;]|(\""|'|\/).*\4)*(#.*)?$"));

    public static readonly IndentationRule PhpIndentationRules = new(
        DecreaseIndentPattern: JsRegExp.Of(@"^(.*\*\/)?\s*((\})|(\)+[;,])|(\]\)*[;,])|\b(else:)|\b((end(if|for(each)?|while|switch));))"),
        IncreaseIndentPattern: JsRegExp.Of(@"({(?!.*}).*|\(|\[|((else(\s)?)?if|else|for(each)?|while|switch|case).*:)\s*((\/[/*].*|)?$|\?>)"));

    public static readonly IndentationRule GoIndentationRules = new(
        DecreaseIndentPattern: JsRegExp.Of(@"^\s*(\bcase\b.*:|\bdefault\b:|}[)}]*[),]?|\)[,]?)$"),
        IncreaseIndentPattern: JsRegExp.Of(@"^.*(\bcase\b.*:|\bdefault\b:|(\b(func|if|else|switch|select|for|struct)\b.*)?{[^}""'`]*|\([^)""'`]*)$"));

    public static readonly IndentationRule HtmlIndentationRules = new(
        DecreaseIndentPattern: JsRegExp.Of(@"^\s*(<\/(?!html)[-_\.A-Za-z0-9]+\b[^>]*>|-->|\})"),
        IncreaseIndentPattern: JsRegExp.Of(@"<(?!\?|(?:area|base|br|col|frame|hr|html|img|input|keygen|link|menuitem|meta|param|source|track|wbr)\b|[^>]*\/>)([-_\.A-Za-z0-9]+)(?=\s|>)\b[^>]*>(?!.*<\/\1>)|<!--(?!.*-->)|\{[^}""']*$"));

    public static readonly IndentationRule LatexIndentationRules = new(
        DecreaseIndentPattern: JsRegExp.Of(@"^\s*\\end{(?!document)"),
        IncreaseIndentPattern: JsRegExp.Of(@"\\begin{(?!document)([^}]*)}(?!.*\\end{\1})"));

    public static readonly IndentationRule LuaIndentationRules = new(
        DecreaseIndentPattern: JsRegExp.Of(@"^\s*((\b(elseif|else|end|until)\b)|(\})|(\)))"),
        IncreaseIndentPattern: JsRegExp.Of(@"^((?!(\-\-)).)*((\b(else|function|then|do|repeat)\b((?!\b(end|until)\b).)*)|(\{\s*))$"));

    public static readonly IndentationRule VbIndentationRules = new(
        // Decrease indent when line starts with End <keyword>, Else, ElseIf, Case, Catch, Finally, Loop, Next, Wend, Until
        DecreaseIndentPattern: JsRegExp.Of(@"^\s*((End\s+(If|Sub|Function|Class|Module|Enum|Structure|Interface|Namespace|With|Select|Try|While|For|Property|Get|Set|SyncLock|Using|AddHandler|RaiseEvent|RemoveHandler|Event|Operator))|Else|ElseIf|Case|Catch|Finally|Loop|Next|Wend|Until)\b", "i"),
        // Increase indent after lines with block-starting keywords (Sub, Function, Class, Module, If...Then, etc.)
        // Both alternatives are anchored to start of line with ^\s*
        IncreaseIndentPattern: JsRegExp.Of(@"^\s*((If|ElseIf).*Then(?!.*End\s+If)\s*(('|REM).*)?|(Else|While|For|Do|Select\s+Case|Case|Sub|Function|Class|Module|Enum|Structure|Interface|Namespace|With|Try|Catch|Finally|SyncLock|Using|Property|Get|Set|AddHandler|RaiseEvent|RemoveHandler|Event|Operator)\b(?!.*\bEnd\s+(If|Sub|Function|Class|Module|Enum|Structure|Interface|Namespace|With|Select|Try|While|For|Property|Get|Set|SyncLock|Using|AddHandler|RaiseEvent|RemoveHandler|Event|Operator)\b).*(('|REM).*)?)$", "i"));
}
