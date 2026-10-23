using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ModelPickerCustomWordingDemo : UserControl
{
    public ModelPickerCustomWordingDemo()
    {
        InitializeComponent();
        Picker.Providers.AddRange(ModelPickerDemoData.Providers());
        Picker.Models.AddRange(ModelPickerDemoData.Models());
        Picker.SummaryNote = "Preise in USD pro 1 Mio. Tokens · Beispielwerte";
        // Replace what you want to say differently; everything else keeps its English default. The level names are
        // shown by the pill, the menu, the summary and the confirmation, so each part gets the same function.
        Picker.Labels = new ModelPickerLabels
        {
            EffortName = Level,
            ModelNotice = (previous, next) => $"{previous} → {next}",
            EffortNotice = (previous, next) => $"Aufwand: {previous} → {next}",
            ModelAdjustedNotice = (previous, next, level) => $"{previous} → {next} · Aufwand auf {level} angepasst",
            ModelAnnouncement = (model, effort) => effort is null ? $"Jetzt aktiv: {model}" : $"Jetzt aktiv: {model}, Aufwand {effort}",
            EffortAnnouncement = (model, effort) => $"{model}, Aufwand {effort}",
            Pill = new ModelPillLabels
            {
                EffortName = Level,
                AutomationName = (model, effort) => $"Modell: {model.Name}, Aufwand {Level(effort)}. Modell wechseln",
                AutomationNameWithoutEffort = model => $"Modell: {model.Name}. Modell wechseln"
            },
            Menu = new ModelMenuLabels
            {
                Menu = "Modelle",
                Featured = "Empfohlene Modelle",
                Effort = "Aufwand",
                EffortMenu = "Aufwand",
                MoreModels = "Weitere Modelle",
                MoreModelsMenu = "Weitere Modelle",
                Default = "Standard",
                EffortName = Level
            },
            Summary = new ModelSummaryLabels
            {
                Region = "Gewähltes Modell",
                Context = "Kontext",
                Input = "Eingabe",
                Output = "Ausgabe",
                Effort = "Aufwand",
                NotAdjustable = "Fest",
                ContextValue = tokens => $"{tokens} Tokens",
                EffortName = Level
            }
        };
    }

    private static string Level(ModelEffort effort) => effort switch
    {
        ModelEffort.Low => "Niedrig",
        ModelEffort.Medium => "Mittel",
        ModelEffort.High => "Hoch",
        ModelEffort.ExtraHigh => "Sehr hoch",
        _ => "Maximal"
    };
}
