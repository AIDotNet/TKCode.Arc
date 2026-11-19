using System.Text.RegularExpressions;
using Xunit;

namespace StreamMark.Core.Tests;

/// <summary>
/// <c>Core/</c> and <c>Avalonia/</c> share one assembly (TKCode.Arc.StreamMark), so the compiler no longer keeps the core
/// free of UI types. The core stays testable without Avalonia and parses on worker threads: none of its sources may name
/// an Avalonia or StreamMark.Avalonia namespace.
/// </summary>
public sealed partial class CoreLayerTests
{
    [GeneratedRegex(@"^\s*(global\s+)?using\s+(static\s+)?(\w+\s*=\s*)?(global::)?(Avalonia|StreamMark\.Avalonia)\b|\b(global::)?(Avalonia|StreamMark\.Avalonia)\.[A-Z]\w*\.", RegexOptions.Multiline)]
    private static partial Regex UiReference();

    [Fact]
    public void Core_Does_Not_Reference_Avalonia()
    {
        var core = Path.Combine(RepositoryRoot(), "src", "TKCode.Arc.StreamMark", "Core");
        var offenders = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => UiReference().IsMatch(StripComments(File.ReadAllText(file))))
            .Select(file => Path.GetRelativePath(core, file))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Pattern_Catches_Using_And_Qualified_Names()
    {
        Assert.Matches(UiReference(), "using Avalonia.Media;");
        Assert.Matches(UiReference(), "using StreamMark.Avalonia.Rendering;");
        Assert.Matches(UiReference(), "var c = global::Avalonia.Media.Colors.Red;");
        Assert.DoesNotMatch(UiReference(), "// rendered by StreamMark.Avalonia");
        Assert.DoesNotMatch(UiReference(), "using StreamMark.Core.Model;");
    }

    private static string StripComments(string source) =>
        Regex.Replace(source, @"//[^\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);

    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "TKCode.Arc.slnx")))
            {
                return folder.FullName;
            }
        }
        throw new InvalidOperationException("TKCode.Arc.slnx was not found above the test folder");
    }
}
