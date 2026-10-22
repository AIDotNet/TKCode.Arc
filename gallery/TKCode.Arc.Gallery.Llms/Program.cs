using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Headless;

namespace TKCode.Arc.Gallery.Llms;

/// <summary>
/// Writes the files for AI assistants (<see cref="LlmsSite"/>) into the web root of a published Browser host:
/// <code>
///   dotnet run --project gallery/TKCode.Arc.Gallery.Llms -c Release -- &lt;wwwroot&gt; [--base-url https://docs.tkcode.si]
/// </code>
/// Fails, writing nothing, when a page links to a Markdown or text file of the site that is not written.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length is not (1 or 3) || (args.Length == 3 && args[1] != "--base-url"))
        {
            Console.Error.WriteLine($"usage: TKCode.Arc.Gallery.Llms <wwwroot> [--base-url {LlmsSite.DefaultBaseUrl}]");
            return 2;
        }
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        // The guides are resources of the gallery, read through Avalonia's asset loader.
        AppBuilder.Configure<Application>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                  .SetupWithoutStarting();

        var root = Path.GetFullPath(args[0]);
        var baseUrl = args.Length == 3 ? args[2] : LlmsSite.DefaultBaseUrl;
        var files = LlmsSite.Build(baseUrl);
        if (LlmsSite.BrokenLinks(files, baseUrl) is { Count: > 0 } broken)
        {
            Console.Error.WriteLine($"Links to files that are not written:\n  {string.Join("\n  ", broken)}");
            return 1;
        }
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        foreach (var file in files)
        {
            var path = Path.Combine(root, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Text, utf8);
        }
        Console.WriteLine($"Wrote {files.Count} files ({files.Sum(f => utf8.GetByteCount(f.Text)) / 1024} KB) for {baseUrl} to {root}");
        return 0;
    }
}
