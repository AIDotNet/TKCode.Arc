using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CodeBlockDefaultDemo : UserControl
{
    public CodeBlockDefaultDemo()
    {
        InitializeComponent();
        Block.Code = """
            import { useState } from "react";

            /** Says hello, then counts the clicks. */
            export function Greeting({ name }: { name: string }) {
              const [count, setCount] = useState(0);
              return <button onClick={() => setCount(count + 1)}>Hello {name}, {count} clicks</button>;
            }
            """;
    }

    private void OnCopied(object? sender, RoutedEventArgs e) =>
        Status.Text = $"Copied {Block.LineCount} lines at {DateTime.Now:T}";
}
