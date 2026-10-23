using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class CtaSectionPreviewDemo : UserControl
{
    private static readonly SpringSpec s_smooth = SpringSpec.FromVisual(0.4, 0);

    private readonly MotionValue<double> _height;
    private readonly MotionValue<double> _fade;
    private DateTime _armedUntil = DateTime.MinValue;
    private bool _measured;
    private bool _changed;

    public CtaSectionPreviewDemo()
    {
        InitializeComponent();
        // Centered's faces and the setup card's teammates are drawn in code (CtaSectionDemoData), never photographs.
        foreach (var face in CtaSectionDemoData.CreateFaces())
        {
            Section.Faces.Add(face);
        }
        foreach (var face in CtaSectionDemoData.CreateSetupFaces())
        {
            Section.SetupFaces.Add(face);
        }
        // The frame's height is a measured value that springs after a layout switch, so it is driven here (Arc's useFrameHeight).
        _height = new MotionValue<double>(this, 0, MotionVectors.Double);
        _height.Changed += value => Frame.Height = value;
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += value => Body.Opacity = Math.Clamp(value, 0, 1);
        Layouts.SelectionChanged += OnLayoutChanged;
        Section.Dismissed += OnDismissed;
        RestoreButton.Click += OnRestore;
        Body.GetObservable(BoundsProperty).Subscribe(new BoundsObserver(this));
    }

    private sealed class BoundsObserver(CtaSectionPreviewDemo owner) : IObserver<Rect>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(Rect value) => owner.OnContentResized(value.Height);
    }

    private bool IsFull => ArcMotion.IsFull(this);

    /// <summary>A discrete change (a new layout, the restore button) arms the spring for 700 ms; every other resize follows at once.</summary>
    private void Arm() => _armedUntil = DateTime.UtcNow.AddMilliseconds(700);

    private void OnContentResized(double next)
    {
        if (next <= 0)
        {
            return;
        }
        if (!_measured || !IsFull || DateTime.UtcNow > _armedUntil)
        {
            _measured = true;
            _height.Jump(next + 2);
            Frame.Height = next + 2;
            return;
        }
        _ = _height.AnimateTo(next + 2, MotionSpec.Spring(s_smooth));
    }

    private void SwapView(Action change)
    {
        Arm();
        change();
        // The first layout appears as it is; later ones fade in while the frame springs to their height.
        if (_changed && IsFull)
        {
            _fade.Jump(0);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        _changed = true;
    }

    private void OnLayoutChanged(object? sender, SelectionChangedEventArgs e)
    {
        SwapView(() =>
        {
            Section.Variant = Layouts.SelectedValue switch
            {
                "split" => CtaVariant.Split,
                "banner" => CtaVariant.Banner,
                _ => CtaVariant.Centered
            };
            Section.Show();
            Section.IsVisible = true;
            Restore.IsVisible = false;
        });
    }

    private void OnDismissed(object? sender, RoutedEventArgs e)
    {
        SwapView(() =>
        {
            Section.IsVisible = false;
            Restore.IsVisible = true;
        });
    }

    private void OnRestore(object? sender, RoutedEventArgs e)
    {
        SwapView(() =>
        {
            Restore.IsVisible = false;
            Section.Show();
            Section.IsVisible = true;
        });
    }
}
