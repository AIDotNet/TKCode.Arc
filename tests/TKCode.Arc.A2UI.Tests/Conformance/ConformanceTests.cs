namespace TKCode.Arc.A2UI.Tests.Conformance;

public sealed class ConformanceTests
{
    public static IEnumerable<object[]> DataModel() => ConformanceHarness.Cases("data_model");
    public static IEnumerable<object[]> DataContext() => ConformanceHarness.Cases("data_context");
    public static IEnumerable<object[]> DataDeletion() => ConformanceHarness.Cases("data_deletion");
    public static IEnumerable<object[]> DataModelPointers() => ConformanceHarness.Cases("data_model_pointers");
    public static IEnumerable<object[]> Expressions() => ConformanceHarness.Cases("expressions");
    public static IEnumerable<object[]> Functions() => ConformanceHarness.Cases("functions");
    public static IEnumerable<object[]> IndexFunction() => ConformanceHarness.Cases("index_function");
    public static IEnumerable<object[]> Validator() => ConformanceHarness.Cases("validator_v1_0");
    public static IEnumerable<object[]> MessageProcessor() => ConformanceHarness.Cases("message_processor_v1_0");
    public static IEnumerable<object[]> ErrorResponses() => ConformanceHarness.Cases("error_responses");
    public static IEnumerable<object[]> CompositionConstraints() => ConformanceHarness.Cases("composition_constraints");
    public static IEnumerable<object[]> ReservedKeys() => ConformanceHarness.Cases("reserved_keys");
    public static IEnumerable<object[]> ValidationResult() => ConformanceHarness.Cases("validation_result");
    public static IEnumerable<object[]> MultiCatalog() => ConformanceHarness.Cases("multi_catalog");

    [Theory, MemberData(nameof(DataModel))] public void Data_model(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(DataContext))] public void Data_context(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(DataDeletion))] public void Data_deletion(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(DataModelPointers))] public void Data_model_pointers(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(Expressions))] public void Expressions_(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(Functions))] public void Functions_(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(IndexFunction))] public void Index_function(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(Validator))] public void Validator_v1_0(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(MessageProcessor))] public void Message_processor_v1_0(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(ErrorResponses))] public void Error_responses(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(CompositionConstraints))] public void Composition_constraints(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(ReservedKeys))] public void Reserved_keys(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(ValidationResult))] public void Validation_result(string suite, string name) => ConformanceHarness.Run(suite, name);
    [Theory, MemberData(nameof(MultiCatalog))] public void Multi_catalog(string suite, string name) => ConformanceHarness.Run(suite, name);
}
