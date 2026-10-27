using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The edit sheet's rules (internal, pure).</summary>
internal static partial class ProfilePageValidation
{
    // An optional lower-case http:// or https://, two or more dot-separated labels of ASCII letters, digits, "_" or "-"
    // (no port), then an optional path that starts with "/" and has no whitespace.
    [GeneratedRegex(@"^(?:https?://)?[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)+(?:/\S*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex WebsitePattern();

    /// <summary>A name is required: something other than white space.</summary>
    public static bool IsNameValid(string? raw) => !string.IsNullOrWhiteSpace(raw);

    /// <summary>A website is optional (an empty field is fine); otherwise its trimmed value must look like a web address.</summary>
    public static bool IsWebsiteValid(string? raw) => string.IsNullOrEmpty(raw) || WebsitePattern().IsMatch(raw.Trim());

    /// <summary>The fields whose trimmed values differ.</summary>
    public static ProfileFields Changed(ProfileDetails before, ProfileDetails after)
    {
        var changed = ProfileFields.None;
        if (T(before.Name) != T(after.Name))
        {
            changed |= ProfileFields.Name;
        }
        if (T(before.Headline) != T(after.Headline))
        {
            changed |= ProfileFields.Headline;
        }
        if (T(before.Location) != T(after.Location))
        {
            changed |= ProfileFields.Location;
        }
        if (T(before.Website) != T(after.Website))
        {
            changed |= ProfileFields.Website;
        }
        if (T(before.Bio) != T(after.Bio))
        {
            changed |= ProfileFields.Bio;
        }
        return changed;
    }

    private static string T(string? value) => (value ?? string.Empty).Trim();
}

/// <summary>
/// The edit sheet (internal), inside the page: a header with the title and a close button, the fields (Name, Headline,
/// Location and Website side by side, Bio with a characters-left counter), an error strip for a failed save and the
/// footer with Cancel and Save. Errors stay hidden until the first submit and then follow every keystroke. Enter in a
/// one-line field submits, Escape dismisses, Tab cycles inside the sheet.
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(ClosePart, typeof(ProfilePageButton))]
[TemplatePart(NameBoxPart, typeof(TextBox))]
[TemplatePart(HeadlineBoxPart, typeof(TextBox))]
[TemplatePart(LocationBoxPart, typeof(TextBox))]
[TemplatePart(WebsiteBoxPart, typeof(TextBox))]
[TemplatePart(BioBoxPart, typeof(TextBox))]
[TemplatePart(PairPart, typeof(ProfilePageColumns))]
[TemplatePart(CancelPart, typeof(ProfilePageButton))]
[TemplatePart(SavePart, typeof(ProfilePageButton))]
[TemplatePart(SaveStackPart, typeof(ProfilePageSwapStack))]
[TemplatePart(AlertHostPart, typeof(PresenceHost))]
internal sealed class ProfilePageSheet : TemplatedControl
{
    public const string SurfacePart = "PART_Surface";
    public const string TitlePart = "PART_Title";
    public const string ClosePart = "PART_Close";
    public const string NameLabelPart = "PART_NameLabel";
    public const string NameBoxPart = "PART_NameBox";
    public const string NameErrorPart = "PART_NameError";
    public const string HeadlineLabelPart = "PART_HeadlineLabel";
    public const string HeadlineBoxPart = "PART_HeadlineBox";
    public const string LocationLabelPart = "PART_LocationLabel";
    public const string LocationBoxPart = "PART_LocationBox";
    public const string WebsiteLabelPart = "PART_WebsiteLabel";
    public const string WebsiteBoxPart = "PART_WebsiteBox";
    public const string WebsiteErrorPart = "PART_WebsiteError";
    public const string BioLabelPart = "PART_BioLabel";
    public const string BioBoxPart = "PART_BioBox";
    public const string BioCounterPart = "PART_BioCounter";
    public const string PairPart = "PART_Pair";
    public const string AlertHostPart = "PART_AlertHost";
    public const string AlertTextPart = "PART_AlertText";
    public const string CancelPart = "PART_Cancel";
    public const string SavePart = "PART_Save";
    public const string SaveStackPart = "PART_SaveStack";

    private TextBlock? _title;
    private ProfilePageButton? _close;
    private TextBlock? _nameLabel, _headlineLabel, _locationLabel, _websiteLabel, _bioLabel;
    private TextBox? _name, _headline, _location, _website, _bio;
    private TextBlock? _nameError, _websiteError, _bioCounter, _alertText;
    private ProfilePageColumns? _pair;
    private PresenceHost? _alertHost;
    private ProfilePageButton? _cancel, _save;
    private ProfilePageSwapStack? _saveStack;
    private TextBlock? _saveLabel, _savingLabel;
    private ProfileDetails _source = new();
    private ProfilePageLabels _labels = new();
    private int _bioMax = 200;
    private bool _submitted;
    private bool _isSaving;
    private bool _loading;
    private int _pairColumns = 2;

    /// <summary>Raised by Save or Enter in a one-line field.</summary>
    public event EventHandler? SubmitRequested;

    /// <summary>Raised by Escape, the close button and Cancel.</summary>
    public event EventHandler? DismissRequested;

    public TextBox? NameBox => _name;
    public TextBox? HeadlineBox => _headline;
    public TextBox? LocationBox => _location;
    public TextBox? WebsiteBox => _website;
    public TextBox? BioBox => _bio;
    public ProfilePageButton? CloseButton => _close;
    public ProfilePageButton? CancelButton => _cancel;
    public ProfilePageButton? SaveButton => _save;
    public TextBlock? NameError => _nameError;
    public TextBlock? WebsiteError => _websiteError;
    public TextBlock? BioCounter => _bioCounter;
    public TextBlock? AlertText => _alertText;
    public PresenceHost? AlertHost => _alertHost;
    public ProfilePageColumns? Pair => _pair;
    public ProfilePageSwapStack? SaveStack => _saveStack;

    /// <summary>Whether the draft differs from the profile (raw strings: a trailing space counts).</summary>
    public bool IsDirty =>
        Raw(_name) != _source.Name || Raw(_headline) != _source.Headline || Raw(_location) != _source.Location ||
        Raw(_website) != _source.Website || Raw(_bio) != _source.Bio;

    public bool HasSubmitted => _submitted;

    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            _isSaving = value;
            if (_saveStack is not null)
            {
                _saveStack.Shown = value ? 1 : 0;
            }
            UpdateSaveEnabled();
        }
    }

    /// <summary>Whether the error strip shows a message.</summary>
    public bool HasAlert => _alertHost?.IsPresent == true;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _close = e.NameScope.Find<ProfilePageButton>(ClosePart);
        _nameLabel = e.NameScope.Find<TextBlock>(NameLabelPart);
        _headlineLabel = e.NameScope.Find<TextBlock>(HeadlineLabelPart);
        _locationLabel = e.NameScope.Find<TextBlock>(LocationLabelPart);
        _websiteLabel = e.NameScope.Find<TextBlock>(WebsiteLabelPart);
        _bioLabel = e.NameScope.Find<TextBlock>(BioLabelPart);
        _name = e.NameScope.Find<TextBox>(NameBoxPart);
        _headline = e.NameScope.Find<TextBox>(HeadlineBoxPart);
        _location = e.NameScope.Find<TextBox>(LocationBoxPart);
        _website = e.NameScope.Find<TextBox>(WebsiteBoxPart);
        _bio = e.NameScope.Find<TextBox>(BioBoxPart);
        _nameError = e.NameScope.Find<TextBlock>(NameErrorPart);
        _websiteError = e.NameScope.Find<TextBlock>(WebsiteErrorPart);
        _bioCounter = e.NameScope.Find<TextBlock>(BioCounterPart);
        _alertText = e.NameScope.Find<TextBlock>(AlertTextPart);
        _pair = e.NameScope.Find<ProfilePageColumns>(PairPart);
        _alertHost = e.NameScope.Find<PresenceHost>(AlertHostPart);
        _cancel = e.NameScope.Find<ProfilePageButton>(CancelPart);
        _save = e.NameScope.Find<ProfilePageButton>(SavePart);
        _saveStack = e.NameScope.Find<ProfilePageSwapStack>(SaveStackPart);
        if (_saveStack is not null && _saveStack.Children.Count == 0)
        {
            _saveStack.AddLayer(ProfilePageSwapStack.GlyphLabel(null, _labels.Save, out var save));
            _saveLabel = save;
            var spinner = new ArcSpinner { Width = 15, Height = 15, Thickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
            spinner.Bind(ArcSpinner.ForegroundProperty, this.GetObservable(ForegroundProperty));
            _saveStack.AddLayer(ProfilePageSwapStack.GlyphLabel(null, _labels.Saving, out var saving, spinner));
            _savingLabel = saving;
            _saveStack.Shown = _isSaving ? 1 : 0;
        }
        foreach (var box in OneLineBoxes())
        {
            box.AddHandler(KeyDownEvent, OnOneLineKeyDown, RoutingStrategies.Tunnel);
            box.TextChanged += OnTextChanged;
        }
        if (_bio is not null)
        {
            _bio.TextChanged += OnTextChanged;
        }
        if (_close is not null)
        {
            _close.Click += OnDismissClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnDismissClick;
        }
        if (_save is not null)
        {
            _save.Click += OnSaveClick;
        }
        if (_pair is not null)
        {
            _pair.Columns = _pairColumns;
        }
        ApplyLabels();
        Fill();
    }

    private IEnumerable<TextBox> OneLineBoxes()
    {
        foreach (var box in new[] { _name, _headline, _location, _website })
        {
            if (box is not null)
            {
                yield return box;
            }
        }
    }

    private void Unhook()
    {
        foreach (var box in OneLineBoxes())
        {
            box.RemoveHandler(KeyDownEvent, OnOneLineKeyDown);
            box.TextChanged -= OnTextChanged;
        }
        if (_bio is not null)
        {
            _bio.TextChanged -= OnTextChanged;
        }
        if (_close is not null)
        {
            _close.Click -= OnDismissClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnDismissClick;
        }
        if (_save is not null)
        {
            _save.Click -= OnSaveClick;
        }
    }

    /// <summary>Starts a new draft from <paramref name="profile"/>: errors and the error strip hidden, nothing submitted.</summary>
    public void Load(ProfileDetails profile, ProfilePageLabels labels, int bioMax)
    {
        _source = profile;
        _labels = labels;
        _bioMax = Math.Max(1, bioMax);
        _submitted = false;
        _isSaving = false;
        if (_saveStack is not null)
        {
            _saveStack.Shown = 0;
        }
        ShowAlert(null);
        ApplyLabels();
        Fill();
    }

    public void SetLabels(ProfilePageLabels labels)
    {
        _labels = labels;
        ApplyLabels();
        Revalidate();
    }

    /// <summary>The page's width: the sheet is min(420, width) wide; under 420 Location and Website stack.</summary>
    public void UpdateWidth(double pageWidth, double sheetWidth)
    {
        if (!(pageWidth > 0))
        {
            return;
        }
        Width = Math.Min(sheetWidth, pageWidth);
        _pairColumns = pageWidth < 420 ? 1 : 2;
        if (_pair is not null)
        {
            _pair.Columns = _pairColumns;
        }
    }

    private void Fill()
    {
        _loading = true;
        try
        {
            if (_name is not null)
            {
                _name.Text = _source.Name;
            }
            if (_headline is not null)
            {
                _headline.Text = _source.Headline;
            }
            if (_location is not null)
            {
                _location.Text = _source.Location;
            }
            if (_website is not null)
            {
                _website.Text = _source.Website;
            }
            if (_bio is not null)
            {
                _bio.MaxLength = _bioMax;
                _bio.Text = _source.Bio.Length > _bioMax ? _source.Bio[.._bioMax] : _source.Bio;
            }
        }
        finally
        {
            _loading = false;
        }
        Revalidate();
    }

    private void ApplyLabels()
    {
        Set(_title, _labels.EditProfile);
        Set(_nameLabel, _labels.NameField);
        Set(_headlineLabel, _labels.HeadlineField);
        Set(_locationLabel, _labels.LocationField);
        Set(_websiteLabel, _labels.WebsiteField);
        Set(_bioLabel, _labels.BioField);
        Set(_nameError, _labels.NameRequired);
        Set(_websiteError, _labels.WebsiteInvalid);
        Set(_saveLabel, _labels.Save);
        Set(_savingLabel, _labels.Saving);
        if (_cancel is not null)
        {
            _cancel.Content = _labels.Cancel;
        }
        if (_close is not null)
        {
            AutomationProperties.SetName(_close, _labels.Close);
        }
        if (_save is not null)
        {
            AutomationProperties.SetName(_save, _isSaving ? _labels.Saving : _labels.Save);
        }
        AutomationProperties.SetName(this, _labels.EditProfile);
        NameBox(_name, _labels.NameField);
        NameBox(_headline, _labels.HeadlineField);
        NameBox(_location, _labels.LocationField);
        NameBox(_website, _labels.WebsiteField);
        NameBox(_bio, _labels.BioField);
        UpdateCounter();

        static void Set(TextBlock? block, string text)
        {
            if (block is not null)
            {
                block.Text = text;
            }
        }

        static void NameBox(TextBox? box, string text)
        {
            if (box is not null)
            {
                AutomationProperties.SetName(box, text);
            }
        }
    }

    /// <summary>The trimmed draft, keeping what the sheet does not edit (handle, pictures, joined text).</summary>
    public ProfileDetails BuildDraft()
    {
        var draft = _source.Clone();
        draft.Name = Raw(_name).Trim();
        draft.Headline = Raw(_headline).Trim();
        draft.Location = Raw(_location).Trim();
        draft.Website = Raw(_website).Trim();
        draft.Bio = Raw(_bio).Trim();
        return draft;
    }

    /// <summary>Shows the errors from now on; returns whether the draft is valid.</summary>
    public bool Submit()
    {
        _submitted = true;
        return Revalidate();
    }

    private bool Revalidate()
    {
        var nameOk = ProfilePageValidation.IsNameValid(Raw(_name));
        var websiteOk = ProfilePageValidation.IsWebsiteValid(Raw(_website));
        SetError(_name, _nameError, _submitted && !nameOk);
        SetError(_website, _websiteError, _submitted && !websiteOk);
        UpdateCounter();
        UpdateSaveEnabled();
        return nameOk && websiteOk;
    }

    private static void SetError(TextBox? box, TextBlock? error, bool show)
    {
        if (error is not null)
        {
            error.IsVisible = show;
        }
        if (box is not null)
        {
            box.Classes.Set("invalid", show);
            AutomationProperties.SetHelpText(box, show ? error?.Text : null);
        }
    }

    private void UpdateCounter()
    {
        if (_bioCounter is not null)
        {
            _bioCounter.Text = _labels.Remaining(Math.Max(0, _bioMax - Raw(_bio).Length));
        }
    }

    private void UpdateSaveEnabled()
    {
        if (_save is not null)
        {
            _save.IsEnabled = !_isSaving && IsDirty;
            AutomationProperties.SetName(_save, _isSaving ? _labels.Saving : _labels.Save);
        }
    }

    /// <summary>Shows <paramref name="message"/> in the error strip (role alert), or hides it.</summary>
    public void ShowAlert(string? message)
    {
        if (_alertText is not null && message is not null)
        {
            _alertText.Text = message;
            if (_alertText.Parent is Control strip)
            {
                AutomationProperties.SetName(strip, message);
            }
        }
        if (_alertHost is not null)
        {
            _alertHost.IsPresent = message is not null;
        }
    }

    /// <summary>Focuses the name field (the first field).</summary>
    public void FocusFirst(NavigationMethod method) => _name?.Focus(method);

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }
        Revalidate();
    }

    private void OnOneLineKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            SubmitRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            DismissRequested?.Invoke(this, EventArgs.Empty);
            return;
        }
        base.OnKeyDown(e);
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SubmitRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string Raw(TextBox? box) => box?.Text ?? string.Empty;
}
