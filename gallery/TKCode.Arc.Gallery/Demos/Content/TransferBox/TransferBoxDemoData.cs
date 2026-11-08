using System.Collections.ObjectModel;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A person the TransferBox demos assign to a project.</summary>
public sealed record TransferBoxMember(string Name, string Role);

/// <summary>A permission the TransferBox demos grant to a role.</summary>
public sealed record TransferBoxPermission(string Name, string Description);

public static class TransferBoxDemoData
{
    public static ObservableCollection<string> Languages() =>
        ["C#", "Java", "JavaScript", "TypeScript", "C++", "Swift", "Kotlin", "Ruby", "PHP", "Dart"];

    public static ObservableCollection<TransferBoxMember> Members() =>
    [
        new("Ada Lovelace", "Engineering"),
        new("Grace Hopper", "Engineering"),
        new("Katherine Johnson", "Research"),
        new("Margaret Hamilton", "Engineering"),
        new("Hedy Lamarr", "Design"),
        new("Annie Easley", "Research"),
        new("Radia Perlman", "Infrastructure"),
        new("Barbara Liskov", "Engineering"),
    ];

    public static ObservableCollection<TransferBoxPermission> Permissions() =>
    [
        new("Read projects", "View every project and its files"),
        new("Edit projects", "Change settings, files and members"),
        new("Delete projects", "Remove a project and its history"),
        new("Manage billing", "Change the plan and payment method"),
        new("Invite people", "Send invitations to the workspace"),
        new("View audit log", "See who changed what and when"),
    ];
}
