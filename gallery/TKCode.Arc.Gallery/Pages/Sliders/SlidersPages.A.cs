namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterSlidersA()
    {
        Register("Slider", SliderPage.Create);
        Register("ElasticSlider", ElasticSliderPage.Create);
    }
}
