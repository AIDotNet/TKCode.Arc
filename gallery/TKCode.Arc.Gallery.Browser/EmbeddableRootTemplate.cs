using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Embedding;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Styling;

namespace TKCode.Arc.Gallery.Browser;

/// <summary>
/// The browser has no popup windows, so every popup (Popover, Tooltip, Select, pickers) is hosted in the root's overlay
/// layer. Avalonia 12 enables that layer on the <see cref="VisualLayerManager"/> the root template names
/// <c>PART_VisualLayerManager</c>. AtomUI 6.2.6's <see cref="EmbeddableControlRoot"/> template has the manager but leaves it
/// unnamed, so opening a popup throws "no overlay layer is found for the target control" and nothing appears. This style
/// swaps in the same template with the part named; a style outranks the theme, which keeps its other setters (background,
/// foreground, font). Drop it when AtomUI names the part itself.
/// </summary>
internal static class EmbeddableRootTemplate
{
    public static Style Create() => new(static x => x.OfType<EmbeddableControlRoot>())
    {
        Setters = { new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<EmbeddableControlRoot>(Build)) }
    };

    private static Control Build(EmbeddableControlRoot root, INameScope scope)
    {
        var fallback = new Border { IsHitTestVisible = false };
        scope.Register("PART_TransparencyFallback", fallback);

        var presenter = new ContentPresenter();
        presenter.Bind(ContentPresenter.ContentProperty, root.GetObservable(ContentControl.ContentProperty));
        presenter.Bind(ContentPresenter.ContentTemplateProperty, root.GetObservable(ContentControl.ContentTemplateProperty));
        presenter.Bind(Layoutable.MarginProperty, root.GetObservable(TemplatedControl.PaddingProperty));
        scope.Register("PART_ContentPresenter", presenter);

        var layers = new VisualLayerManager { Child = presenter };
        scope.Register("PART_VisualLayerManager", layers);

        var background = new Border { Child = layers };
        background.Bind(Border.BackgroundProperty, root.GetObservable(TemplatedControl.BackgroundProperty));

        return new Panel { Children = { fallback, background } };
    }
}
