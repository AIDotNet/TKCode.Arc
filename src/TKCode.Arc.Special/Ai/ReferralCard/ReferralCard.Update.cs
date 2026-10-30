using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// Bringing the parts in line with the state: the derived list, the funnel, the milestone, the rows and the hint.
public partial class ReferralCard
{
    /// <summary>How long a share button shows its check before the channel glyph returns.</summary>
    internal static readonly TimeSpan ShareConfirmTime = TimeSpan.FromSeconds(1.6);

    private readonly List<Referral> _items = [];
    private bool _itemsReady;
    private readonly Dictionary<string, (AiHeightBox Reveal, ReferralCardRow Row)> _rows = [];
    private string? _shownTitle;
    private string? _shownMilestoneKey;
    private bool? _breakpoint;
    private int? _shownPaid;
    private bool _milestoneFired;
    private string? _lastAnnouncement;
    private readonly int[] _shareTicks = new int[3];

    /// <summary>The referrals on screen, newest first.</summary>
    internal IReadOnlyList<Referral> Items
    {
        get
        {
            EnsureItems();
            return _items;
        }
    }

    /// <summary>The last thing the live region said.</summary>
    internal string? LastAnnouncement => _lastAnnouncement;

    /// <summary>The formatted reward ("$20").</summary>
    internal string RewardText => ReferralCardFormat.Money(Reward, Currency);

    /// <summary>The heading on screen.</summary>
    internal string DisplayedTitle => Title ?? (Reward > 0 ? Labels.Title(RewardText) : Labels.TitleWithoutReward);

    /// <summary>The line under the heading on screen.</summary>
    internal string DisplayedDescription => Description ?? (Reward > 0 ? Labels.Description(RewardText) : Labels.DescriptionWithoutReward);

    /// <summary>The funnel values on screen: invited, signed up and the earned total.</summary>
    internal (int Invited, int SignedUp, long EarnedTotal) Funnel
    {
        get
        {
            var (invited, signed, earned) = ReferralCardFormat.Count(Items);
            return (invited, signed, (long)earned * Reward);
        }
    }

    /// <summary>Paid referrals.</summary>
    internal int Paid => ReferralCardFormat.Count(Items).Earned;

    /// <summary>Whether the milestone's goal is reached.</summary>
    internal bool IsUnlocked => Milestone is { } milestone && Paid >= Math.Max(1, milestone.Goal);

    /// <summary>The milestone bar (tests).</summary>
    internal ReferralCardMilestoneBar? Bar => _bar;

    /// <summary>The rows in list order (tests).</summary>
    internal IReadOnlyList<ReferralCardRow> RowViews =>
        _listPanel is null ? [] : _listPanel.Children.OfType<AiHeightBox>().Select(r => (ReferralCardRow)r.Content!).ToList();

    private void EnsureItems()
    {
        if (!_itemsReady)
        {
            ResetItems();
        }
    }

    /// <summary>Takes the list from <see cref="Referrals"/>, or the simulation's program, or none.</summary>
    private void ResetItems()
    {
        _itemsReady = true;
        _items.Clear();
        if (Referrals is { } referrals)
        {
            foreach (var referral in referrals)
            {
                if (referral is not null)
                {
                    _items.Add(referral);
                }
            }
        }
        else if (Simulation is { } simulation)
        {
            foreach (var referral in simulation.Referrals)
            {
                if (referral is not null)
                {
                    _items.Add(referral);
                }
            }
        }
    }

    /// <summary>Replaces one referral (same id) and refreshes.</summary>
    internal bool ReplaceItem(Referral next)
    {
        EnsureItems();
        var index = _items.FindIndex(r => r.Id == next.Id);
        if (index < 0)
        {
            return false;
        }
        _items[index] = next;
        Refresh(animate: true);
        return true;
    }

    private void Refresh(bool animate)
    {
        EnsureItems();
        if (_root is null)
        {
            return;
        }
        var anim = animate && IsLoaded;
        var labels = Labels;
        var title = DisplayedTitle;
        if (_shownTitle != title)
        {
            _shownTitle = title;
            _title!.Text = title;
        }
        _description!.Text = DisplayedDescription;

        var link = Link ?? string.Empty;
        var (domain, code) = ReferralCardFormat.SplitLink(link);
        _linkDomain!.Text = domain;
        _linkCode!.Text = code;
        AutomationProperties.SetName(_linkText!, link);
        Tooltip.SetTip(_linkText!, link);
        if (_copy!.Value != link)
        {
            _copy.Value = link;
        }

        RefreshFunnel(anim);
        RefreshMilestone(anim);
        RefreshRows(anim, labels);
        RefreshHint(anim);
        PseudoClasses.Set(UnlockedPseudoClass, IsUnlocked);
    }

    private void RefreshFunnel(bool animate)
    {
        var (invited, signed, total) = Funnel;
        var (prefix, suffix) = ReferralCardFormat.MoneyAffixes(Currency);
        var rolls = animate && ArcMotion.IsFull(this);
        Set(0, invited, null, null);
        Set(1, signed, null, null);
        Set(2, total, prefix, suffix);
        var labels = Labels;
        AutomationProperties.SetName(_funnelCells[0], labels.Invited + ": " + invited);
        AutomationProperties.SetName(_funnelCells[1], labels.SignedUp + ": " + signed);
        AutomationProperties.SetName(_funnelCells[2], labels.Earned + ": " + ReferralCardFormat.Money(total, Currency));

        void Set(int index, double value, string? pre, string? post)
        {
            var digits = _funnelNumbers[index];
            digits.IsInstant = !rolls;
            if (digits.Prefix != pre)
            {
                digits.Prefix = pre;
            }
            if (digits.Suffix != post)
            {
                digits.Suffix = post;
            }
            if (digits.Value != value)
            {
                digits.Value = value;
            }
        }
    }

    private void RefreshMilestone(bool animate)
    {
        if (_milestoneSection is null)
        {
            return;
        }
        var milestone = Milestone;
        _milestoneSection.IsVisible = milestone is not null;
        if (milestone is null)
        {
            return;
        }
        var goal = Math.Max(1, milestone.Goal);
        var paid = Paid;
        var unlocked = paid >= goal;
        var labels = Labels;
        _bar!.Set(ReferralCardFormat.Segments(goal), ReferralCardFormat.Filled(paid, goal), unlocked, animate);
        AutomationProperties.SetHelpText(_bar, labels.MilestoneValue(Math.Min(paid, goal), goal));
        var text = unlocked ? labels.MilestoneUnlocked(milestone.Reward) : labels.MilestoneToGo(goal - paid, milestone.Reward);
        var narrow = _breakpoint == true;
        var key = text + "\u0001" + unlocked + "\u0001" + milestone.Detail + "\u0001" + narrow;
        if (_shownMilestoneKey != key)
        {
            _shownMilestoneKey = key;
            var view = MilestoneLineView(unlocked, text, milestone.Detail, narrow);
            AutomationProperties.SetName(view, unlocked && !string.IsNullOrWhiteSpace(milestone.Detail) ? text + ". " + milestone.Detail : text);
            _milestoneLine!.Swap(view, animate);
        }

        // The event fires once, when paid referrals cross the goal while the card is on screen.
        if (_shownPaid is { } before && before < goal && unlocked && !_milestoneFired)
        {
            _milestoneFired = true;
            Announce(labels.MilestoneAnnouncement(milestone.Reward));
            RaiseEvent(new ReferralMilestoneEventArgs(MilestoneReachedEvent, milestone));
        }
        _shownPaid = paid;
    }

    /// <summary>
    /// One row per referral id, in list order. A row whose referral changed updates in place (its stage crossfades); a new
    /// referral opens at the top on the height spring; a referral that left is removed.
    /// </summary>
    private void RefreshRows(bool animate, ReferralCardLabels labels)
    {
        var panel = _listPanel!;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var firstBuild = _rows.Count == 0;
        for (var i = 0; i < _items.Count; i++)
        {
            var referral = _items[i];
            var id = string.IsNullOrEmpty(referral.Id) ? referral.Email : referral.Id;
            if (!seen.Add(id))
            {
                continue;
            }
            var stage = StageWord(referral.Stage, labels);
            var date = labels.Date(referral.InvitedAt);
            if (!_rows.TryGetValue(id, out var pair))
            {
                var row = new ReferralCardRow();
                // A new invite opens at the top on the height spring while it fades in; the list keeps its fixed height.
                var reveal = new AiHeightBox { HeightSpring = AiHeightBox.PanelSpring, Content = row };
                pair = (reveal, row);
                _rows[id] = pair;
                row.Show(referral, stage, date, animate: false);
                var at = Math.Min(seen.Count - 1, panel.Children.Count);
                panel.Children.Insert(at, reveal);
                if (animate && !firstBuild)
                {
                    reveal.OpenWhenLoaded();
                }
                else
                {
                    reveal.OpenInstantly();
                }
            }
            else
            {
                pair.Row.Show(referral, stage, date, animate);
                var at = seen.Count - 1;
                var current = panel.Children.IndexOf(pair.Reveal);
                if (current != at && at < panel.Children.Count)
                {
                    panel.Children.RemoveAt(current);
                    panel.Children.Insert(Math.Min(at, panel.Children.Count), pair.Reveal);
                }
            }
        }
        foreach (var id in _rows.Keys.Where(k => !seen.Contains(k)).ToList())
        {
            panel.Children.Remove(_rows[id].Reveal);
            _rows.Remove(id);
        }
        _listEmpty!.IsVisible = _items.Count == 0;
        _listScroller!.IsVisible = _items.Count > 0;
    }

    private string StageWord(ReferralStage stage, ReferralCardLabels labels) => stage switch
    {
        ReferralStage.SignedUp => labels.StageSignedUp,
        ReferralStage.Earned => Reward > 0 ? labels.StageEarned(RewardText) : labels.Earned,
        _ => labels.StageInvited
    };

    /// <summary>Says <paramref name="text"/> in the polite live region.</summary>
    internal void Announce(string text)
    {
        _lastAnnouncement = text;
        if (_status is not null)
        {
            _status.Text = text;
            AutomationProperties.SetName(_status, text);
        }
    }

    private void OnCopied(object? sender, EventArgs e)
    {
        Announce(Labels.LinkCopied);
        RaiseEvent(new ReferralLinkEventArgs(LinkCopiedEvent, Link ?? string.Empty));
    }

    /// <summary>
    /// A share button: raises <see cref="ShareRequested"/> with a ready URL. When a handler took it, the live region says the
    /// share opened; otherwise the share is simulated and the region says nothing was posted. Either way the button's glyph
    /// crossfades to a check and back.
    /// </summary>
    internal void Share(ReferralShareChannel channel)
    {
        var link = Link ?? string.Empty;
        var format = channel switch
        {
            ReferralShareChannel.Post => PostShareUrlFormat,
            ReferralShareChannel.Network => NetworkShareUrlFormat,
            _ => null
        };
        var url = ReferralCardFormat.ShareUrl(channel, link, ShareMessage, format, EmailSubject);
        var args = new ReferralShareEventArgs(ShareRequestedEvent, channel, url, link, ShareMessage);
        RaiseEvent(args);
        var labels = Labels;
        Announce(args.Handled ? labels.Shared(ShareName(channel, labels)) : labels.ShareSimulated);
        ConfirmShare(Array.IndexOf(s_channels, channel));
    }

    private void ConfirmShare(int index)
    {
        if (index < 0 || _root is null)
        {
            return;
        }
        var swap = _shareGlyphs[index];
        var tick = ++_shareTicks[index];
        swap.Swap(ShareGlyph(ArcIcons.Check).ReferralArc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Success), animate: true);
        DispatcherTimer.RunOnce(() =>
        {
            if (_shareTicks[index] == tick)
            {
                swap.Swap(ShareGlyph(ReferralCardIcons.Share(s_channels[index])), animate: true);
            }
        }, ShareConfirmTime);
    }

    /// <summary>The check a share button shows after a share (tests).</summary>
    internal bool IsShareConfirmed(ReferralShareChannel channel) =>
        _shareGlyphs[Array.IndexOf(s_channels, channel)].Content is ArcGlyph glyph && ReferenceEquals(glyph.Data, ArcIcons.Check);
}
