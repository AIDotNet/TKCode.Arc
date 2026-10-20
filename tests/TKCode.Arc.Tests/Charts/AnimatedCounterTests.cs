using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Charts;

public class AnimatedCounterTests
{
    [AvaloniaFact]
    public void Shows_Formatted_Value_With_Affixes()
    {
        var counter = new AnimatedCounter { Value = 1234.5, Decimals = 1, Prefix = "$", Label = "Revenue" };
        var window = TestHelpers.Show(counter);
        try
        {
            var digits = TestHelpers.Descendant<Primitives.RollingDigits>(counter);
            digits.DisplayText.ShouldBe("$1,234.5");
            counter.Value = 999;
            counter.Decimals = 0;
            Dispatcher.UIThread.RunJobs();
            digits.DisplayText.ShouldBe("$999");
        }
        finally
        {
            window.Close();
        }
    }
}
