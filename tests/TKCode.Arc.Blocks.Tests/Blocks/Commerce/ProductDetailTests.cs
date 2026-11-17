using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Commerce;

/// <summary><see cref="ProductDetail"/>: pictures per color, sizes and stock, quantity limits, adding and the keyboard.</summary>
public class ProductDetailTests
{
    private static (ProductDetail Detail, Window Window) Show(double width = 1000)
    {
        var detail = new ProductDetail();
        ProductDetailSamples.Apply(detail);
        var window = TestHelpers.Show(new Border { Width = width, Child = detail });
        window.Width = 1100;
        window.Height = 1600;
        Pump();
        return (detail, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static List<ProductDetailOption> Options(ProductDetail detail, string kind)
        => detail.GetVisualDescendants().OfType<ProductDetailOption>()
            .Where(o => kind == "size" ? !o.Classes.Contains("thumb") && !o.Classes.Contains("swatch") : o.Classes.Contains(kind)).ToList();

    [AvaloniaFact]
    public void Choosing_A_Color_Swaps_The_Pictures_And_Resets_To_The_First()
    {
        var (detail, window) = Show();
        try
        {
            var main = TestHelpers.Descendant<Image>(detail, "PART_Main");
            main.Source.ShouldBe(detail.Colors[0].Images[0]);
            Options(detail, "thumb").Count.ShouldBe(4);
            detail.PictureIndex = 2;
            main.Source.ShouldBe(detail.Colors[0].Images[2]);
            Options(detail, "swatch")[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            detail.SelectedColorIndex.ShouldBe(1);
            detail.PictureIndex.ShouldBe(0);
            main.Source.ShouldBe(detail.Colors[1].Images[0]);
            TestHelpers.Descendant<ProductDetailStage>(detail).Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            detail.PictureIndex.ShouldBe(3);
            AutomationProperties.GetName(TestHelpers.Descendant<ProductDetailStage>(detail)).ShouldBe("Picture 4 of 4");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Add_Waits_For_A_Size_And_Respects_Stock()
    {
        var (detail, window) = Show();
        try
        {
            var sizes = Options(detail, "size");
            sizes.Count.ShouldBe(5);
            sizes[0].IsEnabled.ShouldBeFalse();
            AutomationProperties.GetName(sizes[0]).ShouldBe("XS, sold out");

            ProductDetailAddedEventArgs? added = null;
            detail.AddedToCart += (_, e) => added = e;
            detail.AddToCart().ShouldBeFalse();
            detail.SizeError.ShouldBe("Choose a size first.");
            sizes[1].IsFocused.ShouldBeTrue();

            detail.Quantity = 5;
            detail.SelectedSizeIndex = 2;
            detail.SizeError.ShouldBeNull();
            detail.MaxQuantity.ShouldBe(2);
            detail.Quantity.ShouldBe(2);
            detail.AddToCart().ShouldBeTrue();
            added!.Size!.Label.ShouldBe("M");
            added.Quantity.ShouldBe(2);
            added.Color!.Name.ShouldBe("Sand");
            detail.LastAnnouncement.ShouldBe("2 × Field tote added to cart");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Skip_Sold_Out_Sizes()
    {
        var (detail, window) = Show();
        try
        {
            var sizes = Options(detail, "size");
            sizes[2].Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            sizes[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            sizes[1].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Save_Toggles_And_Narrow_Block_Stacks()
    {
        var (detail, window) = Show(width: 420);
        try
        {
            detail.IsCompact.ShouldBeTrue();
            detail.IsSaved = true;
            detail.GetVisualDescendants().OfType<Button>().Any(b => AutomationProperties.GetName(b) == "Saved").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
