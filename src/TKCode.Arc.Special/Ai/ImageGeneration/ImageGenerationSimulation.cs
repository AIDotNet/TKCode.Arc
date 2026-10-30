using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// A generator and an upscaler an <see cref="ImageGeneration"/> plays while the app wires no
/// <see cref="ImageGeneration.Generator"/>, fed by the caller: the prompt it starts with, the models and suggestions on
/// offer, and a <see cref="Painter"/> that paints a picture for a prompt (the same prompt and seed always paint the same
/// picture). Each tile reports progress frame by frame over two to three seconds and resolves from the painted preview;
/// Upscale paints it again at twice the detail over about two seconds. Nothing is downloaded or sent anywhere. Set the
/// data before handing the simulation to the card; to offer other pictures, give the card a new simulation.
/// </summary>
public sealed class ImageGenerationSimulation
{
    /// <summary>The long edge of a painted picture in pixels (the image reports the aspect's own size).</summary>
    internal const int SampleEdge = 320;

    private readonly ConditionalWeakTable<ImageGenerationImage, SampleInfo> _samples = new();

    /// <summary>The prompt the card starts with while its own <see cref="ImageGeneration.Prompt"/> is null.</summary>
    public string? Prompt { get; set; }

    /// <summary>The models on offer (the card's own <see cref="ImageGeneration.Models"/> win).</summary>
    public IReadOnlyList<ImageGenerationModel>? Models { get; set; }

    /// <summary>The suggestions under the prompt (the card's own <see cref="ImageGeneration.Suggestions"/> win).</summary>
    public IReadOnlyList<string>? Suggestions { get; set; }

    /// <summary>Paints the pictures; null makes images without a picture.</summary>
    public ImageGenerationPainter? Painter { get; set; }

    /// <summary>Makes one image for a request, reporting progress as it goes; cancel through the context.</summary>
    public async Task<ImageGenerationImage> Generate(ImageGenerationRequest request, ImageGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var token = context.CancellationToken;
        var seed = Seed(request);
        var prompt = request.Prompt;
        var painter = Painter;
        var (width, height) = PaintSize(request.Aspect.Width, request.Aspect.Height, SampleEdge);
        var paint = Task.Run(() => painter?.Invoke(prompt, seed, width, height), token);
        var duration = 2.0 + seed % 900 / 1000.0;
        var start = Stopwatch.GetTimestamp();
        Bitmap? preview = null;
        context.ReportProgress(0);
        while (true)
        {
            await Task.Delay(40, token);
            if (preview is null && paint.IsCompleted)
            {
                preview = await paint is { } first ? CreateBitmap(first, width, height) : null;
            }
            var t = Stopwatch.GetElapsedTime(start).TotalSeconds / duration;
            if (t >= 1)
            {
                break;
            }
            context.ReportProgress(Curve(t, seed), preview);
        }
        preview ??= await paint is { } pixels ? CreateBitmap(pixels, width, height) : null;
        context.ReportProgress(1, preview);
        // A short beat on the finished mosaic before the image takes over.
        await Task.Delay(140, token);
        var image = new ImageGenerationImage
        {
            Id = "simulated-" + seed.ToString("x8"),
            Source = preview,
            Description = request.Prompt,
            Width = request.Aspect.Width,
            Height = request.Aspect.Height,
        };
        if (painter is not null)
        {
            _samples.AddOrUpdate(image, new SampleInfo(painter, prompt, seed));
        }
        return image;
    }

    /// <summary>Upscales an image two times (a painted one is painted again at twice the detail) over about two seconds.</summary>
    public async Task<ImageGenerationImage> Upscale(ImageGenerationImage image, ImageGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(context);
        var token = context.CancellationToken;
        Task<byte[]?>? paint = null;
        var (width, height) = (0, 0);
        if (_samples.TryGetValue(image, out var info) && image.Source is { } source)
        {
            (width, height) = PaintSize(source.PixelSize.Width, source.PixelSize.Height, SampleEdge * 2);
            paint = Task.Run(() => (byte[]?)info.Painter(info.Prompt, info.Seed, width, height), token);
        }
        const double duration = 1.9;
        var start = Stopwatch.GetTimestamp();
        context.ReportProgress(0);
        while (true)
        {
            await Task.Delay(40, token);
            var t = Stopwatch.GetElapsedTime(start).TotalSeconds / duration;
            if (t >= 1)
            {
                break;
            }
            context.ReportProgress(1 - Math.Pow(1 - t, 1.4));
        }
        var upscaled = paint is null ? image.Source : (await paint is { } pixels ? CreateBitmap(pixels, width, height) : null) ?? image.Source;
        context.ReportProgress(1);
        var result = new ImageGenerationImage
        {
            Id = image.Id + "-2x",
            Source = upscaled,
            Description = image.Description,
            Width = image.Width * 2,
            Height = image.Height * 2,
        };
        if (info is not null)
        {
            _samples.AddOrUpdate(result, info);
        }
        return result;
    }

    /// <summary>The seed of a request: its prompt, model, tile and the image it varies.</summary>
    internal static uint Seed(ImageGenerationRequest request)
    {
        unchecked
        {
            var seed = Hash(request.Prompt) * 31u + Hash(request.Model.Id);
            seed = seed * 31u + (uint)request.Index * 2654435761u;
            if (request.VariationOf is { } source)
            {
                seed ^= Hash(source.Id) * 40503u + 1u;
            }
            return seed;
        }
    }

    /// <summary>The paint size for a shape: the long edge is <paramref name="edge"/> pixels.</summary>
    internal static (int Width, int Height) PaintSize(int width, int height, int edge)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        return width >= height
            ? (edge, Math.Max(1, (int)Math.Round(edge * (double)height / width)))
            : (Math.Max(1, (int)Math.Round(edge * (double)width / height)), edge);
    }

    /// <summary>Progress over time: a slow start, a steady middle and one short hesitation, like a real model.</summary>
    internal static double Curve(double t, uint seed)
    {
        t = Math.Clamp(t, 0, 1);
        var eased = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
        var pause = 0.45 + seed % 200 / 1000.0;
        var hesitation = 0.04 * Math.Exp(-Math.Pow((t - pause) / 0.06, 2));
        return Math.Clamp(eased * 0.97 - hesitation, 0, 0.99);
    }

    /// <summary>Turns painted pixels into a bitmap and remembers them for the mosaic; null where bitmaps are unavailable.</summary>
    internal static Bitmap? CreateBitmap(byte[] bgra, int width, int height)
    {
        try
        {
            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using (var locked = bitmap.Lock())
            {
                var stride = width * 4;
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(bgra, y * stride, locked.Address + y * locked.RowBytes, stride);
                }
            }
            ImageGenerationMosaic.Register(bitmap, bgra, width, height);
            return bitmap;
        }
        catch (Exception)
        {
            // No bitmaps on this platform: the image has no picture, and the tile keeps its placeholder.
            return null;
        }
    }

    private sealed record SampleInfo(ImageGenerationPainter Painter, string Prompt, uint Seed);

    /// <summary>A stable hash of a string (FNV-1a), so the same prompt and model always seed the same pictures.</summary>
    internal static uint Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash = (hash ^ c) * 16777619u;
        }
        return hash;
    }
}

/// <summary>
/// Paints a picture for an <see cref="ImageGenerationSimulation"/>: <paramref name="width"/> × <paramref name="height"/>
/// opaque BGRA bytes, row by row (width × height × 4). The same prompt and seed should paint the same picture. Called off
/// the UI thread.
/// </summary>
/// <param name="prompt">The prompt the picture is for.</param>
/// <param name="seed">A seed from the prompt, the model, the tile and the image it varies.</param>
/// <param name="width">The picture's width in pixels.</param>
/// <param name="height">The picture's height in pixels.</param>
public delegate byte[] ImageGenerationPainter(string prompt, uint seed, int width, int height);
