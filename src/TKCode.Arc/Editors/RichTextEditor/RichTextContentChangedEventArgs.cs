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

/// <summary>Arguments of <see cref="RichTextEditor.ContentChanged"/>.</summary>
public sealed class RichTextContentChangedEventArgs : RoutedEventArgs
{
    internal RichTextContentChangedEventArgs(RoutedEvent routedEvent, RichTextValue value) : base(routedEvent)
    {
        Value = value;
    }

    /// <summary>The editor's content after the change (HTML, Markdown and text are computed on first read).</summary>
    public RichTextValue Value { get; }
}
