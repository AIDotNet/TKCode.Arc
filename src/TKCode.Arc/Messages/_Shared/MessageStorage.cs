using System.Collections.Concurrent;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Where Messages controls remember choices that Arc keeps in <c>localStorage</c>: the dismissal of an
/// <see cref="AnnouncementBar"/> and the consent saved by <see cref="CookieConsent"/>. Replace
/// <see cref="MessageStorage.Default"/> (or set a control's <c>Storage</c>) to keep them elsewhere.
/// </summary>
public interface IMessageStorage
{
    /// <summary>Returns the stored value, or null when nothing is stored (or storage is unavailable).</summary>
    string? Read(string key);

    /// <summary>Stores a value; null removes it. Failures are swallowed like blocked browser storage.</summary>
    void Write(string key, string? value);
}

/// <summary>The storage used by Messages controls when they have no storage of their own.</summary>
public static class MessageStorage
{
    private static IMessageStorage? s_default;

    /// <summary>
    /// Shared default storage: a <see cref="FileMessageStorage"/> in the local application data folder of the
    /// running application. Set it at startup to use another store (settings service, tests).
    /// </summary>
    public static IMessageStorage Default
    {
        get => s_default ??= new FileMessageStorage();
        set => s_default = value ?? throw new ArgumentNullException(nameof(value));
    }
}

/// <summary>Keeps values in memory for the lifetime of the instance (previews and tests).</summary>
public sealed class InMemoryMessageStorage : IMessageStorage
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string? Read(string key) => _values.TryGetValue(key, out var value) ? value : null;

    /// <inheritdoc />
    public void Write(string key, string? value)
    {
        if (value is null)
        {
            _values.TryRemove(key, out _);
        }
        else
        {
            _values[key] = value;
        }
    }
}

/// <summary>
/// Stores each key as a small UTF-8 file. The default folder is
/// <c>&lt;LocalApplicationData&gt;/TKCode.Arc/&lt;application name&gt;/messages</c>.
/// </summary>
public sealed class FileMessageStorage : IMessageStorage
{
    /// <summary>Creates a storage in <paramref name="directory"/>, or in the default folder when null.</summary>
    public FileMessageStorage(string? directory = null)
    {
        Directory = directory ?? DefaultDirectory();
    }

    /// <summary>The folder holding the stored values.</summary>
    public string Directory { get; }

    /// <inheritdoc />
    public string? Read(string key)
    {
        try
        {
            var path = PathOf(key);
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void Write(string key, string? value)
    {
        try
        {
            var path = PathOf(key);
            if (value is null)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return;
            }
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(path, value, Encoding.UTF8);
        }
        catch (IOException)
        {
            // Storage can be unavailable; the controls still report the choice.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }

    private string PathOf(string key)
    {
        var builder = new StringBuilder(key.Length);
        foreach (var c in key)
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        }
        // A short hash keeps keys that differ only in replaced characters apart.
        var hash = Fnv(key);
        builder.Append('-').Append(hash.ToString("x8"));
        return Path.Combine(Directory, builder + ".txt");
    }

    private static uint Fnv(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash = (hash ^ c) * 16777619u;
        }
        return hash;
    }

    private static string DefaultDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(root))
        {
            root = Path.GetTempPath();
        }
        var app = AppDomain.CurrentDomain.FriendlyName;
        if (string.IsNullOrWhiteSpace(app))
        {
            app = "app";
        }
        return Path.Combine(root, "TKCode.Arc", app, "messages");
    }
}
