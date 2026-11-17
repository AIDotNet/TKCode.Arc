using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Finance;

/// <summary><see cref="InvoiceStudio"/>: totals, editing both ways, the preview, validation, sending and the narrow layout.</summary>
public class InvoiceStudioTests
{
    private static (InvoiceStudio Studio, Window Window) Show(double width = 1100)
    {
        var studio = new InvoiceStudio { IssueDate = new DateTime(2026, 11, 9) };
        InvoiceStudioSamples.Apply(studio);
        var window = TestHelpers.Show(new Border { Width = width, Child = studio });
        window.Width = 1200;
        Pump();
        return (studio, window);
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
    public void Discount_Comes_Off_Before_Tax_And_Tax_Is_Per_Line()
    {
        InvoiceLineItem[] lines = [new("A", 1, 1800m, 0.08m), new("B", 6, 95m, 0.08m), new("C", 2, 42.5m, 0.2m)];
        var totals = InvoiceStudioMath.Compute(lines, InvoiceDiscountKind.Percent, 10);
        totals.Subtotal.ShouldBe(2455m);
        totals.Discount.ShouldBe(245.5m);
        // Lines keep 90%: (1800 + 570) × 0.9 × 8% = 170.64, 85 × 0.9 × 20% = 15.30.
        totals.Tax.ShouldBe(185.94m);
        totals.Total.ShouldBe(2395.44m);
        InvoiceStudioMath.Compute(lines, InvoiceDiscountKind.Amount, 5000).Discount.ShouldBe(2455m);
        InvoiceStudioMath.Compute([], InvoiceDiscountKind.Percent, 10).Total.ShouldBe(0m);
        InvoiceStudioMath.IsBillable(new InvoiceLineItem("", 1, 10)).ShouldBeFalse();
        InvoiceStudioMath.IsBillable(new InvoiceLineItem("Paper", 0, 10)).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Editing_A_Line_Updates_The_Totals_And_The_Preview()
    {
        var (studio, window) = Show();
        try
        {
            studio.Totals.Total.ShouldBe(2395.44m);
            var row = studio.GetVisualDescendants().OfType<InvoiceStudioLineRow>().First();
            ((NumberField)row.Quantity).Value = 2;
            studio.Lines[0].Quantity.ShouldBe(2);
            studio.Totals.Subtotal.ShouldBe(4255m);
            ((TextBlock)row.AmountText).Text.ShouldBe("$3,600.00");

            studio.Lines[1].Description = "Logo refinements, round two";
            ((Input)studio.GetVisualDescendants().OfType<InvoiceStudioLineRow>().ElementAt(1).Description).Text.ShouldBe("Logo refinements, round two");

            studio.BuildPreview();
            var ink = studio.Paper!.GetLogicalDescendantsText();
            ink.ShouldContain("Harbor & Pine Studio");
            ink.ShouldContain("Logo refinements, round two");
            ink.ShouldContain("Dec 9, 2026");
            ink.ShouldContain(studio.Money(studio.Totals.Total));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Send_Checks_The_Invoice_Then_Calls_The_Handler()
    {
        var (studio, window) = Show();
        try
        {
            studio.SelectedClient = null;
            (await studio.SendInvoiceAsync()).ShouldBeFalse();
            studio.ErrorText.ShouldBe("Choose who the invoice is for.");

            studio.SelectedClient = InvoiceStudioSamples.CreateClients()[1];
            studio.ErrorText.ShouldBeNull();
            studio.Lines.Add(new InvoiceLineItem());
            InvoiceDraft? sent = null;
            studio.InvoiceSent += (_, e) => sent = e.Invoice;
            var gate = new TaskCompletionSource<bool>();
            studio.SendAsync = (_, _) => gate.Task;
            var pending = studio.SendInvoiceAsync();
            studio.State.ShouldBe(InvoiceStudioState.Sending);
            gate.SetResult(true);
            (await pending).ShouldBeTrue();
            studio.State.ShouldBe(InvoiceStudioState.Sent);
            sent!.Lines.Count.ShouldBe(3);
            sent.Client.Name.ShouldBe("Lumen Field Bakery");
            sent.DueDate.ShouldBe(new DateTime(2026, 12, 9));
            studio.LastAnnouncement.ShouldBe("Sent to hello@lumenfield.example");

            studio.Notes = "Changed";
            studio.State.ShouldBe(InvoiceStudioState.Draft);

            studio.SendAsync = (_, _) => throw new InvalidOperationException();
            (await studio.SendInvoiceAsync()).ShouldBeFalse();
            studio.ErrorText.ShouldBe("The invoice could not be sent. Try again.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_A_Line_And_Adding_One()
    {
        var (studio, window) = Show();
        try
        {
            var line = studio.AddLine();
            line.TaxRate.ShouldBe(0.2m);
            studio.GetVisualDescendants().OfType<InvoiceStudioLineRow>().Count().ShouldBe(4);
            var remove = (Button)studio.GetVisualDescendants().OfType<InvoiceStudioLineRow>().First().Remove;
            remove.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
            studio.Lines.Count.ShouldBe(3);
            studio.Lines[0].Description.ShouldBe("Logo refinements");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Block_Switches_Between_Form_And_Preview()
    {
        var (studio, window) = Show(width: 500);
        try
        {
            studio.IsCompact.ShouldBeTrue();
            studio.Classes.Contains(":narrow").ShouldBeTrue();
            var form = TestHelpers.Descendant<StackPanel>(studio, "PART_Form");
            form.IsVisible.ShouldBeTrue();
            studio.Paper!.IsEffectivelyVisible.ShouldBeFalse();
            studio.IsPreviewShown = true;
            Pump();
            form.IsVisible.ShouldBeFalse();
            studio.Paper.IsEffectivelyVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}

internal static class InvoiceStudioTestText
{
    /// <summary>All text of the sheet, joined by new lines.</summary>
    public static string GetLogicalDescendantsText(this Control root)
        => string.Join("\n", root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
}
