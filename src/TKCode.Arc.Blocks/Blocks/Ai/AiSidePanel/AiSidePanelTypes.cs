using Avalonia;
using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>A block of the page the assistant reads and edits: a heading, a paragraph or a table.</summary>
public abstract class AiPanelBlock : AvaloniaObject
{
    /// <summary>Stable id; selections and changes refer to blocks by it.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<AiPanelBlock, string>(nameof(Id), string.Empty);

    /// <summary>The name of the block ("Introduction", "Pricing table"): shown on the selection pill, the change card, the placeholder and to screen readers.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<AiPanelBlock, string?>(nameof(Label));

    /// <summary>The id of the block.</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The name of the block.</summary>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>A copy of the block with the same content (used to keep the text before a change).</summary>
    public abstract AiPanelBlock Clone();
}

/// <summary>A heading of the page.</summary>
public class AiPanelHeading : AiPanelBlock
{
    /// <summary>The heading text.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AiPanelHeading, string>(nameof(Text), string.Empty);

    /// <summary>The heading text.</summary>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc/>
    public override AiPanelBlock Clone() => new AiPanelHeading { Id = Id, Label = Label, Text = Text };
}

/// <summary>A paragraph of the page.</summary>
public class AiPanelParagraph : AiPanelBlock
{
    /// <summary>The paragraph text.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<AiPanelParagraph, string>(nameof(Text), string.Empty);

    /// <summary>The paragraph text.</summary>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc/>
    public override AiPanelBlock Clone() => new AiPanelParagraph { Id = Id, Label = Label, Text = Text };
}

/// <summary>How a table column aligns its cells.</summary>
public enum AiPanelColumnAlignment
{
    /// <summary>Left.</summary>
    Left,

    /// <summary>Right, for numbers.</summary>
    Right
}

/// <summary>A column of a table.</summary>
public class AiPanelColumn : AvaloniaObject
{
    /// <summary>The header text.</summary>
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<AiPanelColumn, string>(nameof(Header), string.Empty);

    /// <summary>How the cells align.</summary>
    public static readonly StyledProperty<AiPanelColumnAlignment> AlignmentProperty =
        AvaloniaProperty.Register<AiPanelColumn, AiPanelColumnAlignment>(nameof(Alignment));

    /// <summary>Whether the cells of this column are row headers (the first column usually is).</summary>
    public static readonly StyledProperty<bool> IsRowHeaderProperty =
        AvaloniaProperty.Register<AiPanelColumn, bool>(nameof(IsRowHeader));

    /// <summary>The header text.</summary>
    public string Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>How the cells align.</summary>
    public AiPanelColumnAlignment Alignment { get => GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }

    /// <summary>Whether the cells are row headers.</summary>
    public bool IsRowHeader { get => GetValue(IsRowHeaderProperty); set => SetValue(IsRowHeaderProperty, value); }
}

/// <summary>A row of a table: one text per column.</summary>
public class AiPanelRow : AvaloniaObject
{
    /// <summary>The cell texts, in column order.</summary>
    public AvaloniaList<string> Cells { get; } = new();
}

/// <summary>A table of the page.</summary>
public class AiPanelTable : AiPanelBlock
{
    /// <summary>The columns.</summary>
    public AvaloniaList<AiPanelColumn> Columns { get; } = new();

    /// <summary>The rows.</summary>
    public AvaloniaList<AiPanelRow> Rows { get; } = new();

    /// <inheritdoc/>
    public override AiPanelBlock Clone()
    {
        var copy = new AiPanelTable { Id = Id, Label = Label };
        foreach (var column in Columns)
        {
            copy.Columns.Add(new AiPanelColumn { Header = column.Header, Alignment = column.Alignment, IsRowHeader = column.IsRowHeader });
        }

        foreach (var row in Rows)
        {
            var cells = new AiPanelRow();
            cells.Cells.AddRange(row.Cells);
            copy.Rows.Add(cells);
        }

        return copy;
    }
}

/// <summary>The page the assistant reads and edits: a title, a line of meta text and blocks.</summary>
public class AiPanelPage : AvaloniaObject
{
    /// <summary>The title.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<AiPanelPage, string>(nameof(Title), string.Empty);

    /// <summary>A line under the title ("Updated Mar 3 by Priya").</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<AiPanelPage, string?>(nameof(Meta));

    /// <summary>The title.</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The line under the title.</summary>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <summary>The blocks, in reading order.</summary>
    public AvaloniaList<AiPanelBlock> Blocks { get; } = new();

    /// <summary>The block with the given id, or null.</summary>
    public AiPanelBlock? Find(string id) => Blocks.FirstOrDefault(b => b.Id == id);
}

/// <summary>The text the person selected on the page and asks about.</summary>
/// <param name="BlockId">The id of the block the selection starts in.</param>
/// <param name="Label">That block's label.</param>
/// <param name="Text">The selected text.</param>
public sealed record AiPanelSelection(string BlockId, string Label, string Text);

/// <summary>Where a turn of the thread is.</summary>
public enum AiPanelTurnState
{
    /// <summary>Reading the page ("Reading the page" shimmers).</summary>
    Reading,

    /// <summary>The answer is being written.</summary>
    Streaming,

    /// <summary>Finished.</summary>
    Done,

    /// <summary>Stopped by the person.</summary>
    Stopped,

    /// <summary>Failed; Retry runs it again.</summary>
    Failed
}

/// <summary>Where a proposed change is.</summary>
public enum AiPanelChangeState
{
    /// <summary>Waiting for the person.</summary>
    Proposed,

    /// <summary>Applied to the page.</summary>
    Applied,

    /// <summary>Applied and then undone; it can be applied again.</summary>
    Undone,

    /// <summary>Discarded for good.</summary>
    Discarded
}

/// <summary>A change to one block that the assistant proposes: shown as a card with a word diff, applied or discarded by the person.</summary>
public class AiPanelChange : AvaloniaObject
{
    /// <summary>The id of the block that changes.</summary>
    public static readonly StyledProperty<string> BlockIdProperty =
        AvaloniaProperty.Register<AiPanelChange, string>(nameof(BlockId), string.Empty);

    /// <summary>The block as it is on the page when the card is made; the block to restore on undo.</summary>
    public static readonly StyledProperty<AiPanelBlock?> BeforeProperty =
        AvaloniaProperty.Register<AiPanelChange, AiPanelBlock?>(nameof(Before));

    /// <summary>The whole new block.</summary>
    public static readonly StyledProperty<AiPanelBlock?> AfterProperty =
        AvaloniaProperty.Register<AiPanelChange, AiPanelBlock?>(nameof(After));

    /// <summary>Where the change is.</summary>
    public static readonly StyledProperty<AiPanelChangeState> StateProperty =
        AvaloniaProperty.Register<AiPanelChange, AiPanelChangeState>(nameof(State));

    /// <summary>The id of the block that changes.</summary>
    public string BlockId { get => GetValue(BlockIdProperty); set => SetValue(BlockIdProperty, value); }

    /// <summary>The block before the change.</summary>
    public AiPanelBlock? Before { get => GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }

    /// <summary>The block after the change.</summary>
    public AiPanelBlock? After { get => GetValue(AfterProperty); set => SetValue(AfterProperty, value); }

    /// <summary>Where the change is.</summary>
    public AiPanelChangeState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
}

/// <summary>One exchange of the thread: the question, the context it was asked in, the answer and an optional change.</summary>
public class AiPanelTurn : AvaloniaObject
{
    /// <summary>What the person asked.</summary>
    public static readonly StyledProperty<string> PromptProperty =
        AvaloniaProperty.Register<AiPanelTurn, string>(nameof(Prompt), string.Empty);

    /// <summary>The label of the selection the question was asked about, if any.</summary>
    public static readonly StyledProperty<string?> ContextLabelProperty =
        AvaloniaProperty.Register<AiPanelTurn, string?>(nameof(ContextLabel));

    /// <summary>The answer text; grows while it streams.</summary>
    public static readonly StyledProperty<string> AnswerProperty =
        AvaloniaProperty.Register<AiPanelTurn, string>(nameof(Answer), string.Empty);

    /// <summary>Where the turn is.</summary>
    public static readonly StyledProperty<AiPanelTurnState> StateProperty =
        AvaloniaProperty.Register<AiPanelTurn, AiPanelTurnState>(nameof(State), AiPanelTurnState.Done);

    /// <summary>Why the turn failed.</summary>
    public static readonly StyledProperty<string?> ErrorMessageProperty =
        AvaloniaProperty.Register<AiPanelTurn, string?>(nameof(ErrorMessage));

    /// <summary>The change the assistant proposes with its answer, if any.</summary>
    public static readonly StyledProperty<AiPanelChange?> ChangeProperty =
        AvaloniaProperty.Register<AiPanelTurn, AiPanelChange?>(nameof(Change));

    /// <summary>What the person asked.</summary>
    public string Prompt { get => GetValue(PromptProperty); set => SetValue(PromptProperty, value); }

    /// <summary>The label of the selection.</summary>
    public string? ContextLabel { get => GetValue(ContextLabelProperty); set => SetValue(ContextLabelProperty, value); }

    /// <summary>The answer text.</summary>
    public string Answer { get => GetValue(AnswerProperty); set => SetValue(AnswerProperty, value); }

    /// <summary>Where the turn is.</summary>
    public AiPanelTurnState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <summary>Why the turn failed.</summary>
    public string? ErrorMessage { get => GetValue(ErrorMessageProperty); set => SetValue(ErrorMessageProperty, value); }

    /// <summary>The proposed change.</summary>
    public AiPanelChange? Change { get => GetValue(ChangeProperty); set => SetValue(ChangeProperty, value); }
}
