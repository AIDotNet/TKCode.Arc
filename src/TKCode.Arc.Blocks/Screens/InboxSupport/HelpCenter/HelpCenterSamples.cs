namespace TKCode.Arc;

/// <summary>The sample help center (an invented note-taking app) of a <see cref="HelpCenter"/> without articles.</summary>
internal static class HelpCenterSamples
{
    public static IEnumerable<HelpCenterCategory> Categories() =>
    [
        new("start", "Getting started", "Set up your workspace and invite your team.", ArcIcons.Zap),
        new("billing", "Billing and plans", "Invoices, payment methods and changing plans.", ArcIcons.CreditCard),
        new("account", "Account and security", "Sign-in, two-step verification and data export.", ArcIcons.ShieldCheck),
        new("sharing", "Sharing and teams", "Permissions, guests and shared folders.", ArcIcons.Users),
        new("sync", "Sync and devices", "Offline use, conflicts and the mobile apps.", ArcIcons.RefreshCw),
        new("api", "Integrations and API", "Webhooks, tokens and connected apps.", ArcIcons.Code),
    ];

    public static IEnumerable<HelpCenterArticle> Articles()
    {
        var updated = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        return
        [
            new("invite", "start", "Invite people to your workspace", "Send invites by email or share a join link, and choose what new members can do.",
                """
                Everyone you invite joins with the role you pick, and you can change it later.

                ## Send an invite by email

                1. Open **Settings → Members**.
                2. Select **Invite people** and enter one or more addresses.
                3. Pick a role and select **Send invites**.

                Invites expire after 14 days. You can send them again from the same page.

                ## Share a join link

                A join link lets anyone with an address at your company domain join without an invite.

                ### Turn the link off

                Select **Reset link** to stop the old link from working right away.

                ## Choose a role

                - **Admin** manages members, billing and security.
                - **Editor** creates and edits notes and folders.
                - **Viewer** reads and comments.
                """) { IsPopular = true, UpdatedAt = updated },
            new("first-notebook", "start", "Create your first notebook", "Notebooks hold related notes. Start one from a template or from scratch.",
                """
                A notebook groups notes on one subject, such as a project or a class.

                ## Start from a template

                Pick **New → Notebook from template** and choose one of the starting layouts.

                ## Start from scratch

                Pick **New → Notebook**, give it a name and start writing.
                """) { UpdatedAt = updated.AddDays(-30) },
            new("change-plan", "billing", "Change or cancel your plan", "Move between plans at any time. Changes are prorated to the day.",
                """
                You can upgrade, downgrade or cancel from **Settings → Billing**.

                ## Upgrade

                Upgrades start right away. You pay the difference for the rest of the billing period.

                ## Downgrade

                Downgrades start at the next renewal. Features of the higher plan stay until then.

                ## Cancel

                Your workspace stays readable after you cancel. You can export everything at any time.
                """) { IsPopular = true, UpdatedAt = updated.AddDays(-7) },
            new("invoices", "billing", "Download invoices and receipts", "Every invoice is kept in Settings, as a PDF with your company details.",
                """
                Open **Settings → Billing → Invoices** and select the month you need.

                ## Add company details

                Your company name, address and tax number appear on every invoice issued after you add them.
                """) { UpdatedAt = updated.AddDays(-60) },
            new("two-step", "account", "Turn on two-step verification", "Protect your account with a code from an authenticator app or a security key.",
                """
                Two-step verification asks for a second proof after your password.

                ## Use an authenticator app

                1. Open **Account → Security**.
                2. Select **Set up app** and scan the code.
                3. Enter the six-digit code to finish.

                ## Use a security key

                Plug in the key or hold it near your phone when asked.

                ## Save your recovery codes

                Keep the recovery codes somewhere safe. Each one works once.
                """) { IsPopular = true, UpdatedAt = updated.AddDays(-3) },
            new("export", "account", "Export all of your data", "Get a copy of every note, file and comment as Markdown and attachments.",
                """
                Exports include every notebook you can open.

                ## Start an export

                Select **Account → Export data**. We email you a download link when it is ready.
                """) { UpdatedAt = updated.AddDays(-90) },
            new("permissions", "sharing", "How sharing permissions work", "Who can see, edit and share a note, and how folder permissions pass down.",
                """
                Permissions set on a folder apply to everything inside it.

                ## Levels

                - **Can view** reads and comments.
                - **Can edit** changes content.
                - **Full access** also shares and deletes.

                ## Guests

                Guests see only the notes shared with them and never appear in the member list.
                """) { IsPopular = true, UpdatedAt = updated.AddDays(-12) },
            new("offline", "sync", "Work offline", "Notes you opened recently stay available without a connection and sync when you are back.",
                """
                Recent notebooks are saved on the device automatically.

                ## Keep a notebook offline

                Select **Keep offline** in the notebook menu to keep all of it on this device.

                ## Conflicts

                When the same paragraph changed in two places, both versions are kept and marked for you to choose.
                """) { UpdatedAt = updated.AddDays(-20) },
            new("webhooks", "api", "Send events to your server with webhooks", "Get a signed request whenever a note is created, changed or shared.",
                """
                Webhooks post a JSON payload to the address you choose.

                ## Add an endpoint

                Open **Settings → Developers → Webhooks** and select **Add endpoint**.

                ## Check the signature

                Every request carries a signature header made with your signing secret. Reject requests whose signature does not match.
                """) { UpdatedAt = updated.AddDays(-45) },
        ];
    }
}
