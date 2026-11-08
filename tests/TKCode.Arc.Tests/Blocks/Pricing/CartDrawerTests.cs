using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Blocks.Pricing;

/// <summary>
/// The cart drawer: seeding, adding (with the per-row cap), quantities, removal, totals, promo codes, the drawer's focus
/// model (focus on open, trap, Escape, return), checkout to receipt and the reset on close, flights and swipes.
/// </summary>
public class CartDrawerTests
{
    private static readonly CartDrawerReceipt Receipt = new("All set", "Order T-1 is confirmed.", 1, 67m, "Nov 11 – Nov 13");

    private static CartDrawer Create(Func<CartDrawerOrder, CancellationToken, Task<CartDrawerReceipt>>? place = null)
        => new()
        {
            Labels = CartDrawerSamples.CreateLabels(),
            ProductsSource = CartDrawerSamples.CreateProducts(),
            PlaceOrderAsync = place ?? ((order, _) => Task.FromResult(Receipt with { ItemCount = order.ItemCount, Paid = order.Total }))
        };

    private static Window Show(CartDrawer block, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1100, double height = 820)
    {
        var window = TestHelpers.Show(block, level);
        window.Width = width;
        window.Height = height;
        Settle();
        return window;
    }

    private static void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Frames(ManualFrameClock clock, int milliseconds)
    {
        for (var t = 0; t < milliseconds; t += 16)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static CartDrawerProduct P(CartDrawer block, int index) => block.EffectiveProducts[index];

    private static void Click(AvaloniaButton button)
    {
        button.RaiseEvent(new RoutedEventArgs(AvaloniaButton.ClickEvent));
        Settle();
    }

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    [AvaloniaFact]
    public void Starts_With_The_Seeded_Product_And_Its_Totals()
    {
        var block = Create();
        var window = Show(block);
        try
        {
            block.Lines.Count.ShouldBe(1);
            block.Lines[0].Product.Name.ShouldBe("Fluted water jug");
            block.ItemCount.ShouldBe(1);
            block.DisplayedCount.ShouldBe(1);
            block.Subtotal.ShouldBe(58m);
            block.Shipping.ShouldBe(9m);
            block.Total.ShouldBe(67m);
            block.State.ShouldBe(CartDrawerState.Shopping);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Adding_Appends_A_Row_Or_Raises_The_Quantity_And_Announces()
    {
        var block = Create();
        var window = Show(block);
        var added = new List<CartDrawerItemEventArgs>();
        block.ItemAdded += (_, e) => added.Add(e);
        try
        {
            block.Add(P(block, 3)).ShouldBeTrue();
            block.Lines.Select(l => l.Product.Name).ShouldBe(["Fluted water jug", "Speckled mugs"]);
            block.LastAnnouncement.ShouldBe("Speckled mugs added to cart");
            block.Add(P(block, 3)).ShouldBeTrue();
            block.Lines.Count.ShouldBe(2);
            block.Lines[1].Quantity.ShouldBe(2);
            block.ItemCount.ShouldBe(3);
            block.Subtotal.ShouldBe(58m + 84m);
            added.Count.ShouldBe(2);
            added[1].OldQuantity.ShouldBe(1);
            added[1].NewQuantity.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_The_Row_Cap_Adding_Does_Nothing_And_The_Grid_Button_Rests()
    {
        var block = Create();
        var window = Show(block);
        var added = 0;
        block.ItemAdded += (_, _) => added++;
        try
        {
            block.SetQuantity(P(block, 2), 12);
            block.Lines[0].Quantity.ShouldBe(9);
            var before = block.LastAnnouncement;
            block.Add(P(block, 2)).ShouldBeFalse();
            added.ShouldBe(0);
            block.LastAnnouncement.ShouldBe(before);
            block.ItemCount.ShouldBe(9);
            var gridButtons = block.GetVisualDescendants().OfType<Button>().Where(b => b.Variant == ButtonVariant.Secondary).ToList();
            gridButtons[2].IsEnabled.ShouldBeFalse();
            gridButtons[1].IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Free_Shipping_Follows_The_Subtotal_Before_The_Discount()
    {
        var a = new CartDrawerProduct { Name = "A", Price = 120m, InitialQuantity = 2 };
        var block = new CartDrawer { FreeShippingThreshold = 240m, ShippingFee = 9m, Products = { a } };
        var window = Show(block);
        try
        {
            block.Subtotal.ShouldBe(240m);
            block.Shipping.ShouldBe(0m);
            block.ApplyPromoAsync("WORKSHOP10").Result.ShouldBeTrue();
            Settle();
            block.Discount.ShouldBe(24m);
            block.Shipping.ShouldBe(0m);
            block.Total.ShouldBe(216m);
            block.SetQuantity(a, 1);
            block.Shipping.ShouldBe(9m);
            block.Total.ShouldBe(120m - 12m + 9m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Valid_Code_Becomes_A_Chip_And_Removing_It_Returns_The_Field()
    {
        var block = Create();
        var window = Show(block);
        var applied = 0;
        var removed = 0;
        block.PromoApplied += (_, _) => applied++;
        block.PromoRemoved += (_, _) => removed++;
        try
        {
            block.Open();
            Settle();
            block.PromoInputPart!.Text = "workshop10";
            Click(block.PromoApplyPart!);
            block.AppliedPromo!.Code.ShouldBe("WORKSHOP10");
            block.Discount.ShouldBe(6m);
            block.Total.ShouldBe(61m);
            block.IsDiscountRowShown.ShouldBeTrue();
            block.LastAnnouncement.ShouldBe("Code WORKSHOP10 applied");
            applied.ShouldBe(1);
            block.PromoRemovePart.ShouldNotBeNull();
            Click(block.PromoRemovePart!);
            block.AppliedPromo.ShouldBeNull();
            block.Total.ShouldBe(67m);
            block.IsDiscountRowShown.ShouldBeFalse();
            block.PromoInputPart!.Text.ShouldBe(string.Empty);
            removed.ShouldBe(1);
            Focused(window).ShouldBe(block.PromoInputPart);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Invalid_Code_Is_Refused_Until_The_Text_Changes()
    {
        var block = Create();
        var window = Show(block);
        string? rejected = null;
        block.PromoRejected += (_, e) => rejected = e.Code;
        try
        {
            block.Open();
            Settle();
            block.PromoInputPart!.Text = "spring";
            Settle();
            block.PromoInputPart.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            rejected.ShouldBe("spring");
            block.AppliedPromo.ShouldBeNull();
            block.IsPromoInvalid.ShouldBeTrue();
            block.PromoInputPart.IsInvalid.ShouldBeTrue();
            block.PromoInputPart.Text = "springs";
            Settle();
            block.IsPromoInvalid.ShouldBeFalse();
            // An empty code is ignored.
            block.ApplyPromoAsync("   ").Result.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Steppers_Change_The_Quantity_And_The_Bin_Removes_The_Last_One()
    {
        var block = Create();
        var window = Show(block);
        var removed = new List<CartDrawerItemEventArgs>();
        block.ItemRemoved += (_, e) => removed.Add(e);
        try
        {
            block.Open();
            Settle();
            var row = block.RowFor(P(block, 2))!;
            Click(row.IncreaseButton);
            block.Lines[0].Quantity.ShouldBe(2);
            Click(row.DecreaseButton);
            block.Lines[0].Quantity.ShouldBe(1);
            Click(row.DecreaseButton);
            block.Lines.Count.ShouldBe(0);
            removed.Single().OldQuantity.ShouldBe(1);
            block.LastAnnouncement.ShouldBe("Fluted water jug removed from cart");
            block.BodyView.ShouldBe(block.EmptyViewPart);
            block.CheckoutButtonPart!.IsEnabled.ShouldBeFalse();
            block.IsSummaryShown.ShouldBeFalse();
            block.Total.ShouldBe(0m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_A_Focused_Row_Moves_Focus_To_The_Next_Row()
    {
        var block = Create();
        var window = Show(block);
        try
        {
            block.Add(P(block, 3));
            block.Open();
            Settle();
            var first = block.RowFor(P(block, 2))!;
            first.RemoveButton.Focus(NavigationMethod.Tab);
            Click(first.RemoveButton);
            Settle();
            Focused(window).ShouldBe(block.RowFor(P(block, 3))!.RemoveButton);
            Click(block.RowFor(P(block, 3))!.RemoveButton);
            Settle();
            Focused(window).ShouldBe(block.BrowseButtonPart);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_Focuses_Close_Traps_Tab_And_Escape_Returns_Focus()
    {
        var block = Create();
        var window = Show(block);
        try
        {
            var cart = block.CartButtonPartControl!;
            cart.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            block.IsOpen.ShouldBeTrue();
            block.DrawerPart!.IsVisible.ShouldBeTrue();
            Focused(window).ShouldBe(block.CloseButtonPart);
            KeyboardNavigation.GetTabNavigation(block.DrawerPart).ShouldBe(KeyboardNavigationMode.Cycle);
            // Tab never leaves the drawer.
            for (var i = 0; i < 12; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Settle();
                (Focused(window) as Visual)!.GetVisualAncestors().ShouldContain(block.DrawerPart);
            }
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            block.IsOpen.ShouldBeFalse();
            block.DrawerPart.IsVisible.ShouldBeFalse();
            Focused(window).ShouldBe(cart);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Checkout_Holds_The_Drawer_Then_Shows_The_Receipt_And_Closing_Starts_Over()
    {
        var pending = new TaskCompletionSource<CartDrawerReceipt>();
        var block = Create((_, _) => pending.Task);
        var window = Show(block);
        CartDrawerOrderPlacedEventArgs? placed = null;
        block.OrderPlaced += (_, e) => placed = e;
        try
        {
            block.Open();
            Settle();
            Click(block.CheckoutButtonPart!);
            block.State.ShouldBe(CartDrawerState.Placing);
            block.CheckoutButtonPart!.IsBusy.ShouldBeTrue();
            // Cannot close while placing: neither Escape nor the property.
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            block.Close();
            Settle();
            block.IsOpen.ShouldBeTrue();
            pending.SetResult(Receipt);
            Settle();
            block.State.ShouldBe(CartDrawerState.Placed);
            block.Receipt.ShouldBe(Receipt);
            placed!.Order.Total.ShouldBe(67m);
            placed.Order.ItemCount.ShouldBe(1);
            block.BodyView.ShouldBe(block.ReceiptViewPart);
            block.DiscPart.ShouldNotBeNull();
            Focused(window).ShouldBe(block.ContinueButtonPart);
            block.LastAnnouncement.ShouldBe("Order placed. 1 item, $67.");
            // Closing the receipt any way starts a new cart (Arc keeps the old one).
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            block.IsOpen.ShouldBeFalse();
            block.ItemCount.ShouldBe(0);
            block.DisplayedCount.ShouldBe(0);
            block.State.ShouldBe(CartDrawerState.Shopping);
            block.Receipt.ShouldBeNull();
            block.Open();
            Settle();
            block.BodyView.ShouldBe(block.EmptyViewPart);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Continue_Shopping_Closes_And_Empties_The_Cart()
    {
        var block = Create();
        var window = Show(block);
        try
        {
            block.Open();
            Settle();
            block.CheckoutAsync().Wait();
            Settle();
            block.State.ShouldBe(CartDrawerState.Placed);
            Click(block.ContinueButtonPart!);
            block.IsOpen.ShouldBeFalse();
            block.Lines.ShouldBeEmpty();
            block.Add(P(block, 0)).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Order_Returns_To_The_Cart()
    {
        var block = Create((_, _) => Task.FromException<CartDrawerReceipt>(new InvalidOperationException("declined")));
        var window = Show(block);
        try
        {
            block.Open();
            Settle();
            block.CheckoutAsync().Wait();
            Settle();
            block.State.ShouldBe(CartDrawerState.Shopping);
            block.CheckoutButtonPart!.IsBusy.ShouldBeFalse();
            block.Lines.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Product_Flies_To_The_Cart_And_The_Badge_Counts_It_On_Landing()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Create();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            Frames(clock, 100);
            block.Add(P(block, 1)).ShouldBeTrue();
            block.ActiveFlights.ShouldBe(1);
            block.ItemCount.ShouldBe(2);
            block.DisplayedCount.ShouldBe(1);
            Frames(clock, 400);
            block.DisplayedCount.ShouldBe(1);
            Frames(clock, 500);
            block.ActiveFlights.ShouldBe(0);
            block.DisplayedCount.ShouldBe(2);
            Avalonia.Automation.AutomationProperties.GetName(block.CartButtonPartControl!).ShouldBe("Cart, 2 items");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Counts_At_Once_Without_A_Flight()
    {
        var block = Create();
        var window = Show(block, ArcMotionLevel.Reduced);
        try
        {
            block.Add(P(block, 1));
            block.ActiveFlights.ShouldBe(0);
            block.DisplayedCount.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Drawer_Slides_In_On_A_Spring_And_Out_Again()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Create();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            Frames(clock, 50);
            block.Open();
            Frames(clock, 32);
            block.DrawerOffset.ShouldBeGreaterThan(50);
            Frames(clock, 900);
            block.DrawerOffset.ShouldBe(0, 0.5);
            block.ScrimOpacity.ShouldBe(1, 0.01);
            block.Close();
            Frames(clock, 48);
            block.DrawerOffset.ShouldBeGreaterThan(5);
            Frames(clock, 900);
            block.DrawerPart!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Swiping_A_Row_Past_110px_Removes_It_And_A_Short_Swipe_Springs_Back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Create();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            block.Add(P(block, 5));
            Frames(clock, 900);
            block.Open();
            Frames(clock, 900);
            var row = block.RowFor(P(block, 5))!;

            void Drag(double distance)
            {
                var start = row.TranslatePoint(new Point(row.Bounds.Width / 2, 20), window)!.Value;
                window.MouseMove(start, RawInputModifiers.None);
                window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
                for (var dx = 0.0; dx <= distance; dx += 10)
                {
                    // Pointer velocity comes from the events' real timestamps: move slowly so a short swipe is no flick.
                    Thread.Sleep(30);
                    clock.Advance(TimeSpan.FromMilliseconds(40));
                    window.MouseMove(start - new Point(dx, 0), RawInputModifiers.LeftMouseButton);
                    Dispatcher.UIThread.RunJobs();
                }
                window.MouseUp(start - new Point(distance, 0), MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
            }

            Drag(60);
            Frames(clock, 700);
            row.SwipeOffset.ShouldBe(0, 0.5);
            block.Lines.Count.ShouldBe(2);

            Drag(150);
            block.Lines.Count.ShouldBe(1);
            row.IsLeaving.ShouldBeTrue();
            Frames(clock, 1500);
            row.Opacity.ShouldBe(0, 0.01);
            row.HeightFactor.ShouldBe(0, 0.01);
            row.SwipeOffset.ShouldBeLessThan(-300);
            // The removal continues after Task.WhenAll, whose continuation can run on the thread pool and post back to the
            // UI thread after the last RunJobs above when the machine is busy: pump until it lands.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (block.RowParts.Contains(row) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(5);
                Dispatcher.UIThread.RunJobs();
            }
            block.RowParts.ShouldNotContain(row);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Block_Width()
    {
        var block = Create();
        var window = Show(block, width: 600);
        try
        {
            block.Classes.Contains(CartDrawer.CompactPseudoClass).ShouldBeTrue();
            block.Classes.Contains(CartDrawer.NarrowPseudoClass).ShouldBeFalse();
            TestHelpers.Descendant<CartDrawerGridPanel>(block).Columns.ShouldBe(2);
            window.Width = 360;
            Settle();
            block.Classes.Contains(CartDrawer.NarrowPseudoClass).ShouldBeTrue();
            block.Open();
            Settle();
            block.DrawerPart!.Bounds.Width.ShouldBe(block.Bounds.Width - 8, 1);
        }
        finally
        {
            window.Close();
        }
    }
}
