using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class PlanComparisonLocalizedDemo : UserControl
{
    public PlanComparisonLocalizedDemo()
    {
        InitializeComponent();
        Comparison.Labels = new PlanComparisonLabels
        {
            Culture = CultureInfo.GetCultureInfo("de-DE"),
            Title = "Platz für Ihre Arbeitsweise",
            Description = "Nur was sich zwischen den Tarifen unterscheidet. Wechseln Sie die Abrechnung, um Ihren Preis zu sehen.",
            BillingPeriod = "Abrechnungszeitraum",
            Monthly = "Monatlich",
            Yearly = "Jährlich",
            MonthlyNote = "Monatlich kündbar",
            YearlyNote = percent => $"Jährliche Abrechnung, bis zu {percent} % sparen",
            CompareTitle = "Tarife vergleichen",
            FeatureCount = (shown, total) => $"{shown} von {total} Funktionen",
            DifferencesOnly = "Nur Unterschiede",
            FeatureHeader = "Was sich ändert",
            PricePeriod = "pro Platz / Monat",
            Select = name => $"{name} wählen",
            Selected = "Ausgewählt",
            SelectedAnnouncement = name => $"{name} ausgewählt",
            Included = "Inklusive",
            NotIncluded = "Nicht enthalten",
            TableName = names => $"Tarifvergleich {string.Join(" und ", names)}",
            PlanHeader = name => $"Tarif {name}",
            Footnote = "Preise in EUR pro Platz. Jahrestarife werden jährlich abgerechnet."
        };
        Comparison.PlansSource =
        [
            new PlanComparisonPlan("Team", "Für kleinere Teams", 12, 9.5m),
            new PlanComparisonPlan("Studio", "Für teamübergreifende Arbeit", 25, 20)
        ];
        Comparison.FeaturesSource =
        [
            new PlanComparisonFeature("Aktive Projekte", true, "Unbegrenzt", "Unbegrenzt"),
            new PlanComparisonFeature("Gemeinsame Bereiche", false, "1 Bereich", "Unbegrenzt"),
            new PlanComparisonFeature("Versionsverlauf", false, "30 Tage", "Unbegrenzt"),
            new PlanComparisonFeature("Freigabeabläufe", false, "Nicht enthalten", "Inklusive"),
            new PlanComparisonFeature("Exporte", true, "Inklusive", "Inklusive"),
            new PlanComparisonFeature("Support", false, "E-Mail", "Bevorzugte E-Mail")
        ];
    }
}
