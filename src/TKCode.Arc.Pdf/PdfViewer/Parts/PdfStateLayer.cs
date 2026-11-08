using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// The cover over the pages while there are none to show: a spinner while a document opens, the reason when it failed, the
/// password form. It exists from the first time one of these is needed and hides while pages show.
/// </summary>
[TemplatePart(FormPart, typeof(ContentPresenter))]
[PseudoClasses(LoadingPseudoClass, FailedPseudoClass, PasswordPseudoClass)]
internal sealed class PdfStateLayer : TemplatedControl
{
    public const string FormPart = "PART_Form";
    public const string LoadingPseudoClass = ":loading";
    public const string FailedPseudoClass = ":failed";
    public const string PasswordPseudoClass = ":password";

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<PdfStateLayer, string?>(nameof(Message));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<PdfStateLayer, string?>(nameof(Detail));

    private readonly PdfViewer _viewer;
    private ContentPresenter? _formHost;
    private PdfException? _error;
    private ArcLanguageManager? _languages;

    public PdfStateLayer(PdfViewer viewer)
    {
        _viewer = viewer;
        IsVisible = false;
    }

    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <summary>The password form while it is up.</summary>
    public PdfPasswordForm? Form { get; private set; }

    public void ShowLoading() => Set(LoadingPseudoClass);

    public void ShowFailed(PdfException error)
    {
        RemoveForm();
        _error = error;
        UpdateFailureText();
        Set(FailedPseudoClass);
    }

    /// <summary>Shows the form; on a retry it says the password was wrong and shakes.</summary>
    public void ShowPasswordForm(bool isRetry)
    {
        if (Form is null)
        {
            Form = new PdfPasswordForm(_viewer);
            if (_formHost is { } host)
            {
                host.Content = Form;
            }
        }
        Set(PasswordPseudoClass);
        Form.Prompt(isRetry);
    }

    public void Hide()
    {
        RemoveForm();
        _error = null;
        Set(null);
    }

    private void Set(string? state)
    {
        PseudoClasses.Set(LoadingPseudoClass, state == LoadingPseudoClass);
        PseudoClasses.Set(FailedPseudoClass, state == FailedPseudoClass);
        PseudoClasses.Set(PasswordPseudoClass, state == PasswordPseudoClass);
        IsVisible = state is not null;
    }

    /// <summary>The form goes away with what it held.</summary>
    private void RemoveForm()
    {
        if (_formHost is { } host)
        {
            host.Content = null;
        }
        Form = null;
    }

    private void UpdateFailureText()
    {
        if (_error is not { } error)
        {
            return;
        }
        var (message, detail) = error.Kind switch
        {
            PdfErrorKind.BackendUnavailable => (PdfViewerLangResourceKind.NoBackend, PdfViewerLangResourceKind.BackendHint),
            PdfErrorKind.NotFound => (PdfViewerLangResourceKind.CouldNotOpen, PdfViewerLangResourceKind.FileNotFound),
            PdfErrorKind.Corrupt => (PdfViewerLangResourceKind.CouldNotOpen, PdfViewerLangResourceKind.FileDamaged),
            PdfErrorKind.PasswordRequired => (PdfViewerLangResourceKind.CouldNotOpen, PdfViewerLangResourceKind.PasswordRequired),
            _ => (PdfViewerLangResourceKind.CouldNotOpen, (PdfViewerLangResourceKind?)null)
        };
        Message = PdfViewerLangResources.Get(message);
        Detail = detail is { } kind ? PdfViewerLangResources.Get(kind) : null;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _formHost = e.NameScope.Find<ContentPresenter>(FormPart);
        if (_formHost is { } host)
        {
            host.Content = Form;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _languages = ArcLanguageManager.Current;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        UpdateFailureText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e) =>
        Dispatcher.UIThread.Post(UpdateFailureText);
}
