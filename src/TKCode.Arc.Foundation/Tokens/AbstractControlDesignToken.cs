namespace TKCode.Arc;

/// <summary>
/// Base class of every token class. A token class holds the values of one identity (a control, or the global
/// <c>ArcDesign</c>) as settable properties and computes them in <see cref="CalculateTokenValues"/>; the theme manager
/// then applies the string overrides of the theme state and publishes the properties as resources.
/// </summary>
public abstract class AbstractControlDesignToken
{
    /// <summary>
    /// The theme-wide inputs of the calculation: accent seed and motion level. Set by the theme manager before
    /// <see cref="CalculateTokenValues"/> runs.
    /// </summary>
    public ArcSeedToken EffectiveGlobalToken { get; internal set; } = ArcSeedToken.Default;

    /// <summary>
    /// Computes every token value for the given appearance. Overrides call <c>base</c> first. Must depend only on
    /// <see cref="EffectiveGlobalToken"/> and <paramref name="isDarkMode"/>: the manager may run it off the UI thread and
    /// cache the result.
    /// </summary>
    /// <param name="isDarkMode">True for the dark appearance.</param>
    public virtual void CalculateTokenValues(bool isDarkMode)
    {
    }
}
