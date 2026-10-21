using System.Reflection;
using System.Text.RegularExpressions;

namespace TKCode.Arc.DocShots;

/// <summary>
/// Names a docs page may mention: every public type, member and enum value of the TKCode.Arc, Avalonia, AtomUI and
/// common .NET assemblies, a few names from app code (MVVM toolkit, key names), and the names the page itself declares
/// in its code (view models, records, binding paths). Anything else in backticks or in C# member access is reported, so
/// an invented property shows up before a reader tries it.
/// </summary>
internal static partial class ApiNames
{
    private static readonly string[] s_frameworkAssemblies =
    [
        "System.Runtime", "System.Collections", "System.ObjectModel", "System.Linq", "System.ComponentModel",
        "System.ComponentModel.Annotations", "System.ComponentModel.Primitives", "System.ComponentModel.TypeConverter",
        "System.Net.Http", "System.Text.Json", "System.Threading", "System.Threading.Tasks", "System.Globalization"
    ];

    private static readonly string[] s_appNames =
    [
        // CommunityToolkit.Mvvm, the usual view model toolkit in Avalonia apps.
        "ObservableObject", "ObservableValidator", "ObservableRecipient", "ObservableProperty", "RelayCommand",
        "AsyncRelayCommand", "IRelayCommand", "IAsyncRelayCommand", "NotifyPropertyChangedFor", "NotifyCanExecuteChangedFor",
        "NotifyDataErrorInfo", "NotifyPropertyChangedRecipients", "ValidateAllProperties", "ClearAllErrors", "SetProperty",
        "OnPropertyChanged", "OnPropertyChanging", "NotifyCanExecuteChanged", "ExecuteAsync", "IsRunning", "WeakReferenceMessenger",
        "Messenger", "Send", "Register",
        // Keys and modifiers as people write them.
        "Ctrl", "Cmd", "Command", "Esc", "Backspace", "Option", "Opt", "Return", "Shift", "Alt", "Meta", "Win", "Fn",
        "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "PgUp", "PgDn", "Del", "Ins",
        // Literals and placeholders.
        "True", "False", "Null", "NaN", "Infinity", "TODO"
    ];

    private static HashSet<string>? s_known;

    public static HashSet<string> Known => s_known ??= Build();

    private static HashSet<string> Build()
    {
        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith("TKCode.", StringComparison.Ordinal) || name.StartsWith("Avalonia", StringComparison.Ordinal) ||
                name.StartsWith("AtomUI", StringComparison.Ordinal) || name.StartsWith("StreamMark", StringComparison.Ordinal))
            {
                TryLoad(name);
            }
        }
        foreach (var name in s_frameworkAssemblies)
        {
            TryLoad(name);
        }

        var known = new HashSet<string>(s_appNames, StringComparer.Ordinal);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetExportedTypes();
            }
            catch (Exception)
            {
                continue;
            }
            foreach (var type in types)
            {
                known.Add(StripArity(type.Name));
                // Namespaces are written out in code (System.Globalization.CultureInfo).
                foreach (var part in type.Namespace?.Split('.') ?? [])
                {
                    known.Add(part);
                }
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    known.Add(StripArity(member.Name));
                    // Avalonia properties are fields named <Name>Property; the CLR wrapper is <Name>.
                    if (member is FieldInfo { Name: var field } && field.EndsWith("Property", StringComparison.Ordinal))
                    {
                        known.Add(field[..^"Property".Length]);
                    }
                    if (member is FieldInfo { Name: var routed } && routed.EndsWith("Event", StringComparison.Ordinal))
                    {
                        known.Add(routed[..^"Event".Length]);
                    }
                }
                if (type.IsEnum)
                {
                    foreach (var value in Enum.GetNames(type))
                    {
                        known.Add(value);
                    }
                }
            }
        }
        return known;
    }

    private static void TryLoad(string name)
    {
        try
        {
            Assembly.Load(new AssemblyName(name));
        }
        catch (Exception)
        {
            // Not every name exists on every runtime; the rest still count.
        }
    }

    private static string StripArity(string name) => name.IndexOf('`') is var tick and > 0 ? name[..tick] : name;

    /// <summary>
    /// The names <paramref name="page"/> mentions that are neither known nor declared by its own code, with the line of
    /// their first use.
    /// </summary>
    public static List<(int Line, string Name)> Unknown(DocFile page)
    {
        var declared = Declared(page);
        var unknown = new List<(int, string)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Check(string name, int line)
        {
            // All-capital words are values and units (USD, GB, ISO), not API names.
            if (name.Length > 1 && char.IsUpper(name[0]) && !name.All(c => char.IsUpper(c) || char.IsDigit(c)) &&
                !Known.Contains(name) && !declared.Contains(name) && seen.Add(name))
            {
                unknown.Add((line, name));
            }
        }

        var inside = false;
        var language = string.Empty;
        for (var i = 0; i < page.Lines.Length; i++)
        {
            var line = page.Lines[i];
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inside = !inside;
                language = inside ? line[3..].Trim().Split(' ', 2)[0].ToLowerInvariant() : string.Empty;
                continue;
            }
            if (!inside)
            {
                foreach (Match span in CodeSpan().Matches(line))
                {
                    var code = span.Groups[1].Value;
                    if (code.Contains('{') || code.Contains('"') || code.Contains('\'') || code.Contains(' ') && !code.Contains('('))
                    {
                        continue;
                    }
                    foreach (Match identifier in Identifier().Matches(code))
                    {
                        Check(identifier.Value, i + 1);
                    }
                }
            }
            else if (language is "csharp" or "cs" or "c#")
            {
                var code = StringLiteral().Replace(LineComment().Replace(line, string.Empty), "\"\"");
                foreach (Match access in MemberAccess().Matches(code))
                {
                    Check(access.Groups[1].Value, i + 1);
                }
                foreach (Match created in Creation().Matches(code))
                {
                    Check(created.Groups[1].Value, i + 1);
                }
                foreach (Match receiver in StaticReceiver().Matches(code))
                {
                    Check(receiver.Groups[1].Value, i + 1);
                }
            }
        }
        return unknown;
    }

    /// <summary>Names the page's own code declares: types, members, view model properties and commands, binding paths.</summary>
    private static HashSet<string> Declared(DocFile page)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in page.Blocks)
        {
            if (block.IsXaml)
            {
                foreach (Match name in XamlName().Matches(block.Code))
                {
                    declared.Add(name.Groups[1].Value);
                }
                foreach (Match binding in BindingPath().Matches(block.Code))
                {
                    foreach (Match identifier in Identifier().Matches(binding.Groups[1].Value))
                    {
                        declared.Add(identifier.Value);
                    }
                }
                continue;
            }
            var code = block.Code;
            foreach (Match match in TypeDeclaration().Matches(code))
            {
                declared.Add(match.Groups[1].Value);
            }
            foreach (Match match in RecordParameters().Matches(code))
            {
                foreach (Match parameter in Parameter().Matches(match.Groups[1].Value))
                {
                    declared.Add(parameter.Groups[1].Value);
                }
            }
            foreach (Match match in EnumBody().Matches(code))
            {
                foreach (Match identifier in Identifier().Matches(match.Groups[1].Value))
                {
                    declared.Add(identifier.Value);
                }
            }
            foreach (Match match in MemberDeclaration().Matches(code))
            {
                declared.Add(match.Groups[1].Value);
            }
            // Interface members have no access modifier: Task<Plan> GetPlanAsync(string id); string Name { get; }
            foreach (Match match in BodylessMember().Matches(code))
            {
                declared.Add(match.Groups[1].Value);
            }
            // Namespaces in using directives (using CommunityToolkit.Mvvm.ComponentModel;).
            foreach (Match match in UsingDirective().Matches(code))
            {
                foreach (var part in match.Groups[1].Value.Split('.'))
                {
                    declared.Add(part);
                }
            }
            foreach (Match match in ObservableField().Matches(code))
            {
                var field = match.Groups[1].Value.TrimStart('_');
                if (field.Length > 0)
                {
                    declared.Add(char.ToUpperInvariant(field[0]) + field[1..]);
                }
            }
            foreach (Match match in RelayCommand().Matches(code))
            {
                var method = match.Groups[1].Value;
                declared.Add(method);
                declared.Add((method.EndsWith("Async", StringComparison.Ordinal) ? method[..^5] : method) + "Command");
            }
            foreach (Match match in LocalPascal().Matches(code))
            {
                declared.Add(match.Groups[1].Value);
            }
        }
        return declared;
    }

    [GeneratedRegex(@"`([^`\n]+)`")]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex Identifier();

    [GeneratedRegex(@"//.*$")]
    private static partial Regex LineComment();

    [GeneratedRegex(@"\$?@?""(?:[^""\\]|\\.)*""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"(?<=[\w\)\]>?])\??\.([A-Z][A-Za-z0-9_]*)")]
    private static partial Regex MemberAccess();

    [GeneratedRegex(@"\bnew\s+([A-Z][A-Za-z0-9_]*)")]
    private static partial Regex Creation();

    [GeneratedRegex(@"(?<![\w\.])([A-Z][A-Za-z0-9_]*)\s*\.")]
    private static partial Regex StaticReceiver();

    [GeneratedRegex(@"\{(?:Binding|CompiledBinding|ReflectionBinding)\s+(?:Path=)?([A-Za-z0-9_\.\[\]]+)")]
    private static partial Regex BindingPath();

    [GeneratedRegex(@"\b(?:class|record|struct|interface|enum)\s+([A-Z][A-Za-z0-9_]*)")]
    private static partial Regex TypeDeclaration();

    [GeneratedRegex(@"\brecord\s+(?:class\s+|struct\s+)?[A-Z][A-Za-z0-9_]*\s*\(([^)]*)\)")]
    private static partial Regex RecordParameters();

    [GeneratedRegex(@"[\w<>\[\]?,]+\s+([A-Z][A-Za-z0-9_]*)\s*(?:=|,|$)")]
    private static partial Regex Parameter();

    [GeneratedRegex(@"\benum\s+[A-Z][A-Za-z0-9_]*\s*\{([^}]*)\}")]
    private static partial Regex EnumBody();

    [GeneratedRegex(@"\b(?:public|private|protected|internal|static|override|virtual|async|partial|required|readonly|sealed|abstract)\s+[^=;\n(]*?\b([A-Z][A-Za-z0-9_]*)\s*(?:\(|\{|=>|=|;)")]
    private static partial Regex MemberDeclaration();

    [GeneratedRegex(@"\[ObservableProperty\][^;=]*?\s_?([A-Za-z][A-Za-z0-9_]*)\s*[;=]")]
    private static partial Regex ObservableField();

    [GeneratedRegex(@"\[RelayCommand[^\]]*\][^(]*?\s([A-Z][A-Za-z0-9_]*)\s*\(")]
    private static partial Regex RelayCommand();

    [GeneratedRegex(@"\b(?:var|const)\s+([A-Z][A-Za-z0-9_]*)\s*=")]
    private static partial Regex LocalPascal();

    [GeneratedRegex(@"x:Name=""([A-Za-z_][A-Za-z0-9_]*)""")]
    private static partial Regex XamlName();

    [GeneratedRegex(@"(?m)^\s*[\w\.\[\]?,]+(?:<[^>\n]*>)?\??\s+([A-Z][A-Za-z0-9_]*)\s*(?:\([^;{}\n]*\)\s*;|\{\s*get)")]
    private static partial Regex BodylessMember();

    [GeneratedRegex(@"(?m)^\s*using\s+(?:static\s+)?([A-Za-z_][A-Za-z0-9_\.]*)\s*;")]
    private static partial Regex UsingDirective();
}
