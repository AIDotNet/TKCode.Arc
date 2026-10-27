namespace TKCode.Arc;

/// <summary>The state of a group checkbox for one role.</summary>
internal enum RolesPermissionsCheck
{
    Off,
    On,
    Mixed,
}

/// <summary>
/// The change model of <see cref="RolesPermissions"/>, free of UI so it can be tested on its own. It holds the saved
/// grants (the baseline) and the current ones; a cell is unsaved when the two differ. A duplicated role has no saved
/// state: it counts as one change, and its baseline is what it was copied with, so later edits in its column show dots and
/// count too. Locked roles have every permission and never change.
/// </summary>
internal sealed class RolesPermissionsModel
{
    private readonly HashSet<RolesPermissionsGrant> _saved = new();
    private readonly HashSet<RolesPermissionsGrant> _current = new();
    private readonly Dictionary<string, HashSet<string>> _newBaselines = new(StringComparer.Ordinal);

    /// <summary>Replaces the saved and current grants (current null = same as saved) and forgets the new roles' baselines.</summary>
    public void Reset(IEnumerable<RolesPermissionsGrant>? saved, IEnumerable<RolesPermissionsGrant>? current)
    {
        _saved.Clear();
        _current.Clear();
        _newBaselines.Clear();
        if (saved is not null)
        {
            _saved.UnionWith(saved);
        }
        _current.UnionWith(current ?? _saved);
    }

    /// <summary>Replaces the current grants only (an app writing <c>Grants</c>), keeping the baselines of new roles.</summary>
    public void SetCurrent(IEnumerable<RolesPermissionsGrant> current)
    {
        _current.Clear();
        _current.UnionWith(current);
    }

    /// <summary>Replaces the saved grants only (an app writing <c>SavedGrants</c>).</summary>
    public void SetSaved(IEnumerable<RolesPermissionsGrant> saved)
    {
        _saved.Clear();
        _saved.UnionWith(saved);
    }

    public IReadOnlySet<RolesPermissionsGrant> Saved => _saved;

    public IReadOnlySet<RolesPermissionsGrant> Current => _current;

    public bool IsNew(RolesPermissionsRole role) => _newBaselines.ContainsKey(role.Key);

    public bool IsGranted(RolesPermissionsRole role, string permission)
        => role.IsLocked || _current.Contains(new RolesPermissionsGrant(role.Key, permission));

    /// <summary>The value the cell goes back to on Discard or Revert (a new role's copy-time value).</summary>
    public bool IsBaseline(RolesPermissionsRole role, string permission)
    {
        if (role.IsLocked)
        {
            return true;
        }
        if (_newBaselines.TryGetValue(role.Key, out var baseline))
        {
            return baseline.Contains(permission);
        }
        return _saved.Contains(new RolesPermissionsGrant(role.Key, permission));
    }

    public bool IsUnsaved(RolesPermissionsRole role, string permission)
        => !role.IsLocked && IsGranted(role, permission) != IsBaseline(role, permission);

    /// <summary>Sets one cell; returns false when it already had the value or the role is locked.</summary>
    public bool Set(RolesPermissionsRole role, string permission, bool value)
    {
        if (role.IsLocked)
        {
            return false;
        }
        var grant = new RolesPermissionsGrant(role.Key, permission);
        return value ? _current.Add(grant) : _current.Remove(grant);
    }

    public RolesPermissionsCheck GroupState(RolesPermissionsRole role, IReadOnlyList<string> permissions)
    {
        if (permissions.Count == 0)
        {
            return role.IsLocked ? RolesPermissionsCheck.On : RolesPermissionsCheck.Off;
        }
        var on = 0;
        foreach (var permission in permissions)
        {
            if (IsGranted(role, permission))
            {
                on++;
            }
        }
        return on == 0 ? RolesPermissionsCheck.Off : on == permissions.Count ? RolesPermissionsCheck.On : RolesPermissionsCheck.Mixed;
    }

    /// <summary>
    /// The group checkbox rule: mixed or off switches every permission on, on switches every one off. Returns the
    /// permissions that changed (in group order) and the new value.
    /// </summary>
    public (List<string> Changed, bool Value) ToggleGroup(RolesPermissionsRole role, IReadOnlyList<string> permissions)
    {
        var changed = new List<string>();
        if (role.IsLocked)
        {
            return (changed, true);
        }
        var value = GroupState(role, permissions) != RolesPermissionsCheck.On;
        foreach (var permission in permissions)
        {
            if (Set(role, permission, value))
            {
                changed.Add(permission);
            }
        }
        return (changed, value);
    }

    /// <summary>How many cells of the role differ from its baseline.</summary>
    public int CountCellChanges(RolesPermissionsRole role, IEnumerable<string> permissions)
    {
        if (role.IsLocked)
        {
            return 0;
        }
        var count = 0;
        foreach (var permission in permissions)
        {
            if (IsUnsaved(role, permission))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>Whether the role shows the unsaved dot in its header: it is new or has a changed cell.</summary>
    public bool RoleHasChanges(RolesPermissionsRole role, IEnumerable<string> permissions)
        => IsNew(role) || CountCellChanges(role, permissions) > 0;

    /// <summary>The review bar's count: one per changed cell, plus one per new role.</summary>
    public int PendingCount(IEnumerable<RolesPermissionsRole> roles, IReadOnlyList<string> permissions)
    {
        var count = 0;
        foreach (var role in roles)
        {
            if (IsNew(role))
            {
                count++;
            }
            count += CountCellChanges(role, permissions);
        }
        return count;
    }

    /// <summary>Adds a duplicated role with the source's current values; they are its baseline.</summary>
    public void AddNewRole(RolesPermissionsRole source, RolesPermissionsRole copy, IEnumerable<string> permissions)
    {
        var baseline = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in permissions)
        {
            if (IsGranted(source, permission))
            {
                baseline.Add(permission);
                _current.Add(new RolesPermissionsGrant(copy.Key, permission));
            }
        }
        _newBaselines[copy.Key] = baseline;
    }

    /// <summary>Forgets a role: its grants and its baseline.</summary>
    public void RemoveRole(string roleKey)
    {
        _current.RemoveWhere(g => g.RoleKey == roleKey);
        _saved.RemoveWhere(g => g.RoleKey == roleKey);
        _newBaselines.Remove(roleKey);
    }

    /// <summary>Puts the role's cells back to their baseline; returns the permissions that changed.</summary>
    public List<string> RevertRole(RolesPermissionsRole role, IEnumerable<string> permissions)
    {
        var changed = new List<string>();
        foreach (var permission in permissions)
        {
            if (IsUnsaved(role, permission) && Set(role, permission, IsBaseline(role, permission)))
            {
                changed.Add(permission);
            }
        }
        return changed;
    }

    /// <summary>Makes the current grants the saved ones; new roles become ordinary roles.</summary>
    public void Commit()
    {
        _saved.Clear();
        _saved.UnionWith(_current);
        _newBaselines.Clear();
    }

    /// <summary>The cells that differ from the saved grants, for a save request (column then row order).</summary>
    public List<RolesPermissionsChange> Changes(IEnumerable<RolesPermissionsRole> roles, IReadOnlyList<string> permissions)
    {
        var list = new List<RolesPermissionsChange>();
        foreach (var role in roles)
        {
            if (role.IsLocked)
            {
                continue;
            }
            foreach (var permission in permissions)
            {
                var grant = new RolesPermissionsGrant(role.Key, permission);
                var now = _current.Contains(grant);
                if (now != _saved.Contains(grant))
                {
                    list.Add(new RolesPermissionsChange(role.Key, permission, now));
                }
            }
        }
        return list;
    }

    /// <summary>
    /// The name of a copy: "<paramref name="name"/> copy", then "… copy 2", "… copy 3" while the name is taken (a copy of a
    /// copy is "X copy copy"). The formats come from the labels: {0} is the source name, {1} the number.
    /// </summary>
    public static string CopyName(string name, string format, string numberedFormat, ICollection<string> taken)
    {
        var first = ProjectsText.Format(format, name);
        if (!taken.Contains(first))
        {
            return first;
        }
        for (var n = 2; ; n++)
        {
            var candidate = ProjectsText.Format(numberedFormat, name, n);
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>A key no role uses yet: "<paramref name="sourceKey"/>-copy", "-copy-2", ...</summary>
    public static string CopyKey(string sourceKey, ICollection<string> taken)
    {
        var first = sourceKey + "-copy";
        if (!taken.Contains(first))
        {
            return first;
        }
        for (var n = 2; ; n++)
        {
            var candidate = first + "-" + n;
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// The review bar's second line: the changed roles in column order ("A", "A and B", "A, B and C", "A, B and 2 more")
    /// then the people they reach, or "nobody holds them yet" when only new roles changed.
    /// </summary>
    public static string Summary(IReadOnlyList<RolesPermissionsRole> changedRoles, RolesPermissionsLabels labels)
    {
        if (changedRoles.Count == 0)
        {
            return string.Empty;
        }
        string names;
        var count = changedRoles.Count;
        if (count == 1)
        {
            names = changedRoles[0].Name;
        }
        else if (count == 2)
        {
            names = ProjectsText.Format(labels.TwoRolesFormat, changedRoles[0].Name, changedRoles[1].Name);
        }
        else if (count == 3)
        {
            names = ProjectsText.Format(labels.ThreeRolesFormat, changedRoles[0].Name, changedRoles[1].Name, changedRoles[2].Name);
        }
        else
        {
            names = ProjectsText.Format(labels.ManyRolesFormat, changedRoles[0].Name, changedRoles[1].Name, count - 2);
        }
        var people = 0;
        foreach (var role in changedRoles)
        {
            people += Math.Max(0, role.MemberCount);
        }
        return people == 0
            ? ProjectsText.Format(labels.NoMembersFormat, names)
            : ProjectsText.Format(people == 1 ? labels.ReachOneFormat : labels.ReachFormat, names, people);
    }
}
