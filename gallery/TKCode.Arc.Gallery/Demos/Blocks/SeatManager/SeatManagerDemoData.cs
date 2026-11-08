using System.Globalization;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented data for the SeatManager demos beyond <see cref="SeatManagerSamples"/>.</summary>
public static class SeatManagerDemoData
{
    /// <summary>Saltmarsh Studio: a yearly Studio plan at €96 a licence, 20 members (14 shown as a sample), one invite, no card.</summary>
    public static void ApplyStudio(SeatManager block)
    {
        block.Workspace = "Saltmarsh Studio";
        block.Plan = new SeatManagerPlan { Name = "Studio", PricePerSeat = 96m, Interval = SeatManagerInterval.Year, Currency = "EUR", MaxSeats = 40 };
        block.PeriodStart = new DateTime(2026, 3, 1);
        block.PeriodEnd = new DateTime(2027, 3, 1);
        block.Now = new DateTime(2026, 11, 8);
        block.PaymentMethod = null;
        block.ReceiptEmail = "office@saltmarsh.example";
        string[] names =
        [
            "Aino Paju", "Bruno Keller", "Cato Ruiz", "Dalia Moss", "Emre Tan", "Freya Lund", "Gus Orr",
            "Hana Ito", "Ivo Brandt", "Jola Nemec", "Kofi Asare", "Lio Park", "Maren Sol", "Nils Ek"
        ];
        block.Members.Clear();
        for (var i = 0; i < names.Length; i++)
        {
            block.Members.Add(new SeatManagerMember { Id = $"s{i}", Name = names[i] });
        }
        block.MemberCount = 20;
        block.Invites.Clear();
        block.Invites.Add(new SeatManagerInvite { Id = "s-i1", Email = "pia.voss@saltmarsh.example", InvitedBy = "Maren Sol", Note = "yesterday" });
        block.Purchased = 24;
        block.Target = 24;
    }

    /// <summary>The block's text with "licences" for "seats", amounts and dates formatted for Ireland.</summary>
    public static SeatManagerLabels LicenceLabels()
    {
        static string Licences(int n) => n == 1 ? "1 licence" : $"{n} licences";
        return new SeatManagerLabels
        {
            Culture = CultureInfo.GetCultureInfo("en-IE"),
            Title = "Licences",
            StepperTitle = "Licences",
            UsedTemplate = "{0} of {1} licences in use",
            SpinName = "Licences",
            SpinValueText = Licences,
            Subtitle = (workspace, price, _, date) => $"{workspace} · {price} a licence per year · Renews {date}",
            NoticeOpen = n => $"{Licences(n)} free for new people.",
            NoticeAdding = n => $"{Licences(n)} more once you update.",
            NoticeFull = "Every licence is taken. Invites need a free licence.",
            AddSeat = "Add a licence",
            DeltaSeats = d => (d > 0 ? "+" : "−") + Licences(Math.Abs(d)),
            UpdateSeats = "Update licences",
            Done = (n, email) => $"You're on {Licences(n)}. Receipt sent to {email}."
        };
    }
}
