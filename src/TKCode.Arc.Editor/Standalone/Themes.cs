// Ported from vscode (src/vs/editor/standalone/common/themes.ts, commit b57d5c9e), MIT license.
//
// Differences: the color identifiers (editorBackground, ...) are written as the strings they register
// ('editor.background', ...), since the color registry is not ported; the data is otherwise the same.

namespace TKCode.Arc.Editor.Standalone;

/// <summary>The built-in themes: vs, vs-dark, hc-black and hc-light.</summary>
internal static class Themes
{
    /// <summary>The <c>vs</c> theme.</summary>
    public static readonly StandaloneThemeData Vs = new(
        "vs",
        Inherit: false,
        Rules:
        [
            new("") { Foreground = "000000", Background = "fffffe" },
            new("invalid") { Foreground = "cd3131" },
            new("emphasis") { FontStyle = "italic" },
            new("strong") { FontStyle = "bold" },

            new("variable") { Foreground = "001188" },
            new("variable.predefined") { Foreground = "4864AA" },
            new("constant") { Foreground = "dd0000" },
            new("comment") { Foreground = "008000" },
            new("number") { Foreground = "098658" },
            new("number.hex") { Foreground = "3030c0" },
            new("regexp") { Foreground = "800000" },
            new("annotation") { Foreground = "808080" },
            new("type") { Foreground = "008080" },

            new("delimiter") { Foreground = "000000" },
            new("delimiter.html") { Foreground = "383838" },
            new("delimiter.xml") { Foreground = "0000FF" },

            new("tag") { Foreground = "800000" },
            new("tag.id.pug") { Foreground = "4F76AC" },
            new("tag.class.pug") { Foreground = "4F76AC" },
            new("meta.scss") { Foreground = "800000" },
            new("metatag") { Foreground = "e00000" },
            new("metatag.content.html") { Foreground = "FF0000" },
            new("metatag.html") { Foreground = "808080" },
            new("metatag.xml") { Foreground = "808080" },
            new("metatag.php") { FontStyle = "bold" },

            new("key") { Foreground = "863B00" },
            new("string.key.json") { Foreground = "A31515" },
            new("string.value.json") { Foreground = "0451A5" },

            new("attribute.name") { Foreground = "FF0000" },
            new("attribute.value") { Foreground = "0451A5" },
            new("attribute.value.number") { Foreground = "098658" },
            new("attribute.value.unit") { Foreground = "098658" },
            new("attribute.value.html") { Foreground = "0000FF" },
            new("attribute.value.xml") { Foreground = "0000FF" },

            new("string") { Foreground = "A31515" },
            new("string.html") { Foreground = "0000FF" },
            new("string.sql") { Foreground = "FF0000" },
            new("string.yaml") { Foreground = "0451A5" },

            new("keyword") { Foreground = "0000FF" },
            new("keyword.json") { Foreground = "0451A5" },
            new("keyword.flow") { Foreground = "AF00DB" },
            new("keyword.flow.scss") { Foreground = "0000FF" },

            new("operator.scss") { Foreground = "666666" },
            new("operator.sql") { Foreground = "778899" },
            new("operator.swift") { Foreground = "666666" },
            new("predefined.sql") { Foreground = "C700C7" },
        ],
        Colors: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["editor.background"] = "#FFFFFE",
            ["editor.foreground"] = "#000000",
            ["editor.inactiveSelectionBackground"] = "#E5EBF1",
            ["editorIndentGuide.background1"] = "#D3D3D3",
            ["editorIndentGuide.activeBackground1"] = "#939393",
            ["editor.selectionHighlightBackground"] = "#ADD6FF4D",
        });

    /// <summary>The <c>vs-dark</c> theme.</summary>
    public static readonly StandaloneThemeData VsDark = new(
        "vs-dark",
        Inherit: false,
        Rules:
        [
            new("") { Foreground = "D4D4D4", Background = "1E1E1E" },
            new("invalid") { Foreground = "f44747" },
            new("emphasis") { FontStyle = "italic" },
            new("strong") { FontStyle = "bold" },

            new("variable") { Foreground = "74B0DF" },
            new("variable.predefined") { Foreground = "4864AA" },
            new("variable.parameter") { Foreground = "9CDCFE" },
            new("constant") { Foreground = "569CD6" },
            new("comment") { Foreground = "608B4E" },
            new("number") { Foreground = "B5CEA8" },
            new("number.hex") { Foreground = "5BB498" },
            new("regexp") { Foreground = "B46695" },
            new("annotation") { Foreground = "cc6666" },
            new("type") { Foreground = "3DC9B0" },

            new("delimiter") { Foreground = "DCDCDC" },
            new("delimiter.html") { Foreground = "808080" },
            new("delimiter.xml") { Foreground = "808080" },

            new("tag") { Foreground = "569CD6" },
            new("tag.id.pug") { Foreground = "4F76AC" },
            new("tag.class.pug") { Foreground = "4F76AC" },
            new("meta.scss") { Foreground = "A79873" },
            new("meta.tag") { Foreground = "CE9178" },
            new("metatag") { Foreground = "DD6A6F" },
            new("metatag.content.html") { Foreground = "9CDCFE" },
            new("metatag.html") { Foreground = "569CD6" },
            new("metatag.xml") { Foreground = "569CD6" },
            new("metatag.php") { FontStyle = "bold" },

            new("key") { Foreground = "9CDCFE" },
            new("string.key.json") { Foreground = "9CDCFE" },
            new("string.value.json") { Foreground = "CE9178" },

            new("attribute.name") { Foreground = "9CDCFE" },
            new("attribute.value") { Foreground = "CE9178" },
            new("attribute.value.number.css") { Foreground = "B5CEA8" },
            new("attribute.value.unit.css") { Foreground = "B5CEA8" },
            new("attribute.value.hex.css") { Foreground = "D4D4D4" },

            new("string") { Foreground = "CE9178" },
            new("string.sql") { Foreground = "FF0000" },

            new("keyword") { Foreground = "569CD6" },
            new("keyword.flow") { Foreground = "C586C0" },
            new("keyword.json") { Foreground = "CE9178" },
            new("keyword.flow.scss") { Foreground = "569CD6" },

            new("operator.scss") { Foreground = "909090" },
            new("operator.sql") { Foreground = "778899" },
            new("operator.swift") { Foreground = "909090" },
            new("predefined.sql") { Foreground = "FF00FF" },
        ],
        Colors: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["editor.background"] = "#1E1E1E",
            ["editor.foreground"] = "#D4D4D4",
            ["editor.inactiveSelectionBackground"] = "#3A3D41",
            ["editorIndentGuide.background1"] = "#404040",
            ["editorIndentGuide.activeBackground1"] = "#707070",
            ["editor.selectionHighlightBackground"] = "#ADD6FF26",
        });

    /// <summary>The <c>hc-black</c> theme.</summary>
    public static readonly StandaloneThemeData HcBlack = new(
        "hc-black",
        Inherit: false,
        Rules:
        [
            new("") { Foreground = "FFFFFF", Background = "000000" },
            new("invalid") { Foreground = "f44747" },
            new("emphasis") { FontStyle = "italic" },
            new("strong") { FontStyle = "bold" },

            new("variable") { Foreground = "1AEBFF" },
            new("variable.parameter") { Foreground = "9CDCFE" },
            new("constant") { Foreground = "569CD6" },
            new("comment") { Foreground = "608B4E" },
            new("number") { Foreground = "FFFFFF" },
            new("regexp") { Foreground = "C0C0C0" },
            new("annotation") { Foreground = "569CD6" },
            new("type") { Foreground = "3DC9B0" },

            new("delimiter") { Foreground = "FFFF00" },
            new("delimiter.html") { Foreground = "FFFF00" },

            new("tag") { Foreground = "569CD6" },
            new("tag.id.pug") { Foreground = "4F76AC" },
            new("tag.class.pug") { Foreground = "4F76AC" },
            new("meta") { Foreground = "D4D4D4" },
            new("meta.tag") { Foreground = "CE9178" },
            new("metatag") { Foreground = "569CD6" },
            new("metatag.content.html") { Foreground = "1AEBFF" },
            new("metatag.html") { Foreground = "569CD6" },
            new("metatag.xml") { Foreground = "569CD6" },
            new("metatag.php") { FontStyle = "bold" },

            new("key") { Foreground = "9CDCFE" },
            new("string.key") { Foreground = "9CDCFE" },
            new("string.value") { Foreground = "CE9178" },

            new("attribute.name") { Foreground = "569CD6" },
            new("attribute.value") { Foreground = "3FF23F" },

            new("string") { Foreground = "CE9178" },
            new("string.sql") { Foreground = "FF0000" },

            new("keyword") { Foreground = "569CD6" },
            new("keyword.flow") { Foreground = "C586C0" },

            new("operator.sql") { Foreground = "778899" },
            new("operator.swift") { Foreground = "909090" },
            new("predefined.sql") { Foreground = "FF00FF" },
        ],
        Colors: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["editor.background"] = "#000000",
            ["editor.foreground"] = "#FFFFFF",
            ["editorIndentGuide.background1"] = "#FFFFFF",
            ["editorIndentGuide.activeBackground1"] = "#FFFFFF",
        });

    /// <summary>The <c>hc-light</c> theme.</summary>
    public static readonly StandaloneThemeData HcLight = new(
        "hc-light",
        Inherit: false,
        Rules:
        [
            new("") { Foreground = "292929", Background = "FFFFFF" },
            new("invalid") { Foreground = "B5200D" },
            new("emphasis") { FontStyle = "italic" },
            new("strong") { FontStyle = "bold" },

            new("variable") { Foreground = "264F70" },
            new("variable.predefined") { Foreground = "4864AA" },
            new("constant") { Foreground = "dd0000" },
            new("comment") { Foreground = "008000" },
            new("number") { Foreground = "098658" },
            new("number.hex") { Foreground = "3030c0" },
            new("regexp") { Foreground = "800000" },
            new("annotation") { Foreground = "808080" },
            new("type") { Foreground = "008080" },

            new("delimiter") { Foreground = "000000" },
            new("delimiter.html") { Foreground = "383838" },

            new("tag") { Foreground = "800000" },
            new("tag.id.pug") { Foreground = "4F76AC" },
            new("tag.class.pug") { Foreground = "4F76AC" },
            new("meta.scss") { Foreground = "800000" },
            new("metatag") { Foreground = "e00000" },
            new("metatag.content.html") { Foreground = "B5200D" },
            new("metatag.html") { Foreground = "808080" },
            new("metatag.xml") { Foreground = "808080" },
            new("metatag.php") { FontStyle = "bold" },

            new("key") { Foreground = "863B00" },
            new("string.key.json") { Foreground = "A31515" },
            new("string.value.json") { Foreground = "0451A5" },

            new("attribute.name") { Foreground = "264F78" },
            new("attribute.value") { Foreground = "0451A5" },

            new("string") { Foreground = "A31515" },
            new("string.sql") { Foreground = "B5200D" },

            new("keyword") { Foreground = "0000FF" },
            new("keyword.flow") { Foreground = "AF00DB" },

            new("operator.sql") { Foreground = "778899" },
            new("operator.swift") { Foreground = "666666" },
            new("predefined.sql") { Foreground = "C700C7" },
        ],
        Colors: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["editor.background"] = "#FFFFFF",
            ["editor.foreground"] = "#292929",
            ["editorIndentGuide.background1"] = "#292929",
            ["editorIndentGuide.activeBackground1"] = "#292929",
        });
}
