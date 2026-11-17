using System.Text.RegularExpressions;

namespace TKCode.Arc.Tests.Architecture;

/// <summary>
/// Helpers that Core already has are not written again (#148): color blending (<see cref="ArcColor"/>), number and size
/// formatting (<see cref="Primitives.ArcFormatting"/>), springs (Motion). Each rule starts from an allow-list of the files
/// that had their own copy when it was written (Architecture/Allowlists): a new file with a copy fails, and so does a listed
/// file that no longer has one, so the lists only shrink as components move over.
/// </summary>
public partial class DuplicateHelperTests
{
    [GeneratedRegex(@"static\s+Color\s+(Lerp|Mix)\w*\s*\(")]
    private static partial Regex ColorBlending();

    [GeneratedRegex(@"static\s+string\s+(FormatBytes|FormatSize)\s*\(")]
    private static partial Regex ByteFormatter();

    [GeneratedRegex(@"[A-Za-z]Format(s|ting)?\.cs$")]
    private static partial Regex FormattingFile();

    [GeneratedRegex(@"Springs\.cs$")]
    private static partial Regex SpringFile();

    [Fact]
    public void Color_Blending_Comes_From_ArcColor() => Check("color-blending.txt", file => ColorBlending().IsMatch(File.ReadAllText(file)));

    [Fact]
    public void Byte_Sizes_Come_From_ArcFormatting() => Check("byte-formatters.txt", file => ByteFormatter().IsMatch(File.ReadAllText(file)));

    [Fact]
    public void No_New_Formatting_Helper_Files() => Check("formatting-helpers.txt", file => FormattingFile().IsMatch(file));

    [Fact]
    public void No_New_Spring_Helper_Files() => Check("spring-helpers.txt", file => SpringFile().IsMatch(file));

    private static void Check(string allowList, Func<string, bool> hasCopy)
    {
        var root = RepositoryRoot();
        var allowed = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Architecture", "Allowlists", allowList))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);
        var found = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(path => !path.StartsWith("src/TKCode.Arc.Core/", StringComparison.Ordinal)
                           && !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal)
                           && !path.Contains("/GeneratedFiles/", StringComparison.Ordinal))
            .Where(path => hasCopy(Path.Combine(root, path)))
            .ToHashSet(StringComparer.Ordinal);
        found.Except(allowed).Order(StringComparer.Ordinal).ShouldBeEmpty($"new copies: use the Core helper instead ({allowList})");
        allowed.Except(found).Order(StringComparer.Ordinal).ShouldBeEmpty($"these no longer have a copy: remove them from {allowList}");
    }

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
