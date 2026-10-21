using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CodeBlockLineNumbersDemo : UserControl
{
    public CodeBlockLineNumbersDemo()
    {
        InitializeComponent();
        CartView.Code = """
            using Avalonia.Controls;

            namespace Shop;

            public sealed class CartView : UserControl
            {
                // Totals update when an item changes.
                public decimal Total { get; private set; }

                public void Add(decimal price, int quantity = 1)
                {
                    Total += price * quantity;
                    Status.Text = $"Total: {Total:C}";
                }
            }
            """;
        Usage.Code = """
            <arc:CodeBlock FileName="greeting.tsx"
                           Language="tsx"
                           IsCollapsible="True"
                           MaxLines="6"
                           Code="{Binding Source}" />
            """;
    }
}
