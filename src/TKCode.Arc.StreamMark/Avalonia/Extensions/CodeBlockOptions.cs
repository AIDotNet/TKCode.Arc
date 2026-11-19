namespace StreamMark.Avalonia.Extensions;

/// <summary>代码块的显示选项（<see cref="MarkdownServices.CodeBlocks"/>）。</summary>
public sealed record CodeBlockOptions
{
    public static CodeBlockOptions Default { get; } = new();

    /// <summary>显示行号。围栏信息带起始行（<c>```cs src/App.cs#L10-20</c>）时从该行开始编号。</summary>
    public bool ShowLineNumbers { get; init; }

    /// <summary>超过这个行数的已完成代码块默认折叠（0 表示从不折叠）。流式输出中的代码块不折叠。</summary>
    public int CollapseAfterLines { get; init; } = 40;

    /// <summary>折叠时显示的行数。</summary>
    public int CollapsedLines { get; init; } = 20;

    /// <summary>头部显示"下载"按钮。</summary>
    public bool ShowDownload { get; init; } = true;

    /// <summary>折叠栏文字：参数为总行数。每次显示时调用，可随界面语言变化。</summary>
    public Func<int, string> ExpandText { get; init; } = lines => $"Show all {lines} lines";

    /// <summary>展开后折叠栏的文字（每次显示时调用）。</summary>
    public Func<string> CollapseText { get; init; } = () => "Show less";

    /// <summary>diff 头部切换按钮的提示：参数为要切换到的显示方式（每次显示时调用）。</summary>
    public Func<Rendering.DiffViewMode, string> DiffModeText { get; init; } =
        mode => mode == Rendering.DiffViewMode.Split ? "Split view" : "Inline view";

    /// <summary><c>```diff</c> / <c>```patch</c> 的默认显示方式（用户在头部切换后按块保存）。</summary>
    public Rendering.DiffViewMode DiffMode { get; init; } = Rendering.DiffViewMode.Markdown;

    /// <summary>并排视图所需的最小宽度，更窄时以统一视图显示。</summary>
    public double DiffSplitMinWidth { get; init; } = 600;

    /// <summary>
    /// 保存代码的处理函数。为 null 时用窗口的 <c>IStorageProvider.SaveFilePickerAsync</c> 选择文件并写入 UTF-8 文本。
    /// </summary>
    public Func<CodeSaveRequest, Task>? Save { get; init; }
}

/// <summary>"下载"代码块的请求：建议的文件名与内容。</summary>
public sealed record CodeSaveRequest(string SuggestedFileName, string Code, string? Language, string? Path);

/// <summary>代码语言到文件扩展名的映射（下载时的建议文件名）。</summary>
public static class CodeFileNames
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs"] = "cs", ["csharp"] = "cs", ["c#"] = "cs", ["fs"] = "fs", ["fsharp"] = "fs", ["vb"] = "vb",
        ["js"] = "js", ["javascript"] = "js", ["jsx"] = "jsx", ["ts"] = "ts", ["typescript"] = "ts", ["tsx"] = "tsx",
        ["py"] = "py", ["python"] = "py", ["rb"] = "rb", ["ruby"] = "rb", ["go"] = "go", ["rust"] = "rs", ["rs"] = "rs",
        ["java"] = "java", ["kotlin"] = "kt", ["kt"] = "kt", ["swift"] = "swift", ["c"] = "c", ["cpp"] = "cpp", ["c++"] = "cpp",
        ["h"] = "h", ["hpp"] = "hpp", ["php"] = "php", ["lua"] = "lua", ["r"] = "r", ["dart"] = "dart", ["scala"] = "scala",
        ["sh"] = "sh", ["bash"] = "sh", ["zsh"] = "sh", ["shell"] = "sh", ["powershell"] = "ps1", ["ps1"] = "ps1", ["bat"] = "bat",
        ["html"] = "html", ["xml"] = "xml", ["xaml"] = "xaml", ["axaml"] = "axaml", ["css"] = "css", ["scss"] = "scss",
        ["json"] = "json", ["jsonc"] = "jsonc", ["yaml"] = "yaml", ["yml"] = "yml", ["toml"] = "toml", ["ini"] = "ini",
        ["sql"] = "sql", ["md"] = "md", ["markdown"] = "md", ["diff"] = "diff", ["patch"] = "patch", ["dockerfile"] = "Dockerfile",
        ["makefile"] = "mk", ["graphql"] = "graphql", ["proto"] = "proto", ["vue"] = "vue", ["svelte"] = "svelte", ["tex"] = "tex",
        ["latex"] = "tex", ["mermaid"] = "mmd", ["csv"] = "csv", ["txt"] = "txt", ["text"] = "txt",
    };

    /// <summary>建议文件名：围栏信息中的路径取文件名，否则为 <c>snippet.扩展名</c>。</summary>
    public static string Suggest(string? language, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            var name = System.IO.Path.GetFileName(path.Replace('\\', '/'));
            if (name.Length > 0)
            {
                return name;
            }
        }
        if (language is not null && Extensions.TryGetValue(language, out var ext))
        {
            return ext == "Dockerfile" ? ext : "snippet." + ext;
        }
        return "snippet.txt";
    }
}
