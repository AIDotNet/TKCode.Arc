using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class VectorSearchYourDocumentsDemo : UserControl
{
    public VectorSearchYourDocumentsDemo()
    {
        InitializeComponent();
        Recipes.Documents =
        [
            new VectorSearchDocument("Overnight oats with berries", "Breakfast", "oats morning quick"),
            new VectorSearchDocument("Fluffy buttermilk pancakes", "Breakfast", "pancakes morning griddle"),
            new VectorSearchDocument("Shakshuka with feta", "Breakfast", "eggs tomato morning"),
            new VectorSearchDocument("Avocado toast three ways", "Breakfast", "toast avocado morning quick"),
            new VectorSearchDocument("Weeknight chicken curry", "Dinner", "chicken curry rice spice"),
            new VectorSearchDocument("One-pan lemon salmon", "Dinner", "fish salmon oven quick"),
            new VectorSearchDocument("Mushroom risotto", "Dinner", "rice mushroom parmesan"),
            new VectorSearchDocument("Slow-cooker beef chili", "Dinner", "beef beans spice"),
            new VectorSearchDocument("Dark chocolate mousse", "Dessert", "chocolate cream sweet"),
            new VectorSearchDocument("Lemon tart", "Dessert", "lemon pastry sweet"),
            new VectorSearchDocument("Banana bread", "Dessert", "banana baking sweet"),
            new VectorSearchDocument("No-churn vanilla ice cream", "Dessert", "vanilla cream frozen sweet")
        ];
    }
}
