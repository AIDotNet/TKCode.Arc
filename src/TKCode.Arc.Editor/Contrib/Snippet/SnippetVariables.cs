// Ported from vscode (src/vs/editor/contrib/snippet/browser/snippetVariables.ts, commit b57d5c9e), MIT license.
// Differences:
//  - what upstream takes from platform services is injected: ISnippetLabelService (upstream's ILabelService.getUriLabel;
//    SnippetLabelService.Default gives the file system path), ISnippetWorkspaceService (upstream's
//    IWorkspaceContextService, reduced to the folders and the configuration file), the clipboard as a function, the
//    clock as a TimeProvider (upstream: new Date(); the resolver reads it once, when it is created), random numbers as a
//    Random (upstream: Math.random) and the suggest widget's overtyping capturer as IOvertypingCapturer;
//  - a model's URI is a System.Uri; its fsPath is SnippetUris.FsPath (the local path of a file URI, the unescaped path
//    of any other);
//  - TM_CURRENT_WORD uses the language configuration's word definition when a LanguageConfigurationService is given,
//    else the default one (upstream: textModel.getWordAtPosition);
//  - the day and month names are upstream's English strings (the engine has no localization);
//  - CURRENT_TIMEZONE_NAME is the time provider's local time zone id (IANA on macOS and Linux; on Windows the IANA id
//    when .NET can convert it, else the Windows id).

using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Snippet;

/// <summary>The variable names snippets know (upstream's KnownSnippetVariableNames).</summary>
internal static class KnownSnippetVariableNames
{
    public static readonly FrozenSet<string> All = new[]
    {
        "CURRENT_YEAR", "CURRENT_YEAR_SHORT", "CURRENT_MONTH", "CURRENT_DATE", "CURRENT_HOUR", "CURRENT_MINUTE",
        "CURRENT_SECOND", "CURRENT_MILLISECOND", "CURRENT_DAY_NAME", "CURRENT_DAY_NAME_SHORT", "CURRENT_MONTH_NAME",
        "CURRENT_MONTH_NAME_SHORT", "CURRENT_SECONDS_UNIX", "CURRENT_MILLISECONDS_UNIX", "CURRENT_TIMEZONE_OFFSET",
        "CURRENT_TIMEZONE_NAME", "SELECTION", "CLIPBOARD", "TM_SELECTED_TEXT", "TM_CURRENT_LINE", "TM_CURRENT_WORD",
        "TM_LINE_INDEX", "TM_LINE_NUMBER", "TM_FILENAME", "TM_FILENAME_BASE", "TM_DIRECTORY", "TM_DIRECTORY_BASE",
        "TM_FILEPATH", "CURSOR_INDEX", "CURSOR_NUMBER", "RELATIVE_FILEPATH", "BLOCK_COMMENT_START", "BLOCK_COMMENT_END",
        "LINE_COMMENT", "WORKSPACE_NAME", "WORKSPACE_FOLDER", "RANDOM", "RANDOM_HEX", "UUID",
    }.ToFrozenSet(StringComparer.Ordinal);
}

/// <summary>Turns a document URI into the text snippets show (upstream's ILabelService.getUriLabel).</summary>
internal interface ISnippetLabelService
{
    /// <summary>The label of <paramref name="uri"/>; <paramref name="relative"/> asks for a path relative to the workspace.</summary>
    string GetUriLabel(Uri uri, bool relative = false, bool noPrefix = false);
}

/// <summary>The default label service: a URI's file system path, never relative.</summary>
internal sealed class SnippetLabelService : ISnippetLabelService
{
    public static SnippetLabelService Default { get; } = new();

    public string GetUriLabel(Uri uri, bool relative = false, bool noPrefix = false) => SnippetUris.FsPath(uri);
}

/// <summary>The open workspace: its folders and, for a multi-root workspace, its configuration file.</summary>
/// <param name="Folders">The workspace folders.</param>
/// <param name="ConfigPath">The <c>.code-workspace</c> file of a workspace with a configuration, else null.</param>
internal sealed record SnippetWorkspace(IReadOnlyList<Uri> Folders, Uri? ConfigPath = null);

/// <summary>Gives the open workspace (upstream's IWorkspaceContextService.getWorkspace).</summary>
internal interface ISnippetWorkspaceService
{
    SnippetWorkspace? GetWorkspace();
}

/// <summary>The text the user overtyped last at a cursor (upstream's OvertypingCapturer, from the suggest feature).</summary>
internal interface IOvertypingCapturer
{
    /// <summary>The overtyped text at cursor <paramref name="selectionIdx"/>, or null.</summary>
    (string Value, bool Multiline)? GetLastOvertypedInfo(int selectionIdx);
}

/// <summary>Path helpers on URIs (upstream's URI.fsPath, path.basename / dirname and resources.dirname).</summary>
internal static class SnippetUris
{
    /// <summary>The local path of a file URI, the unescaped path of any other URI (upstream's fsPath).</summary>
    public static string FsPath(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return uri.OriginalString;
        }

        if (uri.IsFile)
        {
            return uri.LocalPath;
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        return OperatingSystem.IsWindows() ? path.Replace('/', '\\') : path;
    }

    /// <summary>The last segment of a path (upstream's path.basename).</summary>
    public static string Basename(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        var idx = trimmed.LastIndexOfAny(['/', '\\']);
        return idx < 0 ? trimmed : trimmed[(idx + 1)..];
    }

    /// <summary>The path without its last segment, "." when there is no directory (upstream's path.dirname).</summary>
    public static string Dirname(string path)
    {
        var trimmed = path.Length > 1 ? path.TrimEnd('/', '\\') : path;
        var idx = trimmed.LastIndexOfAny(['/', '\\']);
        if (idx < 0)
        {
            return ".";
        }

        return idx == 0 ? trimmed[..1] : trimmed[..idx];
    }

    /// <summary>The URI of the parent directory (upstream's resources.dirname).</summary>
    public static Uri Dirname(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            return new Uri(Dirname(uri.OriginalString), UriKind.RelativeOrAbsolute);
        }

        var builder = new UriBuilder(uri) { Path = Dirname(uri.AbsolutePath), Query = "", Fragment = "" };
        return builder.Uri;
    }
}

/// <summary>Asks several resolvers in turn; the first value wins (upstream's CompositeSnippetVariableResolver).</summary>
internal sealed class CompositeSnippetVariableResolver(IReadOnlyList<IVariableResolver> delegates) : IVariableResolver
{
    public string? Resolve(Variable variable)
    {
        foreach (var @delegate in delegates)
        {
            var value = @delegate.Resolve(variable);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }
}

/// <summary>
/// The variables of a cursor: SELECTION, TM_SELECTED_TEXT, TM_CURRENT_LINE, TM_CURRENT_WORD, TM_LINE_INDEX,
/// TM_LINE_NUMBER, CURSOR_INDEX, CURSOR_NUMBER (upstream's SelectionBasedVariableResolver).
/// </summary>
internal sealed class SelectionBasedVariableResolver(
    TextModel model,
    TextSelection selection,
    int selectionIdx,
    IOvertypingCapturer? overtypingCapturer,
    LanguageConfigurationService? languageConfigurationService = null) : IVariableResolver
{
    public string? Resolve(Variable variable)
    {
        var name = variable.Name;

        if (name is "SELECTION" or "TM_SELECTED_TEXT")
        {
            var value = model.GetValueInRange(selection);
            if (value.Length == 0)
            {
                value = null;
            }

            var isMultiline = selection.StartLineNumber != selection.EndLineNumber;

            // If there was no selected text, try to get last overtyped text
            if (value is null && overtypingCapturer is not null)
            {
                if (overtypingCapturer.GetLastOvertypedInfo(selectionIdx) is { } info)
                {
                    value = info.Value;
                    isMultiline = info.Multiline;
                }
            }

            if (!string.IsNullOrEmpty(value) && isMultiline && variable.Snippet is { } snippet)
            {
                // Selection is a multiline string which we indentation we now
                // need to adjust. We compare the indentation of this variable
                // with the indentation at the editor position and add potential
                // extra indentation to the value

                var line = model.GetLineContent(selection.StartLineNumber);
                var lineLeadingWhitespace = SnippetStrings.GetLeadingWhitespace(line, 0, selection.StartColumn - 1);

                var varLeadingWhitespace = lineLeadingWhitespace;
                snippet.Walk(marker =>
                {
                    if (ReferenceEquals(marker, variable))
                    {
                        return false;
                    }

                    if (marker is Text text)
                    {
                        varLeadingWhitespace = SnippetStrings.GetLeadingWhitespace(SnippetStrings.SplitLines(text.Value)[^1]);
                    }

                    return true;
                });
                var whitespaceCommonLength = SnippetStrings.CommonPrefixLength(varLeadingWhitespace, lineLeadingWhitespace);

                value = SnippetStrings.IndentAfterLineBreaks(value, varLeadingWhitespace[whitespaceCommonLength..]);
            }

            return value;
        }
        else if (name == "TM_CURRENT_LINE")
        {
            return model.GetLineContent(selection.PositionLineNumber);
        }
        else if (name == "TM_CURRENT_WORD")
        {
            var wordDefinition = languageConfigurationService?.GetLanguageConfiguration(model.GetLanguageId()).GetWordDefinition()
                ?? WordHelper.DefaultWordRegExp;
            var info = WordHelper.GetWordAtText(selection.PositionColumn, wordDefinition, model.GetLineContent(selection.PositionLineNumber), 0);
            return string.IsNullOrEmpty(info?.Word) ? null : info.Word;
        }
        else if (name == "TM_LINE_INDEX")
        {
            return (selection.PositionLineNumber - 1).ToString(CultureInfo.InvariantCulture);
        }
        else if (name == "TM_LINE_NUMBER")
        {
            return selection.PositionLineNumber.ToString(CultureInfo.InvariantCulture);
        }
        else if (name == "CURSOR_INDEX")
        {
            return selectionIdx.ToString(CultureInfo.InvariantCulture);
        }
        else if (name == "CURSOR_NUMBER")
        {
            return (selectionIdx + 1).ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }
}

/// <summary>
/// The variables of the document: TM_FILENAME, TM_FILENAME_BASE, TM_DIRECTORY, TM_DIRECTORY_BASE, TM_FILEPATH,
/// RELATIVE_FILEPATH (upstream's ModelBasedVariableResolver).
/// </summary>
internal sealed class ModelBasedVariableResolver(ISnippetLabelService labelService, TextModel model) : IVariableResolver
{
    public string? Resolve(Variable variable)
    {
        var name = variable.Name;
        var fsPath = SnippetUris.FsPath(model.Uri);

        if (name == "TM_FILENAME")
        {
            return SnippetUris.Basename(fsPath);
        }
        else if (name == "TM_FILENAME_BASE")
        {
            var fileName = SnippetUris.Basename(fsPath);
            var idx = fileName.LastIndexOf('.');
            if (idx <= 0)
            {
                return fileName;
            }
            else
            {
                return fileName[..idx];
            }
        }
        else if (name == "TM_DIRECTORY")
        {
            if (SnippetUris.Dirname(fsPath) == ".")
            {
                return "";
            }

            return labelService.GetUriLabel(SnippetUris.Dirname(model.Uri));
        }
        else if (name == "TM_DIRECTORY_BASE")
        {
            if (SnippetUris.Dirname(fsPath) == ".")
            {
                return "";
            }

            return SnippetUris.Basename(SnippetUris.Dirname(fsPath));
        }
        else if (name == "TM_FILEPATH")
        {
            return labelService.GetUriLabel(model.Uri);
        }
        else if (name == "RELATIVE_FILEPATH")
        {
            return labelService.GetUriLabel(model.Uri, relative: true, noPrefix: true);
        }

        return null;
    }
}

/// <summary>The CLIPBOARD variable (upstream's ClipboardBasedVariableResolver).</summary>
/// <param name="readClipboardText">Reads the clipboard text.</param>
/// <param name="selectionIdx">The cursor the snippet is inserted at.</param>
/// <param name="selectionCount">How many cursors there are.</param>
/// <param name="spread">Give each cursor one line when the clipboard has as many lines as there are cursors.</param>
internal sealed class ClipboardBasedVariableResolver(Func<string?> readClipboardText, int selectionIdx, int selectionCount, bool spread) : IVariableResolver
{
    public string? Resolve(Variable variable)
    {
        if (variable.Name != "CLIPBOARD")
        {
            return null;
        }

        var clipboardText = readClipboardText();
        if (string.IsNullOrEmpty(clipboardText))
        {
            return null;
        }

        // `spread` is assigning each cursor a line of the clipboard
        // text whenever there the line count equals the cursor count
        // and when enabled
        if (spread)
        {
            var lines = SnippetStrings.SplitLines(clipboardText).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (lines.Count == selectionCount)
            {
                return lines[selectionIdx];
            }
        }

        return clipboardText;
    }
}

/// <summary>LINE_COMMENT, BLOCK_COMMENT_START, BLOCK_COMMENT_END (upstream's CommentBasedVariableResolver).</summary>
internal sealed class CommentBasedVariableResolver(
    TextModel model,
    TextSelection selection,
    LanguageConfigurationService languageConfigurationService) : IVariableResolver
{
    public string? Resolve(Variable variable)
    {
        var name = variable.Name;
        var langId = model.GetLanguageIdAtPosition(selection.SelectionStartLineNumber, selection.SelectionStartColumn);
        var config = languageConfigurationService.GetLanguageConfiguration(langId).Comments;
        if (config is null)
        {
            return null;
        }

        if (name == "LINE_COMMENT")
        {
            return string.IsNullOrEmpty(config.LineCommentToken) ? null : config.LineCommentToken;
        }
        else if (name == "BLOCK_COMMENT_START")
        {
            return string.IsNullOrEmpty(config.BlockCommentStartToken) ? null : config.BlockCommentStartToken;
        }
        else if (name == "BLOCK_COMMENT_END")
        {
            return string.IsNullOrEmpty(config.BlockCommentEndToken) ? null : config.BlockCommentEndToken;
        }

        return null;
    }
}

/// <summary>The CURRENT_* date and time variables, all from one reading of the clock (upstream's TimeBasedVariableResolver).</summary>
internal sealed class TimeBasedVariableResolver : IVariableResolver
{
    private static readonly string[] s_dayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    private static readonly string[] s_dayNamesShort = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    private static readonly string[] s_monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    private static readonly string[] s_monthNamesShort = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private readonly DateTimeOffset _date;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>Reads the clock of <paramref name="timeProvider"/> (the system's by default) once.</summary>
    public TimeBasedVariableResolver(TimeProvider? timeProvider = null)
    {
        timeProvider ??= TimeProvider.System;
        _date = timeProvider.GetLocalNow();
        _timeZone = timeProvider.LocalTimeZone;
    }

    public string? Resolve(Variable variable)
    {
        var inv = CultureInfo.InvariantCulture;
        switch (variable.Name)
        {
            case "CURRENT_YEAR":
                return _date.Year.ToString(inv);
            case "CURRENT_YEAR_SHORT":
                var year = _date.Year.ToString(inv);
                return year.Length > 2 ? year[^2..] : year;
            case "CURRENT_MONTH":
                return _date.Month.ToString("00", inv);
            case "CURRENT_DATE":
                return _date.Day.ToString("00", inv);
            case "CURRENT_HOUR":
                return _date.Hour.ToString("00", inv);
            case "CURRENT_MINUTE":
                return _date.Minute.ToString("00", inv);
            case "CURRENT_SECOND":
                return _date.Second.ToString("00", inv);
            case "CURRENT_MILLISECOND":
                return _date.Millisecond.ToString("000", inv);
            case "CURRENT_DAY_NAME":
                return s_dayNames[(int)_date.DayOfWeek];
            case "CURRENT_DAY_NAME_SHORT":
                return s_dayNamesShort[(int)_date.DayOfWeek];
            case "CURRENT_MONTH_NAME":
                return s_monthNames[_date.Month - 1];
            case "CURRENT_MONTH_NAME_SHORT":
                return s_monthNamesShort[_date.Month - 1];
            case "CURRENT_SECONDS_UNIX":
                return Math.Floor(_date.ToUnixTimeMilliseconds() / 1000.0).ToString(inv);
            case "CURRENT_MILLISECONDS_UNIX":
                return _date.ToUnixTimeMilliseconds().ToString(inv);
            case "CURRENT_TIMEZONE_OFFSET":
            {
                // upstream: getTimezoneOffset() is UTC minus local, so its sign is the opposite of the offset's
                var offsetMinutes = (int)_date.Offset.TotalMinutes;
                var sign = offsetMinutes < 0 ? '-' : '+';
                var hours = Math.Abs(offsetMinutes) / 60;
                var minutes = Math.Abs(offsetMinutes) - hours * 60;
                return sign + hours.ToString("00", inv) + ":" + minutes.ToString("00", inv);
            }
            case "CURRENT_TIMEZONE_NAME":
                return TimeZoneName(_timeZone);
        }

        return null;
    }

    private static string TimeZoneName(TimeZoneInfo zone)
    {
        if (zone.HasIanaId)
        {
            return zone.Id;
        }

        return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : zone.Id;
    }
}

/// <summary>WORKSPACE_NAME and WORKSPACE_FOLDER (upstream's WorkspaceBasedVariableResolver).</summary>
internal sealed class WorkspaceBasedVariableResolver(ISnippetWorkspaceService? workspaceService) : IVariableResolver
{
    private const string WorkspaceExtension = "code-workspace";

    public string? Resolve(Variable variable)
    {
        if (workspaceService?.GetWorkspace() is not { } workspace)
        {
            return null;
        }

        // toWorkspaceIdentifier: a configuration makes a workspace, one folder a single folder workspace, else empty
        if (workspace.ConfigPath is null && workspace.Folders.Count != 1)
        {
            return null;
        }

        if (variable.Name == "WORKSPACE_NAME")
        {
            return ResolveWorkspaceName(workspace);
        }
        else if (variable.Name == "WORKSPACE_FOLDER")
        {
            return ResolveWorkspacePath(workspace);
        }

        return null;
    }

    private static string ResolveWorkspaceName(SnippetWorkspace workspace)
    {
        if (workspace.ConfigPath is null)
        {
            return SnippetUris.Basename(PosixPath(workspace.Folders[0]));
        }

        var filename = SnippetUris.Basename(PosixPath(workspace.ConfigPath));
        if (filename.EndsWith(WorkspaceExtension, StringComparison.Ordinal))
        {
            filename = filename[..(filename.Length - WorkspaceExtension.Length - 1)];
        }

        return filename;
    }

    private static string ResolveWorkspacePath(SnippetWorkspace workspace)
    {
        if (workspace.ConfigPath is null)
        {
            return NormalizeDriveLetter(SnippetUris.FsPath(workspace.Folders[0]));
        }

        var filename = SnippetUris.Basename(PosixPath(workspace.ConfigPath));
        var folderpath = SnippetUris.FsPath(workspace.ConfigPath);
        if (folderpath.EndsWith(filename, StringComparison.Ordinal))
        {
            folderpath = folderpath[..Math.Max(0, folderpath.Length - filename.Length - 1)];
        }

        return folderpath.Length > 0 ? NormalizeDriveLetter(folderpath) : "/";
    }

    private static string PosixPath(Uri uri) => uri.IsAbsoluteUri ? Uri.UnescapeDataString(uri.AbsolutePath) : uri.OriginalString;

    // labels.normalizeDriveLetter: c:\ -> C:\ on Windows
    private static string NormalizeDriveLetter(string path)
    {
        if (OperatingSystem.IsWindows() && path.Length >= 2 && path[1] == ':' && char.IsAsciiLetterLower(path[0]))
        {
            return char.ToUpperInvariant(path[0]) + path[1..];
        }

        return path;
    }
}

/// <summary>RANDOM, RANDOM_HEX and UUID (upstream's RandomBasedVariableResolver).</summary>
internal sealed class RandomBasedVariableResolver(Random? random = null) : IVariableResolver
{
    private readonly Random _random = random ?? Random.Shared;

    public string? Resolve(Variable variable)
    {
        var name = variable.Name;

        if (name == "RANDOM")
        {
            return _random.Next(0, 1_000_000).ToString("000000", CultureInfo.InvariantCulture);
        }
        else if (name == "RANDOM_HEX")
        {
            return _random.Next(0, 0x1000000).ToString("x6", CultureInfo.InvariantCulture);
        }
        else if (name == "UUID")
        {
            Span<byte> bytes = stackalloc byte[16];
            _random.NextBytes(bytes);
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40); // version 4
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variant
            return new Guid(bytes).ToString("D");
        }

        return null;
    }
}

/// <summary>The string helpers of upstream's strings.ts the snippet code uses.</summary>
internal static class SnippetStrings
{
    /// <summary>The spaces and tabs at the start of <paramref name="str"/>, between <paramref name="start"/> and <paramref name="end"/>.</summary>
    public static string GetLeadingWhitespace(string str, int start = 0, int? end = null)
    {
        var stop = Math.Min(end ?? str.Length, str.Length);
        for (var i = start; i < stop; i++)
        {
            var ch = str[i];
            if (ch != ' ' && ch != '\t')
            {
                return str[start..i];
            }
        }

        return stop > start ? str[start..stop] : "";
    }

    /// <summary>The length of the common prefix.</summary>
    public static int CommonPrefixLength(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return len;
    }

    /// <summary>Splits on \r\n, \r and \n.</summary>
    public static string[] SplitLines(string str)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < str.Length; i++)
        {
            var ch = str[i];
            if (ch == '\r' || ch == '\n')
            {
                lines.Add(str[start..i]);
                if (ch == '\r' && i + 1 < str.Length && str[i + 1] == '\n')
                {
                    i++;
                }

                start = i + 1;
            }
        }

        lines.Add(str[start..]);
        return lines.ToArray();
    }

    // value.replace(/(\r\n|\r|\n)(.*)/g, (m, newline, rest) => newline + indent + rest)
    internal static string IndentAfterLineBreaks(string value, string indent)
    {
        if (indent.Length == 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + indent.Length * 4);
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            builder.Append(ch);
            if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
                {
                    builder.Append('\n');
                    i++;
                }

                builder.Append(indent);
            }
        }

        return builder.ToString();
    }
}
