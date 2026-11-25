// Ported from vscode (src/vs/editor/common/viewEventHandler.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - upstream extends Disposable; this implements IDisposable with a virtual Dispose that does nothing by default;
//  - HandleEvents casts each event to the class its Type names; upstream's default branch (console.info for an
//    unknown event) cannot be reached with the closed ViewEventType and is left out.

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>Receives view events and remembers whether it has to render again.</summary>
internal class ViewEventHandler : IDisposable
{
    private bool _shouldRender;

    public ViewEventHandler()
    {
        _shouldRender = true;
    }

    public virtual void Dispose()
    {
    }

    public bool ShouldRender()
    {
        return _shouldRender;
    }

    public void ForceShouldRender()
    {
        _shouldRender = true;
    }

    protected void SetShouldRender()
    {
        _shouldRender = true;
    }

    public void OnDidRender()
    {
        _shouldRender = false;
    }

    // --- begin event handlers

    public virtual bool OnCompositionStart(ViewCompositionStartEvent e)
    {
        return false;
    }

    public virtual bool OnCompositionEnd(ViewCompositionEndEvent e)
    {
        return false;
    }

    public virtual bool OnConfigurationChanged(ViewConfigurationChangedEvent e)
    {
        return false;
    }

    public virtual bool OnCursorStateChanged(ViewCursorStateChangedEvent e)
    {
        return false;
    }

    public virtual bool OnDecorationsChanged(ViewDecorationsChangedEvent e)
    {
        return false;
    }

    public virtual bool OnFlushed(ViewFlushedEvent e)
    {
        return false;
    }

    public virtual bool OnFocusChanged(ViewFocusChangedEvent e)
    {
        return false;
    }

    public virtual bool OnLanguageConfigurationChanged(ViewLanguageConfigurationEvent e)
    {
        return false;
    }

    public virtual bool OnLineMappingChanged(ViewLineMappingChangedEvent e)
    {
        return false;
    }

    public virtual bool OnLinesChanged(ViewLinesChangedEvent e)
    {
        return false;
    }

    public virtual bool OnLinesDeleted(ViewLinesDeletedEvent e)
    {
        return false;
    }

    public virtual bool OnLinesInserted(ViewLinesInsertedEvent e)
    {
        return false;
    }

    public virtual bool OnRevealRangeRequest(ViewRevealRangeRequestEvent e)
    {
        return false;
    }

    public virtual bool OnScrollChanged(ViewScrollChangedEvent e)
    {
        return false;
    }

    public virtual bool OnThemeChanged(ViewThemeChangedEvent e)
    {
        return false;
    }

    public virtual bool OnTokensChanged(ViewTokensChangedEvent e)
    {
        return false;
    }

    public virtual bool OnTokensColorsChanged(ViewTokensColorsChangedEvent e)
    {
        return false;
    }

    public virtual bool OnZonesChanged(ViewZonesChangedEvent e)
    {
        return false;
    }

    // --- end event handlers

    public virtual void HandleEvents(IReadOnlyList<ViewEvent> events)
    {
        var shouldRender = false;

        for (int i = 0, len = events.Count; i < len; i++)
        {
            var e = events[i];

            var handled = e.Type switch
            {
                ViewEventType.ViewCompositionStart => OnCompositionStart((ViewCompositionStartEvent)e),
                ViewEventType.ViewCompositionEnd => OnCompositionEnd((ViewCompositionEndEvent)e),
                ViewEventType.ViewConfigurationChanged => OnConfigurationChanged((ViewConfigurationChangedEvent)e),
                ViewEventType.ViewCursorStateChanged => OnCursorStateChanged((ViewCursorStateChangedEvent)e),
                ViewEventType.ViewDecorationsChanged => OnDecorationsChanged((ViewDecorationsChangedEvent)e),
                ViewEventType.ViewFlushed => OnFlushed((ViewFlushedEvent)e),
                ViewEventType.ViewFocusChanged => OnFocusChanged((ViewFocusChangedEvent)e),
                ViewEventType.ViewLanguageConfigurationChanged => OnLanguageConfigurationChanged((ViewLanguageConfigurationEvent)e),
                ViewEventType.ViewLineMappingChanged => OnLineMappingChanged((ViewLineMappingChangedEvent)e),
                ViewEventType.ViewLinesChanged => OnLinesChanged((ViewLinesChangedEvent)e),
                ViewEventType.ViewLinesDeleted => OnLinesDeleted((ViewLinesDeletedEvent)e),
                ViewEventType.ViewLinesInserted => OnLinesInserted((ViewLinesInsertedEvent)e),
                ViewEventType.ViewRevealRangeRequest => OnRevealRangeRequest((ViewRevealRangeRequestEvent)e),
                ViewEventType.ViewScrollChanged => OnScrollChanged((ViewScrollChangedEvent)e),
                ViewEventType.ViewTokensChanged => OnTokensChanged((ViewTokensChangedEvent)e),
                ViewEventType.ViewThemeChanged => OnThemeChanged((ViewThemeChangedEvent)e),
                ViewEventType.ViewTokensColorsChanged => OnTokensColorsChanged((ViewTokensColorsChangedEvent)e),
                ViewEventType.ViewZonesChanged => OnZonesChanged((ViewZonesChangedEvent)e),
                _ => false,
            };

            if (handled)
            {
                shouldRender = true;
            }
        }

        if (shouldRender)
        {
            _shouldRender = true;
        }
    }
}
