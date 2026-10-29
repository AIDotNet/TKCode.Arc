using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Localization;

namespace TKCode.Arc.Gallery.Localization;

/// <summary>
/// The gallery shell's text in the current Arc language. Strings live in <c>Localization/*.xlf</c>
/// (<see cref="GalleryShellLangResourceKind"/>); Arc publishes them as application resources keyed by the enum
/// value, so <see cref="Bind"/> follows language changes by itself. Text that is composed from state (for example the
/// "no match" message) is recomputed from <see cref="Track"/>.
/// </summary>
public static class GalleryText
{
    /// <summary>The Arc language manager, or null before the application has started.</summary>
    public static ArcLanguageManager? Languages => ArcLanguageManager.Current;

    /// <summary>The current language tag (en-US when there is no language manager).</summary>
    public static string CurrentLanguage => Languages?.Language.Value ?? GalleryDocs.SourceLanguage;

    /// <summary>Switches the whole application, the Code tab included, to <paramref name="languageTag"/>.</summary>
    public static void SetLanguage(string languageTag) => Languages?.Set(LanguageTag.Parse(languageTag));

    public static string Get(GalleryShellLangResourceKind kind)
        => GalleryShellLangResources.Get(kind);

    public static string Format(GalleryShellLangResourceKind kind, params object?[] arguments)
        => GalleryShellLangResources.Format(kind, arguments);

    /// <summary>Binds <paramref name="property"/> of <paramref name="target"/> to a string; it changes with the language.</summary>
    public static IDisposable Bind(StyledElement target, AvaloniaProperty property, GalleryShellLangResourceKind kind)
        => target.Bind(property, target.GetResourceObservable(kind));

    /// <summary>Like <see cref="Bind"/> for a string with <c>{0}</c> placeholders filled from <paramref name="arguments"/>.</summary>
    public static IDisposable BindFormat(StyledElement target, AvaloniaProperty property, GalleryShellLangResourceKind kind,
                                         params object?[] arguments)
        => target.Bind(property, target.GetResourceObservable(kind, pattern => pattern is string text
            ? string.Format(ArcLanguage.Culture, text, arguments)
            : pattern));

    /// <summary>
    /// Runs <paramref name="refresh"/> now and whenever the language changes, for as long as <paramref name="host"/> is on the
    /// visual tree (the subscription is dropped when it leaves, so short-lived pages do not pile up handlers).
    /// </summary>
    public static void Track(Visual host, Action refresh)
    {
        ArcLanguageManager? subscribed = null;
        EventHandler<LanguageChangedEventArgs> changed = (_, _) =>
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                refresh();
            }
            else
            {
                Dispatcher.UIThread.Post(refresh);
            }
        };
        host.AttachedToVisualTree += (_, _) =>
        {
            subscribed = Languages;
            if (subscribed is not null)
            {
                subscribed.Changed += changed;
            }
            refresh();
        };
        host.DetachedFromVisualTree += (_, _) =>
        {
            if (subscribed is not null)
            {
                subscribed.Changed -= changed;
                subscribed = null;
            }
        };
    }

    /// <summary>The sidebar title of a catalog category (<c>Buttons</c>, <c>TextFields</c>, …), or false for an unknown code.</summary>
    public static bool TryGetCategory(string code, out GalleryShellLangResourceKind kind)
    {
        GalleryShellLangResourceKind? found = code switch
        {
            "Buttons" => GalleryShellLangResourceKind.Category_Buttons,
            "Gestures" => GalleryShellLangResourceKind.Category_Gestures,
            "Menus" => GalleryShellLangResourceKind.Category_Menus,
            "TextFields" => GalleryShellLangResourceKind.Category_TextFields,
            "SpecialInputs" => GalleryShellLangResourceKind.Category_SpecialInputs,
            "Selects" => GalleryShellLangResourceKind.Category_Selects,
            "Toggles" => GalleryShellLangResourceKind.Category_Toggles,
            "Sliders" => GalleryShellLangResourceKind.Category_Sliders,
            "Pickers" => GalleryShellLangResourceKind.Category_Pickers,
            "Editors" => GalleryShellLangResourceKind.Category_Editors,
            "Navigation" => GalleryShellLangResourceKind.Category_Navigation,
            "Expand" => GalleryShellLangResourceKind.Category_Expand,
            "Overlays" => GalleryShellLangResourceKind.Category_Overlays,
            "Messages" => GalleryShellLangResourceKind.Category_Messages,
            "Progress" => GalleryShellLangResourceKind.Category_Progress,
            "Avatars" => GalleryShellLangResourceKind.Category_Avatars,
            "Cards" => GalleryShellLangResourceKind.Category_Cards,
            "Charts" => GalleryShellLangResourceKind.Category_Charts,
            "AdvancedCharts" => GalleryShellLangResourceKind.Category_AdvancedCharts,
            "Workflow" => GalleryShellLangResourceKind.Category_Workflow,
            "Tables" => GalleryShellLangResourceKind.Category_Tables,
            "Activity" => GalleryShellLangResourceKind.Category_Activity,
            "Media" => GalleryShellLangResourceKind.Category_Media,
            "TextEffects" => GalleryShellLangResourceKind.Category_TextEffects,
            "Content" => GalleryShellLangResourceKind.Category_Content,
            "Ai" => GalleryShellLangResourceKind.Category_Ai,
            "Illustrations" => GalleryShellLangResourceKind.Category_Illustrations,
            "Cursor" => GalleryShellLangResourceKind.Category_Cursor,
            "Blocks" => GalleryShellLangResourceKind.Category_Blocks,
            "Docs" => GalleryShellLangResourceKind.Category_Docs,
            "Mobile" => GalleryShellLangResourceKind.Category_Mobile,
            _ => null
        };
        kind = found.GetValueOrDefault();
        return found is not null;
    }

    /// <summary>
    /// The sidebar title of a Blocks subgroup (<c>Analytics</c>, <c>LineCharts</c>, …). Kept apart from
    /// <see cref="TryGetCategory"/>: the codes <c>Media</c> and <c>Content</c> name Arc and extras groups too.
    /// </summary>
    public static bool TryGetBlockGroup(string code, out GalleryShellLangResourceKind kind)
    {
        GalleryShellLangResourceKind? found = code switch
        {
            "Analytics" => GalleryShellLangResourceKind.BlockGroup_Analytics,
            "Finance" => GalleryShellLangResourceKind.BlockGroup_Finance,
            "LineCharts" => GalleryShellLangResourceKind.BlockGroup_LineCharts,
            "Ai" => GalleryShellLangResourceKind.BlockGroup_Ai,
            "AppShell" => GalleryShellLangResourceKind.BlockGroup_AppShell,
            "Auth" => GalleryShellLangResourceKind.BlockGroup_Auth,
            "Settings" => GalleryShellLangResourceKind.BlockGroup_Settings,
            "People" => GalleryShellLangResourceKind.BlockGroup_People,
            "Developer" => GalleryShellLangResourceKind.BlockGroup_Developer,
            "Pricing" => GalleryShellLangResourceKind.BlockGroup_Pricing,
            "Commerce" => GalleryShellLangResourceKind.BlockGroup_Commerce,
            "Support" => GalleryShellLangResourceKind.BlockGroup_Support,
            "Media" => GalleryShellLangResourceKind.BlockGroup_Media,
            "Projects" => GalleryShellLangResourceKind.BlockGroup_Projects,
            "Heroes" => GalleryShellLangResourceKind.BlockGroup_Heroes,
            "Features" => GalleryShellLangResourceKind.BlockGroup_Features,
            "SocialProof" => GalleryShellLangResourceKind.BlockGroup_SocialProof,
            "Content" => GalleryShellLangResourceKind.BlockGroup_Content,
            "HeadersFooters" => GalleryShellLangResourceKind.BlockGroup_HeadersFooters,
            "Showpieces" => GalleryShellLangResourceKind.BlockGroup_Showpieces,
            _ => null
        };
        kind = found.GetValueOrDefault();
        return found is not null;
    }
}
