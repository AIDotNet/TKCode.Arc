using System.Text;

namespace TKCode.Arc.Tests.Editors;

/// <summary>What a picked file is, decided from its first bytes before anything is decoded.</summary>
public class AvatarImageTypesTests
{
    private static byte[] Bytes(params byte[] head) => head.Concat(new byte[16]).ToArray();

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text).Concat(new byte[16]).ToArray();

    [Fact]
    public void The_First_Bytes_Name_The_Type()
    {
        AvatarImageTypes.Sniff(Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)).ShouldBe("image/png");
        AvatarImageTypes.Sniff(Bytes(0xFF, 0xD8, 0xFF, 0xE0)).ShouldBe("image/jpeg");
        AvatarImageTypes.Sniff(Ascii("GIF89a")).ShouldBe("image/gif");
        AvatarImageTypes.Sniff(Ascii("GIF87a")).ShouldBe("image/gif");
        AvatarImageTypes.Sniff(Ascii("RIFF\0\0\0\0WEBPVP8 ")).ShouldBe("image/webp");
        AvatarImageTypes.Sniff(Ascii("BM")).ShouldBe("image/bmp");
        AvatarImageTypes.Sniff(Ascii("\0\0\0\u0018ftypavif")).ShouldBe("image/avif");
        AvatarImageTypes.Sniff(Ascii("\0\0\0\u0018ftypheic")).ShouldBe("image/heic");
        AvatarImageTypes.Sniff(Ascii("%PDF-1.7")).ShouldBe("application/pdf");
        AvatarImageTypes.Sniff(Ascii("plain text")).ShouldBeNull();
        AvatarImageTypes.Sniff(ReadOnlySpan<byte>.Empty).ShouldBeNull();
    }

    [Fact]
    public void The_Name_Is_Only_Used_When_The_Bytes_Say_Nothing()
    {
        // A PDF renamed to .png is a PDF; a picture with an odd name is still a picture.
        AvatarImageTypes.Detect(Ascii("%PDF-1.7"), "photo.png").ShouldBe("application/pdf");
        AvatarImageTypes.Detect(Bytes(0xFF, 0xD8, 0xFF, 0xE0), "photo.dat").ShouldBe("image/jpeg");
        AvatarImageTypes.Detect(Ascii("unknown"), "photo.png").ShouldBe("image/png");
        AvatarImageTypes.Detect(Ascii("unknown"), "notes.txt").ShouldBe("text/plain");
        AvatarImageTypes.Detect(Ascii("unknown"), "noextension").ShouldBeNull();
        AvatarImageTypes.Detect(Ascii("unknown"), null).ShouldBeNull();
    }

    [Fact]
    public void Accept_Matches_Like_A_File_Input()
    {
        AvatarImageTypes.Accepts(null, "image/png", "a.png").ShouldBeTrue();
        AvatarImageTypes.Accepts(null, "image/gif", "a.gif").ShouldBeTrue();
        AvatarImageTypes.Accepts(null, "image/avif", "a.avif").ShouldBeFalse();
        AvatarImageTypes.Accepts("", "application/pdf", "a.png").ShouldBeFalse();
        AvatarImageTypes.Accepts("image/png,image/jpeg", "image/gif", "a.gif").ShouldBeFalse();
        AvatarImageTypes.Accepts("image/png,image/jpeg", null, "a.png").ShouldBeFalse();
        AvatarImageTypes.Accepts("image/*", "image/avif", "a.avif").ShouldBeTrue();
        AvatarImageTypes.Accepts("image/*", "application/pdf", "a.pdf").ShouldBeFalse();
        AvatarImageTypes.Accepts(".png, .jpg", "image/jpeg", "photo.jpeg").ShouldBeTrue();
        AvatarImageTypes.Accepts(".png", "image/jpeg", "photo.jpg").ShouldBeFalse();
        AvatarImageTypes.Accepts(".PNG", "image/png", "PHOTO.PNG").ShouldBeTrue();
    }

    [Fact]
    public void The_Accepted_Types_Read_As_Words()
    {
        AvatarImageTypes.Describe("image/png,image/jpeg,image/webp").ShouldBe("PNG, JPEG or WebP");
        AvatarImageTypes.Describe("image/png").ShouldBe("PNG");
        AvatarImageTypes.Describe("image/png,image/jpeg").ShouldBe("PNG or JPEG");
        AvatarImageTypes.Describe(".jpg,.jpeg,image/jpeg").ShouldBe("JPEG");
        AvatarImageTypes.Describe("image/*").ShouldBe("image");
        AvatarImageTypes.Describe(null).ShouldBe("PNG, JPEG, WebP, GIF or BMP");
    }

    [Fact]
    public void The_Saved_Name_Follows_The_Saved_Format()
    {
        AvatarImageTypes.SavedName("holiday.HEIC", "image/jpeg").ShouldBe("holiday.jpg");
        AvatarImageTypes.SavedName("me.png", "image/png").ShouldBe("me.png");
        AvatarImageTypes.SavedName("", "image/png").ShouldBe("avatar.png");
        AvatarImageTypes.SavedName(null, "image/jpeg").ShouldBe("avatar.jpg");
    }
}
