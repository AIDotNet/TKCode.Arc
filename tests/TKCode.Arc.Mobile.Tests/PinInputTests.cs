using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile.Tests;

public class PinInputTests
{
    [AvaloniaFact]
    public void Filters_Non_Digits_And_Raises_Completed_With_Haptics()
    {
        var recorded = new List<HapticKind>();
        var previous = Haptics.Platform;
        Haptics.Platform = new RecordingHaptics(recorded);
        try
        {
            var pin = new PinInput { Length = 4 };
            var changes = new List<string>();
            string? completed = null;
            pin.CodeChanged += (_, e) => changes.Add(e.Code);
            pin.Completed += (_, e) => completed = e.Code;

            var window = new Window { Width = 360, Height = 200, Content = pin };
            window.Show();
            try
            {
                pin.Code = "12-ab-34-56";
                pin.Code.ShouldBe("1234");
                pin.IsComplete.ShouldBeTrue();
                completed.ShouldBe("1234");
                changes.ShouldBe(["1234"]);
                recorded.ShouldContain(HapticKind.Success);

                pin.Clear();
                pin.Code.ShouldBe(string.Empty);
                pin.IsComplete.ShouldBeFalse();
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            Haptics.Platform = previous;
        }
    }

    [AvaloniaFact]
    public void Alphanumeric_Masking_And_Error_State_Work()
    {
        var recorded = new List<HapticKind>();
        var previous = Haptics.Platform;
        Haptics.Platform = new RecordingHaptics(recorded);
        try
        {
            var pin = new PinInput { Length = 4, NumericOnly = false, MaskChar = '•' };
            var window = new Window { Width = 360, Height = 200, Content = pin };
            window.Show();
            try
            {
                pin.Code = "aB 9!";
                pin.Code.ShouldBe("aB9!");
                var cells = pin.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == "•").ToList();
                cells.Count.ShouldBe(4);

                pin.HasError = true;
                pin.Classes.Contains(":error").ShouldBeTrue();
                recorded.ShouldContain(HapticKind.Error);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            Haptics.Platform = previous;
        }
    }

    private sealed class RecordingHaptics(List<HapticKind> sink) : IHapticsPlatform
    {
        public void Trigger(HapticKind kind) => sink.Add(kind);
    }
}
