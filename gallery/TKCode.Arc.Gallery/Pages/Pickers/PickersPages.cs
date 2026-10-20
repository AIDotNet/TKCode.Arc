namespace TKCode.Arc.Gallery.Pages;

// Pages of this category. Teams working in parallel each own one hook and implement it in
// PickersPages.A.cs / PickersPages.B.cs / PickersPages.C.cs (unimplemented hooks compile away).
public static partial class GalleryPages
{
    static partial void RegisterPickers()
    {
        Register("Calendar", CalendarPage.Create);
        RegisterPickersA();
        RegisterPickersB();
        RegisterPickersC();
    }

    static partial void RegisterPickersA();

    static partial void RegisterPickersB();

    static partial void RegisterPickersC();
}
