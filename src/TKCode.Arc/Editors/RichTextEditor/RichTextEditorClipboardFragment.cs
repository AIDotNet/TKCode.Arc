using TKCode.Arc.Primitives;
using System.Collections.Immutable;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The in-process clipboard payload: the copied blocks, pasted back without loss.</summary>
internal sealed class RichTextEditorClipboardFragment(IReadOnlyList<RichTextEditorBlock> blocks)
{
    public IReadOnlyList<RichTextEditorBlock> Blocks { get; } = blocks;
}
