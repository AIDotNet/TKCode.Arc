using Avalonia.Metadata;

// Mobile controls have their own XAML namespace so they can sit next to the desktop controls of the same name.
// Core's tokens, motion and primitives stay under the "arc" namespace.
[assembly: XmlnsDefinition("https://github.com/AIDotNet/TKCode.Arc.Mobile", "TKCode.Arc.Mobile")]
[assembly: XmlnsDefinition("https://github.com/AIDotNet/TKCode.Arc.Mobile", "TKCode.Arc.Mobile.DesignTokens")]
[assembly: XmlnsPrefix("https://github.com/AIDotNet/TKCode.Arc.Mobile", "arcm")]
