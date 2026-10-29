using Avalonia.Animation.Easings;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>Raised when a theme state cannot be applied; the previous theme stays in place.</summary>
public sealed class ArcThemeException : Exception
{
    /// <summary>Creates the exception from the individual problems.</summary>
    /// <param name="errors">One message per rejected override.</param>
    public ArcThemeException(IReadOnlyList<string> errors)
        : base("The theme was not applied:" + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", errors))
    {
        Errors = errors;
    }

    /// <summary>One message per rejected override.</summary>
    public IReadOnlyList<string> Errors { get; }
}

/// <summary>Computes token values (calculation, then overrides) and publishes them into a resource dictionary.</summary>
internal static class ArcTokenCalculator
{
    /// <summary>
    /// Computes every token of <paramref name="descriptors"/> for <paramref name="state"/>. Pure: touches no Avalonia
    /// object tree, so it can run on any thread. Throws <see cref="ArcThemeException"/> listing every bad override.
    /// </summary>
    public static Dictionary<object, object?> Compute(IReadOnlyList<ArcTokenDescriptor> descriptors, int tokenCount,
                                                      ArcThemeState state, bool isDark)
    {
        var seed = new ArcSeedToken(state.Accent ?? ArcSeedToken.DefaultAccent, state.MotionLevel);
        List<string>? errors = null;
        var used = state.Overrides.Count == 0 ? null : new HashSet<string>(StringComparer.Ordinal);
        var result = new Dictionary<object, object?>(tokenCount);
        var buffer = new object?[16];

        foreach (var descriptor in descriptors)
        {
            var token = descriptor.CreateToken();
            token.EffectiveGlobalToken = seed;
            token.CalculateTokenValues(isDark);

            // Presets first (in state order), then the state's own overrides, so the app wins over every preset.
            foreach (var preset in state.Presets)
            {
                ApplyPreset(descriptor, token, preset, isDark, ref errors);
            }

            if (used is not null)
            {
                if (descriptor.IsGlobal)
                {
                    ApplyOverrides(descriptor, token, state, ArcThemeState.GlobalIdentity, used, ref errors);
                }
                ApplyOverrides(descriptor, token, state, descriptor.Identity, used, ref errors);
            }

            var count = descriptor.Keys.Count;
            if (buffer.Length < count)
            {
                buffer = new object?[count];
            }
            descriptor.Write(token, buffer);
            var keys = descriptor.Keys;
            for (var i = 0; i < count; i++)
            {
                result[keys[i]] = buffer[i];
            }
        }

        if (used is not null)
        {
            foreach (var identity in state.Overrides.Keys)
            {
                if (!used.Contains(identity))
                {
                    errors ??= new List<string>();
                    errors.Add(identity == ArcThemeState.GlobalIdentity
                        ? "Token overrides name no identity, but no global token identity is registered."
                        : $"No token identity named '{identity}' is registered (is its package enabled in UseArc?).");
                }
            }
        }

        if (errors is not null)
        {
            throw new ArcThemeException(errors);
        }
        return result;
    }

    /// <summary>
    /// Applies a preset's overrides for one identity. Unlike the state's overrides, a preset naming an identity that is not
    /// registered is not an error (a preset covers packages the app may not enable), so nothing is recorded as used.
    /// </summary>
    private static void ApplyPreset(ArcTokenDescriptor descriptor, AbstractControlDesignToken token, ArcThemePreset preset,
                                    bool isDark, ref List<string>? errors)
    {
        var overrides = preset.TokensFor(isDark);
        if (overrides.Count == 0)
        {
            return;
        }
        if (descriptor.IsGlobal && overrides.TryGetValue(ArcThemePreset.GlobalIdentity, out var global))
        {
            SetTokens(descriptor, token, preset, global, ref errors);
        }
        if (overrides.TryGetValue(descriptor.Identity, out var own))
        {
            SetTokens(descriptor, token, preset, own, ref errors);
        }
    }

    private static void SetTokens(ArcTokenDescriptor descriptor, AbstractControlDesignToken token, ArcThemePreset preset,
                                  IReadOnlyDictionary<string, string> overrides, ref List<string>? errors)
    {
        foreach (var (name, value) in overrides)
        {
            if (!descriptor.TrySetToken(token, name, value, out var error))
            {
                errors ??= new List<string>();
                errors.Add($"Preset '{preset.Name}': " + (error ?? $"Token '{descriptor.Identity}.{name}' rejected '{value}'."));
            }
        }
    }

    private static void ApplyOverrides(ArcTokenDescriptor descriptor, AbstractControlDesignToken token, ArcThemeState state,
                                       string identity, HashSet<string> used, ref List<string>? errors)
    {
        if (!state.Overrides.TryGetValue(identity, out var overrides))
        {
            return;
        }
        used.Add(identity);
        foreach (var (name, value) in overrides)
        {
            if (!descriptor.TrySetToken(token, name, value, out var error))
            {
                errors ??= new List<string>();
                errors.Add(error ?? $"Token '{descriptor.Identity}.{name}' rejected '{value}'.");
            }
        }
    }

    /// <summary>
    /// Writes <paramref name="values"/> into <paramref name="target"/>, touching only keys whose value changed, in one
    /// <see cref="ResourceDictionary.SetItems"/> call (one ResourcesChanged notification). Unchanged keys keep their
    /// instances, so targets bound to them see no property change.
    /// </summary>
    /// <returns>The number of keys written.</returns>
    public static int Publish(ResourceDictionary target, Dictionary<object, object?> values)
    {
        List<KeyValuePair<object, object?>>? changed = null;
        var empty = target.Count == 0;
        foreach (var pair in values)
        {
            if (!empty && target.TryGetValue(pair.Key, out var current) && SameValue(current, pair.Value))
            {
                continue;
            }
            changed ??= new List<KeyValuePair<object, object?>>(empty ? values.Count : 64);
            changed.Add(pair);
        }
        if (changed is null)
        {
            return 0;
        }
        if (empty)
        {
            target.EnsureCapacity(values.Count);
        }
        target.SetItems(changed);
        return changed.Count;
    }

    /// <summary>
    /// Value equality of published values. Brushes, shadows, fonts and structs implement it; easings do not, and a token
    /// calculation creates new ones every time, so they are compared by type and control points.
    /// </summary>
    private static bool SameValue(object? current, object? next)
    {
        if (Equals(current, next))
        {
            return true;
        }
        return (current, next) switch
        {
            (SplineEasing a, SplineEasing b) => a.X1 == b.X1 && a.Y1 == b.Y1 && a.X2 == b.X2 && a.Y2 == b.Y2,
            // Avalonia's other built-in easings carry no state; a custom easing class might.
            (Easing a, Easing b) => a.GetType() == b.GetType() && a is not SplineEasing
                                    && a.GetType().Assembly == typeof(Easing).Assembly,
            _ => false
        };
    }
}
