using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeViewFileTreeDemo : UserControl
{
    private static readonly Dictionary<string, (string Language, string Code)> s_sources = new()
    {
        ["button.tsx"] = ("tsx", """
            export function Button({ children }: { children: React.ReactNode }) {
              return <button className="button">{children}</button>;
            }
            """),
        ["button.module.css"] = ("css", """
            .button {
              height: 44px;
              border-radius: 18px;
              padding-inline: 16px;
            }
            """),
        ["tree-view.tsx"] = ("tsx", """
            export function TreeView({ nodes }: TreeViewProps) {
              const [expanded, setExpanded] = useState(new Set<string>());
              return <div role="tree">{nodes.map(render)}</div>;
            }
            """),
        ["README.md"] = ("md", """
            # Arc

            Components with legible hierarchy and calm motion.
            """),
        ["package.json"] = ("json", """
            {
              "name": "arc",
              "version": "1.0.0"
            }
            """),
        ["motion-tokens.ts"] = ("ts", """
            export const motionTokens = {
              duration: { fast: 0.16, standard: 0.24 },
            };
            """)
    };

    public TreeViewFileTreeDemo()
    {
        InitializeComponent();
    }

    private void OnItemInvoked(object? sender, TreeViewItemEventArgs e)
    {
        var name = ((TreeViewItem)e.Item).Header ?? string.Empty;
        Status.Text = $"Invoked {name}";
        if (s_sources.TryGetValue(name, out var source))
        {
            Viewer.FileName = name;
            Viewer.Language = source.Language;
            Viewer.Code = source.Code;
        }
    }
}
