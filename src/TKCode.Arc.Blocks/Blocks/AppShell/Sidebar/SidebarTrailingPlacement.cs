namespace TKCode.Arc;

/// <summary>Where a <see cref="Sidebar"/> row shows its trailing content (<see cref="SidebarItem.TrailingIcon"/> or <see cref="Sidebar.TrailingTemplate"/>).</summary>
public enum SidebarTrailingPlacement
{
    /// <summary>The default: right after the label's text, so a label and its icon read as one ("More" and its chevron).</summary>
    AfterLabel,

    /// <summary>At the end of the row (before the count, the unread dot and a parent's chevron), e.g. an add or a menu button.</summary>
    End
}
