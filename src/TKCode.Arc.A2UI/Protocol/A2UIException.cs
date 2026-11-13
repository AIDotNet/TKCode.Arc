namespace TKCode.Arc.A2UI;

/// <summary>The kind of an A2UI failure, matching the categories of the protocol's conformance suites.</summary>
public enum A2UIErrorCategory
{
    /// <summary>Malformed JSON or an envelope that cannot be read.</summary>
    Parse,

    /// <summary>A well-formed message that breaks the protocol or a catalog schema.</summary>
    Validation,

    /// <summary>A surface lifecycle fault: duplicate <c>createSurface</c>, a message for an unknown surface, dangling references.</summary>
    Integrity,

    /// <summary>A cycle in the component graph, or nesting past the depth limit.</summary>
    Recursion,

    /// <summary>A catalog that is missing, conflicting or cannot be resolved.</summary>
    Catalog,

    /// <summary>An invalid JSON Pointer, a forbidden path segment or a write the data model cannot perform.</summary>
    Data,

    /// <summary>An expression or template that does not parse, or a function call that fails.</summary>
    Expression,

    /// <summary>An operation on a disposed or otherwise unusable object.</summary>
    State
}

/// <summary>
/// An A2UI protocol failure. <see cref="Code"/> is the code sent to the agent in an <c>error</c> message (for example
/// <c>VALIDATION_FAILED</c>); <see cref="Category"/> groups failures the way the protocol's conformance suites do.
/// </summary>
public class A2UIException : Exception
{
    public A2UIException(A2UIErrorCategory category, string code, string message, string? surfaceId = null, string? path = null,
                         string? functionCallId = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        Code = code;
        SurfaceId = surfaceId;
        Path = path;
        FunctionCallId = functionCallId;
    }

    /// <summary>The kind of failure.</summary>
    public A2UIErrorCategory Category { get; }

    /// <summary>The protocol error code (<see cref="A2UIErrorCodes"/>).</summary>
    public string Code { get; }

    /// <summary>The surface the failure belongs to, when there is one.</summary>
    public string? SurfaceId { get; }

    /// <summary>A JSON Pointer into the offending message, when there is one.</summary>
    public string? Path { get; }

    /// <summary>The function call the failure belongs to, when there is one.</summary>
    public string? FunctionCallId { get; }

    internal static A2UIException DataError(string message) => new(A2UIErrorCategory.Data, A2UIErrorCodes.ValidationFailed, message);

    internal static A2UIException Parse(string message, Exception? inner = null)
        => new(A2UIErrorCategory.Parse, A2UIErrorCodes.ValidationFailed, message, innerException: inner);

    internal static A2UIException Validation(string message, string? surfaceId = null, string? path = null)
        => new(A2UIErrorCategory.Validation, A2UIErrorCodes.ValidationFailed, message, surfaceId, path);

    internal static A2UIException Integrity(string message, string? surfaceId = null, string? path = null)
        => new(A2UIErrorCategory.Integrity, A2UIErrorCodes.IntegrityError, message, surfaceId, path);

    internal static A2UIException Expression(string message, string? surfaceId = null)
        => new(A2UIErrorCategory.Expression, A2UIErrorCodes.ExpressionError, message, surfaceId);

    internal static A2UIException Catalog(string message, string? surfaceId = null, string? path = null)
        => new(A2UIErrorCategory.Catalog, A2UIErrorCodes.ValidationFailed, message, surfaceId, path);

    internal static A2UIException State(string message) => new(A2UIErrorCategory.State, A2UIErrorCodes.ExecutionError, message);
}

/// <summary>Error codes of the A2UI 1.0 <c>error</c> message, plus the two this renderer adds.</summary>
public static class A2UIErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string UnallowedParent = "UNALLOWED_PARENT";
    public const string UnallowedChild = "UNALLOWED_CHILD";
    public const string IntegrityError = "INTEGRITY_ERROR";
    public const string InvalidFunctionCall = "INVALID_FUNCTION_CALL";
    public const string ExpressionError = "EXPRESSION_ERROR";
    public const string ExecutionError = "EXECUTION_ERROR";
    public const string InvalidReservedKey = "INVALID_RESERVED_KEY";

    /// <summary>A resource limit of <see cref="A2UILimits"/> was exceeded (not a protocol code).</summary>
    public const string LimitExceeded = "LIMIT_EXCEEDED";

    /// <summary>An envelope named a protocol version this renderer does not read (not a protocol code).</summary>
    public const string UnsupportedVersion = "UNSUPPORTED_VERSION";

    /// <summary>True for the codes that use the "validation failed" shape (<c>surfaceId</c> and <c>path</c> required).</summary>
    public static bool IsValidationShape(string code)
        => code is ValidationFailed or UnallowedParent or UnallowedChild;
}
