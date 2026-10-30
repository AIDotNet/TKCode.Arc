using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="PixelMascot"/> says, in English by default: the accessible label for each status, where
/// <c>{0}</c> is the assistant's name. Replace the whole object to localize or reword.
/// </summary>
public sealed class PixelMascotLabels
{
    /// <summary>The English defaults.</summary>
    public static PixelMascotLabels Default { get; } = new();

    /// <summary>The label while idle ("Assistant is idle").</summary>
    public string Idle { get; init; } = "{0} is idle";

    /// <summary>The label while thinking ("Assistant is thinking").</summary>
    public string Thinking { get; init; } = "{0} is thinking";

    /// <summary>The label while writing ("Assistant is writing").</summary>
    public string Writing { get; init; } = "{0} is writing";

    /// <summary>The label once done ("Assistant is done").</summary>
    public string Done { get; init; } = "{0} is done";

    /// <summary>The label after a failure ("Assistant ran into an error").</summary>
    public string Error { get; init; } = "{0} ran into an error";

    /// <summary>The label for a status, with the assistant's name filled in.</summary>
    public string Describe(string name, PixelMascotStatus status)
    {
        var template = status switch
        {
            PixelMascotStatus.Thinking => Thinking,
            PixelMascotStatus.Writing => Writing,
            PixelMascotStatus.Done => Done,
            PixelMascotStatus.Error => Error,
            _ => Idle
        };
        return string.Format(CultureInfo.CurrentCulture, template, name);
    }
}
