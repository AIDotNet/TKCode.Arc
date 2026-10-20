namespace TKCode.Arc.Gallery.Pages;

public static partial class GalleryPages
{
    static partial void RegisterExpandA()
    {
        Register("ScrollArea", ScrollAreaPage.Create);
        Register("Accordion", AccordionPage.Create);
        Register("ExpandableText", ExpandableTextPage.Create);
        Register("ExpandableCard", ExpandableCardPage.Create);
        Register("ResizablePanels", ResizablePanelsPage.Create);
    }
}
