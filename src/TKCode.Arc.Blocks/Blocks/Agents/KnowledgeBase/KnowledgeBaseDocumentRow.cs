using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One document in a <see cref="KnowledgeBase"/> list: the kind mark, the name with a line of facts (type, size, chunks,
/// date) or the error, the stage with a progress ring, and Retry, Re-index and Delete. Delete swaps the row for an inline
/// question with Cancel and Delete. The row reads its document from the data context, so the list can recycle it.
/// </summary>
[TemplatePart(ReindexPart, typeof(Button))]
[TemplatePart(DeletePart, typeof(Button))]
[TemplatePart(RetryPart, typeof(Button))]
[TemplatePart(CancelPart, typeof(Button))]
[TemplatePart(ConfirmDeletePart, typeof(Button))]
[PseudoClasses(ReadyPseudoClass, BusyPseudoClass, QueuedPseudoClass, FailedPseudoClass, ConfirmingPseudoClass, LocatedPseudoClass,
    ReadOnlyPseudoClass)]
internal sealed class KnowledgeBaseDocumentRow : TemplatedControl
{
    internal const string MainPart = "PART_Main";
    internal const string KindGlyphPart = "PART_KindGlyph";
    internal const string NamePart = "PART_Name";
    internal const string MetaPart = "PART_Meta";
    internal const string ErrorPart = "PART_Error";
    internal const string RingPart = "PART_Ring";
    internal const string StatusGlyphPart = "PART_StatusGlyph";
    internal const string StatusTextPart = "PART_StatusText";
    internal const string RetryPart = "PART_Retry";
    internal const string ReindexPart = "PART_Reindex";
    internal const string DeletePart = "PART_Delete";
    internal const string ConfirmPart = "PART_Confirm";
    internal const string ConfirmTextPart = "PART_ConfirmText";
    internal const string CancelPart = "PART_Cancel";
    internal const string ConfirmDeletePart = "PART_ConfirmDelete";

    internal const string ReadyPseudoClass = ":ready";
    internal const string BusyPseudoClass = ":busy";
    internal const string QueuedPseudoClass = ":queued";
    internal const string FailedPseudoClass = ":failed";
    internal const string ConfirmingPseudoClass = ":confirming";
    internal const string LocatedPseudoClass = ":located";
    internal const string ReadOnlyPseudoClass = ":readonly";

    private readonly KnowledgeBase _owner;
    private KnowledgeDocument? _document;
    // Whether this row shows the question now; the question itself belongs to the document (KnowledgeBase.ConfirmingDocument),
    // so a row the list recycles while the question is open gives it to the row that shows the document next.
    private bool _confirming;
    private bool _attached;

    private Control? _main;
    private ArcGlyph? _kindGlyph;
    private TextBlock? _name;
    private TextBlock? _meta;
    private TextBlock? _error;
    private ArcRing? _ring;
    private ArcGlyph? _statusGlyph;
    private TextBlock? _statusText;
    private Button? _retry;
    private Button? _reindex;
    private Button? _delete;
    private Control? _confirm;
    private TextBlock? _confirmText;
    private Button? _cancel;
    private Button? _confirmDelete;

    public KnowledgeBaseDocumentRow(KnowledgeBase owner)
    {
        _owner = owner;
    }

    /// <summary>The document the row shows (its data context).</summary>
    public KnowledgeDocument? Document => _document;

    /// <summary>Whether the delete question shows.</summary>
    public bool IsConfirming => _confirming;

    internal Button? RetryButton => _retry;
    internal Button? ReindexButton => _reindex;
    internal Button? DeleteButton => _delete;
    internal Button? CancelButton => _cancel;
    internal Button? ConfirmDeleteButton => _confirmDelete;
    internal TextBlock? NameText => _name;
    internal TextBlock? MetaText => _meta;
    internal TextBlock? ErrorText => _error;
    internal TextBlock? StatusText => _statusText;
    internal ArcRing? Ring => _ring;
    internal TextBlock? ConfirmText => _confirmText;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        foreach (var button in new[] { _retry, _reindex, _delete, _cancel, _confirmDelete })
        {
            if (button is not null)
            {
                button.Click -= OnButtonClick;
            }
        }
        _confirm?.RemoveHandler(KeyDownEvent, OnConfirmKeyDown);
        T? Find<T>(string name) where T : class => e.NameScope.Find<T>(name);
        _main = Find<Control>(MainPart);
        _kindGlyph = Find<ArcGlyph>(KindGlyphPart);
        _name = Find<TextBlock>(NamePart);
        _meta = Find<TextBlock>(MetaPart);
        _error = Find<TextBlock>(ErrorPart);
        _ring = Find<ArcRing>(RingPart);
        _statusGlyph = Find<ArcGlyph>(StatusGlyphPart);
        _statusText = Find<TextBlock>(StatusTextPart);
        _retry = Find<Button>(RetryPart);
        _reindex = Find<Button>(ReindexPart);
        _delete = Find<Button>(DeletePart);
        _confirm = Find<Control>(ConfirmPart);
        _confirmText = Find<TextBlock>(ConfirmTextPart);
        _cancel = Find<Button>(CancelPart);
        _confirmDelete = Find<Button>(ConfirmDeletePart);
        foreach (var button in new[] { _retry, _reindex, _delete, _cancel, _confirmDelete })
        {
            if (button is not null)
            {
                button.Click += OnButtonClick;
            }
        }
        _confirm?.AddHandler(KeyDownEvent, OnConfirmKeyDown, RoutingStrategies.Bubble);
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _owner.RowsInvalidated += OnRowsInvalidated;
        Watch(DataContext as KnowledgeDocument);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _owner.RowsInvalidated -= OnRowsInvalidated;
        Watch(null);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_attached)
        {
            Watch(DataContext as KnowledgeDocument);
        }
    }

    private void Watch(KnowledgeDocument? document)
    {
        if (ReferenceEquals(document, _document))
        {
            Refresh();
            return;
        }
        if (_document is not null)
        {
            _document.PropertyChanged -= OnDocumentChanged;
        }
        _document = document;
        if (_document is not null)
        {
            _document.PropertyChanged += OnDocumentChanged;
        }
        Refresh();
    }

    private void OnRowsInvalidated(object? sender, EventArgs e) => Refresh();

    private void OnDocumentChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == KnowledgeDocument.ProgressProperty)
        {
            UpdateProgress();
            return;
        }
        Refresh();
    }

    /// <summary>Writes every text, glyph, name and state of the document into the parts.</summary>
    private void Refresh()
    {
        var document = _document;
        if (document is null)
        {
            return;
        }
        var labels = _owner.Labels;
        var culture = _owner.FormatCulture;
        var status = document.Status;
        var kind = labels.KindName(document.Kind);

        if (_kindGlyph is not null)
        {
            _kindGlyph.Data = KindGlyph(document.Kind);
        }
        SetText(_name, document.Name);
        var facts = new List<string> { kind, KnowledgeBaseText.FormatSize(document.SizeBytes, culture) };
        if (document.ChunkCount > 0 && status != KnowledgeDocumentStatus.Failed)
        {
            facts.Add(labels.Chunks(document.ChunkCount));
        }
        if (document.UpdatedAt is { } updated && !_owner.IsCompactLayout && status == KnowledgeDocumentStatus.Ready)
        {
            facts.Add(labels.Updated(updated.ToLocalTime().ToString("MMM d", culture)));
        }
        SetText(_meta, string.Join(" · ", facts));
        SetText(_error, status == KnowledgeDocumentStatus.Failed ? document.Error : null);
        if (_statusGlyph is not null)
        {
            _statusGlyph.Data = status switch
            {
                KnowledgeDocumentStatus.Ready => ArcIcons.CircleCheck,
                KnowledgeDocumentStatus.Failed => ArcIcons.CircleAlert,
                KnowledgeDocumentStatus.Queued => ArcIcons.Clock,
                _ => null
            };
        }
        UpdateProgress();

        var name = document.Name;
        SetName(_retry, $"{labels.Retry} {name}");
        SetName(_reindex, labels.Reindex(name));
        SetName(_delete, labels.Delete(name));
        if (_reindex is not null)
        {
            Tooltip.SetTip(_reindex, labels.Reindex(name));
        }
        if (_delete is not null)
        {
            Tooltip.SetTip(_delete, labels.Delete(name));
        }
        if (_retry is not null)
        {
            _retry.Content = labels.Retry;
        }
        SetText(_confirmText, labels.ConfirmDelete(name, document.ChunkCount));
        if (_cancel is not null)
        {
            _cancel.Content = labels.Cancel;
        }
        if (_confirmDelete is not null)
        {
            _confirmDelete.Content = labels.ConfirmDeleteButton;
            AutomationProperties.SetName(_confirmDelete, labels.Delete(name));
        }
        if (_reindex is not null)
        {
            // Nothing to re-index while a run is under way or waiting; Retry stands in for it on a failed row.
            _reindex.IsEnabled = status is KnowledgeDocumentStatus.Ready;
        }

        PseudoClasses.Set(ReadyPseudoClass, status == KnowledgeDocumentStatus.Ready);
        PseudoClasses.Set(BusyPseudoClass, document.IsBusy);
        PseudoClasses.Set(QueuedPseudoClass, status == KnowledgeDocumentStatus.Queued);
        PseudoClasses.Set(FailedPseudoClass, status == KnowledgeDocumentStatus.Failed);
        PseudoClasses.Set(LocatedPseudoClass, ReferenceEquals(_owner.LocatedDocument, document));
        PseudoClasses.Set(ReadOnlyPseudoClass, _owner.IsReadOnly);
        ShowConfirming(!_owner.IsReadOnly && ReferenceEquals(_owner.ConfirmingDocument, document));
        UpdateAutomationName();
    }

    private void UpdateProgress()
    {
        var document = _document;
        if (document is null)
        {
            return;
        }
        var labels = _owner.Labels;
        if (_ring is not null)
        {
            _ring.Value = document.IsBusy ? Math.Max(0.04, document.Progress) : 0;
        }
        var stage = labels.StatusName(document.Status);
        SetText(_statusText, document.IsBusy
            ? string.Format(_owner.FormatCulture, "{0} {1:0}%", stage, document.Progress * 100)
            : stage);
        UpdateAutomationName();
    }

    private void UpdateAutomationName()
    {
        if (_document is not { } document)
        {
            return;
        }
        var parts = new List<string> { document.Name, _statusText?.Text ?? string.Empty };
        if (_meta?.Text is { Length: > 0 } meta)
        {
            parts.Add(meta);
        }
        if (_error?.Text is { Length: > 0 } error)
        {
            parts.Add(error);
        }
        AutomationProperties.SetName(this, string.Join(", ", parts.Where(p => p.Length > 0)));
    }

    private static Geometry KindGlyph(KnowledgeDocumentKind kind) => kind switch
    {
        KnowledgeDocumentKind.Spreadsheet => ArcIcons.FileSpreadsheet,
        KnowledgeDocumentKind.Code => ArcIcons.FileCode,
        KnowledgeDocumentKind.Web => ArcIcons.Globe,
        KnowledgeDocumentKind.Pdf or KnowledgeDocumentKind.Document => ArcIcons.FileText,
        KnowledgeDocumentKind.Markdown => ArcIcons.Hash,
        _ => ArcIcons.File
    };

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null && block.Text != text)
        {
            block.Text = text;
        }
    }

    private static void SetName(Control? control, string name)
    {
        if (control is not null)
        {
            AutomationProperties.SetName(control, name);
        }
    }

    #region Actions

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_document is not { } document)
        {
            return;
        }
        e.Handled = true;
        if (ReferenceEquals(sender, _delete))
        {
            SetConfirming(true, focus: true);
        }
        else if (ReferenceEquals(sender, _cancel))
        {
            SetConfirming(false, focus: true);
        }
        else if (ReferenceEquals(sender, _confirmDelete))
        {
            _ = ConfirmAsync(document);
        }
        else if (ReferenceEquals(sender, _reindex) || ReferenceEquals(sender, _retry))
        {
            var keyboard = (sender as Control)?.IsKeyboardFocusWithin == true;
            _owner.Reindex(document);
            if (keyboard)
            {
                // The pressed button turns off (Re-index) or away (Retry) while the run is under way: Delete keeps the focus.
                _delete?.Focus(NavigationMethod.Tab);
            }
        }
    }

    private async Task ConfirmAsync(KnowledgeDocument document)
    {
        var index = _owner.Items.IndexOf(document);
        var removed = await _owner.RemoveAsync(document);
        if (!removed)
        {
            SetConfirming(false, focus: true);
            return;
        }
        // Focus moves to the row that took this one's place, or to the one above it; with none left, to Add files.
        var count = _owner.Items.Count;
        if (count == 0)
        {
            if (_owner.AddButton is { IsEffectivelyVisible: true } add)
            {
                add.Focus(NavigationMethod.Tab);
            }
            else
            {
                _owner.SearchBox?.Focus(NavigationMethod.Tab);
            }
            return;
        }
        _owner.FocusRow(Math.Min(index, count - 1), KnowledgeBaseRowAction.Delete, NavigationMethod.Tab);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && e.Source is Visual source && _owner.MoveFromRow(this, source, e.Key))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnConfirmKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _confirming)
        {
            e.Handled = true;
            SetConfirming(false, focus: true);
        }
    }

    /// <summary>Opens or closes the delete question for this row's document; with <paramref name="focus"/>, focus goes to Cancel when it opens and back to Delete when it closes.</summary>
    internal void SetConfirming(bool value, bool focus)
    {
        if (_document is not { } document)
        {
            return;
        }
        _owner.SetConfirmingDocument(value ? document : null, document);
        if (focus)
        {
            _owner.FocusAfterConfirm(document, value);
        }
    }

    /// <summary>Focuses Cancel (the question is open) or Delete (it is closed).</summary>
    internal bool FocusConfirmTarget(bool open) =>
        (open ? _cancel : _delete) is { IsEffectivelyVisible: true } button && (button.IsFocused || button.Focus(NavigationMethod.Tab));

    private void ShowConfirming(bool value)
    {
        if (_confirming == value)
        {
            return;
        }
        _confirming = value;
        PseudoClasses.Set(ConfirmingPseudoClass, value);
        if (value && _confirm is not null)
        {
            // Fades in from nothing at full and reduced motion (the transition is in the theme); Off shows it at once.
            if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
            {
                _confirm.Opacity = 0;
                Dispatcher.UIThread.Post(() =>
                {
                    if (_confirm is not null)
                    {
                        _confirm.Opacity = 1;
                    }
                }, DispatcherPriority.Render);
            }
            else
            {
                _confirm.Opacity = 1;
            }
        }
    }

    /// <summary>Which button of this row holds <paramref name="source"/> (Delete when none).</summary>
    internal KnowledgeBaseRowAction FocusedAction(Visual source)
    {
        if (_reindex is not null && (ReferenceEquals(source, _reindex) || _reindex.IsVisualAncestorOf(source)))
        {
            return KnowledgeBaseRowAction.Reindex;
        }
        if (_retry is not null && (ReferenceEquals(source, _retry) || _retry.IsVisualAncestorOf(source)))
        {
            return KnowledgeBaseRowAction.Retry;
        }
        return KnowledgeBaseRowAction.Delete;
    }

    /// <summary>Focuses the row's button for <paramref name="action"/>, or the nearest one that can take it.</summary>
    internal bool FocusAction(KnowledgeBaseRowAction action, NavigationMethod method)
    {
        Button?[] order = action switch
        {
            KnowledgeBaseRowAction.Reindex => [_reindex, _retry, _delete],
            KnowledgeBaseRowAction.Retry => [_retry, _reindex, _delete],
            _ => [_delete, _reindex, _retry]
        };
        foreach (var button in order)
        {
            if (button is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } && button.Focus(method))
            {
                return true;
            }
        }
        return false;
    }

    #endregion
}
