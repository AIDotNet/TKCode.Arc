namespace TKCode.Arc.Gallery.Catalog;

/// <summary>
/// A component of the TKCode.Arc.Mobile package, as the gallery lists it.
/// </summary>
/// <param name="ClassName">The catalog key and the name of the page's folder: the type's name with a <c>Mobile</c> prefix,
/// so it never clashes with a desktop control of the same name (<c>Button</c>, <c>Switch</c>).</param>
/// <param name="Title">The title of the page.</param>
/// <param name="Slug">The address of the page (<c>/components/mobile-button</c>).</param>
/// <param name="Description">One sentence; it is also the summary of the page's en-US front matter.</param>
public sealed record MobileComponent(string ClassName, string Title, string Slug, string Description);

/// <summary>
/// The components of <c>TKCode.Arc.Mobile</c> the gallery shows in its Mobile section, in sidebar order. Their pages are
/// <c>Docs/Mobile/&lt;ClassName&gt;/&lt;lang&gt;.md</c> and their demos <c>Demos/Mobile/&lt;ClassName&gt;/&lt;Name&gt;.axaml</c>;
/// the demos render inside a <see cref="Shell.PhoneFrame"/>. They are not Arc components and are not counted as such.
/// </summary>
public static class MobileCatalog
{
    /// <summary>The folder name below <c>Docs/</c> and <c>Demos/</c>, and the code of the sidebar group.</summary>
    public const string Folder = "Mobile";

    /// <summary>The components, in sidebar order.</summary>
    public static readonly MobileComponent[] Components =
    [
        new("MobilePage", "Mobile page", "mobile-page",
            "A phone screen: a header, the content and a footer laid out inside the device's safe area."),
        new("MobileNavbar", "Mobile navbar", "mobile-navbar",
            "The 44 px top bar of a page: a leading slot, a centered title and a trailing slot for actions."),
        new("MobileTabbar", "Mobile tabbar", "mobile-tabbar",
            "The bottom tab bar of a page: icon-over-label tabs that share the width, with the selected one highlighted."),
        new("MobileNavigationStack", "Mobile navigation stack", "mobile-navigation-stack",
            "A stack of pages: a pushed page slides in over the one below, and a swipe from the left edge or a back button slides it away."),
        new("MobileListGroup", "Mobile list", "mobile-list",
            "Grouped rows for settings and menus: icon, title, subtitle, a value or control at the end and a chevron."),
        new("MobileSheet", "Mobile sheet", "mobile-sheet",
            "A bottom sheet that rises over a page with a scrim, closed by tapping the scrim, pulling the handle down or flicking it."),
        new("MobileButton", "Mobile button", "mobile-button",
            "A touch-first button with a press state instead of a hover state and a 44 px minimum touch target."),
        new("MobileSwitch", "Mobile switch", "mobile-switch",
            "A 51 by 31 toggle for settings that take effect at once, with a spring thumb and a 44 px hit area."),
        new("MobileTextField", "Mobile text field", "mobile-text-field",
            "A 44 px filled text field with an optional icon and a clear button, whose border takes the accent color while focused."),
        new("MobileSearchbar", "Mobile search bar", "mobile-searchbar",
            "A pill-shaped text field with a search glyph, a clear button and a Search key that raises Submitted."),
    ];
}
