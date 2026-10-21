using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Gallery.Generators;

/// <summary>
/// One live example: <c>Demos/&lt;Category&gt;/&lt;Component&gt;/&lt;Name&gt;.axaml</c>, compiled by Avalonia like any view,
/// with an optional <c>&lt;Name&gt;.axaml.cs</c> code-behind. Its id is <c>Component.Name</c>; the demo's Code tab shows
/// both files as they are written.
/// </summary>
internal sealed class DemoFile
{
    public const string Folder = "Demos/";
    private const string AvaloniaNamespace = "https://github.com/avaloniaui";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private DemoFile(string path, string relativePath)
    {
        Path = path;
        RelativePath = relativePath;
    }

    /// <summary>The full path of the .axaml file.</summary>
    public string Path { get; }

    /// <summary>The path relative to the gallery project, with forward slashes (what the Code tab shows).</summary>
    public string RelativePath { get; }

    public string Component { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Id => Component + "." + Name;

    /// <summary>The <c>x:Class</c>, which the registry instantiates.</summary>
    public string? ClassName { get; private set; }

    /// <summary>Whether the root element is Avalonia's <c>UserControl</c> (required without a code-behind).</summary>
    public bool IsUserControl { get; private set; }

    public string Source { get; private set; } = string.Empty;

    /// <summary>Why the file cannot be a demo (null when it can).</summary>
    public string? Problem { get; private set; }

    public Location Location => Location.Create(Path, default, default);

    /// <summary>
    /// Reads an .axaml file under the project's <c>Demos/</c> folder, or returns null for any other file (themes, assets).
    /// </summary>
    public static DemoFile? Parse(string path, string? text, string? projectDirectory)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.EndsWith(".axaml", System.StringComparison.OrdinalIgnoreCase) || projectDirectory is not { Length: > 0 })
        {
            return null;
        }
        var root = projectDirectory!.Replace('\\', '/').TrimEnd('/') + "/";
        if (!normalized.StartsWith(root + Folder, System.StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var demo = new DemoFile(path, normalized.Substring(root.Length));
        demo.Source = (text ?? string.Empty).Replace("\r\n", "\n").TrimEnd('\n');
        var parts = demo.RelativePath.Split('/');
        if (parts.Length != 4)
        {
            demo.Problem = "demos live in Demos/<Category>/<Component>/<Name>.axaml";
            return demo;
        }
        demo.Component = parts[2];
        demo.Name = System.IO.Path.GetFileNameWithoutExtension(parts[3]);
        if (!IsIdentifier(demo.Component) || !IsIdentifier(demo.Name))
        {
            demo.Problem = "the component folder and the file name should be C# identifiers, such as Button/Variants.axaml";
            return demo;
        }

        try
        {
            using var reader = XmlReader.Create(new StringReader(demo.Source), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            reader.MoveToContent();
            demo.IsUserControl = reader.LocalName == "UserControl" && reader.NamespaceURI == AvaloniaNamespace;
            demo.ClassName = reader.GetAttribute("Class", XamlNamespace);
        }
        catch (XmlException exception)
        {
            demo.Problem = "the file is not well-formed XML: " + exception.Message;
            return demo;
        }
        if (demo.ClassName is not { Length: > 0 } className || !className.Split('.').All(IsIdentifier))
        {
            demo.Problem = "the root element needs an x:Class, such as x:Class=\"TKCode.Arc.Gallery.Demos." + demo.Component + demo.Name + "Demo\"";
        }
        return demo;
    }

    private static bool IsIdentifier(string value)
        => value.Length > 0 && (char.IsLetter(value[0]) || value[0] == '_') && value.All(c => char.IsLetterOrDigit(c) || c == '_');

    /// <summary>The code-behind (<c>&lt;Name&gt;.axaml.cs</c>) among the compilation's files, if the demo has one.</summary>
    public SyntaxTree? FindCodeBehind(Compilation compilation)
    {
        var expected = Path.Replace('\\', '/') + ".cs";
        return compilation.SyntaxTrees.FirstOrDefault(t => string.Equals(t.FilePath.Replace('\\', '/'), expected, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The text of a code-behind as the Code tab shows it.</summary>
    public static string TextOf(SyntaxTree tree) => tree.GetText().ToString().Replace("\r\n", "\n").TrimEnd('\n');

    public static Location LocationOf(SyntaxTree tree) => Location.Create(tree.FilePath, default, new LinePositionSpan());
}
