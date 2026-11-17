using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Commerce;

/// <summary><see cref="ProductListing"/>: filtering and sorting, the grid columns, quick add, keyboard and the narrow layout.</summary>
public class ProductListingTests
{
    private static (ProductListing Listing, Window Window) Show(double width = 1100)
    {
        var listing = new ProductListing { ProductsSource = ProductListingSamples.CreateProducts() };
        var window = TestHelpers.Show(new Border { Width = width, Child = listing });
        window.Width = 1200;
        window.Height = 2000;
        Pump();
        return (listing, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [Fact]
    public void Math_Filters_And_Sorts_With_Stable_Ties()
    {
        var products = ProductListingSamples.CreateProducts();
        var bags = ProductListingMath.Apply(products, ["Bags"], 0, 1000, false, ListingSort.Featured);
        bags.Select(p => p.Id).ShouldBe(["tote", "tote-mini"]);
        var cheap = ProductListingMath.Apply(products, [], 0, 25, true, ListingSort.PriceLowToHigh);
        cheap.Select(p => p.Id).ShouldBe(["notebook", "mug"]);
        ProductListingMath.Apply(products, [], 0, 1000, false, ListingSort.PriceHighToLow)[0].Id.ShouldBe("tote");
        ProductListingMath.Apply(products, [], 0, 1000, false, ListingSort.TopRated).Take(2).Select(p => p.Id).ShouldBe(["mug", "tote"]);
        ProductListingMath.Apply(products, [], 0, 1000, false, ListingSort.Newest)[0].Id.ShouldBe("cap-wool");
        ProductListingMath.Columns(1000, 190, 20, 4).ShouldBe(4);
        ProductListingMath.Columns(400, 190, 20, 4).ShouldBe(2);
        ProductListingMath.Columns(100, 190, 20, 4).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Filters_Change_The_Grid_And_Clear_Restores_It()
    {
        var (listing, window) = Show();
        try
        {
            listing.VisibleProducts.Count.ShouldBe(12);
            listing.Categories.ShouldBe(["Bags", "Outdoor", "Kitchen", "Home"]);
            listing.InStockOnly = true;
            Pump();
            listing.VisibleProducts.Count.ShouldBe(10);
            listing.LastAnnouncement.ShouldBe("10 products shown");
            listing.SelectedCategories = ["Kitchen"];
            listing.VisibleProducts.Select(p => p.Id).ShouldBe(["mug", "mug-pair"]);
            TestHelpers.Descendant<TextBlock>(listing, "PART_Count").Text.ShouldBe("2 products");
            listing.MaxPrice = 10;
            listing.VisibleProducts.ShouldBeEmpty();
            listing.Classes.Contains(":empty").ShouldBeTrue();
            listing.ClearFilters();
            listing.VisibleProducts.Count.ShouldBe(12);
            listing.MaxPrice.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Quick_Add_Counts_Raises_And_Refuses_Sold_Out()
    {
        var (listing, window) = Show();
        try
        {
            var added = new List<string>();
            listing.AddedToCart += (_, e) => added.Add(e.Product.Key);
            var products = listing.EffectiveProducts;
            listing.AddToCart(products[0]).ShouldBeTrue();
            listing.AddToCart(products.First(p => !p.IsInStock)).ShouldBeFalse();
            listing.CartCount.ShouldBe(1);
            added.ShouldBe(["tote"]);
            listing.LastAnnouncement.ShouldBe("Market tote added to cart");
            AutomationProperties.GetName(TestHelpers.Descendant<Button>(listing, "PART_Cart")).ShouldBe("Cart, 1 item");
            var soldOut = listing.GetVisualDescendants().OfType<Button>().Where(b => b.Content as string == "Sold out").ToList();
            soldOut.Count.ShouldBe(2);
            soldOut.ShouldAllBe(b => !b.IsEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_Keys_Move_Between_Cards_By_Row()
    {
        var (listing, window) = Show();
        try
        {
            var columns = listing.ColumnCount;
            columns.ShouldBeGreaterThan(1);
            var cards = listing.GetVisualDescendants().OfType<ProductListingCardButton>().ToList();
            cards[0].Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            cards[1].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            cards[1 + columns].IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            cards[^1].IsFocused.ShouldBeTrue();
            ListingProduct? opened = null;
            listing.ProductInvoked += (_, e) => opened = e.Product;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            opened.ShouldBe(cards[^1].Product);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Block_Folds_The_Filters()
    {
        var (listing, window) = Show(width: 420);
        try
        {
            listing.IsCompact.ShouldBeTrue();
            var filters = TestHelpers.Descendant<StackPanel>(listing, "PART_Filters");
            filters.IsVisible.ShouldBeFalse();
            var toggle = TestHelpers.Descendant<Button>(listing, "PART_FiltersToggle");
            toggle.IsVisible.ShouldBeTrue();
            toggle.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            filters.IsVisible.ShouldBeTrue();
            listing.ColumnCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }
}
