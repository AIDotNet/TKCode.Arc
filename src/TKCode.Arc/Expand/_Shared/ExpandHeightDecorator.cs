using TKCode.Arc.Primitives;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// <see cref="SpringSizeDecorator"/> (P6) for disclosure panels inside stacks. The Core decorator treats a change of
/// its available size as passive and jumps; an infinite available height compares as unequal to itself
/// (∞ − ∞ is NaN), so inside a vertical stack every expand would jump. Infinite constraints are handed on as
/// <see cref="double.MaxValue"/>, which measures the same and compares equal.
/// </summary>
internal sealed class ExpandHeightDecorator : SpringSizeDecorator
{
    protected override Type StyleKeyOverride => typeof(SpringSizeDecorator);

    protected override Size MeasureOverride(Size availableSize)
        => base.MeasureOverride(new Size(Finite(availableSize.Width), Finite(availableSize.Height)));

    private static double Finite(double value) => double.IsPositiveInfinity(value) ? double.MaxValue : value;
}
