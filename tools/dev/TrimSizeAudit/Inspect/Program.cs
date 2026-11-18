using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

var manifest = JsonDocument.Parse(File.ReadAllText(args[0])).RootElement;
var expectedThemes = manifest.GetProperty("ThemeTypes").EnumerateArray().Select(value => value.GetString()!).ToArray();
var trackedControls = manifest.GetProperty("ControlTypes").EnumerateArray().Select(value => value.GetString()!).ToArray();
var path = args[1];
using var stream = File.OpenRead(path);
using var pe = new PEReader(stream);
var metadata = pe.GetMetadataReader();
var allTypes = metadata.TypeDefinitions.Select(handle => metadata.GetTypeDefinition(handle)).ToArray();
var topLevelTypes = allTypes.Where(type => !type.IsNested)
    .ToDictionary(type => metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name));
var themes = expectedThemes.Where(topLevelTypes.ContainsKey).Order().ToArray();
foreach (var name in themes)
{
    var type = topLevelTypes[name];
    if (BaseName(type.BaseType) != "Avalonia.Controls.ResourceDictionary"
        || (type.Attributes & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface)
    {
        throw new InvalidOperationException("Tracked theme no longer matches a top-level ResourceDictionary class: " + name);
    }
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    File = Path.GetFileName(path), Bytes = stream.Length,
    Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
    TypeCount = allTypes.Length, MethodCount = allTypes.Sum(type => type.GetMethods().Count),
    SourceThemeCount = themes.Length, SourceThemes = themes,
    TrackedTypes = trackedControls.ToDictionary(name => name, topLevelTypes.ContainsKey)
}));

string BaseName(EntityHandle handle)
{
    if (handle.Kind == HandleKind.TypeReference)
    {
        var type = metadata.GetTypeReference((TypeReferenceHandle)handle);
        return metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
    }
    if (handle.Kind == HandleKind.TypeDefinition)
    {
        var type = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
        return metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
    }
    return handle.Kind.ToString();
}
