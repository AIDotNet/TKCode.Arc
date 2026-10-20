namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterProgressA()
    {
        Register("Progress", ProgressPage.Create);
        Register("Skeleton", SkeletonPage.Create);
        Register("Stepper", StepperPage.Create);
        Register("Countdown", CountdownPage.Create);
        Register("UsageMeter", UsageMeterPage.Create);
    }
}
