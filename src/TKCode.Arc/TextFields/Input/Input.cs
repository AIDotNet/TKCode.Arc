namespace TKCode.Arc;

/// <summary>
/// Arc input: a single-line field with a visible label, helper and error rows that open on a spring and
/// rewrite only the words that change, and the Arc frame states (hover and focus darken the border, an error
/// turns it danger, disabled fades). Everything from Avalonia's TextBox is kept: caret, selection, IME, undo,
/// clipboard and a two-way <c>Text</c>.
/// <code>&lt;arc:Input Label="Email" Placeholder="you@example.com" Description="We never share it." /&gt;</code>
/// </summary>
public class Input : FieldTextBox
{
}
