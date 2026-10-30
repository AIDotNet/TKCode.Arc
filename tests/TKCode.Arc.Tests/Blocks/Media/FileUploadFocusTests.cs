using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests.Blocks.Media;

/// <summary>
/// The Blocks package's focus moves follow the input (BlockFocusModality): Remove hands focus to the next row's Remove
/// with the keyboard ring after a key only.
/// </summary>
public class FileUploadFocusTests
{
    private static (FileUpload Upload, FileUploadItem[] Items, Window Window) Show()
    {
        // Uploads that never finish keep every row (and its Remove) on screen.
        var upload = new FileUpload { Uploader = static (_, _, token) => Task.Delay(Timeout.Infinite, token) };
        var window = TestHelpers.Show(upload);
        window.Width = 640;
        window.Height = 800;
        FileUploadItem[] items = [new(null, "a.pdf", 1000), new(null, "b.pdf", 1000), new(null, "c.pdf", 1000)];
        upload.AddFiles(items);
        Pump();
        return (upload, items, window);
    }

    [AvaloniaFact]
    public void A_Clicked_Remove_Moves_Focus_On_Without_The_Ring()
    {
        var (upload, items, window) = Show();
        try
        {
            Click(window, upload.RowOf(items[1])!.RemoveButton!);
            upload.Files.Count.ShouldBe(2);
            upload.RowOf(items[2])!.RemoveButton!.ShouldBeFocusedWithoutRing();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Remove_Pressed_With_Enter_Moves_Focus_On_With_The_Ring()
    {
        var (upload, items, window) = Show();
        try
        {
            upload.RowOf(items[1])!.RemoveButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.Enter);
            upload.Files.Count.ShouldBe(2);
            upload.RowOf(items[2])!.RemoveButton!.ShouldBeFocusedWithRing();
        }
        finally
        {
            window.Close();
        }
    }
}
