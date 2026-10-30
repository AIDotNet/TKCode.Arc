namespace TKCode.Arc;

/// <summary>
/// Texts a <see cref="SmartFormFill"/> plays without an extractor, from data you give it: the samples behind Try sample,
/// each with the result a read returns, and the model named beside the paste area. Set it as
/// <see cref="SmartFormFill.Simulation"/> while the card has no <see cref="SmartFormFill.Extract"/>: Try sample pastes the
/// samples in turn, a read returns the sample's result after a short wait (other text is read by a few plain rules), and
/// unless <see cref="SmartFormFill.AutoPlay"/> is false the card loops paste, fill, review and undo until someone touches
/// it. Nothing is sent.
/// </summary>
public sealed class SmartFormFillSimulation
{
    /// <summary>The texts behind Try sample, cycled in order, each with its <see cref="SmartFormFillSample.Result"/>.</summary>
    public IReadOnlyList<SmartFormFillSample>? Samples { get; set; }

    /// <summary>The model named beside the paste area when the card has no <see cref="SmartFormFill.ModelName"/>.</summary>
    public string? ModelName { get; set; }
}
