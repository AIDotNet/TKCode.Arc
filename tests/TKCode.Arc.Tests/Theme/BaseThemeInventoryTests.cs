using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Av = Avalonia.Controls;
using AvPrimitives = Avalonia.Controls.Primitives;

namespace TKCode.Arc.Tests.Theme;

/// <summary>
/// Inventory for the AtomUI removal (design/architecture/base-theme-inventory.md): which Avalonia built-in controls that
/// Arc uses get their default ControlTheme (the one found by type key) from a theme Arc does not define, and which get
/// none at all. The candidates are the output of <c>tools/dev/avalonia_builtins.py</c>: built-ins used without an
/// explicit theme, plus hosts and template parts. AtomUI supplied the themes of eight of them; since phase F3 Arc's base theme
/// (<see cref="ArcBaseTheme"/>, registered by <c>UseArcCore()</c>) supplies exactly those eight.
/// </summary>
public class BaseThemeInventoryTests
{
    private static readonly Type[] s_candidates =
    [
        // Used in Arc templates or code without an explicit Theme.
        typeof(Av.ScrollViewer), typeof(Av.Button), typeof(Av.TextBox), typeof(Av.ItemsControl), typeof(Av.ContentControl),
        typeof(AvPrimitives.ToggleButton), typeof(Av.PathIcon),
        // Template parts and helpers every app meets.
        typeof(AvPrimitives.ScrollBar), typeof(AvPrimitives.Thumb), typeof(Av.RepeatButton), typeof(Av.ToolTip),
        typeof(Av.DataValidationErrors), typeof(AvPrimitives.TextSelectionHandle), typeof(Av.ContextMenu),
        typeof(Av.MenuFlyoutPresenter), typeof(Av.MenuItem), typeof(Av.FlyoutPresenter), typeof(Av.UserControl),
        // Not templated, listed to show whether a theme styles them anyway.
        typeof(Av.SelectableTextBlock), typeof(Av.TextBlock)
    ];

    /// <summary>
    /// Built-ins whose default theme comes from Arc's base theme (the eight AtomUI used to supply).
    /// </summary>
    private static readonly string[] s_fromBaseTheme =
    [
        "EmbeddableControlRoot", "ItemsControl", "OverlayPopupHost", "PathIcon", "PopupRoot", "ScrollBar", "ScrollViewer",
        "TextSelectionHandle", "Window"
    ];

    private sealed record Row(string Type, string Source, string Template);

    [AvaloniaFact]
    public void The_Base_Theme_Styles_Exactly_The_Built_Ins_AtomUI_Styled()
    {
        var rows = new List<Row>();
        foreach (var type in s_candidates)
        {
            var control = (Control)Activator.CreateInstance(type)!;
            Window? window = null;
            string template;
            try
            {
                window = TestHelpers.Show(control);
                template = Describe(control);
            }
            catch (Exception e)
            {
                template = $"template throws ({e.GetType().Name}: {e.Message})";
            }
            rows.Add(new Row(type.Name, SourceOf(control, type), template));
            window?.Close();
        }

        // Hosts: a window and an overlay popup (the headless test window enables the overlay layer).
        var native = new Window { Width = 100, Height = 100 };
        native.Show();
        rows.Add(new Row(nameof(Window), SourceOf(native, typeof(Window)), Describe(native)));
        var anchor = new Border { Width = 10, Height = 10 };
        var popupWindow = TestHelpers.Show(anchor);
        var popup = new Popup { Child = new Border { Width = 10, Height = 10 }, PlacementTarget = anchor, ShouldUseOverlayLayer = true };
        ((ISetLogicalParent)popup).SetParent(anchor);
        popup.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        if (popup.Child!.GetVisualAncestors().OfType<TemplatedControl>().FirstOrDefault(v => v is OverlayPopupHost or PopupRoot) is { } host)
        {
            rows.Add(new Row(host.GetType().Name, SourceOf(host, host.GetType()), Describe(host)));
        }
        popup.IsOpen = false;
        ((ISetLogicalParent)popup).SetParent(null);
        popupWindow.Close();
        native.Close();
        // Hosts headless cannot create: resolved by type key only.
        foreach (var type in new[] { typeof(AvPrimitives.PopupRoot), typeof(Avalonia.Controls.Embedding.EmbeddableControlRoot), typeof(AvPrimitives.OverlayPopupHost) })
        {
            if (rows.All(r => r.Type != type.Name))
            {
                rows.Add(new Row(type.Name, SourceOf(null, type), "not instantiated (headless)"));
            }
        }

        var report = string.Join(Environment.NewLine, rows.Select(r => $"{r.Type}\t{r.Source}\t{r.Template}"));
        TestContext.Current.TestOutputHelper?.WriteLine(report);
        if (Environment.GetEnvironmentVariable("ARC_BASE_THEME_INVENTORY") is { Length: > 0 } path)
        {
            File.WriteAllText(path, report + Environment.NewLine);
        }

        string.Join(", ", rows.Where(r => r.Source.EndsWith("BaseTheme", StringComparison.Ordinal)).Select(r => r.Type).Order(StringComparer.Ordinal))
            .ShouldBe(string.Join(", ", s_fromBaseTheme), report);
        rows.ShouldNotContain(r => r.Source.StartsWith("AtomUI", StringComparison.Ordinal), report);
    }

    /// <summary>
    /// Whether the control has a template, where it comes from (a theme, or the default value Avalonia 12 gives some
    /// controls) and whether it built anything.
    /// </summary>
    private static string Describe(Control control)
    {
        if (control is not TemplatedControl templated)
        {
            return "not templated";
        }
        if (templated.Template is null)
        {
            return "no template";
        }
        var priority = Avalonia.Diagnostics.AvaloniaObjectExtensions.GetDiagnostic(templated, TemplatedControl.TemplateProperty).Priority;
        var from = priority == Avalonia.Data.BindingPriority.Unset ? "Avalonia default template" : "template from theme";
        return templated.GetVisualChildren().Any() ? from : from + ", nothing built";
    }

    /// <summary>
    /// Where the default theme of <paramref name="type"/> lives: the type name of the resource provider that answers the
    /// type key (walking the application's styles and resources), or "none".
    /// </summary>
    private static string SourceOf(Control? control, Type type)
    {
        var app = Application.Current!;
        object? theme = null;
        if (control is not null)
        {
            // The theme actually in effect on the control (internal API; this is a test).
            theme = typeof(StyledElement).GetMethod("GetEffectiveTheme", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(control, null);
        }
        if (theme is null && !app.TryFindResource(type, ThemeVariant.Light, out theme))
        {
            return "none";
        }
        if (theme is not ControlTheme)
        {
            return "none";
        }
        foreach (var provider in Providers(app))
        {
            if (provider.TryGetResource(type, ThemeVariant.Light, out var found) && ReferenceEquals(found, theme))
            {
                return provider.GetType().FullName ?? provider.GetType().Name;
            }
        }
        return "unknown provider";
    }

    private static IEnumerable<IResourceProvider> Providers(Application app)
    {
        var stack = new Stack<object>();
        stack.Push(app.Resources);
        stack.Push(app.Styles);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            switch (node)
            {
                case Styles styles:
                    stack.Push(styles.Resources);
                    foreach (var child in styles)
                    {
                        stack.Push(child);
                    }
                    break;
                case StyleBase style:
                    stack.Push(style.Resources);
                    foreach (var child in style.Children)
                    {
                        stack.Push(child);
                    }
                    break;
                case IResourceDictionary dictionary:
                    foreach (var merged in dictionary.MergedDictionaries)
                    {
                        stack.Push(merged);
                    }
                    foreach (var variant in dictionary.ThemeDictionaries.Values)
                    {
                        stack.Push(variant);
                    }
                    if (dictionary.Count > 0)
                    {
                        yield return dictionary;
                    }
                    break;
                case IResourceProvider provider:
                    yield return provider;
                    break;
            }
        }
    }
}
