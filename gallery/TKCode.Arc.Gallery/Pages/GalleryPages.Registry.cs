namespace TKCode.Arc.Gallery.Pages;

// Each category registers its pages in Pages/<Category>/<Category>Pages.cs (one file per category, so
// categories can be developed independently).
public static partial class GalleryPages
{
    static partial void RegisterAll()
    {
        RegisterButtons();
        RegisterGestures();
        RegisterMenus();
        RegisterTextFields();
        RegisterSpecialInputs();
        RegisterSelects();
        RegisterToggles();
        RegisterSliders();
        RegisterPickers();
        RegisterEditors();
        RegisterNavigation();
        RegisterExpand();
        RegisterOverlays();
        RegisterMessages();
        RegisterProgress();
        RegisterAvatars();
        RegisterCards();
        RegisterCharts();
        RegisterTables();
        RegisterActivity();
        RegisterMedia();
        RegisterTextEffects();
        RegisterBlocks();
        // Extras (not Arc components).
        RegisterContent();
        RegisterIllustrations();
    }

    static partial void RegisterButtons();
    static partial void RegisterGestures();
    static partial void RegisterMenus();
    static partial void RegisterTextFields();
    static partial void RegisterSpecialInputs();
    static partial void RegisterSelects();
    static partial void RegisterToggles();
    static partial void RegisterSliders();
    static partial void RegisterPickers();
    static partial void RegisterEditors();
    static partial void RegisterNavigation();
    static partial void RegisterExpand();
    static partial void RegisterOverlays();
    static partial void RegisterMessages();
    static partial void RegisterProgress();
    static partial void RegisterAvatars();
    static partial void RegisterCards();
    static partial void RegisterCharts();
    static partial void RegisterTables();
    static partial void RegisterActivity();
    static partial void RegisterMedia();
    static partial void RegisterTextEffects();
    static partial void RegisterBlocks();
    static partial void RegisterContent();
    static partial void RegisterIllustrations();
}
