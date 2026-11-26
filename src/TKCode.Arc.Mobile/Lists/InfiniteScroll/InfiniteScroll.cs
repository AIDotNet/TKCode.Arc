using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Mobile;

/// <summary>Data of <see cref="InfiniteScroll.LoadMoreRequested"/>. An asynchronous handler may take a deferral.</summary>
public sealed class LoadMoreRequestedEventArgs : EventArgs
{
    private readonly Action _completed;
    private int _pending;
    private bool _deferred;
    private bool _sealed;

    internal LoadMoreRequestedEventArgs(Action completed)
    {
        _completed = completed;
    }

    /// <summary>
    /// Takes a deferral: <see cref="InfiniteScroll.IsLoading"/> stays true until every taken deferral is disposed, then
    /// clears automatically on the UI thread. Safe to dispose on any thread.
    /// </summary>
    public IDisposable GetDeferral()
    {
        _deferred = true;
        Interlocked.Increment(ref _pending);
        return new Deferral(this);
    }

    internal void Seal()
    {
        _sealed = true;
        if (_deferred && Volatile.Read(ref _pending) == 0)
        {
            _completed();
        }
    }

    private void Release()
    {
        if (Interlocked.Decrement(ref _pending) == 0 && _sealed)
        {
            _completed();
        }
    }

    private sealed class Deferral(LoadMoreRequestedEventArgs owner) : IDisposable
    {
        private int _done;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
            {
                owner.Release();
            }
        }
    }
}

/// <summary>
/// Mobile infinite-scroll container: wraps list content inside a vertical <see cref="ScrollViewer"/> and triggers
/// <see cref="LoadMoreRequested"/> and <see cref="Command"/> automatically as the person scrolls within
/// <see cref="Threshold"/> pixels of the bottom. Shows a loading spinner while <see cref="IsLoading"/> is true,
/// <see cref="FinishedText"/> when <see cref="HasMore"/> becomes false, and a tap-to-retry footer when
/// <see cref="HasError"/> is true. Composes cleanly inside <see cref="PullToRefresh"/>.
/// </summary>
[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer))]
[TemplatePart("PART_Footer", typeof(Control))]
[TemplatePart("PART_Spinner", typeof(Control))]
[TemplatePart("PART_StatusText", typeof(TextBlock))]
[PseudoClasses(":loading", ":finished", ":error")]
public class InfiniteScroll : ContentControl
{
    /// <summary>Whether a page fetch is currently in progress (two-way).</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<InfiniteScroll, bool>(nameof(IsLoading), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether more items remain to be fetched: true.</summary>
    public static readonly StyledProperty<bool> HasMoreProperty =
        AvaloniaProperty.Register<InfiniteScroll, bool>(nameof(HasMore), true);

    /// <summary>Whether the last load failed, turning the footer into a tap-to-retry target: false.</summary>
    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<InfiniteScroll, bool>(nameof(HasError));

    /// <summary>Distance in px from the bottom of the scroll viewport at which loading triggers: 80.</summary>
    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<InfiniteScroll, double>(nameof(Threshold), 80);

    /// <summary>Text shown next to the spinner while <see cref="IsLoading"/> is true: "Loading more…".</summary>
    public static readonly StyledProperty<string?> LoadingTextProperty =
        AvaloniaProperty.Register<InfiniteScroll, string?>(nameof(LoadingText), "Loading more…");

    /// <summary>Text shown at the bottom once <see cref="HasMore"/> is false: "No more items".</summary>
    public static readonly StyledProperty<string?> FinishedTextProperty =
        AvaloniaProperty.Register<InfiniteScroll, string?>(nameof(FinishedText), "No more items");

    /// <summary>Text shown in the retry footer while <see cref="HasError"/> is true: "Failed to load. Tap to retry.".</summary>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AvaloniaProperty.Register<InfiniteScroll, string?>(nameof(ErrorText), "Failed to load. Tap to retry.");

    /// <summary>Command executed when more items are requested, passed <see cref="CommandParameter"/>.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<InfiniteScroll, ICommand?>(nameof(Command));

    /// <summary>Optional parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<InfiniteScroll, object?>(nameof(CommandParameter));

    private ScrollViewer? _scroller;
    private Control? _footer;
    private Control? _spinner;
    private TextBlock? _statusText;
    private int _loadGeneration;

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <inheritdoc cref="HasMoreProperty"/>
    public bool HasMore
    {
        get => GetValue(HasMoreProperty);
        set => SetValue(HasMoreProperty, value);
    }

    /// <inheritdoc cref="HasErrorProperty"/>
    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    /// <inheritdoc cref="ThresholdProperty"/>
    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    /// <inheritdoc cref="LoadingTextProperty"/>
    public string? LoadingText
    {
        get => GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    /// <inheritdoc cref="FinishedTextProperty"/>
    public string? FinishedText
    {
        get => GetValue(FinishedTextProperty);
        set => SetValue(FinishedTextProperty, value);
    }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText
    {
        get => GetValue(ErrorTextProperty);
        set => SetValue(ErrorTextProperty, value);
    }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>Raised when scrolling reaches the bottom threshold or the user retries from an error.</summary>
    public event EventHandler<LoadMoreRequestedEventArgs>? LoadMoreRequested;

    /// <summary>The inner scroll viewer, exposed for tests.</summary>
    internal ScrollViewer? InnerScrollViewer => _scroller;

    protected override Type StyleKeyOverride => typeof(InfiniteScroll);

    /// <summary>Starts loading the next page when enabled, idle, not errored and <see cref="HasMore"/> is true.</summary>
    public bool TryLoadMore()
    {
        if (!IsEffectivelyEnabled || IsLoading || !HasMore || HasError)
        {
            return false;
        }
        StartLoad();
        return true;
    }

    /// <summary>Clears <see cref="HasError"/> and requests the next page again.</summary>
    public void Retry()
    {
        if (!IsEffectivelyEnabled || IsLoading || !HasMore)
        {
            return;
        }
        if (HasError)
        {
            SetCurrentValue(HasErrorProperty, false);
            Haptics.Trigger(HapticKind.Light);
        }
        StartLoad();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _scroller = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _footer = e.NameScope.Find<Control>("PART_Footer");
        _spinner = e.NameScope.Find<Control>("PART_Spinner");
        _statusText = e.NameScope.Find<TextBlock>("PART_StatusText");

        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
        if (_footer is not null)
        {
            _footer.PointerPressed += OnFooterPointerPressed;
        }
        UpdateFooterState();
    }

    private void DetachParts()
    {
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }
        if (_footer is not null)
        {
            _footer.PointerPressed -= OnFooterPointerPressed;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLoadingProperty || change.Property == HasMoreProperty
            || change.Property == HasErrorProperty || change.Property == LoadingTextProperty
            || change.Property == FinishedTextProperty || change.Property == ErrorTextProperty)
        {
            UpdateFooterState();
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroller is null || !IsEffectivelyEnabled || IsLoading || !HasMore || HasError)
        {
            return;
        }
        var extent = _scroller.Extent.Height;
        var viewport = _scroller.Viewport.Height;
        if (extent <= viewport || viewport <= 0)
        {
            return;
        }
        var offsetY = _scroller.Offset.Y;
        if (e.OffsetDelta.Y <= 0 || offsetY <= 0)
        {
            return;
        }
        var remaining = extent - viewport - offsetY;
        var threshold = Math.Max(0, Threshold);
        if (remaining <= threshold)
        {
            StartLoad();
        }
    }

    private void OnFooterPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!HasError || !IsEffectivelyEnabled)
        {
            return;
        }
        e.Handled = true;
        Retry();
    }

    private void StartLoad()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        SetCurrentValue(IsLoadingProperty, true);

        var args = new LoadMoreRequestedEventArgs(() =>
        {
            void Finish()
            {
                if (Volatile.Read(ref _loadGeneration) == generation)
                {
                    SetCurrentValue(IsLoadingProperty, false);
                }
            }
            if (Dispatcher.UIThread.CheckAccess())
            {
                Finish();
            }
            else
            {
                Dispatcher.UIThread.Post(Finish);
            }
        });

        LoadMoreRequested?.Invoke(this, args);
        args.Seal();

        var command = Command;
        var parameter = CommandParameter;
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private void UpdateFooterState()
    {
        var loading = IsLoading;
        var error = !loading && HasError;
        var finished = !loading && !error && !HasMore;

        PseudoClasses.Set(":loading", loading);
        PseudoClasses.Set(":error", error);
        PseudoClasses.Set(":finished", finished);

        if (_spinner is not null)
        {
            _spinner.IsVisible = loading;
        }
        if (_statusText is not null)
        {
            if (loading)
            {
                _statusText.Text = LoadingText;
            }
            else if (error)
            {
                _statusText.Text = ErrorText;
            }
            else if (finished)
            {
                _statusText.Text = FinishedText;
            }
            else
            {
                _statusText.Text = string.Empty;
            }
        }
        if (_footer is not null)
        {
            _footer.IsVisible = loading || error || finished;
        }
    }
}
