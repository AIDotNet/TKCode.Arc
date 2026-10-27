using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class UptimeStatusFormatsDemo : UserControl
{
    public UptimeStatusFormatsDemo()
    {
        InitializeComponent();
        Status.Today = UptimeStatusDemoData.Now;
        Status.UpdatedAt = UptimeStatusDemoData.UpdatedAt;
        Status.Services.AddRange(UptimeStatusDemoData.Services());
        Status.Incidents.AddRange(UptimeStatusDemoData.Incidents());

        // The hover card, the announcements and the chart's tick labels use these.
        Status.LatencyFormatter = ms => ms >= 1000 ? $"{ms / 1000:0.0#} s" : $"{ms:0} ms";
        Status.PercentFormatter = fraction => $"{fraction * 100:0.000}%";
        Status.DateFormatter = day => day.ToString("d MMM", new System.Globalization.CultureInfo("fr-FR"));
        Status.Labels = new UptimeStatusLabels
        {
            UptimeText = "Disponibilité",
            UptimeHintFormat = "{0} derniers jours · choisissez un service pour son graphique",
            OperationalText = "Opérationnel",
            DegradedText = "Dégradé",
            OutageText = "Panne majeure",
            UptimeWordText = "dispo",
            TodayText = "Aujourd'hui",
            NoIncidentsRecordedText = "Aucun incident enregistré",
            ResponseTimeText = "Temps de réponse",
            ResponseSubtitleFormat = "{0} · dernières 24 heures",
            LatestFormat = "Dernier, {0}",
            IncidentsText = "Incidents",
            IncidentsCountFormat = "{0} sur les {1} derniers jours",
            ResolvedText = "Résolu",
            UpdatedFormat = "Mis à jour le {0:d MMM} à {0:HH:mm} UTC"
        };
    }
}
