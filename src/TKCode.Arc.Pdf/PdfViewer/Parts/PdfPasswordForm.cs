using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The built-in password form. The field is emptied the moment a password is handed over, and it never undoes back to one.
/// A wrong password shows the field's error state and shakes the form once (full motion only). The shake is code-driven
/// (<see cref="MotionValue{T}"/>) because it is a keyframe run fired by an event, not a state a ControlTheme can select.
/// </summary>
[TemplatePart(FieldPart, typeof(PasswordField))]
[TemplatePart(OpenPart, typeof(Button))]
[TemplatePart(CancelPart, typeof(Button))]
[PseudoClasses(BusyPseudoClass)]
internal sealed class PdfPasswordForm : TemplatedControl
{
    public const string FieldPart = "PART_Password";
    public const string OpenPart = "PART_Open";
    public const string CancelPart = "PART_Cancel";
    public const string BusyPseudoClass = ":busy";

    private static readonly double[] s_shake = [0, -8, 8, -6, 4, 0];
    private static readonly TimeSpan s_shakeDuration = TimeSpan.FromMilliseconds(320);

    private readonly PdfViewer _viewer;
    private readonly TranslateTransform _translate = new();
    private PasswordField? _field;
    private Button? _open;
    private Button? _cancel;
    private MotionValue<double>? _shake;
    private bool _isRetry;
    private bool _pendingPrompt;

    public PdfPasswordForm(PdfViewer viewer)
    {
        _viewer = viewer;
        RenderTransform = _translate;
    }

    /// <summary>Shakes played so far (tests).</summary>
    internal int ShakeCount { get; private set; }

    /// <summary>Whether the form can take input right now.</summary>
    internal bool IsBusy => PseudoClasses.Contains(BusyPseudoClass);

    internal PasswordField? Field => _field;

    /// <summary>Waits for a password.</summary>
    public void Prompt(bool isRetry)
    {
        _isRetry = isRetry;
        SetBusy(false);
        if (_field is null)
        {
            _pendingPrompt = true;
            return;
        }
        ApplyPrompt();
    }

    private void ApplyPrompt()
    {
        var field = _field!;
        field.IsInvalid = _isRetry;
        field.ErrorMessage = _isRetry ? PdfViewerLangResources.Get(PdfViewerLangResourceKind.PasswordIncorrect) : null;
        if (_isRetry)
        {
            Shake();
        }
        // After layout: a field that is only now being shown does not take focus yet.
        Dispatcher.UIThread.Post(() => field.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>Hands the field's text to the viewer and empties the field; the form waits for the answer.</summary>
    internal void Submit()
    {
        if (_field is not { } field || IsBusy)
        {
            return;
        }
        var password = field.Text;
        if (string.IsNullOrEmpty(password))
        {
            field.Focus();
            return;
        }
        field.Text = null;
        field.IsRevealed = false;
        SetBusy(true);
        _viewer.OnFormSubmitted(password);
    }

    internal void Cancel()
    {
        if (_field is { } field)
        {
            field.Text = null;
        }
        _viewer.OnFormCancelled();
    }

    private void SetBusy(bool busy)
    {
        PseudoClasses.Set(BusyPseudoClass, busy);
        if (_field is { } field)
        {
            field.IsEnabled = !busy;
        }
        if (_open is { } open)
        {
            open.IsLoading = busy;
        }
    }

    private void Shake()
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        ShakeCount++;
        if (_shake is null)
        {
            _shake = new MotionValue<double>(this, 1, MotionVectors.Double);
            _shake.Changed += OnShakeChanged;
        }
        _shake.Jump(0);
        _ = _shake.AnimateTo(1, MotionSpec.Tween(s_shakeDuration, new Avalonia.Animation.Easings.LinearEasing()));
    }

    private void OnShakeChanged(double progress)
    {
        var position = Math.Clamp(progress, 0, 1) * (s_shake.Length - 1);
        var segment = Math.Min(s_shake.Length - 2, (int)position);
        _translate.X = s_shake[segment] + (s_shake[segment + 1] - s_shake[segment]) * (position - segment);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_open is not null)
        {
            _open.Click -= OnOpenClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        _field = e.NameScope.Find<PasswordField>(FieldPart);
        _open = e.NameScope.Find<Button>(OpenPart);
        _cancel = e.NameScope.Find<Button>(CancelPart);
        if (_open is not null)
        {
            _open.Click += OnOpenClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_pendingPrompt && _field is not null)
        {
            _pendingPrompt = false;
            ApplyPrompt();
        }
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e) => Submit();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Cancel();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Enter)
        {
            Submit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
        }
    }
}
