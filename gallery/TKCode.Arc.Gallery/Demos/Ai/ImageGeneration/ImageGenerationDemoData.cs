namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// What the image generation demos offer: a golden hour prompt, three suggestions, three image models and a painter that
/// paints pictures in code to match the prompt (a lake at golden hour, a city street after rain, a cup on a sunlit sill or
/// a pastel room). The model catalog the other AI demos use lists no image generation models, so these models keep
/// invented names with neutral marks. Demo content only.
/// </summary>
internal static class ImageGenerationDemoData
{
    /// <summary>The prompt the card starts with.</summary>
    public const string Prompt = "A quiet mountain lake at golden hour, mist over the water, pines on the far shore, soft film grain";

    /// <summary>The image models on offer (invented names, neutral Lucide marks).</summary>
    public static IReadOnlyList<ImageGenerationModel> Models { get; } =
    [
        new() { Id = "atlas-vision", Name = "Atlas Vision", Mark = ArcIcons.Sparkles },
        new() { Id = "atlas-3-pro", Name = "Atlas 3 Pro", Mark = ArcIcons.Palette },
        new() { Id = "atlas-mini", Name = "Atlas Mini", Mark = ArcIcons.Zap },
    ];

    /// <summary>The suggestions in the empty grid.</summary>
    public static IReadOnlyList<string> Suggestions { get; } =
    [
        "A ceramic coffee cup on a sunlit windowsill, soft morning shadows",
        "A neon city street after rain at night, reflections on the asphalt",
        "A cosy pastel reading nook with an arched window and a plant",
    ];

    /// <summary>Paints the picture for a prompt: the scene its words name, varied by the seed.</summary>
    public static ImageGenerationPainter Painter { get; } =
        (prompt, seed, width, height) => ImageGenerationDemoScenes.Paint(ImageGenerationDemoScenes.Pick(prompt), seed, width, height);

    /// <summary>A new simulation (code uses it; the XAML demos spell it out).</summary>
    public static ImageGenerationSimulation Simulation() => new()
    {
        Prompt = Prompt,
        Models = Models,
        Suggestions = Suggestions,
        Painter = Painter,
    };
}
