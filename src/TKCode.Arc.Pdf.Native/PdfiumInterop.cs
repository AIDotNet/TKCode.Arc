using System.Runtime.InteropServices;

namespace TKCode.Arc;

/// <summary>
/// The PDFium C API of chromium/8111, each signature checked against its headers (fpdfview.h, fpdf_doc.h, fpdf_text.h). Call it
/// only on <see cref="PdfiumThread"/>: PDFium is not thread-safe. C's <c>unsigned long</c> is 32 bits on Windows and 64 elsewhere,
/// hence <see cref="CULong"/>. Strings are bytes the caller encoded (UTF-8, null-terminated) or buffers PDFium fills (UTF-16LE).
/// </summary>
internal static unsafe partial class PdfiumInterop
{
    public const string Library = "pdfium";

    public const int ErrorFile = 2;
    public const int ErrorFormat = 3;
    public const int ErrorPassword = 4;
    public const int ErrorSecurity = 5;

    public const int RenderAnnotations = 0x01;
    public const int BitmapBgra = 4;

    public const int ActionGoTo = 1;
    public const int ActionUri = 3;

    public const int ViewFitH = 3;
    public const int ViewFitV = 4;
    public const int ViewFitR = 5;
    public const int ViewFitBH = 7;
    public const int ViewFitBV = 8;

    /// <summary><c>FPDF_LIBRARY_CONFIG</c> up to version 2, the version the header asks for.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LibraryConfig
    {
        public int Version;
        public byte** UserFontPaths;
        public void* Isolate;
        public uint V8EmbedderSlot;
    }

    /// <summary><c>FPDF_FILEACCESS</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FileAccess
    {
        public CULong FileLength;
        public delegate* unmanaged[Cdecl]<void*, CULong, byte*, CULong, int> GetBlock;
        public void* Param;
    }

    /// <summary><c>FS_SIZEF</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SizeF
    {
        public float Width;
        public float Height;
    }

    /// <summary><c>FS_RECTF</c>; in page space <see cref="Top"/> is the larger y.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RectF
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;
    }

    // fpdfview.h
    [LibraryImport(Library)]
    public static partial void FPDF_InitLibraryWithConfig(LibraryConfig* config);

    [LibraryImport(Library)]
    public static partial nint FPDF_LoadDocument(byte* filePath, byte* password);

    [LibraryImport(Library)]
    public static partial nint FPDF_LoadMemDocument64(void* data, nuint size, byte* password);

    [LibraryImport(Library)]
    public static partial nint FPDF_LoadCustomDocument(FileAccess* fileAccess, byte* password);

    [LibraryImport(Library)]
    public static partial CULong FPDF_GetLastError();

    [LibraryImport(Library)]
    public static partial void FPDF_CloseDocument(nint document);

    [LibraryImport(Library)]
    public static partial int FPDF_GetPageCount(nint document);

    [LibraryImport(Library)]
    public static partial int FPDF_GetPageSizeByIndexF(nint document, int pageIndex, SizeF* size);

    [LibraryImport(Library)]
    public static partial nint FPDF_LoadPage(nint document, int pageIndex);

    [LibraryImport(Library)]
    public static partial void FPDF_ClosePage(nint page);

    [LibraryImport(Library)]
    public static partial void FPDF_RenderPageBitmap(nint bitmap, nint page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    [LibraryImport(Library)]
    public static partial int FPDF_PageToDevice(nint page, int startX, int startY, int sizeX, int sizeY, int rotate, double pageX, double pageY, int* deviceX, int* deviceY);

    [LibraryImport(Library)]
    public static partial nint FPDFBitmap_CreateEx(int width, int height, int format, void* firstScan, int stride);

    [LibraryImport(Library)]
    public static partial int FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, CULong color);

    [LibraryImport(Library)]
    public static partial void FPDFBitmap_Destroy(nint bitmap);

    // fpdf_doc.h
    [LibraryImport(Library)]
    public static partial CULong FPDF_GetMetaText(nint document, byte* tag, void* buffer, CULong length);

    [LibraryImport(Library)]
    public static partial CULong FPDF_GetPageLabel(nint document, int pageIndex, void* buffer, CULong length);

    [LibraryImport(Library)]
    public static partial nint FPDFBookmark_GetFirstChild(nint document, nint bookmark);

    [LibraryImport(Library)]
    public static partial nint FPDFBookmark_GetNextSibling(nint document, nint bookmark);

    [LibraryImport(Library)]
    public static partial CULong FPDFBookmark_GetTitle(nint bookmark, void* buffer, CULong length);

    [LibraryImport(Library)]
    public static partial nint FPDFBookmark_GetDest(nint document, nint bookmark);

    [LibraryImport(Library)]
    public static partial nint FPDFBookmark_GetAction(nint bookmark);

    [LibraryImport(Library)]
    public static partial CULong FPDFAction_GetType(nint action);

    [LibraryImport(Library)]
    public static partial nint FPDFAction_GetDest(nint document, nint action);

    [LibraryImport(Library)]
    public static partial CULong FPDFAction_GetURIPath(nint document, nint action, void* buffer, CULong length);

    [LibraryImport(Library)]
    public static partial int FPDFDest_GetDestPageIndex(nint document, nint dest);

    [LibraryImport(Library)]
    public static partial CULong FPDFDest_GetView(nint dest, CULong* parameterCount, float* parameters);

    [LibraryImport(Library)]
    public static partial int FPDFDest_GetLocationInPage(nint dest, int* hasX, int* hasY, int* hasZoom, float* x, float* y, float* zoom);

    [LibraryImport(Library)]
    public static partial int FPDFLink_Enumerate(nint page, int* startPosition, nint* linkAnnotation);

    [LibraryImport(Library)]
    public static partial nint FPDFLink_GetDest(nint document, nint link);

    [LibraryImport(Library)]
    public static partial nint FPDFLink_GetAction(nint link);

    [LibraryImport(Library)]
    public static partial int FPDFLink_GetAnnotRect(nint linkAnnotation, RectF* rect);

    // fpdf_text.h
    [LibraryImport(Library)]
    public static partial nint FPDFText_LoadPage(nint page);

    [LibraryImport(Library)]
    public static partial void FPDFText_ClosePage(nint textPage);

    [LibraryImport(Library)]
    public static partial int FPDFText_CountChars(nint textPage);

    [LibraryImport(Library)]
    public static partial uint FPDFText_GetUnicode(nint textPage, int index);

    [LibraryImport(Library)]
    public static partial int FPDFText_GetLooseCharBox(nint textPage, int index, RectF* rect);

    [LibraryImport(Library)]
    public static partial nint FPDFLink_LoadWebLinks(nint textPage);

    [LibraryImport(Library)]
    public static partial int FPDFLink_CountWebLinks(nint linkPage);

    [LibraryImport(Library)]
    public static partial int FPDFLink_GetURL(nint linkPage, int linkIndex, ushort* buffer, int length);

    [LibraryImport(Library)]
    public static partial int FPDFLink_CountRects(nint linkPage, int linkIndex);

    [LibraryImport(Library)]
    public static partial int FPDFLink_GetRect(nint linkPage, int linkIndex, int rectIndex, double* left, double* top, double* right, double* bottom);

    [LibraryImport(Library)]
    public static partial void FPDFLink_CloseWebLinks(nint linkPage);
}
