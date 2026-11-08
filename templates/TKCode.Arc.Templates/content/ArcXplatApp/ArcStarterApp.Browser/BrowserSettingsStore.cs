using System.Runtime.InteropServices.JavaScript;
using ArcStarterApp.Settings;

namespace ArcStarterApp.Browser;

/// <summary>Keeps the settings in the page's local storage (wwwroot/main.js provides the two functions).</summary>
internal sealed partial class BrowserSettingsStore : ISettingsStore
{
    private const string Key = "ArcStarterApp.settings";

    public string? Read()
    {
        try
        {
            return ReadItem(Key);
        }
        catch (JSException)
        {
            // Storage switched off (a private window in some browsers): no saved settings.
            return null;
        }
    }

    public void Write(string json)
    {
        try
        {
            WriteItem(Key, json);
        }
        catch (JSException)
        {
        }
    }

    [JSImport("settings.read", "main.js")]
    private static partial string? ReadItem(string key);

    [JSImport("settings.write", "main.js")]
    private static partial void WriteItem(string key, string value);
}
