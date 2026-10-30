using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Demos;

public partial class DesignToCodeYourModelDemo : UserControl
{
    // What a backend might send for the sample pricing card: the regions first, then code tagged with its region.
    private static readonly DesignToCodeRegion[] s_regions =
    [
        new("title", "Title", 7.5, 6, 85, 21.5),
        new("plans", "Plan switch", 7.5, 30.5, 85, 9.5),
        new("amount", "Amount", 7.5, 42.5, 52, 14.5),
        new("perks", "Perks", 7.5, 59, 85, 22),
        new("action", "Action", 7.5, 83, 85, 12),
    ];

    private static readonly (string Code, string? Region)[] s_code =
    [
        ("export function ProCard({ yearly, onChange }: Props) {\n  return (\n    <Card>\n", null),
        ("      <CardTitle badge=\"Pro\">Pro plan</CardTitle>\n      <CardDescription>For growing teams</CardDescription>\n", "title"),
        ("      <Segmented value={yearly ? \"yearly\" : \"monthly\"} onChange={onChange}\n        options={[\"Monthly\", \"Yearly\"]} />\n", "plans"),
        ("      <Price amount={yearly ? 19 : 24} period=\"month\" />\n", "amount"),
        ("      <Checklist items={[\"Unlimited projects\", \"Shared workspaces\", \"Priority support\"]} />\n", "perks"),
        ("      <Button block>Start free trial</Button>\n", "action"),
        ("    </Card>\n  );\n}\n", null),
    ];

    public DesignToCodeYourModelDemo()
    {
        InitializeComponent();
        Card.Model = new AgentModel(AiModelCatalog.Gemini38Flash.Name, ArcIcons.Eye);
        Card.Generator = GenerateAsync;
        // Your renderer goes here, ideally a sandboxed web view. Never run generated code inside your app.
        Card.PreviewFactory = (code, _) => new TextBlock
        {
            Text = $"Your sandbox renders {Card.FileName} here ({code.Split('\n').Length - 1} lines).",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 40, 0, 0),
        };
    }

    private static async Task GenerateAsync(DesignToCodeSource source, DesignToCodeContext context)
    {
        var token = context.CancellationToken;
        foreach (var region in s_regions)
        {
            await Task.Delay(140, token);
            context.AddRegion(region);
        }
        var random = new Random(7);
        foreach (var (code, region) in s_code)
        {
            // A model streams uneven pieces; the card paces them into whole words.
            for (var i = 0; i < code.Length;)
            {
                var length = Math.Min(code.Length - i, random.Next(6, 24));
                await Task.Delay(random.Next(20, 60), token);
                context.Write(code.Substring(i, length), region);
                i += length;
            }
        }
    }

    private void OnCompleted(object? sender, DesignToCodeCompletedEventArgs e)
    {
        Log.Text = $"Completed: {e.LineCount} lines from {e.Design.Name} in {e.Elapsed.TotalSeconds:0.0}s.";
    }
}
