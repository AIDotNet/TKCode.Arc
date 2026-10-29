using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Text;

namespace TKCode.Arc.Foundation.Generators
{
    /// <summary>A top-level key of a theme file: <c>{x:Type prefix:Name}</c> or a plain string.</summary>
    internal sealed class ThemeKey : IEquatable<ThemeKey>
    {
        public ThemeKey(string text, string? xmlNamespace, string? typeName, int line, int column)
        {
            Text = text;
            XmlNamespace = xmlNamespace;
            TypeName = typeName;
            Line = line;
            Column = column;
        }

        /// <summary>The x:Key text as written.</summary>
        public string Text { get; }

        /// <summary>For a type key, the XML namespace of its prefix; null for a string key.</summary>
        public string? XmlNamespace { get; }

        /// <summary>For a type key, the type name without prefix.</summary>
        public string? TypeName { get; }

        public int Line { get; }
        public int Column { get; }

        public bool IsType => TypeName is not null;

        public bool Equals(ThemeKey? other)
            => other is not null && Text == other.Text && XmlNamespace == other.XmlNamespace && Line == other.Line;

        public override bool Equals(object? obj) => Equals(obj as ThemeKey);

        public override int GetHashCode() => Text.GetHashCode();
    }

    /// <summary>A parsed <c>Themes/*Theme.axaml</c> file.</summary>
    internal sealed class ThemeFile : IEquatable<ThemeFile>
    {
        public ThemeFile(string path, string relativePath, string? className, bool isPublic, string? error, EquatableArray<ThemeKey> keys)
        {
            Path = path;
            RelativePath = relativePath;
            ClassName = className;
            IsPublic = isPublic;
            Error = error;
            Keys = keys;
        }

        public string Path { get; }

        /// <summary>Path relative to the project directory, with forward slashes.</summary>
        public string RelativePath { get; }

        /// <summary>The x:Class value (full name), or null.</summary>
        public string? ClassName { get; }

        public bool IsPublic { get; }

        /// <summary>Why the file could not be read, or null.</summary>
        public string? Error { get; }

        public EquatableArray<ThemeKey> Keys { get; }

        public bool Equals(ThemeFile? other)
            => other is not null && Path == other.Path && RelativePath == other.RelativePath && ClassName == other.ClassName
               && IsPublic == other.IsPublic && Error == other.Error && Keys.Equals(other.Keys);

        public override bool Equals(object? obj) => Equals(obj as ThemeFile);

        public override int GetHashCode() => Path.GetHashCode();

        /// <summary>True for <c>…/Themes/XxxTheme.axaml</c>.</summary>
        public static bool IsThemeFile(string path)
        {
            var normalized = path.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(normalized);
            var directory = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(normalized) ?? string.Empty);
            return directory == "Themes" && name.EndsWith("Theme.axaml", StringComparison.Ordinal);
        }

        private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        public static ThemeFile Parse(string path, string relativePath, SourceText? text)
        {
            if (text is null)
            {
                return new ThemeFile(path, relativePath, null, false, "the file could not be read", EquatableArray<ThemeKey>.Empty);
            }
            XDocument document;
            try
            {
                document = XDocument.Parse(text.ToString(), LoadOptions.SetLineInfo);
            }
            catch (XmlException exception)
            {
                return new ThemeFile(path, relativePath, null, false, exception.Message, EquatableArray<ThemeKey>.Empty);
            }
            var root = document.Root!;
            if (root.Name.LocalName != "ResourceDictionary")
            {
                return new ThemeFile(path, relativePath, null, false,
                    $"the root element is '{root.Name.LocalName}', not ResourceDictionary", EquatableArray<ThemeKey>.Empty);
            }
            var className = (string?)root.Attribute(XName.Get("Class", XamlNamespace));
            var modifier = (string?)root.Attribute(XName.Get("ClassModifier", XamlNamespace));
            var isPublic = modifier is null || string.Equals(modifier, "public", StringComparison.OrdinalIgnoreCase);

            var keys = new List<ThemeKey>();
            foreach (var element in root.Elements())
            {
                // Property elements (ResourceDictionary.MergedDictionaries, .ThemeDictionaries) are not resources.
                if (element.Name.LocalName.IndexOf('.') >= 0)
                {
                    continue;
                }
                var key = element.Attribute(XName.Get("Key", XamlNamespace));
                if (key is null)
                {
                    continue;
                }
                var info = (IXmlLineInfo)key;
                keys.Add(ParseKey(key.Value, element, info.LineNumber, info.LinePosition));
            }
            return new ThemeFile(path, relativePath, className, isPublic, null, new EquatableArray<ThemeKey>(keys));
        }

        private static ThemeKey ParseKey(string value, XElement element, int line, int column)
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                var inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
                var space = inner.IndexOf(' ');
                if (space > 0)
                {
                    var extension = inner.Substring(0, space);
                    var argument = inner.Substring(space + 1).Trim();
                    if (argument.StartsWith("TypeName=", StringComparison.Ordinal))
                    {
                        argument = argument.Substring("TypeName=".Length).Trim();
                    }
                    if (extension is "x:Type" or "x:TypeExtension")
                    {
                        var colon = argument.IndexOf(':');
                        var prefix = colon > 0 ? argument.Substring(0, colon) : string.Empty;
                        var name = colon > 0 ? argument.Substring(colon + 1) : argument;
                        var ns = prefix.Length == 0 ? element.GetDefaultNamespace() : element.GetNamespaceOfPrefix(prefix);
                        return new ThemeKey(trimmed, ns?.NamespaceName ?? string.Empty, name, line, column);
                    }
                }
            }
            return new ThemeKey(value, null, null, line, column);
        }

        public static string MakeRelative(string path, string? projectDirectory)
        {
            var normalized = path.Replace('\\', '/');
            if (!string.IsNullOrEmpty(projectDirectory))
            {
                var directory = projectDirectory!.Replace('\\', '/');
                if (!directory.EndsWith("/", StringComparison.Ordinal))
                {
                    directory += "/";
                }
                if (normalized.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized.Substring(directory.Length);
                }
            }
            return System.IO.Path.GetFileName(normalized);
        }

        public LinePositionSpan LineSpan(ThemeKey key)
        {
            var start = new LinePosition(Math.Max(0, key.Line - 1), Math.Max(0, key.Column - 1));
            return new LinePositionSpan(start, start);
        }

        /// <summary>
        /// The class a theme file should name: <c>&lt;RootNamespace&gt;.&lt;FileName&gt;</c>, flat like every Arc type
        /// (<c>Buttons/Button/Themes/ButtonTheme.axaml</c> → <c>TKCode.Arc.ButtonTheme</c>). Two theme files of one project
        /// with the same file name need different names; tools/dev/migrate_atomui.py prefixes the folder names then.
        /// </summary>
        public static string SuggestClassName(string relativePath, string? rootNamespace)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(relativePath.Split('/').Last(p => p.Length > 0));
            return string.IsNullOrEmpty(rootNamespace) ? name : rootNamespace + "." + name;
        }
    }
}
