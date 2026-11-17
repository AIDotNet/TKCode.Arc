using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using ArcButton = TKCode.Arc.Button;

internal static class Program
{
    private static readonly List<JsonObject> Checks = new();
    private static readonly List<JsonObject> Frames = new();
    private static readonly AuditLog Log = new();

    [STAThread]
    public static int Main(string[] args)
    {
        var output = args[0];
        Directory.CreateDirectory(output);
        Window? window = null;
        string? error = null;
        try
        {
            Logger.Sink = Log;
            AppBuilder.Configure<Application>().UsePlatformDetect()
                .UseArc(arc => arc.UseArcControls()
                    .WithTheme(theme => theme.Appearance(ArcAppearance.Light).MotionLevel(ArcMotionLevel.Off)))
                .SetupWithoutStarting();
            var button = new ArcButton
            {
                Content = "Save", HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var panel = new Border { Width = 420, Height = 160, Child = button };
            ArcTokens.Bind(panel, Border.BackgroundProperty, ArcDesignTokenKind.Background);
            window = new Window
            {
                Width = 420, Height = 160, Content = panel, CanResize = false,
                WindowDecorations = WindowDecorations.None, ShowInTaskbar = false,
                ShowActivated = false, Position = new PixelPoint(-10000, -10000)
            };
            window.Show();
            Flush(window);
            Check("attached to visual root", TopLevel.GetTopLevel(button) == window);
            Check("template instantiated", button.Template is not null && Part<Border>(button, "PART_Frame") is not null);
            // Check every published Button/global resource, not just the dictionary factory.
            foreach (var descriptor in new[] { ButtonTokens.Descriptor, ArcDesignTokens.Descriptor })
            {
                for (var index = 0; index < descriptor.Keys.Count; index++)
                {
                    var identity = descriptor.Identity + "." + descriptor.TokenNames[index];
                    Check("resource " + identity,
                        button.TryFindResource(descriptor.Keys[index], button.ActualThemeVariant, out var value) && value is not null);
                }
            }
            Check("default padding", button.Padding == new Thickness(16, 0));
            Check("default height", button.MinHeight == 44 && button.Bounds.Height >= 44);
            Check("foreground resolved", button.Foreground is ISolidColorBrush);
            Check("font token resolved", button.FontFamily == ArcTokens.Get<FontFamily>(button, ArcDesignTokenKind.BodyFontFamily));
            Check("template content", Part<ArcContentSwap>(button, "PART_Label").Content?.ToString() == "Save");
            Check("spring part", Part<SpringSizeDecorator>(button, "PART_LabelSlot").Bounds.Width > 0);
            Check("spinner lazy", !button.GetVisualDescendants().OfType<ArcSpinner>().Any());
            Frame("light-primary", panel, output);

            button.Size = ControlSize.Small;
            Flush(window);
            Check("small geometry", button.MinHeight == 36 && button.Padding == new Thickness(12, 0));
            Frame("small", panel, output);
            button.Size = ControlSize.Large;
            Flush(window);
            Check("large geometry", button.MinHeight == 50 && button.Padding == new Thickness(20, 0));
            Frame("large", panel, output);
            button.Size = ControlSize.Medium;
            button.IsLoading = true;
            Flush(window);
            var spinner = Part<ArcSpinner>(button, "PART_Spinner");
            Check("spinner visible and token sized", spinner.IsVisible && spinner.Width == 16 && spinner.Height == 16);
            Check("spinner centered", Math.Abs(spinner.Bounds.Center.X - button.Bounds.Width / 2) < 0.5
                && Math.Abs(spinner.Bounds.Center.Y - button.Bounds.Height / 2) < 0.5);
            Check("spinner inherited foreground", spinner.Foreground is ISolidColorBrush);
            Frame("loading", panel, output);
            button.IsLoading = false;
            Check("spinner hidden", !spinner.IsVisible);
            Check("keyboard focus accepted", button.Focus(NavigationMethod.Tab));
            Flush(window);
            var ring = Part<Border>(button, "PART_FocusRing");
            Check("keyboard ring visible", ring.IsVisible && button.IsFocused);
            Check("ring resources resolved", ring.CornerRadius == new CornerRadius(22) && ring.Margin == new Thickness(-4)
                && ring.BorderBrush is ISolidColorBrush && ring.BorderThickness.Left > 0);
            Frame("keyboard-focus", panel, output);

            var lightBackground = ArcTokens.GetColor(button, ArcDesignTokenKind.Background);
            ArcTheme.Apply(theme => theme.Appearance(ArcAppearance.Dark));
            Flush(window);
            Check("dark resources updated", ArcTokens.GetColor(button, ArcDesignTokenKind.Background) != lightBackground);
            Check("template brush follows dark", Part<Border>(button, "PART_Frame").Background is ISolidColorBrush frameBrush
                && frameBrush.Color == ArcTokens.GetColor(button, ArcDesignTokenKind.Foreground));
            Frame("dark-focus", panel, output);
            var ringColor = ((ISolidColorBrush)ring.BorderBrush!).Color;
            ArcTheme.Apply(theme => theme.Accent(Color.Parse("#7747FF"))
                .Token("Button", "SpinnerSize", "24").Token("Button", "InlinePaddingMD", "22,0"));
            button.IsLoading = true;
            Flush(window);
            Check("spinner token override", spinner.Width == 24 && spinner.Height == 24);
            Check("padding token override", button.Padding == new Thickness(22, 0));
            Check("accent reaches focus ring", ((ISolidColorBrush)ring.BorderBrush!).Color != ringColor);
            Frame("overrides-loading", panel, output);
            button.IsLoading = false;
            button.IsEnabled = false;
            Flush(window);
            Check("disabled style", !button.IsEffectivelyEnabled && Math.Abs(button.Opacity - 0.52) < 0.001);
            Frame("disabled", panel, output);
            Check("no binding/resource errors", !Log.Errors.Any());
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Console.Error.WriteLine(exception);
        }
        finally
        {
            window?.Close();
            var runtime = ArcRuntime.Current;
            var result = new JsonObject
            {
                ["Passed"] = error is null, ["Error"] = error,
                ["Checks"] = new JsonArray(Checks.Select(check => (JsonNode)check).ToArray()),
                ["Frames"] = new JsonArray(Frames.Select(frame => (JsonNode)frame).ToArray()),
                ["Logs"] = Strings(Log.Errors),
                ["RegisteredThemes"] = runtime?.Themes.AssetCount,
                ["LoadedThemes"] = runtime?.Themes.LoadedCount,
                ["TokenIdentities"] = Strings(runtime?.Theme.Descriptors.Select(descriptor => descriptor.Identity).Order().AsEnumerable() ?? Enumerable.Empty<string>()),
                ["Catalogs"] = Strings(runtime?.Languages.Catalogs.Select(catalog => catalog.Id).Order().AsEnumerable() ?? Enumerable.Empty<string>())
            };
            File.WriteAllText(Path.Combine(output, "validation.json"), result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        return error is null ? 0 : 1;
    }

    private static T Part<T>(Control control, string name) where T : Control
        => control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

    private static void Check(string name, bool condition)
    {
        Checks.Add(new JsonObject { ["Name"] = name, ["Passed"] = condition });
        if (!condition)
        {
            throw new InvalidOperationException("CHECK FAILED: " + name);
        }
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Frame(string name, Control panel, string output)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(420, 160), new Vector(96, 96));
        bitmap.Render(panel);
        bitmap.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
        using var readable = new WriteableBitmap(bitmap.PixelSize, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var pixels = readable.Lock();
        bitmap.CopyPixels(pixels);
        var bytes = new byte[pixels.RowBytes * pixels.Size.Height];
        Marshal.Copy(pixels.Address, bytes, 0, bytes.Length);
        var first = BitConverter.ToInt32(bytes, 0);
        var differentPixels = Enumerable.Range(0, bytes.Length / 4).Count(index => BitConverter.ToInt32(bytes, index * 4) != first);
        Check("nonempty render " + name, differentPixels > 100);
        Frames.Add(new JsonObject { ["Name"] = name, ["PixelSha256"] = Convert.ToHexString(SHA256.HashData(bytes)), ["DifferentPixels"] = differentPixels });
    }

    private static JsonArray Strings(IEnumerable<string> values)
        => new(values.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    private sealed class AuditLog : ILogSink
    {
        public List<string> Errors { get; } = new();
        public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
            => Log(level, area, source, messageTemplate, Array.Empty<object?>());
        public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] values)
        {
            if (level >= LogEventLevel.Error || area.Contains("Binding", StringComparison.OrdinalIgnoreCase)
                || area.Contains("Resource", StringComparison.OrdinalIgnoreCase))
            {
                Errors.Add(area + ": " + messageTemplate + " " + string.Join(", ", values.Select(value => value?.ToString())));
            }
        }
    }
}
