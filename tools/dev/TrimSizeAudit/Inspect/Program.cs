using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

foreach (var path in args)
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var metadata = pe.GetMetadataReader();
    var types = metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle)).ToList();
    var names = types.Select(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name)).ToHashSet();
    var themes = types.Where(type => metadata.GetString(type.Name).EndsWith("Theme", StringComparison.Ordinal))
        .Select(type => metadata.GetString(type.Name)).Order().ToArray();
    var controls = new[] { "Button", "Calendar", "Markdown", "BarChart", "ColorPicker", "PropertyGrid", "CodeBlock" }
        .ToDictionary(name => name, name => names.Contains("TKCode.Arc." + name));

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        File = Path.GetFileName(path),
        Bytes = stream.Length,
        Types = types.Count,
        Methods = types.Sum(type => type.GetMethods().Count),
        ThemeCount = themes.Length,
        Themes = themes,
        Controls = controls
    }));
}
