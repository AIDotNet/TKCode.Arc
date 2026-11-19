using System.Collections.Frozen;

namespace StreamMark.Highlighting.TextMate;

/// <summary>
/// Markdown 围栏语言名 / 常见别名 → TextMateSharp.Grammars 的 language id。
/// 表里没有的名字再依次尝试按 language id、按扩展名（".name"）解析。
/// </summary>
internal static class LanguageAliases
{
    private static readonly FrozenDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // C#
        ["cs"] = "csharp", ["csharp"] = "csharp", ["c#"] = "csharp", ["c-sharp"] = "csharp", ["csx"] = "csharp", ["cake"] = "csharp",
        // JavaScript / TypeScript
        ["js"] = "javascript", ["javascript"] = "javascript", ["mjs"] = "javascript", ["cjs"] = "javascript", ["node"] = "javascript",
        ["ecmascript"] = "javascript", ["es6"] = "javascript",
        ["jsx"] = "javascriptreact", ["javascriptreact"] = "javascriptreact",
        ["ts"] = "typescript", ["typescript"] = "typescript", ["mts"] = "typescript", ["cts"] = "typescript",
        ["tsx"] = "typescriptreact", ["typescriptreact"] = "typescriptreact",
        // Python
        ["py"] = "python", ["python"] = "python", ["python3"] = "python", ["py3"] = "python", ["pyw"] = "python", ["gyp"] = "python",
        // Shell
        ["sh"] = "shellscript", ["bash"] = "shellscript", ["shell"] = "shellscript", ["zsh"] = "shellscript", ["ksh"] = "shellscript",
        ["console"] = "shellscript", ["shellscript"] = "shellscript", ["shell-session"] = "shellscript", ["shellsession"] = "shellscript",
        ["terminal"] = "shellscript",
        ["ps1"] = "powershell", ["powershell"] = "powershell", ["pwsh"] = "powershell", ["ps"] = "powershell", ["psm1"] = "powershell",
        ["psd1"] = "powershell",
        ["bat"] = "bat", ["cmd"] = "bat", ["batch"] = "bat", ["batchfile"] = "bat",
        // 数据 / 配置
        ["json"] = "json", ["json5"] = "jsonc", ["jsonc"] = "jsonc", ["jsonl"] = "json", ["ndjson"] = "json", ["geojson"] = "json",
        ["yaml"] = "yaml", ["yml"] = "yaml",
        ["toml"] = "ini", // TextMateSharp.Grammars 没有 TOML 语法，INI 语法对 TOML 的键值/节/注释足够接近
        ["ini"] = "ini", ["cfg"] = "properties", ["conf"] = "properties", ["properties"] = "properties", ["editorconfig"] = "ini",
        ["gitconfig"] = "properties",
        ["xml"] = "xml", ["svg"] = "xml", ["xaml"] = "xml", ["axaml"] = "xml", ["xsd"] = "xml", ["plist"] = "xml", ["csproj"] = "xml",
        ["fsproj"] = "xml", ["vbproj"] = "xml", ["props"] = "xml", ["targets"] = "xml", ["msbuild"] = "xml", ["nuspec"] = "xml",
        ["resx"] = "xml", ["rss"] = "xml", ["atom"] = "xml", ["wsdl"] = "xml", ["manifest"] = "xml",
        ["xsl"] = "xsl", ["xslt"] = "xsl",
        ["dockerfile"] = "dockerfile", ["docker"] = "dockerfile", ["containerfile"] = "dockerfile",
        ["makefile"] = "makefile", ["make"] = "makefile", ["mk"] = "makefile", ["mak"] = "makefile",
        ["diff"] = "diff", ["patch"] = "diff", ["udiff"] = "diff",
        ["gitignore"] = "ignore", ["ignore"] = "ignore", ["dockerignore"] = "ignore",
        ["log"] = "log",
        // 标记 / 文档
        ["md"] = "markdown", ["markdown"] = "markdown", ["mkd"] = "markdown", ["mdown"] = "markdown",
        ["html"] = "html", ["htm"] = "html", ["xhtml"] = "html", ["shtml"] = "html", ["vue"] = "html", ["svelte"] = "html",
        ["css"] = "css", ["scss"] = "scss", ["less"] = "less",
        ["tex"] = "latex", ["latex"] = "latex", ["bibtex"] = "bibtex", ["bib"] = "bibtex",
        ["adoc"] = "asciidoc", ["asciidoc"] = "asciidoc",
        ["typ"] = "typst", ["typst"] = "typst",
        ["razor"] = "razor", ["cshtml"] = "razor",
        ["hbs"] = "handlebars", ["handlebars"] = "handlebars",
        ["pug"] = "jade", ["jade"] = "jade",
        // 系统语言
        ["rs"] = "rust", ["rust"] = "rust",
        ["go"] = "go", ["golang"] = "go",
        ["java"] = "java",
        ["swift"] = "swift",
        ["cpp"] = "cpp", ["c++"] = "cpp", ["cc"] = "cpp", ["cxx"] = "cpp", ["hpp"] = "cpp", ["hh"] = "cpp", ["hxx"] = "cpp",
        ["h++"] = "cpp", ["ino"] = "cpp", ["arduino"] = "cpp",
        ["c"] = "c", ["h"] = "c",
        ["cuda"] = "cuda-cpp", ["cu"] = "cuda-cpp",
        ["objc"] = "objective-c", ["objective-c"] = "objective-c", ["objectivec"] = "objective-c",
        ["objcpp"] = "objective-cpp", ["objective-cpp"] = "objective-cpp", ["objectivecpp"] = "objective-cpp",
        ["fs"] = "fsharp", ["fsharp"] = "fsharp", ["f#"] = "fsharp", ["fsx"] = "fsharp", ["fsi"] = "fsharp",
        ["vb"] = "vb", ["vbnet"] = "vb", ["vb.net"] = "vb", ["vbs"] = "vb", ["vbscript"] = "vb", ["visualbasic"] = "vb",
        ["dart"] = "dart",
        ["asm"] = "x86asm", ["x86asm"] = "x86asm", ["nasm"] = "x86asm", ["x86"] = "x86asm", ["assembly"] = "x86asm",
        ["hlsl"] = "hlsl", ["shaderlab"] = "shaderlab",
        ["pascal"] = "pascal", ["delphi"] = "pascal", ["pas"] = "pascal",
        // 脚本语言
        ["rb"] = "ruby", ["ruby"] = "ruby", ["gemspec"] = "ruby", ["rake"] = "ruby", ["podfile"] = "ruby",
        ["php"] = "php",
        ["lua"] = "lua",
        ["r"] = "r", ["rscript"] = "r",
        ["pl"] = "perl", ["perl"] = "perl", ["pm"] = "perl", ["raku"] = "perl6", ["perl6"] = "perl6",
        ["jl"] = "julia", ["julia"] = "julia",
        ["groovy"] = "groovy", ["gradle"] = "groovy", ["jenkinsfile"] = "groovy", ["nextflow"] = "groovy",
        ["clj"] = "clojure", ["clojure"] = "clojure", ["cljs"] = "clojure", ["cljc"] = "clojure", ["edn"] = "clojure",
        ["coffee"] = "coffeescript", ["coffeescript"] = "coffeescript",
        ["sql"] = "sql", ["mysql"] = "sql", ["postgres"] = "sql", ["postgresql"] = "sql", ["psql"] = "sql", ["pgsql"] = "sql",
        ["sqlite"] = "sql", ["tsql"] = "sql", ["plsql"] = "sql", ["mssql"] = "sql", ["sqlserver"] = "sql",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>明确不着色的名字（不能走扩展名兜底）。</summary>
    private static readonly FrozenSet<string> PlainNames = FrozenSet.ToFrozenSet(
        ["text", "txt", "plain", "plaintext", "output", "none", "nohighlight", "raw", "ascii"], StringComparer.Ordinal);

    /// <summary>把围栏信息串规整成小写语言名；空或无效返回 null。</summary>
    public static string? Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        ReadOnlySpan<char> span = language.AsSpan().Trim();
        // 兼容 "{.python}"、"python{1,3}"、"python title=x"、"language-python" 这类写法
        span = span.TrimStart('{').TrimStart('.');
        int cut = span.IndexOfAny(" \t{},:;");
        if (cut >= 0)
        {
            span = span[..cut];
        }

        if (span.StartsWith("language-", StringComparison.OrdinalIgnoreCase))
        {
            span = span["language-".Length..];
        }

        return span.IsEmpty ? null : span.ToString().ToLowerInvariant();
    }

    public static bool IsPlain(string normalized) => PlainNames.Contains(normalized);

    public static bool TryGetLanguageId(string normalized, out string languageId) =>
        Aliases.TryGetValue(normalized, out languageId!);
}
