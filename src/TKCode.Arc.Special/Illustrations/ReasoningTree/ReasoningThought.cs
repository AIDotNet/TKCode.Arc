using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One thought of a <see cref="ReasoningTree"/>: a short label for the node, the full sentence for the tag and screen
/// readers, an illustrative score and the thoughts it leads to. The tree reads three levels: the thoughts given to
/// <see cref="ReasoningTree.Thoughts"/> are depth 1, their children depth 2 and their grandchildren depth 3; deeper
/// thoughts are ignored. Add a thought to one parent only.
/// </summary>
public sealed class ReasoningThought : AvaloniaObject
{
    /// <summary>Defines the <see cref="Key"/> property.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<ReasoningThought, string?>(nameof(Key));

    /// <summary>Defines the <see cref="Label"/> property.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ReasoningThought, string>(nameof(Label), string.Empty);

    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ReasoningThought, string?>(nameof(Text));

    /// <summary>Defines the <see cref="Score"/> property.</summary>
    public static readonly StyledProperty<double?> ScoreProperty =
        AvaloniaProperty.Register<ReasoningThought, double?>(nameof(Score));

    private readonly List<ReasoningThought> _watched = new();
    private bool _raising;

    /// <summary>Creates an empty thought.</summary>
    public ReasoningThought()
    {
        Children = new AvaloniaList<ReasoningThought>();
        Children.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>Creates a thought with a label, an optional score and an optional full sentence.</summary>
    public ReasoningThought(string label, double? score = null, string? text = null) : this()
    {
        Label = label;
        Score = score;
        Text = text;
    }

    /// <summary>
    /// A stable identifier (optional). It seeds the score of a thought without one in place of the label, so renaming a
    /// thought with a key keeps its seeded score.
    /// </summary>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <summary>The short text on the node: about 2 to 4 words, under about 20 characters (one line, cut with an ellipsis).</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The full thought, shown in the tag on hover and focus and read to screen readers; null uses the label.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>
    /// An illustrative score from 0 to 0.99 (values outside are clamped); null gets a score seeded from
    /// <see cref="ReasoningTree.Seed"/>, so the same tree and seed always search the same way.
    /// </summary>
    public double? Score { get => GetValue(ScoreProperty); set => SetValue(ScoreProperty, value); }

    /// <summary>The thoughts this one leads to, one depth further (read to depth 3).</summary>
    [Content]
    public AvaloniaList<ReasoningThought> Children { get; }

    /// <summary>Raised when this thought or any thought under it changes (the tree searches again).</summary>
    internal event EventHandler? Changed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KeyProperty || change.Property == LabelProperty || change.Property == TextProperty ||
            change.Property == ScoreProperty)
        {
            RaiseChanged();
        }
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var child in _watched)
        {
            child.Changed -= OnChildChanged;
        }
        _watched.Clear();
        foreach (var child in Children)
        {
            if (child is null || ReferenceEquals(child, this))
            {
                continue;
            }
            child.Changed += OnChildChanged;
            _watched.Add(child);
        }
        RaiseChanged();
    }

    private void OnChildChanged(object? sender, EventArgs e) => RaiseChanged();

    private void RaiseChanged()
    {
        // A thought placed under itself would echo forever; one notification per change is enough.
        if (_raising)
        {
            return;
        }
        _raising = true;
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _raising = false;
        }
    }
}
