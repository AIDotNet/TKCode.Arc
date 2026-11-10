namespace TKCode.Arc.Gallery.Demos;

/// <summary>A file or folder of the sample trees the TreeSelect demos use. <c>Path</c> is unique across a tree.</summary>
public sealed record TreeSelectFile(string Path, string Name, IReadOnlyList<TreeSelectFile>? Children = null, bool Disabled = false)
{
    public bool IsFolder => Children is not null;

    public override string ToString() => Name;
}

public static class TreeSelectDemoData
{
    /// <summary>A small app repository: folders and files, one file disabled.</summary>
    public static IReadOnlyList<TreeSelectFile> Project() =>
    [
        Folder("app",
        [
            Folder("app/api", [File("app/api/route.ts"), File("app/api/auth.ts")]),
            Folder("app/dashboard", [File("app/dashboard/page.tsx"), File("app/dashboard/layout.tsx")]),
            File("app/page.tsx"),
            File("app/layout.tsx"),
        ]),
        Folder("components",
        [
            File("components/AppShell.tsx"),
            File("components/Button.tsx"),
            File("components/DataTable.tsx"),
        ]),
        Folder("lib", [File("lib/db.ts"), File("lib/utils.ts"), File("lib/legacy-auth.ts", disabled: true)]),
        Folder("public", [File("public/favicon.ico"), File("public/app-icon.png")]),
        File("package.json"),
        File("README.md"),
    ];

    /// <summary>Folders only, for picking a destination.</summary>
    public static IReadOnlyList<TreeSelectFile> Folders() =>
    [
        Folder("Documents",
        [
            Folder("Documents/Contracts", [Folder("Documents/Contracts/2025", []), Folder("Documents/Contracts/2026", [])]),
            Folder("Documents/Invoices", []),
        ]),
        Folder("Projects",
        [
            Folder("Projects/Website", [Folder("Projects/Website/Assets", []), Folder("Projects/Website/Copy", [])]),
            Folder("Projects/Mobile app", []),
        ]),
        Folder("Archive", []),
    ];

    /// <summary>Teams and people, for the check strategy demo.</summary>
    public static IReadOnlyList<TreeSelectFile> Teams() =>
    [
        Folder("design", "Design", [Person("design/ava", "Ava"), Person("design/noah", "Noah")]),
        Folder("engineering", "Engineering",
        [
            Folder("engineering/web", "Web", [Person("engineering/web/mia", "Mia"), Person("engineering/web/liam", "Liam")]),
            Folder("engineering/platform", "Platform", [Person("engineering/platform/zoe", "Zoe"), Person("engineering/platform/ethan", "Ethan")]),
        ]),
        Person("ceo", "Olivia"),
    ];

    /// <summary>40 packages with 150 files each: 6,040 nodes.</summary>
    public static IReadOnlyList<TreeSelectFile> Monorepo() =>
        Enumerable.Range(1, 40).Select(p => new TreeSelectFile($"packages/pkg-{p:00}", $"pkg-{p:00}",
                Enumerable.Range(1, 150).Select(i => new TreeSelectFile($"packages/pkg-{p:00}/module-{i:000}.ts", $"module-{i:000}.ts")).ToList()))
            .ToList();

    private static TreeSelectFile Folder(string path, IReadOnlyList<TreeSelectFile> children) => new(path, NameOf(path), children);

    private static TreeSelectFile Folder(string path, string name, IReadOnlyList<TreeSelectFile> children) => new(path, name, children);

    private static TreeSelectFile File(string path, bool disabled = false) => new(path, NameOf(path), null, disabled);

    private static TreeSelectFile Person(string path, string name) => new(path, name);

    private static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];

    /// <summary>Wires the delegates every demo shares: children, label, value (the path), disabled and, optionally, icons.</summary>
    public static void Configure(TreeSelect select, bool icons = true)
    {
        select.ChildrenSelector = node => ((TreeSelectFile)node).Children;
        select.TextSelector = node => ((TreeSelectFile)node).Name;
        select.ValueSelector = node => ((TreeSelectFile)node).Path;
        select.DisabledSelector = node => ((TreeSelectFile)node).Disabled;
        if (icons)
        {
            select.IconSelector = node => ((TreeSelectFile)node).IsFolder ? ArcIcons.Folder : ArcIcons.File;
        }
    }

    /// <summary>The values as one line.</summary>
    public static string Describe(IReadOnlyList<object?> values) =>
        values.Count == 0 ? "SelectedValues: none" : "SelectedValues: " + string.Join(", ", values);
}
