using TKCode.Arc.DesignTokens;
using TKCode.Arc.Themes.Corners;
using AtomUI.Theme.Configuration;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using TKCode.Arc.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Theme;

/// <summary>The global corner style: square corners everywhere, and nothing but corners changes.</summary>
public class CornerStyleTests
{
    private static ThemeConfig Config(ArcCornerStyle style) => new ThemeConfigBuilder().WithArcCorners(style, ArcCornerScope.All).Build();

    [AvaloniaFact]
    public async Task Square_Zeroes_The_ArcDesign_Radii_And_Keeps_Every_Other_Token()
    {
        var host = new Border();
        var window = TestHelpers.Show(host);
        try
        {
            var before = Snapshot(host);
            await TestHelpers.ApplyAsync(Config(ArcCornerStyle.Square));
            var after = Snapshot(host);

            var radii = Enum.GetValues<ArcDesignTokenKind>().Where(k => k.ToString().StartsWith("Radius", StringComparison.Ordinal)).ToArray();
            radii.Length.ShouldBeGreaterThan(30);
            foreach (var kind in radii)
            {
                after[kind].ShouldBe(new CornerRadius(0), kind.ToString());
            }
            foreach (var kind in before.Keys.Except(radii))
            {
                after[kind].ShouldBe(before[kind], kind.ToString());
            }
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Default_Writes_Nothing()
    {
        var host = new Border();
        var window = TestHelpers.Show(host);
        try
        {
            var before = Snapshot(host);
            await TestHelpers.ApplyAsync(Config(ArcCornerStyle.Default));
            Snapshot(host).ShouldBe(before);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Square_Reaches_Checkbox_Box_And_Mark_And_Can_Be_Undone()
    {
        var checkbox = new Checkbox();
        var window = TestHelpers.Show(checkbox);
        try
        {
            var square = TestHelpers.Descendant<Border>(checkbox, "PART_Square");
            var mark = TestHelpers.Descendant<CheckboxMark>(checkbox);
            square.CornerRadius.ShouldBe(new CornerRadius(5));
            mark.Radius.ShouldBe(5);

            await TestHelpers.ApplyAsync(Config(ArcCornerStyle.Square));
            square.CornerRadius.ShouldBe(new CornerRadius(0));
            mark.Radius.ShouldBe(0);

            await TestHelpers.ApplyAsync(null);
            square.CornerRadius.ShouldBe(new CornerRadius(5));
            mark.Radius.ShouldBe(5);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public async Task Square_Combines_With_An_Accent_In_One_WithArc_Call()
    {
        var host = new Border();
        var window = TestHelpers.Show(host);
        try
        {
            var config = new ThemeConfigBuilder()
                .WithArc(arc => arc.Accent(ArcAccentPreset.Violet).Corners(ArcCornerStyle.Square, ArcCornerScope.Controls))
                .Build();
            var before = Snapshot(host);
            await TestHelpers.ApplyAsync(config);
            var after = Snapshot(host);
            after[ArcDesignTokenKind.RadiusControl].ShouldBe(new CornerRadius(0));
            after[ArcDesignTokenKind.Accent].ShouldNotBe(before[ArcDesignTokenKind.Accent]);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    /// <summary>
    /// Creates every Arc control of the main package with its default template and walks the visual tree under the square
    /// style. Anything still rounded is listed so it can be added to the table or fixed in the control.
    /// </summary>
    [AvaloniaFact]
    public async Task No_Control_Keeps_A_Rounded_Element_Under_Square()
    {
        var types = ControlTypes();

        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        await TestHelpers.ApplyAsync(Config(ArcCornerStyle.Square));
        try
        {
            foreach (var type in types)
            {
                Control control;
                try
                {
                    control = (Control)Activator.CreateInstance(type)!;
                }
                catch
                {
                    continue;
                }
                Window window;
                try
                {
                    window = TestHelpers.Show(control);
                }
                catch
                {
                    continue;
                }
                try
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    foreach (var visual in control.GetVisualDescendants().Prepend(control))
                    {
                        var radius = visual switch
                        {
                            Border border => border.CornerRadius,
                            TemplatedControl templated => templated.CornerRadius,
                            _ => default
                        };
                        if (radius != default)
                        {
                            var name = (visual as Control)?.Name;
                            offenders.Add($"{type.Name}: {visual.GetType().Name}{(name is null ? "" : "#" + name)} = {radius}");
                        }
                    }
                }
                finally
                {
                    window.Close();
                }
            }
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }

        offenders.ShouldBeEmpty(string.Join('\n', offenders));
    }

    [Fact]
    public void Scale_Validates_And_Meets_Default_And_Square()
    {
        ArcCornerStyle.Scale(1).ShouldBe(ArcCornerStyle.Default);
        ArcCornerStyle.Scale(0).ShouldBe(ArcCornerStyle.Square);
        default(ArcCornerStyle).ShouldBe(ArcCornerStyle.Default);
        ArcCornerStyle.Scale(0.5).ShouldNotBe(ArcCornerStyle.Default);
        Should.Throw<ArgumentOutOfRangeException>(() => ArcCornerStyle.Scale(-0.1));
        Should.Throw<ArgumentOutOfRangeException>(() => ArcCornerStyle.Scale(double.NaN));
        Should.Throw<ArgumentOutOfRangeException>(() => ArcCornerStyle.Scale(double.PositiveInfinity));
    }

    [AvaloniaFact]
    public async Task Scale_Halves_The_Radii_And_Keeps_Pills_And_Every_Other_Token()
    {
        var checkbox = new Checkbox();
        var window = TestHelpers.Show(checkbox);
        try
        {
            var before = Snapshot(checkbox);
            await TestHelpers.ApplyAsync(Config(ArcCornerStyle.Scale(0.5)));
            var after = Snapshot(checkbox);

            after[ArcDesignTokenKind.RadiusControl].ShouldBe(new CornerRadius(9));
            after[ArcDesignTokenKind.RadiusPanel].ShouldBe(new CornerRadius(13));
            after[ArcDesignTokenKind.RadiusStep14p5].ShouldBe(new CornerRadius(7.25));
            after[ArcDesignTokenKind.RadiusStep19Top].ShouldBe(new CornerRadius(9.5, 9.5, 0, 0));
            after[ArcDesignTokenKind.RadiusPill].ShouldBe(new CornerRadius(9999));
            foreach (var kind in before.Keys.Where(k => !k.ToString().StartsWith("Radius", StringComparison.Ordinal)))
            {
                after[kind].ShouldBe(before[kind], kind.ToString());
            }

            TestHelpers.Descendant<Border>(checkbox, "PART_Square").CornerRadius.ShouldBe(new CornerRadius(2.5));
            TestHelpers.Descendant<CheckboxMark>(checkbox).Radius.ShouldBe(2.5);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    /// <summary>
    /// The table's defaults are read from the sources, so they are only right if every radius on screen really is the
    /// default times the factor. Walks every control under the default style and under a half scale and compares the
    /// elements one by one; whatever is not half (and not a pill, which keeps its value) is a radius that bypasses the table.
    /// </summary>
    [AvaloniaFact]
    public async Task Scale_Halves_Every_Radius_On_Screen()
    {
        var types = ControlTypes();
        var normal = await RadiiAsync(types, ArcCornerStyle.Default);
        var half = await RadiiAsync(types, ArcCornerStyle.Scale(0.5));
        normal.Values.Sum(list => list.Count).ShouldBeGreaterThan(400, "the walk must find the rounded elements, or the comparison proves nothing");

        // Controls whose collapsed shape is a pill drawn at half its height (a computed 22, not the token's 9999): a pill keeps its
        // shape under Scale, and only Square flattens it.
        string[] pillShapes = ["ExpandingSearch"];

        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (type, radii) in normal.Where(entry => !pillShapes.Contains(entry.Key)))
        {
            var scaled = half[type];
            if (scaled.Count != radii.Count)
            {
                offenders.Add($"{type}: {radii.Count} rounded elements by default, {scaled.Count} at half");
                continue;
            }
            for (var i = 0; i < radii.Count; i++)
            {
                if (!Matches(radii[i].Radius, scaled[i].Radius))
                {
                    offenders.Add($"{type}: {radii[i].Name} {radii[i].Radius} -> {scaled[i].Radius}");
                }
            }
        }
        offenders.ShouldBeEmpty(string.Join('\n', offenders));
    }

    private static bool Matches(CornerRadius normal, CornerRadius half)
        => Matches(normal.TopLeft, half.TopLeft) && Matches(normal.TopRight, half.TopRight)
           && Matches(normal.BottomRight, half.BottomRight) && Matches(normal.BottomLeft, half.BottomLeft);

    private static bool Matches(double normal, double half)
        => Math.Abs(half - normal * 0.5) < 0.01 || (normal >= 999 && Math.Abs(half - normal) < 0.01);

    private static async Task<Dictionary<string, List<(string Name, CornerRadius Radius)>>> RadiiAsync(Type[] types, ArcCornerStyle style)
    {
        var result = new Dictionary<string, List<(string, CornerRadius)>>();
        await TestHelpers.ApplyAsync(Config(style));
        try
        {
            foreach (var type in types)
            {
                if (!TryShow(type, out var control, out var window))
                {
                    continue;
                }
                try
                {
                    var list = new List<(string, CornerRadius)>();
                    // Open the control's popups and overlays too, then read the whole window.
                    OpenFlags(control);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    foreach (var popup in control.GetVisualDescendants().OfType<ArcPopup>().ToArray())
                    {
                        try
                        {
                            popup.IsOpen = true;
                            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        }
                        catch
                        {
                            // Covered by the open flag.
                        }
                    }
                    foreach (var visual in window.GetVisualDescendants())
                    {
                        var radius = visual switch
                        {
                            Border border => border.CornerRadius,
                            TemplatedControl templated => templated.CornerRadius,
                            _ => default
                        };
                        if (radius != default)
                        {
                            var name = (visual as Control)?.Name;
                            list.Add(($"{visual.GetType().Name}{(name is null ? "" : "#" + name)}", radius));
                        }
                    }
                    result[type.Name] = list;
                }
                finally
                {
                    window.Close();
                }
            }
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }
        return result;
    }

    private static readonly string[] States = [":pointerover", ":pressed", ":focus", ":focus-visible", ":selected", ":checked", ":disabled", ":open"];

    /// <summary>
    /// The same walk with every popup opened (the popup's content and the host elements above it are checked, so a shell
    /// that brings its own radius shows up) and with each interaction state forced on the control and everything in it.
    /// </summary>
    [AvaloniaFact]
    public async Task No_Popup_Or_Interaction_State_Keeps_A_Rounded_Element_Under_Square()
    {
        var types = ControlTypes();
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        var reached = new SortedSet<string>();
        await TestHelpers.ApplyAsync(Config(ArcCornerStyle.Square));
        try
        {
            foreach (var type in types)
            {
                if (!TryShow(type, out var control, out var window))
                {
                    continue;
                }
                try
                {
                    // Popups: open the control the way a user does (its own open flag, so it builds its content), then check
                    // every popup's content and the host elements above it, and the overlay layer.
                    var before = window.GetVisualDescendants().Count();
                    OpenFlags(control);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    foreach (var popup in control.GetVisualDescendants().OfType<ArcPopup>().ToArray())
                    {
                        try
                        {
                            popup.IsOpen = true;
                            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        }
                        catch
                        {
                            // A popup that cannot open without its owner's state is covered by the open flag above.
                        }
                        if (popup.Child is { } child)
                        {
                            reached.Add(type.Name);
                            Collect(type.Name + " popup", child, offenders);
                            foreach (var host in child.GetVisualAncestors())
                            {
                                Collect(type.Name + " popup host", host, offenders, includeDescendants: false);
                            }
                        }
                    }
                    // Popups hosted in the window's overlay layer (and anything else the control put in the window).
                    var grown = window.GetVisualDescendants().Count() - before;
                    if (grown > 5)
                    {
                        reached.Add(type.Name);
                    }
                    Collect(type.Name + " window", window, offenders);
                    Collect(type.Name + " opened", control, offenders);
                    CloseFlags(control);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                    // Interaction states, one at a time.
                    foreach (var state in States)
                    {
                        var targets = control.GetVisualDescendants().OfType<Control>().Prepend(control).ToArray();
                        foreach (var target in targets)
                        {
                            ((IPseudoClasses)target.Classes).Set(state, true);
                        }
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                        Collect(type.Name + " " + state, control, offenders);
                        foreach (var target in targets)
                        {
                            ((IPseudoClasses)target.Classes).Set(state, false);
                        }
                    }
                }
                finally
                {
                    window.Close();
                }
            }
        }
        finally
        {
            await TestHelpers.ApplyAsync(null);
        }

        offenders.ShouldBeEmpty(string.Join('\n', offenders));

        // The sweep must really reach the popups, or it proves nothing. Controls with an open flag that are not listed here
        // were not opened by this walk (expanders are not popups; ExpandingSearch, FilterMenu, MentionInput, MoneyInput, UserMenu,
        // Toast and ToastStack need data or a host the walk does not provide).
        string[] mustReach =
        [
            "Combobox", "Select", "MultiSelect", "MorphSelect", "DropdownMenu", "ContextMenu", "SplitButton", "Popover", "HoverCard",
            "Dialog", "Drawer", "BottomSheet", "DatePicker", "DateRangePicker", "TimePicker", "ColorPicker", "PhoneInput"
        ];
        mustReach.Except(reached).ShouldBeEmpty("controls whose popup or overlay the walk no longer reaches");
    }

    private static IEnumerable<System.Reflection.PropertyInfo> OpenProperties(Control control)
        => control.GetType().GetProperties()
            .Where(p => p.PropertyType == typeof(bool) && p.CanWrite && p.GetIndexParameters().Length == 0
                        && System.Text.RegularExpressions.Regex.IsMatch(p.Name, "^Is\\w*(Open|Expanded|Shown)$"));

    private static void OpenFlags(Control control)
    {
        foreach (var property in OpenProperties(control))
        {
            try
            {
                property.SetValue(control, true);
            }
            catch
            {
                // The control may refuse to open without content.
            }
        }
    }

    private static void CloseFlags(Control control)
    {
        foreach (var property in OpenProperties(control))
        {
            try
            {
                property.SetValue(control, false);
            }
            catch
            {
                // Same as OpenFlags.
            }
        }
    }

    private static Type[] ControlTypes()
    {
        // Every control package of the scope; the Mobile package has its own tokens and is not part of it.
        string[] assemblies = ["TKCode.Arc", "TKCode.Arc.Blocks", "TKCode.Arc.Special", "TKCode.Arc.AdvancedCharts", "TKCode.Arc.Workflow"];
        var types = assemblies
            .Select(name => System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(name)))
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false } && t.IsSubclassOf(typeof(Control))
                        && t.GetConstructor(Type.EmptyTypes) is not null && t.Namespace == "TKCode.Arc")
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();
        types.Length.ShouldBeGreaterThan(200);
        return types;
    }

    /// <summary>Gives a control the content, trigger and menu items it needs to have something to open.</summary>
    private static void Fill(Control control)
    {
        if (control is ContentControl { Content: null } content)
        {
            content.Content = new TextBlock { Text = "Content" };
        }
        if (control is HeaderedContentControl { Header: null } headered)
        {
            headered.Header = "Header";
        }
        var type = control.GetType();
        if (type.GetProperty("Trigger") is { CanWrite: true } trigger && trigger.PropertyType.IsAssignableFrom(typeof(Button)) && trigger.GetValue(control) is null)
        {
            trigger.SetValue(control, new Button { Content = "Anchor" });
        }
        if (type.GetProperty("Menu")?.GetValue(control) is ItemsControl menu)
        {
            menu.ItemsSource = new[] { "One", "Two", "Three" };
        }
    }

    private static bool TryShow(Type type, out Control control, out Window window)
    {
        control = null!;
        window = null!;
        try
        {
            control = (Control)Activator.CreateInstance(type)!;
            if (control is ItemsControl items)
            {
                items.ItemsSource = new[] { "One", "Two", "Three" };
            }
            Fill(control);
            window = TestHelpers.Show(control);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Collect(string tag, Visual root, ISet<string> offenders, bool includeDescendants = true)
    {
        var visuals = includeDescendants ? root.GetVisualDescendants().Prepend(root) : [root];
        foreach (var visual in visuals)
        {
            var radius = visual switch
            {
                Border border => border.CornerRadius,
                TemplatedControl templated => templated.CornerRadius,
                _ => default
            };
            if (radius != default)
            {
                var name = (visual as Control)?.Name;
                var owner = string.Join(" < ", visual.GetVisualAncestors().Take(4).Select(a => a.GetType().Name));
                offenders.Add($"{tag}: {visual.GetType().Name}{(name is null ? "" : "#" + name)} = {radius} (in {owner})");
            }
        }
    }

    private static Dictionary<ArcDesignTokenKind, object?> Snapshot(Control host)
    {
        var result = new Dictionary<ArcDesignTokenKind, object?>();
        foreach (var kind in Enum.GetValues<ArcDesignTokenKind>())
        {
            // Easings are rebuilt on every theme change, so they never compare equal.
            if (!kind.ToString().StartsWith("Ease", StringComparison.Ordinal) && host.TryFindResource(kind, host.ActualThemeVariant, out var value))
            {
                result[kind] = value is IImmutableBrush brush ? ((ISolidColorBrush)brush).Color : value;
            }
        }
        return result;
    }
}
