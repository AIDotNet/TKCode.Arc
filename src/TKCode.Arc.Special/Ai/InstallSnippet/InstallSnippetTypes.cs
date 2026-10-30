using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The package manager whose command step 1 of an <see cref="InstallSnippet"/> shows.</summary>
public enum InstallSnippetPackageManager
{
    /// <summary><c>npm install</c>.</summary>
    Npm,

    /// <summary><c>pnpm add</c>.</summary>
    Pnpm,

    /// <summary><c>yarn add</c>.</summary>
    Yarn,

    /// <summary><c>bun add</c>.</summary>
    Bun
}

/// <summary>One of the three numbered steps of an <see cref="InstallSnippet"/>.</summary>
public enum InstallSnippetStep
{
    /// <summary>Step 1: install the SDK.</summary>
    Install,

    /// <summary>Step 2: add the API key.</summary>
    Key,

    /// <summary>Step 3: send the first event.</summary>
    Code
}

/// <summary>Where the Generate key button of an <see cref="InstallSnippet"/> is.</summary>
public enum InstallSnippetKeyState
{
    /// <summary>No key has been asked for yet (or the app passes its own key).</summary>
    Idle,

    /// <summary>The key generator is running.</summary>
    Generating,

    /// <summary>A key was generated and is in the snippets.</summary>
    Ready,

    /// <summary>The key generator failed; the button reads Try again.</summary>
    Failed
}

/// <summary>
/// One snippet of an <see cref="InstallSnippetFramework"/>: the file it goes in, its code and one line saying what to do
/// with it. Write <c>{{key}}</c> where the API key goes.
/// </summary>
public sealed class InstallSnippetFile : AvaloniaObject
{
    /// <summary>The file name in the code block's header, such as <c>.env</c> or <c>app.py</c>; "Terminal" for a command.</summary>
    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<InstallSnippetFile, string?>(nameof(FileName));

    /// <summary>The code; <c>{{key}}</c> marks where the API key goes. Keep it under ten lines.</summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<InstallSnippetFile, string?>(nameof(Code));

    /// <summary>The highlighting language (<c>bash</c>, <c>ts</c>, <c>python</c>, <c>go</c>, <c>html</c>, ...); null infers it from the file name.</summary>
    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<InstallSnippetFile, string?>(nameof(Language));

    /// <summary>The line under the step's title; null uses the step's default from the labels.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<InstallSnippetFile, string?>(nameof(Description));

    /// <summary>Creates an empty snippet.</summary>
    public InstallSnippetFile()
    {
    }

    /// <summary>Creates a snippet for a file.</summary>
    public InstallSnippetFile(string? fileName, string? code, string? description = null, string? language = null)
    {
        FileName = fileName;
        Code = code;
        Description = description;
        Language = language;
    }

    /// <inheritdoc cref="FileNameProperty"/>
    public string? FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }

    /// <inheritdoc cref="CodeProperty"/>
    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <inheritdoc cref="LanguageProperty"/>
    public string? Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>Raised when any property changes (the owning framework passes it on).</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// One framework tab of an <see cref="InstallSnippet"/>: its id and label, an optional mark, and the three snippets.
/// Step 1 is a package install when <see cref="Packages"/> is set (the reader picks npm, pnpm, yarn or bun) and
/// <see cref="Install"/> otherwise (a command such as <c>pip install</c>, or a script tag).
/// </summary>
public sealed class InstallSnippetFramework : AvaloniaObject
{
    /// <summary>A stable id, such as <c>node</c>. The ids <c>node</c>, <c>python</c>, <c>go</c> and <c>html</c> get a mark of their own.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<InstallSnippetFramework, string?>(nameof(Id));

    /// <summary>The tab's name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<InstallSnippetFramework, string?>(nameof(Label));

    /// <summary>The tab's mark, a 24 × 24 stroked glyph; null uses the built-in mark of a known id, or none.</summary>
    public static readonly StyledProperty<Geometry?> MarkProperty =
        AvaloniaProperty.Register<InstallSnippetFramework, Geometry?>(nameof(Mark));

    /// <summary>Package names for step 1, separated by spaces (<c>@acme/sdk</c>); the command follows the package manager.</summary>
    public static readonly StyledProperty<string?> PackagesProperty =
        AvaloniaProperty.Register<InstallSnippetFramework, string?>(nameof(Packages));

    /// <summary>Step 1 when there are no <see cref="Packages"/>: a command or a script tag.</summary>
    public static readonly StyledProperty<InstallSnippetFile?> InstallProperty =
        AvaloniaProperty.Register<InstallSnippetFramework, InstallSnippetFile?>(nameof(Install));

    /// <summary>Step 2: where the key goes, with <c>{{key}}</c> in the code.</summary>
    public static readonly StyledProperty<InstallSnippetFile?> KeyProperty =
        AvaloniaProperty.Register<InstallSnippetFramework, InstallSnippetFile?>(nameof(Key));

    /// <summary>Step 3: the code that sends the first event.</summary>
    public static readonly StyledProperty<InstallSnippetFile?> CodeProperty =
        AvaloniaProperty.Register<InstallSnippetFramework, InstallSnippetFile?>(nameof(Code));

    /// <summary>The description of step 1 for a package install; null uses the label's default.</summary>
    public static readonly StyledProperty<string?> PackagesDescriptionProperty =
        AvaloniaProperty.Register<InstallSnippetFramework, string?>(nameof(PackagesDescription));

    /// <summary>Creates an empty framework.</summary>
    public InstallSnippetFramework()
    {
    }

    /// <summary>Creates a framework with its id and label.</summary>
    public InstallSnippetFramework(string id, string label)
    {
        Id = id;
        Label = label;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public Geometry? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="PackagesProperty"/>
    public string? Packages { get => GetValue(PackagesProperty); set => SetValue(PackagesProperty, value); }

    /// <inheritdoc cref="InstallProperty"/>
    public InstallSnippetFile? Install { get => GetValue(InstallProperty); set => SetValue(InstallProperty, value); }

    /// <inheritdoc cref="KeyProperty"/>
    public InstallSnippetFile? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="CodeProperty"/>
    public InstallSnippetFile? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <inheritdoc cref="PackagesDescriptionProperty"/>
    public string? PackagesDescription { get => GetValue(PackagesDescriptionProperty); set => SetValue(PackagesDescriptionProperty, value); }

    /// <summary>Whether step 1 is a package install with the package manager tabs.</summary>
    internal bool UsesPackages => !string.IsNullOrWhiteSpace(Packages);

    /// <summary>The id, or the label when the id is empty.</summary>
    internal string TabId => string.IsNullOrEmpty(Id) ? Label ?? string.Empty : Id;

    /// <summary>Raised when the framework or one of its snippets changes.</summary>
    internal event EventHandler? Changed;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InstallProperty || change.Property == KeyProperty || change.Property == CodeProperty)
        {
            if (change.OldValue is InstallSnippetFile old)
            {
                old.Changed -= OnFileChanged;
            }
            if (change.NewValue is InstallSnippetFile file)
            {
                file.Changed += OnFileChanged;
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnFileChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Arguments of <see cref="InstallSnippet.FrameworkChanged"/>.</summary>
public sealed class InstallSnippetFrameworkChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public InstallSnippetFrameworkChangedEventArgs(RoutedEvent routedEvent, string frameworkId) : base(routedEvent)
    {
        FrameworkId = frameworkId;
    }

    /// <summary>The id of the framework the reader picked.</summary>
    public string FrameworkId { get; }
}

/// <summary>Arguments of <see cref="InstallSnippet.PackageManagerChanged"/>.</summary>
public sealed class InstallSnippetPackageManagerChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public InstallSnippetPackageManagerChangedEventArgs(RoutedEvent routedEvent, InstallSnippetPackageManager packageManager)
        : base(routedEvent)
    {
        PackageManager = packageManager;
    }

    /// <summary>The package manager the reader picked.</summary>
    public InstallSnippetPackageManager PackageManager { get; }
}

/// <summary>Arguments of <see cref="InstallSnippet.KeyGenerated"/>.</summary>
public sealed class InstallSnippetKeyGeneratedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public InstallSnippetKeyGeneratedEventArgs(RoutedEvent routedEvent, string key, bool isSimulated) : base(routedEvent)
    {
        Key = key;
        IsSimulated = isSimulated;
    }

    /// <summary>The new key, as it now appears in the snippets.</summary>
    public string Key { get; }

    /// <summary>True when no key generator was set and the key is an invented demo key (nothing was sent).</summary>
    public bool IsSimulated { get; }
}

/// <summary>Arguments of <see cref="InstallSnippet.Copied"/>.</summary>
public sealed class InstallSnippetCopiedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public InstallSnippetCopiedEventArgs(RoutedEvent routedEvent, InstallSnippetStep step, string value) : base(routedEvent)
    {
        Step = step;
        Value = value;
    }

    /// <summary>The step whose snippet was copied.</summary>
    public InstallSnippetStep Step { get; }

    /// <summary>The exact text put on the clipboard.</summary>
    public string Value { get; }
}
