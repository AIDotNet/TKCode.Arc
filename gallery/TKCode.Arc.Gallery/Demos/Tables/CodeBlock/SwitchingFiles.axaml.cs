using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CodeBlockSwitchingFilesDemo : UserControl
{
    private const string Greeting = """
        import { useState } from "react";

        /** Says hello, then counts the clicks. */
        export function Greeting({ name }: { name: string }) {
          const [count, setCount] = useState(0);
          return <button onClick={() => setCount(count + 1)}>Hello {name}, {count} clicks</button>;
        }
        """;

    private const string Css = """
        .card {
          display: grid;
          gap: 12px;
          border-radius: 26px;
          padding: 24px;
        }
        """;

    private const string Config = """
        {
          "name": "arc-demo",
          "version": "1.4.0",
          "private": true,
          "scripts": { "dev": "next dev", "build": "next build" },
          "dependencies": { "motion": "^12.0.0", "lucide-react": "^0.460.0" }
        }
        """;

    private static readonly Dictionary<string, (string Language, string Code)> s_files = new()
    {
        ["greeting.tsx"] = ("tsx", Greeting),
        ["card.module.css"] = ("css", Css),
        ["package.json"] = ("json", Config)
    };

    public CodeBlockSwitchingFilesDemo()
    {
        InitializeComponent();
        Viewer.Code = Greeting;
    }

    private void OnFileClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string name } && s_files.TryGetValue(name, out var file))
        {
            Viewer.FileName = name;
            Viewer.Language = file.Language;
            Viewer.Code = file.Code;
        }
    }
}
