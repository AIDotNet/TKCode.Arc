namespace TKCode.Arc.Gallery.Demos;

/// <summary>A node of the sample trees the Cascader demos use. <c>Code</c> is unique across a whole tree.</summary>
public sealed record CascaderRegion(string Code, string Name, IReadOnlyList<CascaderRegion>? Children = null, bool Disabled = false);

public static class CascaderDemoData
{
    public static IReadOnlyList<CascaderRegion> Regions() =>
    [
        new("44", "Guangdong",
        [
            new("4403", "Shenzhen", [new("440305", "Nanshan"), new("440304", "Futian"), new("440303", "Luohu")]),
            new("4401", "Guangzhou", [new("440106", "Tianhe"), new("440104", "Yuexiu")]),
            new("4419", "Dongguan"),
        ]),
        new("33", "Zhejiang",
        [
            new("3301", "Hangzhou", [new("330106", "Xihu"), new("330102", "Shangcheng")]),
            new("3302", "Ningbo", [new("330203", "Haishu"), new("330205", "Jiangbei")]),
        ]),
        new("31", "Shanghai", [new("3101", "Huangpu"), new("3104", "Xuhui", Disabled: true), new("3105", "Changning")]),
    ];

    public static IReadOnlyList<CascaderRegion> Categories() =>
    [
        new("electronics", "Electronics",
        [
            new("phones", "Phones", [new("smartphones", "Smartphones"), new("feature", "Feature phones")]),
            new("laptops", "Laptops"),
            new("audio", "Audio", [new("headphones", "Headphones"), new("speakers", "Speakers")]),
        ]),
        new("home", "Home",
        [
            new("kitchen", "Kitchen", [new("cookware", "Cookware"), new("small-appliances", "Small appliances")]),
            new("garden", "Garden"),
        ]),
        new("books", "Books"),
    ];

    public static Func<object, System.Collections.IEnumerable?> Children => node => ((CascaderRegion)node).Children;

    public static Func<object, string?> Text => node => ((CascaderRegion)node).Name;

    public static Func<object, object?> Value => node => ((CascaderRegion)node).Code;

    public static Func<object, bool> Disabled => node => ((CascaderRegion)node).Disabled;

    /// <summary>Wires the delegates every demo shares.</summary>
    public static void Configure(Cascader cascader)
    {
        cascader.ChildrenSelector = Children;
        cascader.TextSelector = Text;
        cascader.ValueSelector = Value;
        cascader.DisabledSelector = Disabled;
    }
}
