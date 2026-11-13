using Avalonia;
using Avalonia.Data;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A capability an agent can be allowed to use, shown as a row with a switch in <see cref="AgentBuilder"/>.</summary>
public class AgentBuilderTool : AvaloniaObject
{
    /// <summary>Stable id, saved in <see cref="AgentBuilderConfig.ToolIds"/>.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AgentBuilderTool, string>(nameof(Id), string.Empty);

    /// <summary>The name in the row, such as "Web search".</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<AgentBuilderTool, string>(nameof(Name), string.Empty);

    /// <summary>One line under the name that says what the agent can do with it.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AgentBuilderTool, string?>(nameof(Description));

    /// <summary>A 24 × 24 stroke glyph (such as <c>ArcIcons.Globe</c>); null shows a wrench.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<AgentBuilderTool, Geometry?>(nameof(Glyph));

    /// <summary>Whether the agent may use the tool (two-way: the row's switch edits it).</summary>
    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<AgentBuilderTool, bool>(nameof(IsOn), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The words the test chat shows while the tool runs, such as "Searching the web"; null uses the name.</summary>
    public static readonly StyledProperty<string?> RunningLabelProperty =
        AvaloniaProperty.Register<AgentBuilderTool, string?>(nameof(RunningLabel));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="GlyphProperty"/>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    /// <inheritdoc cref="IsOnProperty"/>
    public bool IsOn { get => GetValue(IsOnProperty); set => SetValue(IsOnProperty, value); }

    /// <inheritdoc cref="RunningLabelProperty"/>
    public string? RunningLabel { get => GetValue(RunningLabelProperty); set => SetValue(RunningLabelProperty, value); }
}

/// <summary>What a knowledge source is.</summary>
public enum AgentBuilderSourceKind
{
    /// <summary>An uploaded file.</summary>
    File,

    /// <summary>A web page or site, added by its link.</summary>
    Website
}

/// <summary>Where a knowledge source is in its indexing.</summary>
public enum AgentBuilderSourceStatus
{
    /// <summary>Being read and split into passages; <see cref="AgentBuilderSource.Progress"/> says how far.</summary>
    Indexing,

    /// <summary>Indexed and searchable.</summary>
    Ready,

    /// <summary>Indexing failed; <see cref="AgentBuilderSource.ErrorMessage"/> says why and Retry runs it again.</summary>
    Failed
}

/// <summary>A document or site the agent can search, shown as a row in the Knowledge section of <see cref="AgentBuilder"/>.</summary>
public class AgentBuilderSource : AvaloniaObject
{
    /// <summary>Stable id, saved in <see cref="AgentBuilderConfig.Sources"/>.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AgentBuilderSource, string>(nameof(Id), string.Empty);

    /// <summary>The name in the row: a file name or the page's address without the scheme.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<AgentBuilderSource, string>(nameof(Name), string.Empty);

    /// <summary>A file or a website.</summary>
    public static readonly StyledProperty<AgentBuilderSourceKind> KindProperty =
        AvaloniaProperty.Register<AgentBuilderSource, AgentBuilderSourceKind>(nameof(Kind));

    /// <summary>The page's address for a website; null for a file.</summary>
    public static readonly StyledProperty<Uri?> UrlProperty =
        AvaloniaProperty.Register<AgentBuilderSource, Uri?>(nameof(Url));

    /// <summary>The file size in bytes, shown in the row; null hides it.</summary>
    public static readonly StyledProperty<long?> SizeProperty =
        AvaloniaProperty.Register<AgentBuilderSource, long?>(nameof(Size));

    /// <summary>Where the source is in its indexing.</summary>
    public static readonly StyledProperty<AgentBuilderSourceStatus> StatusProperty =
        AvaloniaProperty.Register<AgentBuilderSource, AgentBuilderSourceStatus>(nameof(Status), AgentBuilderSourceStatus.Ready);

    /// <summary>How far indexing is, from 0 to 1 (shown while <see cref="Status"/> is Indexing).</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AgentBuilderSource, double>(nameof(Progress));

    /// <summary>How many passages the index holds once ready; null hides the count.</summary>
    public static readonly StyledProperty<int?> ChunkCountProperty =
        AvaloniaProperty.Register<AgentBuilderSource, int?>(nameof(ChunkCount));

    /// <summary>Why indexing failed.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<AgentBuilderSource, string?>(nameof(ErrorMessage));

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="KindProperty"/>
    public AgentBuilderSourceKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="UrlProperty"/>
    public Uri? Url { get => GetValue(UrlProperty); set => SetValue(UrlProperty, value); }

    /// <inheritdoc cref="SizeProperty"/>
    public long? Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public AgentBuilderSourceStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    /// <inheritdoc cref="ChunkCountProperty"/>
    public int? ChunkCount { get => GetValue(ChunkCountProperty); set => SetValue(ChunkCountProperty, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }
}

/// <summary>Where the agent stands between drafts and what people use.</summary>
public enum AgentBuilderPublishState
{
    /// <summary>Never published.</summary>
    Draft,

    /// <summary>What is saved is what people use.</summary>
    Published,

    /// <summary>Published once, but the saved or edited settings differ from the published version.</summary>
    Changed
}

/// <summary>
/// A snapshot of an agent's settings: what <see cref="AgentBuilder"/> saves, publishes and hands the test chat. Two
/// snapshots are equal when every field and every id list (in order) match.
/// </summary>
/// <param name="Name">The agent's name.</param>
/// <param name="Description">The one-line description.</param>
/// <param name="Instructions">The instructions (system prompt).</param>
/// <param name="ModelId">The <see cref="AiModel.Id"/> of the chosen model, or null.</param>
/// <param name="ToolIds">The ids of the tools that are on, in the order of <see cref="AgentBuilder.Tools"/>.</param>
/// <param name="Sources">The knowledge sources, in order.</param>
public sealed record AgentBuilderConfig(string Name, string Description, string Instructions, string? ModelId,
    IReadOnlyList<string> ToolIds, IReadOnlyList<AgentBuilderSource> Sources)
{
    /// <summary>An agent with nothing set.</summary>
    public static AgentBuilderConfig Empty { get; } = new(string.Empty, string.Empty, string.Empty, null, [], []);

    /// <summary>Whether a tool with <paramref name="id"/> is on.</summary>
    /// <param name="id">The tool id.</param>
    public bool HasTool(string id) => ToolIds.Contains(id);

    /// <inheritdoc />
    public bool Equals(AgentBuilderConfig? other) =>
        other is not null
        && Name == other.Name
        && Description == other.Description
        && Instructions == other.Instructions
        && ModelId == other.ModelId
        && ToolIds.SequenceEqual(other.ToolIds)
        && Sources.Select(s => s.Id).SequenceEqual(other.Sources.Select(s => s.Id));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Description, Instructions, ModelId, ToolIds.Count, Sources.Count);
}

/// <summary>What the test chat asks a responder: the settings the chat started with and the conversation so far.</summary>
/// <param name="Config">The agent's settings when the test chat started (not later edits; restart the chat for those).</param>
/// <param name="Model">The model of <see cref="AgentBuilderConfig.ModelId"/>, if the catalog has it.</param>
/// <param name="Tools">The tools that are on in <paramref name="Config"/>.</param>
/// <param name="Chat">The chat's request: the messages in order, ending with the prompt to answer.</param>
public sealed record AgentBuilderTestRequest(AgentBuilderConfig Config, AiModel? Model, IReadOnlyList<AgentBuilderTool> Tools, AiChatRequest Chat);

/// <summary>Answers the test chat of an <see cref="AgentBuilder"/> as the agent would: connect it to your real agent runtime.</summary>
public interface IAgentBuilderResponder
{
    /// <summary>
    /// Streams the reply to the last message of <paramref name="request"/>. Throw (other than
    /// <see cref="OperationCanceledException"/>) or send an <see cref="AiChatErrorEvent"/> to fail the reply.
    /// </summary>
    /// <param name="request">The settings and the conversation.</param>
    /// <param name="cancellationToken">Cancelled when the person stops the reply or restarts the chat.</param>
    IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AgentBuilderTestRequest request, CancellationToken cancellationToken);
}
