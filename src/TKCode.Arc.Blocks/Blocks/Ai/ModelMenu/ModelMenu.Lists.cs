using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

public partial class ModelMenu
{
    /// <summary>The cached lists: the main list, the two submenus and the two pages of a narrow window.</summary>
    private enum ViewSlot
    {
        Main,
        EffortSubmenu,
        MoreSubmenu,
        EffortPage,
        MorePage
    }

    private readonly ModelMenuPageView?[] _views = new ModelMenuPageView?[5];
    private readonly int[] _viewVersions = new int[5];
    private int _version = 1;

    /// <summary>
    /// Data, selection or labels changed: lists are rebuilt (at once for the ones on screen, else on demand). A pick closes
    /// the menu right away, so its lists wait for the next open.
    /// </summary>
    private void InvalidatePages()
    {
        _version++;
        if (IsMainShown && !_picking)
        {
            RefreshShown();
        }
    }

    private ModelMenuPageView View(ViewSlot slot)
    {
        var index = (int)slot;
        if (_views[index] is { } cached && _viewVersions[index] == _version)
        {
            return cached;
        }
        var view = slot switch
        {
            ViewSlot.Main => BuildMain(),
            ViewSlot.EffortSubmenu => BuildEffort(withBack: false),
            ViewSlot.MoreSubmenu => BuildMore(withBack: false),
            ViewSlot.EffortPage => BuildEffort(withBack: true),
            _ => BuildMore(withBack: true)
        };
        _views[index] = view;
        _viewVersions[index] = _version;
        return view;
    }

    /// <summary>Shows the rebuilt lists in the open panels, keeping the highlight (and focus) on the same entry.</summary>
    private void RefreshShown()
    {
        if (_mainPanel is { } main)
        {
            var slot = main.Page switch
            {
                ModelMenuPage.Effort => ViewSlot.EffortPage,
                ModelMenuPage.More => ViewSlot.MorePage,
                _ => ViewSlot.Main
            };
            Replace(main, View(slot));
            if (_openSub is { } open && main.View is { } mainView)
            {
                // The open submenu's row was rebuilt too.
                var trigger = ActionRow(mainView, open == ModelMenuPage.Effort ? ModelMenuRowKind.Effort : ModelMenuRowKind.More);
                if (trigger is null)
                {
                    CloseSubmenu(animated: true);
                }
                else
                {
                    _subTrigger = trigger;
                    trigger.IsExpanded = true;
                    if (OpenSubPresence is { } presence)
                    {
                        presence.Popup.SubmenuAnchor = topLevel => SubmenuAnchorFor(trigger, topLevel);
                    }
                }
            }
        }
        if (OpenSubPresence is { } sub)
        {
            Replace(sub.Panel, View(_openSub == ModelMenuPage.Effort ? ViewSlot.EffortSubmenu : ViewSlot.MoreSubmenu));
        }
    }

    private static void Replace(ModelMenuPanel panel, ModelMenuPageView view)
    {
        if (ReferenceEquals(panel.View, view))
        {
            return;
        }
        var active = panel.Active;
        var focused = active?.IsKeyboardFocusWithin == true;
        panel.ShowView(view, 0);
        if (active is null)
        {
            return;
        }
        foreach (var row in view.Rows)
        {
            if (SameEntry(row, active))
            {
                panel.SetActive(row, ModelMenuMove.Keyboard);
                if (focused)
                {
                    row.Focus(NavigationMethod.Directional);
                }
                return;
            }
        }
        if (focused)
        {
            panel.Focus(NavigationMethod.Pointer);
        }
    }

    private static bool SameEntry(ModelMenuItem a, ModelMenuItem b) => (a, b) switch
    {
        (ModelMenuModelRow x, ModelMenuModelRow y) => ReferenceEquals(x.Model, y.Model),
        (ModelMenuEffortRow x, ModelMenuEffortRow y) => x.Level == y.Level,
        (ModelMenuActionRow x, ModelMenuActionRow y) => x.Kind == y.Kind,
        (ModelMenuEffortStrip, ModelMenuEffortStrip) => true,
        _ => false
    };

    private bool IsInline => EffortLayout == ModelMenuEffortLayout.Inline;

    /// <summary>The chat's simple list: one short row per model (not with the inline effort layout).</summary>
    internal bool IsSimple => Layout == ModelMenuLayout.Simple && !IsInline;

    /// <summary>The models of the main list: the featured ones, and without "More models" every other one after them.</summary>
    private List<AiModel> ListedModels()
    {
        var listed = new List<AiModel>(_featured);
        if (!ShowMoreModels)
        {
            foreach (var model in _all)
            {
                if (!_featured.Contains(model))
                {
                    listed.Add(model);
                }
            }
        }
        return listed;
    }

    private ModelMenuPageView BuildMain()
    {
        var labels = Labels;
        var root = new StackPanel();
        var rows = new List<ModelMenuItem>();
        if (_all.Count == 0)
        {
            var empty = new ModelMenuGroupHeader { Text = labels.Empty };
            empty.Classes.Add("empty");
            root.Children.Add(empty);
            return new ModelMenuPageView(ModelMenuPage.Main, root, rows, null);
        }
        var inline = IsInline;
        var listed = ListedModels();
        var featured = new ModelMenuGroup();
        // Without "More models" the group is every model, not a selection of them.
        AutomationProperties.SetName(featured, ShowMoreModels ? labels.Featured : labels.Menu);
        foreach (var model in listed)
        {
            var row = ModelRow(model, compact: false, inline);
            featured.Children.Add(row);
            rows.Add(row);
        }
        root.Children.Add(featured);
        var selected = SelectedModel;
        var showsEffort = selected?.SupportsEffort == true;
        var showsMore = ShowMoreModels && _other.Count > 0;
        if (inline)
        {
            return BuildInlineMain(root, rows, listed.Count > 0, showsMore, showsEffort ? selected : null);
        }
        if (listed.Count > 0 && (showsEffort || showsMore))
        {
            root.Children.Add(new ModelMenuSeparator());
        }
        if (showsEffort)
        {
            var effort = new ModelMenuActionRow(ModelMenuRowKind.Effort)
            {
                Label = labels.Effort,
                Value = Effort is { } level ? labels.EffortName(level) : null,
                HasSubmenu = !_paged
            };
            root.Children.Add(effort);
            rows.Add(effort);
        }
        if (showsMore)
        {
            root.Children.Add(MoreRow(rows, selected));
        }
        return new ModelMenuPageView(ModelMenuPage.Main, root, rows, null);
    }

    private ModelMenuActionRow MoreRow(List<ModelMenuItem> rows, AiModel? selected)
    {
        var more = new ModelMenuActionRow(ModelMenuRowKind.More)
        {
            Label = Labels.MoreModels,
            // The selected model is named here when it is not one of the featured rows.
            Value = selected is not null && _other.Contains(selected) ? selected.Name : null,
            HasSubmenu = !_paged
        };
        rows.Add(more);
        return more;
    }

    /// <summary>
    /// The rest of the inline layout after the models: "More models" (when shown), then the effort section of a model with
    /// levels: a separator, the "Effort" header with the current level's description, and the segmented row (one stop).
    /// </summary>
    private ModelMenuPageView BuildInlineMain(StackPanel root, List<ModelMenuItem> rows, bool hasModels, bool showsMore, AiModel? effortModel)
    {
        var labels = Labels;
        if (showsMore)
        {
            if (hasModels)
            {
                root.Children.Add(new ModelMenuSeparator { IsInline = true });
            }
            root.Children.Add(MoreRow(rows, SelectedModel));
        }
        if (effortModel?.EffortLevels is not { Count: > 0 } offered)
        {
            return new ModelMenuPageView(ModelMenuPage.Main, root, rows, null);
        }
        if (hasModels || showsMore)
        {
            root.Children.Add(new ModelMenuSeparator { IsInline = true });
        }
        var levels = offered.Distinct().OrderBy(static level => (int)level).ToArray();
        var header = new ModelMenuEffortHeader { Title = labels.Effort };
        var strip = new ModelMenuEffortStrip(levels, labels.EffortSegmentName) { Title = labels.Effort };
        root.Children.Add(header);
        root.Children.Add(strip);
        rows.Add(strip);
        var view = new ModelMenuPageView(ModelMenuPage.Main, root, rows, null) { EffortHeader = header, EffortStrip = strip };
        ShowEffortIn(view);
        return view;
    }

    /// <summary>The current level in a built inline list: the segmented row's selection (the thumb glides there), the header's description, the row's name.</summary>
    private void ShowEffortIn(ModelMenuPageView view)
    {
        if (view.EffortStrip is not { } strip || view.EffortHeader is not { } header)
        {
            return;
        }
        var labels = Labels;
        var level = Effort is { } current && strip.Offers(current) ? current : (ModelEffort?)null;
        strip.Level = level;
        header.Description = level is { } shown ? labels.EffortDescription(shown) : null;
        AutomationProperties.SetName(strip, level is { } named ? labels.EffortStrip(labels.Effort, labels.EffortName(named)) : labels.Effort);
    }

    /// <summary>
    /// In the inline layout a new effort only moves the segmented row's selection and changes the header: the built list
    /// stays (the thumb glides, focus stays on the row). False when the list has to be rebuilt instead.
    /// </summary>
    private bool UpdateEffortInPlace()
    {
        var index = (int)ViewSlot.Main;
        if (!IsInline || _views[index] is not { EffortStrip: { } strip } view || _viewVersions[index] != _version ||
            Effort is not { } level || !strip.Offers(level))
        {
            return false;
        }
        ShowEffortIn(view);
        return true;
    }

    private ModelMenuPageView BuildEffort(bool withBack)
    {
        var labels = Labels;
        var root = new StackPanel();
        var rows = new List<ModelMenuItem>();
        var back = withBack ? AddBackRow(root, rows, labels.EffortMenu, labels) : null;
        var model = SelectedModel;
        var group = new ModelMenuGroup();
        AutomationProperties.SetName(group, labels.EffortMenu);
        if (model?.EffortLevels is { Count: > 0 } offered)
        {
            var levels = offered.Distinct().OrderBy(static level => (int)level);
            var defaultLevel = model.DefaultEffort is { } preferred && offered.Contains(preferred) ? preferred : (ModelEffort?)null;
            foreach (var level in levels)
            {
                var row = new ModelMenuEffortRow(level)
                {
                    LevelName = labels.EffortName(level),
                    Description = labels.EffortDescription(level),
                    DefaultText = labels.Default,
                    IsDefault = level == defaultLevel,
                    IsChecked = level == Effort
                };
                group.Children.Add(row);
                rows.Add(row);
            }
        }
        root.Children.Add(group);
        return new ModelMenuPageView(ModelMenuPage.Effort, root, rows, back);
    }

    private ModelMenuPageView BuildMore(bool withBack)
    {
        var labels = Labels;
        var root = new StackPanel();
        var rows = new List<ModelMenuItem>();
        var back = withBack ? AddBackRow(root, rows, labels.MoreModelsMenu, labels) : null;
        var first = true;
        foreach (var provider in _groups)
        {
            var group = new ModelMenuGroup();
            if (!string.IsNullOrEmpty(provider.Name))
            {
                AutomationProperties.SetName(group, provider.Name);
                group.Children.Add(new ModelMenuGroupHeader { Text = provider.Name, IsFirst = first });
            }
            first = false;
            foreach (var model in provider.Models)
            {
                var row = ModelRow(model, compact: true);
                group.Children.Add(row);
                rows.Add(row);
            }
            root.Children.Add(group);
        }
        return new ModelMenuPageView(ModelMenuPage.More, root, rows, back);
    }

    private static ModelMenuActionRow AddBackRow(StackPanel root, List<ModelMenuItem> rows, string page, ModelMenuLabels labels)
    {
        var back = new ModelMenuActionRow(ModelMenuRowKind.Back) { Label = page };
        AutomationProperties.SetName(back, labels.BackTo(page));
        root.Children.Add(back);
        root.Children.Add(new ModelMenuSeparator());
        rows.Add(back);
        return back;
    }

    private ModelMenuModelRow ModelRow(AiModel model, bool compact, bool inline = false)
    {
        var mark = ModelMark.Resolve(model, Providers, out var color);
        return new ModelMenuModelRow(model)
        {
            IsCompact = compact,
            IsInline = inline && !compact,
            IsSimple = IsSimple && !compact && !inline,
            NoteText = IsSimple && !compact && !inline ? model.Note : null,
            ModelName = model.Name,
            // The composer's one line under the name: the blurb, or the short note when a catalog has only that.
            Blurb = compact ? null : inline ? model.Blurb ?? model.Note : model.Blurb,
            TagText = compact ? model.Tag : null,
            Mark = mark,
            MarkColor = color,
            IsChecked = ReferenceEquals(model, SelectedModel)
        };
    }
}
