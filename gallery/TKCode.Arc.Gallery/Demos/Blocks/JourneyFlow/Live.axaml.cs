using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class JourneyFlowLiveDemo : UserControl
{
    private uint _week = 1;

    public JourneyFlowLiveDemo()
    {
        InitializeComponent();
        var baseline = JourneyFlowDemoData.AllLearners();
        var segment = JourneyFlowDemoData.Segments()[0];
        Journey.Stages.AddRange(JourneyFlowDemoData.Stages());
        Journey.Nodes.AddRange(JourneyFlowDemoData.Nodes());
        Journey.Segments.Add(segment);

        // Each "week" draws new counts around the baseline (seeded, so the sequence repeats), keeping every node
        // balanced: what leaves a step is split from what reached it.
        NextWeek.Click += (_, _) => JourneyFlowDemoData.Apply(segment, Balance(JourneyFlowDemoData.Jitter(baseline, ++_week)));
    }

    private static double[] Balance(double[] v)
    {
        // Pairs: 0,1 from arrived; 2,3 from started (= v0); 4,5 from learned (= v2); 6,7 from hooked (= v4); 8,9 from drifting (= v5).
        Split(v, 2, 3, v[0]);
        Split(v, 4, 5, v[2]);
        Split(v, 6, 7, v[4]);
        Split(v, 8, 9, v[5]);
        return v;
    }

    private static void Split(double[] v, int a, int b, double total)
    {
        var sum = v[a] + v[b];
        v[a] = sum > 0 ? Math.Round(total * v[a] / sum) : 0;
        v[b] = total - v[a];
    }
}
