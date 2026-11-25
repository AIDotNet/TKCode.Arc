namespace TKCode.Arc.Editor.Tests;

/// <summary>
/// The tests that listen to UnexpectedErrors.Occurred, a process-wide event: they run apart from the parallel tests, so
/// that an error another test provokes on purpose (a tokenizer that throws, for one) is not counted as theirs.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UnexpectedErrorsCollection
{
    public const string Name = "UnexpectedErrors";
}
