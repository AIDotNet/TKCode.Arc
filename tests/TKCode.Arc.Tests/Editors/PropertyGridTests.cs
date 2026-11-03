using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Editors;

public enum PgLineType
{
    [Description("Continuous")] Solid,
    Dashed,
    Dotted
}

[PropertyGridModel]
public struct PgPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

[PropertyGridModel]
public sealed class PgVertex
{
    public string Name { get; set; } = "";
    public double Weight { get; set; }
}

[PropertyGridModel]
public class PgShape : INotifyPropertyChanged
{
    private string _layer = "0";

    public event PropertyChangedEventHandler? PropertyChanged;

    [Category("General"), DisplayName("Layer name"), Description("The layer the shape is drawn on.")]
    public string Layer
    {
        get => _layer;
        set
        {
            _layer = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Layer)));
        }
    }

    [Category("General")]
    public Color Color { get; set; } = Colors.Red;

    [Category("General")]
    public PgLineType LineType { get; set; }

    [Category("General")]
    public bool Visible { get; set; } = true;

    [Category("General")]
    public DateOnly Created { get; set; } = new(2026, 11, 3);
}

[PropertyGridModel]
public sealed class PgArc : PgShape
{
    private double _radius = 10;

    [Category("Geometry"), PropertyGridOptions(Minimum = 0, FractionDigits = 2)]
    public double Radius
    {
        get => _radius;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Radius cannot be negative.");
            }
            _radius = value;
        }
    }

    [Category("Geometry")]
    public PgPoint Center { get; set; } = new() { X = 1, Y = 2 };

    [Category("Geometry")]
    public double Area => Math.Round(Math.PI * Radius * Radius, 2);

    [Category("Geometry")]
    public List<double> Bulges { get; } = [0.5, 1.5];

    [Category("Geometry")]
    public ObservableCollection<PgVertex> Vertices { get; } = [new() { Name = "A", Weight = 1 }];

    [Category("Geometry"), PropertyGridOptions(EditorKey = "Code")]
    public string Code { get; set; } = "ARC";
}

[PropertyGridModel]
public sealed class PgLine : PgShape
{
    public double Length { get; set; } = 5;
}

public class PropertyGridTests
{
    private static (Window Window, PropertyGrid Grid) Show(object? target = null, Action<PropertyGrid>? configure = null)
    {
        var grid = new PropertyGrid { Width = 480, Height = 900, SelectedObject = target };
        configure?.Invoke(grid);
        var window = TestHelpers.Show(grid);
        window.Width = 520;
        window.Height = 960;
        Dispatcher.UIThread.RunJobs();
        return (window, grid);
    }

    private static PropertyGridRow Row(PropertyGrid grid, string path)
        => grid.FindRow(path) ?? throw new InvalidOperationException($"No row {path}.");

    private static PropertyGridItem Item(PropertyGrid grid, string path)
        => grid.GetVisualDescendants().OfType<PropertyGridItem>().Single(i => i.Row?.Path == path && i.IsVisible);

    private static Control? Editor(PropertyGrid grid, string path)
        => Item(grid, path).GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == PropertyGridItem.EditorPart).Child;

    [AvaloniaFact]
    public void Generated_models_list_their_properties_with_metadata()
    {
        var (_, grid) = Show(new PgArc());

        grid.Rows.Select(r => r.Name).ShouldBe(["Layer", "Color", "LineType", "Visible", "Created", "Radius", "Center", "Area", "Bulges", "Vertices", "Code"]);
        var layer = Row(grid, "Layer");
        layer.DisplayName.ShouldBe("Layer name");
        layer.Category.ShouldBe("General");
        layer.Description.ShouldBe("The layer the shape is drawn on.");
        Row(grid, "Area").IsReadOnly.ShouldBeTrue();
        Row(grid, "Radius").IsReadOnly.ShouldBeFalse();
        grid.Categories.Select(c => c.Name).ShouldBe(["General", "Geometry"]);
        Row(grid, "LineType").Property.Choices!.Select(c => c.Text).ShouldBe(["Continuous", "Dashed", "Dotted"]);
        Row(grid, "LineType").DisplayText.ShouldBe("Continuous");
    }

    [AvaloniaFact]
    public void Each_type_gets_its_built_in_editor()
    {
        var (_, grid) = Show(new PgArc());

        Editor(grid, "Layer").ShouldBeOfType<Input>();
        Editor(grid, "Radius").ShouldBeOfType<NumberField>();
        Editor(grid, "Visible").ShouldBeOfType<Switch>();
        Editor(grid, "LineType").ShouldBeOfType<Select>();
        Editor(grid, "Color").ShouldBeOfType<ColorPicker>();
        Editor(grid, "Created").ShouldBeOfType<DatePicker>();
        // Read-only values and lists show text.
        Editor(grid, "Area").ShouldBeOfType<TextBlock>().Text.ShouldBe("314.1600");
        Editor(grid, "Bulges").ShouldBeOfType<TextBlock>().Text.ShouldBe("2 items");
        ((NumberField)Editor(grid, "Radius")!).Minimum.ShouldBe(0m);
        ((NumberField)Editor(grid, "Radius")!).Value.ShouldBe(10m);
        ((Select)Editor(grid, "LineType")!).Size.ShouldBe(ControlSize.Small);
    }

    [AvaloniaFact]
    public void Writing_a_row_sets_the_property_and_raises_the_event()
    {
        var arc = new PgArc();
        var (_, grid) = Show(arc);
        PropertyGridValueChangedEventArgs? raised = null;
        grid.PropertyValueChanged += (_, e) => raised = e;

        Row(grid, "Radius").Value = 2m;

        arc.Radius.ShouldBe(2);
        raised.ShouldNotBeNull();
        raised.Row.Name.ShouldBe("Radius");
        raised.OldValues.ShouldBe([10d]);
        raised.NewValue.ShouldBe(2d);
        // Dependent values are read again.
        Row(grid, "Area").DisplayText.ShouldBe("12.5700");
        ((NumberField)Editor(grid, "Radius")!).Value.ShouldBe(2m);
    }

    [AvaloniaFact]
    public void A_setter_that_throws_leaves_the_value_and_reports_the_error()
    {
        var arc = new PgArc();
        var (_, grid) = Show(arc);
        var row = Row(grid, "Radius");

        row.Value = -1d;

        arc.Radius.ShouldBe(10);
        row.ErrorMessage.ShouldNotBeNull();
        row.ErrorMessage.ShouldContain("Radius cannot be negative");
        Item(grid, "Radius").Classes.Contains(PropertyGridItem.InvalidPseudoClass).ShouldBeTrue();

        row.Value = 3d;
        row.ErrorMessage.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Text_that_cannot_be_converted_is_rejected()
    {
        var (_, grid) = Show(new PgArc());
        var row = Row(grid, "Radius");

        row.Value = "wide";

        row.ErrorMessage.ShouldNotBeNull();
        row.Value.ShouldBe(10d);
    }

    [AvaloniaFact]
    public void Struct_values_expand_and_write_back_to_their_owner()
    {
        var arc = new PgArc();
        var (_, grid) = Show(arc);
        var center = Row(grid, "Center");
        center.IsExpandable.ShouldBeTrue();

        center.IsExpanded = true;
        Dispatcher.UIThread.RunJobs();
        center.Children.Select(c => c.Path).ShouldBe(["Center.X", "Center.Y"]);
        grid.VisibleItems.ShouldContain(Row(grid, "Center.X"));

        Row(grid, "Center.X").Value = 42d;

        arc.Center.X.ShouldBe(42);
        arc.Center.Y.ShouldBe(2);
        Row(grid, "Center.X").Value.ShouldBe(42d);
    }

    [AvaloniaFact]
    public void Lists_expand_into_items_that_can_be_edited_added_and_removed()
    {
        var arc = new PgArc();
        var (_, grid) = Show(arc);
        var bulges = Row(grid, "Bulges");
        bulges.IsCollection.ShouldBeTrue();
        bulges.ItemCount.ShouldBe(2);
        bulges.CanAddItem.ShouldBeTrue();

        bulges.IsExpanded = true;
        Row(grid, "Bulges[1]").Value.ShouldBe(1.5);
        Row(grid, "Bulges[1]").DisplayName.ShouldBe("[1]");
        Row(grid, "Bulges[1]").Value = 3d;
        arc.Bulges[1].ShouldBe(3);

        bulges.AddItem();
        arc.Bulges.ShouldBe([0.5, 3, 0]);
        Row(grid, "Bulges").DisplayText.ShouldBe("3 items");
        Row(grid, "Bulges[2]").CanRemove.ShouldBeTrue();

        Row(grid, "Bulges[0]").Remove();
        arc.Bulges.ShouldBe([3, 0]);
        Row(grid, "Bulges").Children.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Items_that_are_models_expand_into_their_own_rows()
    {
        var arc = new PgArc();
        var (_, grid) = Show(arc);

        Row(grid, "Vertices").IsExpanded = true;
        Row(grid, "Vertices[0]").IsExpandable.ShouldBeTrue();
        Row(grid, "Vertices[0]").IsExpanded = true;
        Row(grid, "Vertices[0].Name").Value = "B";

        arc.Vertices[0].Name.ShouldBe("B");

        // An ObservableCollection change outside the grid shows after the dispatcher runs.
        arc.Vertices.Add(new PgVertex { Name = "C" });
        TestHelpers.PumpUntil(() => Row(grid, "Vertices").ItemCount == 2).ShouldBeTrue();
        Row(grid, "Vertices").Children.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Several_objects_show_shared_properties_and_mixed_values()
    {
        var arc = new PgArc { Layer = "Walls" };
        var line = new PgLine { Layer = "Doors", Visible = true };
        var (_, grid) = Show(null, g => g.SelectedObjects = new object[] { arc, line });

        grid.Rows.Select(r => r.Name).ShouldBe(["Layer", "Color", "LineType", "Visible", "Created"]);
        var layer = Row(grid, "Layer");
        layer.IsMixed.ShouldBeTrue();
        layer.Value.ShouldBeNull();
        layer.DisplayText.ShouldBe("Varies");
        Row(grid, "Visible").IsMixed.ShouldBeFalse();
        ((Input)Editor(grid, "Layer")!).Placeholder.ShouldBe("Varies");

        layer.Value = "Roof";

        arc.Layer.ShouldBe("Roof");
        line.Layer.ShouldBe("Roof");
        layer.IsMixed.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Changes_made_outside_the_grid_are_read_again()
    {
        var arc = new PgArc();
        var (_, grid) = Show(arc);

        arc.Layer = "Outside";

        TestHelpers.PumpUntil(() => Row(grid, "Layer").Value as string == "Outside").ShouldBeTrue();
        ((Input)Editor(grid, "Layer")!).Text.ShouldBe("Outside");

        // A plain property without notifications appears after Refresh.
        arc.Visible = false;
        grid.Refresh();
        Row(grid, "Visible").Value.ShouldBe(false);
    }

    [AvaloniaFact]
    public void The_search_text_keeps_matching_rows_and_their_categories()
    {
        var (_, grid) = Show(new PgArc());

        grid.FilterText = "rad";

        grid.VisibleItems.Select(i => i.ToString()).ShouldBe(["Geometry", "Radius"]);

        grid.FilterText = "nothing at all";
        grid.VisibleItems.ShouldBeEmpty();
        grid.Classes.Contains(PropertyGrid.EmptyPseudoClass).ShouldBeTrue();

        grid.FilterText = null;
        grid.VisibleItems.Count.ShouldBe(13);
    }

    [AvaloniaFact]
    public void Categories_collapse_and_the_grid_can_list_alphabetically()
    {
        var (_, grid) = Show(new PgArc());

        grid.Categories[0].IsExpanded = false;
        grid.VisibleItems.OfType<PropertyGridRow>().ShouldNotContain(r => r.Category == "General");
        grid.VisibleItems.Count.ShouldBe(8);

        grid.IsCategorized = false;
        grid.VisibleItems.OfType<PropertyGridCategory>().ShouldBeEmpty();
        grid.VisibleItems.Cast<PropertyGridRow>().Select(r => r.DisplayName).First().ShouldBe("Area");
    }

    [AvaloniaFact]
    public void Custom_editors_match_by_key_then_name_then_type()
    {
        var (_, grid) = Show(new PgArc(), g =>
        {
            g.Editors.Add(new PropertyGridEditor
            {
                PropertyType = typeof(string),
                Template = new FuncDataTemplate<PropertyGridRow>((_, _) => new TextBlock { Tag = "type" })
            });
            g.Editors.Add(new PropertyGridEditor
            {
                PropertyName = "Layer",
                Template = new FuncDataTemplate<PropertyGridRow>((_, _) => new TextBlock { Tag = "name" })
            });
            g.Editors.Add(new PropertyGridEditor
            {
                EditorKey = "Code",
                Template = new FuncDataTemplate<PropertyGridRow>((_, _) => new TextBlock { Tag = "key" })
            });
        });

        Editor(grid, "Layer")!.Tag.ShouldBe("name");
        Editor(grid, "Code")!.Tag.ShouldBe("key");
        Editor(grid, "Layer")!.DataContext.ShouldBe(Row(grid, "Layer"));
        Editor(grid, "Radius").ShouldBeOfType<NumberField>();
    }

    [AvaloniaFact]
    public void Types_registered_in_code_are_edited_like_generated_ones()
    {
        PropertyGridTypes.Register<CodeSize>(t =>
        {
            t.Property("Width", s => s.Width).Update((s, w) => s with { Width = w }).Category("Size").Range(0, 100);
            t.Property("Height", s => s.Height).Update((s, h) => s with { Height = h }).Category("Size");
            t.Property("Area", s => s.Width * s.Height).Category("Size");
            t.Summary(s => $"{s.Width} × {s.Height}");
        });
        PropertyGridTypes.Register<CodeHolder>(t =>
        {
            t.Property("Size", h => h.Size).Set((h, s) => h.Size = s);
        });
        var holder = new CodeHolder();
        var (_, grid) = Show(holder);

        var size = Row(grid, "Size");
        size.DisplayText.ShouldBe("2 × 3");
        size.IsExpanded = true;
        Row(grid, "Size.Width").Property.Maximum.ShouldBe(100);
        Row(grid, "Size.Area").IsReadOnly.ShouldBeTrue();

        Row(grid, "Size.Width").Value = 5;

        holder.Size.ShouldBe(new CodeSize(5, 3));
        Row(grid, "Size").DisplayText.ShouldBe("5 × 3");
        Row(grid, "Size.Area").Value.ShouldBe(15);
    }

    [AvaloniaFact]
    public void Avalonia_objects_are_described_by_their_registered_properties()
    {
        var border = new Border { Width = 120, Opacity = 0.5 };
        var (_, grid) = Show(border);

        Row(grid, "Width").Value.ShouldBe(120d);
        Row(grid, "Opacity").Category.ShouldBe("Visual");
        Row(grid, "HorizontalAlignment").Property.Choices.ShouldNotBeNull();

        Row(grid, "Width").Value = 200d;
        border.Width.ShouldBe(200);
    }

    [AvaloniaFact]
    public void A_read_only_grid_shows_text_only()
    {
        var (_, grid) = Show(new PgArc(), g => g.IsReadOnly = true);

        Row(grid, "Radius").IsReadOnly.ShouldBeTrue();
        Editor(grid, "Radius").ShouldBeOfType<TextBlock>().Text.ShouldBe("10.00");
        Row(grid, "Bulges").CanAddItem.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Arrow_keys_move_between_rows_and_expand_them()
    {
        var (window, grid) = Show(new PgArc());
        var radius = Item(grid, "Radius");
        radius.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        grid.SelectedRow!.Path.ShouldBe("Center");
        TopLevel.GetTopLevel(grid)!.FocusManager!.GetFocusedElement().ShouldBe(Item(grid, "Center"));

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Row(grid, "Center").IsExpanded.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        grid.SelectedRow!.Path.ShouldBe("Center.X");

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        grid.SelectedRow!.Path.ShouldBe("Center");
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Row(grid, "Center").IsExpanded.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_description_area_follows_the_selected_row()
    {
        var (_, grid) = Show(new PgArc());

        grid.SelectedRow = Row(grid, "Layer");
        var texts = grid.GetVisualDescendants().OfType<TextBlock>().ToList();

        texts.Single(t => t.Name == "PART_DescriptionTitle").Text.ShouldBe("Layer name");
        texts.Single(t => t.Name == "PART_DescriptionText").Text.ShouldBe("The layer the shape is drawn on.");
        Item(grid, "Layer").Classes.Contains(PropertyGridItem.SelectedPseudoClass).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Expanded_rows_and_the_selection_survive_a_new_selected_object()
    {
        var (_, grid) = Show(new PgArc());
        Row(grid, "Center").IsExpanded = true;
        grid.SelectedRow = Row(grid, "Center.Y");

        grid.SelectedObject = new PgArc();

        Row(grid, "Center").IsExpanded.ShouldBeTrue();
        grid.SelectedRow!.Path.ShouldBe("Center.Y");
    }

    [AvaloniaFact]
    public void The_text_editor_commits_on_enter()
    {
        var arc = new PgArc();
        var (window, grid) = Show(arc);
        var input = (Input)Editor(grid, "Layer")!;
        input.Focus();
        input.Text = "Typed";
        arc.Layer.ShouldBe("0");

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        arc.Layer.ShouldBe("Typed");
    }

    [AvaloniaFact]
    public void Small_select_and_color_picker_triggers_match_small_fields()
    {
        var select = new Select { Size = ControlSize.Small, ItemsSource = new[] { "A" } };
        var picker = new ColorPicker { Size = ControlSize.Small };
        TestHelpers.Show(new StackPanel { Children = { select, picker } });

        select.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Trigger").Bounds.Height.ShouldBe(36);
        picker.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Trigger").Bounds.Height.ShouldBe(36);
        new Select().Size.ShouldBe(ControlSize.Medium);
    }

    public sealed record CodeSize(int Width, int Height)
    {
        public CodeSize() : this(2, 3)
        {
        }
    }

    public sealed class CodeHolder
    {
        public CodeSize Size { get; set; } = new();
    }
}
