using System.Globalization;
using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>What a function implementation can see while it runs.</summary>
public sealed class A2UIFunctionContext
{
    internal A2UIFunctionContext(A2UIEvaluator evaluator, A2UIScope scope, A2UIFunctionDefinition definition, A2UIEvaluation evaluation)
    {
        Evaluator = evaluator;
        Scope = scope;
        Definition = definition;
        Evaluation = evaluation;
    }

    internal A2UIEvaluator Evaluator { get; }

    internal A2UIScope Scope { get; }

    internal A2UIEvaluation Evaluation { get; }

    /// <summary>The function being called.</summary>
    public A2UIFunctionDefinition Definition { get; }

    /// <summary>The surface the call is evaluated on, or null for a surface-less agent call.</summary>
    public A2UISurface? Surface => Scope.Surface;

    /// <summary>The data path of the calling component's scope (<c>/</c> outside templates).</summary>
    public string DataPath => Scope.DataPath;

    /// <summary>The culture for formatting.</summary>
    public CultureInfo Culture => Evaluator.Culture;

    /// <summary>True when the call runs inside a user gesture (a click, a key press).</summary>
    public bool HasUserActivation => Evaluation.UserActivation;

    /// <summary>Reads a value from the surface's data model (relative paths resolve against <see cref="DataPath"/>).</summary>
    public JsonNode? GetData(string path)
    {
        var absolute = A2UIJsonPointer.Resolve(Scope.DataPath, path);
        Evaluation.Read(absolute);
        return Scope.Data?.Peek(absolute);
    }

    /// <summary>Writes a value to the surface's data model (only from actions).</summary>
    public void SetData(string path, JsonNode? value)
    {
        var absolute = A2UIJsonPointer.Resolve(Scope.DataPath, path);
        Scope.Data?.Set(absolute, value);
    }

    /// <summary>Asks the host to open a URL (the renderer raises <see cref="A2UIRenderer.UrlOpening"/> first).</summary>
    public void RequestOpenUrl(Uri url) => Evaluator.RequestOpenUrl(url, Scope.Surface);
}
