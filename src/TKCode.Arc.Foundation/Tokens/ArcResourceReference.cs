using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.XamlIl.Runtime;

namespace TKCode.Arc;

/// <summary>
/// The dynamic resource reference behind the generated <c>{arc:XxxTokenResource}</c> and <c>{arc:XxxLangResource}</c>
/// markup extensions. Used by generated code.
/// </summary>
/// <remarks>
/// Avalonia's <c>{DynamicResource}</c> applies a value written on an element inside a control template at
/// <see cref="BindingPriority.Template"/>, below the style triggers of that element's own theme (for example a
/// <c>^[Size=Small]</c> setter of the part's theme). Arc's themes were written for local priority: a token written on a
/// template part is the part's value whatever its theme's triggers say, and the templated control's own
/// <c>/template/</c> styles still win over it only through their own triggers. So the reference is created as if outside a
/// template; everything else (the anchor for non-element targets, the theme variant of a theme dictionary) is unchanged.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArcResourceReference
{
    /// <summary>A dynamic resource reference to <paramref name="key"/> at local priority.</summary>
    /// <param name="key">The resource key (a boxed token or language kind).</param>
    /// <param name="serviceProvider">The XAML service provider given to the markup extension.</param>
    public static BindingBase Create(object key, IServiceProvider serviceProvider)
        => new DynamicResourceExtension(key).ProvideValue(new OutsideTemplate(serviceProvider));

    /// <summary>Answers every service like the XAML context, except that it is not inside a control template.</summary>
    private sealed class OutsideTemplate : IServiceProvider
    {
        private readonly IServiceProvider _inner;

        public OutsideTemplate(IServiceProvider inner) => _inner = inner;

        public object? GetService(Type serviceType)
            => serviceType == typeof(IAvaloniaXamlIlControlTemplateProvider) ? null : _inner.GetService(serviceType);
    }
}
