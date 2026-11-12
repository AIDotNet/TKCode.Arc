#!/usr/bin/env python3
"""Generates the sample PDFs and the test fixtures of the PDF viewer. Standard library only; the output is deterministic.

  python3 tools/pdf/make-sample.py

Writes:
  tests/TKCode.Arc.Pdf.Native.Tests/Samples/arc-sample.pdf  12 pages, English and Chinese text, outline, internal and external
                                                               links, page labels, one landscape page.
  tests/TKCode.Arc.Pdf.Native.Tests/Samples/arc-sample-locked.pdf  3 pages, standard security handler (RC4 128-bit, revision 3).
                                                               The password to open it is LOCKED_PASSWORD below: "arc-sample".
  tests/TKCode.Arc.Pdf.Native.Tests/Fixtures/rotated.pdf         /Rotate 0, 90, 180, 270 and a page whose MediaBox does not start at 0.
  tests/TKCode.Arc.Pdf.Native.Tests/Fixtures/cyclic-outline.pdf  an outline whose sibling and child links run in circles.
  tests/TKCode.Arc.Pdf.Native.Tests/Fixtures/deep-outline.pdf    an outline chain of 100 levels.
  tests/TKCode.Arc.Pdf.Native.Tests/Fixtures/corrupt.pdf         a PDF header followed by noise.

The gallery (https://github.com/AIDotNet/ArcGallery) keeps copies of the two samples in gallery/TKCode.Arc.Gallery/Assets/Pdf: copy them
there after regenerating them.

The Chinese text uses the standard CID font reference STSong-Light with the UniGB-UCS2-H encoding and embeds nothing, so whether it
shows depends on the fonts of the machine (the text can be extracted either way). All text is written for this file.
"""
import hashlib
import os
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SAMPLES = os.path.join(ROOT, "tests", "TKCode.Arc.Pdf.Native.Tests", "Samples")
FIXTURES = os.path.join(ROOT, "tests", "TKCode.Arc.Pdf.Native.Tests", "Fixtures")

LOCKED_PASSWORD = "arc-sample"
LOCKED_OWNER_PASSWORD = "arc-sample-owner"
REPOSITORY_URL = "https://github.com/AIDotNet/TKCode.Arc"
PLAIN_URL = "https://www.example.com/arc/pdf-guide"

# ---------------------------------------------------------------------------------------------------------------------------
# PDF objects
# ---------------------------------------------------------------------------------------------------------------------------


class Name:
    def __init__(self, value):
        self.value = value


class Ref:
    def __init__(self, number):
        self.number = number


class Text:
    """A text string: PDFDocEncoding when ASCII, otherwise UTF-16BE with a byte order mark."""

    def __init__(self, value):
        self.data = value.encode("ascii") if value.isascii() else b"\xfe\xff" + value.encode("utf-16-be")


class Stream:
    def __init__(self, data, compress=True):
        self.data = zlib.compress(data, 9) if compress else data
        self.compressed = compress


def rc4(key, data):
    state = list(range(256))
    j = 0
    for i in range(256):
        j = (j + state[i] + key[i % len(key)]) & 0xFF
        state[i], state[j] = state[j], state[i]
    out = bytearray()
    i = j = 0
    for byte in data:
        i = (i + 1) & 0xFF
        j = (j + state[i]) & 0xFF
        state[i], state[j] = state[j], state[i]
        out.append(byte ^ state[(state[i] + state[j]) & 0xFF])
    return bytes(out)


def md5(data):
    return hashlib.md5(data, usedforsecurity=False).digest()


PASSWORD_PADDING = bytes.fromhex("28BF4E5E4E758A4164004E56FFFA01082E2E00B6D0683E802F0CA9FE6453697A")


def pad_password(password):
    return (password.encode("latin-1") + PASSWORD_PADDING)[:32]


class StandardSecurity:
    """The standard security handler with RC4 and a 128-bit key (V 2, R 3): ISO 32000-1, 7.6.3 and 7.6.4, algorithms 1 to 5."""

    PERMISSIONS = -4  # Everything allowed; the password is the point of the sample.

    def __init__(self, user_password, owner_password, file_id):
        self.file_id = file_id
        self.owner = self._owner_entry(user_password, owner_password)
        self.key = self._file_key(user_password)
        self.user = self._user_entry()

    @staticmethod
    def _rc4_rounds(key, data, rounds=range(1, 20)):
        for i in rounds:
            data = rc4(bytes(byte ^ i for byte in key), data)
        return data

    def _owner_entry(self, user_password, owner_password):
        digest = md5(pad_password(owner_password))
        for _ in range(50):
            digest = md5(digest)
        return self._rc4_rounds(digest[:16], rc4(digest[:16], pad_password(user_password)))

    def _file_key(self, user_password):
        permissions = (self.PERMISSIONS & 0xFFFFFFFF).to_bytes(4, "little")
        digest = md5(pad_password(user_password) + self.owner + permissions + self.file_id)
        for _ in range(50):
            digest = md5(digest[:16])
        return digest[:16]

    def _user_entry(self):
        digest = md5(PASSWORD_PADDING + self.file_id)
        return self._rc4_rounds(self.key, rc4(self.key, digest)) + bytes(16)

    def encrypt(self, data, number):
        key = md5(self.key + number.to_bytes(3, "little") + bytes(2))[:16]
        return rc4(key, data)

    def dictionary(self):
        return {
            "Filter": Name("Standard"),
            "V": 2,
            "R": 3,
            "Length": 128,
            "P": self.PERMISSIONS,
            "O": self.owner,
            "U": self.user,
        }


class PdfFile:
    def __init__(self, seed, password=None, owner_password=None):
        self.objects = []
        self.file_id = md5(seed.encode("ascii"))
        self.security = StandardSecurity(password, owner_password, self.file_id) if password is not None else None

    def add(self, value=None):
        """Adds an object, or reserves its number for set()."""
        self.objects.append(value)
        return Ref(len(self.objects))

    def set(self, ref, value):
        self.objects[ref.number - 1] = value

    def _bytes(self, value, number):
        if isinstance(value, bool):
            return b"true" if value else b"false"
        if value is None:
            return b"null"
        if isinstance(value, int):
            return str(value).encode("ascii")
        if isinstance(value, float):
            return (("%.3f" % value).rstrip("0").rstrip(".") or "0").encode("ascii")
        if isinstance(value, Name):
            return b"/" + value.value.encode("ascii")
        if isinstance(value, Ref):
            return b"%d 0 R" % value.number
        if isinstance(value, Text):
            data = self.security.encrypt(value.data, number) if self.security and number else value.data
            return b"<" + data.hex().upper().encode("ascii") + b">"
        if isinstance(value, bytes):  # A hex string that is never encrypted: only the /Encrypt dictionary and /ID have these.
            return b"<" + value.hex().upper().encode("ascii") + b">"
        if isinstance(value, list):
            return b"[" + b" ".join(self._bytes(item, number) for item in value) + b"]"
        if isinstance(value, dict):
            parts = [b"/" + key.encode("ascii") + b" " + self._bytes(item, number) for key, item in value.items()]
            return b"<<" + b" ".join(parts) + b">>"
        raise TypeError(type(value))

    def write(self, path, root, info=None):
        encrypt_ref = self.add(self.security.dictionary()) if self.security else None
        out = bytearray(b"%PDF-1.7\n%\xe2\xe3\xcf\xd3\n")
        offsets = []
        for index, value in enumerate(self.objects):
            number = index + 1
            offsets.append(len(out))
            out += b"%d 0 obj\n" % number
            # The /Encrypt dictionary itself is the one thing that stays in the clear.
            effective = 0 if encrypt_ref and number == encrypt_ref.number else number
            if isinstance(value, Stream):
                data = value.data
                if self.security and effective:
                    data = self.security.encrypt(data, number)
                dictionary = {"Length": len(data)}
                if value.compressed:
                    dictionary["Filter"] = Name("FlateDecode")
                out += self._bytes(dictionary, effective) + b"\nstream\n" + data + b"\nendstream"
            else:
                out += self._bytes(value, effective)
            out += b"\nendobj\n"
        xref = len(out)
        out += b"xref\n0 %d\n0000000000 65535 f \n" % (len(self.objects) + 1)
        for offset in offsets:
            out += b"%010d 00000 n \n" % offset
        trailer = {"Size": len(self.objects) + 1, "Root": root, "ID": [self.file_id, self.file_id]}
        if info:
            trailer["Info"] = info
        if encrypt_ref:
            trailer["Encrypt"] = encrypt_ref
        out += b"trailer\n" + self._bytes(trailer, 0) + b"\nstartxref\n%d\n%%%%EOF\n" % xref
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(out)
        print("%-90s %7d bytes" % (os.path.relpath(path, ROOT), len(out)))


# ---------------------------------------------------------------------------------------------------------------------------
# Page content
# ---------------------------------------------------------------------------------------------------------------------------

# Helvetica's advance widths for 32..126, from the font's published metrics.
HELVETICA = [278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278] + [556] * 10 + [
    278, 278, 584, 584, 584, 556, 1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, 667, 778, 722,
    667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556, 333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500,
    222, 833, 556, 556, 556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584]


def is_chinese(text):
    return any(ord(c) > 0x7F for c in text)


def text_width(text, size):
    if is_chinese(text):
        return sum(size if ord(c) > 0x7F else size / 2 for c in text)
    return sum(HELVETICA[ord(c) - 32] for c in text) * size / 1000


def escape(text):
    return text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")


class Page:
    def __init__(self, width=595, height=842):
        self.width = width
        self.height = height
        self.ops = []
        self.annotations = []  # (rect, kind, target): kind is "page" (index, y), "goto" (index, y) or "uri" (address).

    def text(self, x, y, value, size=11, font="F1", color=(0.13, 0.13, 0.15)):
        self.ops.append("%.3f %.3f %.3f rg" % color)
        if is_chinese(value):
            data = value.encode("utf-16-be").hex().upper()
            self.ops.append("BT /F3 %.2f Tf %.2f %.2f Td <%s> Tj ET" % (size, x, y, data))
        else:
            self.ops.append("BT /%s %.2f Tf %.2f %.2f Td (%s) Tj ET" % (font, size, x, y, escape(value)))

    def rect(self, x, y, w, h, fill=None, stroke=None):
        if fill:
            self.ops.append("%.3f %.3f %.3f rg" % fill)
        if stroke:
            self.ops.append("%.3f %.3f %.3f RG 0.8 w" % stroke)
        self.ops.append("%.2f %.2f %.2f %.2f re %s" % (x, y, w, h, "B" if fill and stroke else "f" if fill else "S"))

    def line(self, x1, y1, x2, y2, color=(0.45, 0.45, 0.5), width=0.8):
        self.ops.append("%.3f %.3f %.3f RG %.2f w %.2f %.2f m %.2f %.2f l S" % (color + (width, x1, y1, x2, y2)))

    def wrap(self, x, y, width, value, size=11, leading=None, font="F1"):
        """Writes a paragraph and returns the y below it. Chinese wraps at any character, English at spaces."""
        leading = leading or size * 1.45
        for line in wrap_lines(value, width, size):
            self.text(x, y, line, size, font)
            y -= leading
        return y

    def link(self, x, y, value, kind, target, size=11):
        """Draws a blue underlined link and adds its annotation."""
        blue = (0.05, 0.36, 0.8)
        self.text(x, y, value, size, "F1", blue)
        width = text_width(value, size)
        self.line(x, y - 1.5, x + width, y - 1.5, blue, 0.6)
        self.annotations.append(([x, y - 3, x + width, y + size], kind, target))
        return width


def wrap_lines(value, width, size):
    lines = []
    if is_chinese(value):
        line = ""
        for c in value:
            if text_width(line + c, size) > width:
                lines.append(line)
                line = ""
            line += c
        if line:
            lines.append(line)
        return lines
    line = ""
    for word in value.split(" "):
        candidate = line + " " + word if line else word
        if text_width(candidate, size) > width:
            lines.append(line)
            line = word
        else:
            line = candidate
    if line:
        lines.append(line)
    return lines


def chinese_font(pdf):
    descriptor = pdf.add({
        "Type": Name("FontDescriptor"), "FontName": Name("STSong-Light"), "Flags": 6, "FontBBox": [-25, -254, 1000, 880],
        "ItalicAngle": 0, "Ascent": 880, "Descent": -120, "CapHeight": 880, "StemV": 93,
    })
    descendant = pdf.add({
        "Type": Name("Font"), "Subtype": Name("CIDFontType0"), "BaseFont": Name("STSong-Light"),
        "CIDSystemInfo": {"Registry": Text("Adobe"), "Ordering": Text("GB1"), "Supplement": 2},
        "FontDescriptor": descriptor, "DW": 1000, "W": [1, 95, 500],
    })
    return pdf.add({
        "Type": Name("Font"), "Subtype": Name("Type0"), "BaseFont": Name("STSong-Light"), "Encoding": Name("UniGB-UCS2-H"),
        "DescendantFonts": [descendant],
    })


def latin_font(pdf, name):
    return pdf.add({"Type": Name("Font"), "Subtype": Name("Type1"), "BaseFont": Name(name), "Encoding": Name("WinAnsiEncoding")})


def write_document(path, seed, pages, info, outline=None, labels=None, password=None, owner_password=None):
    pdf = PdfFile(seed, password, owner_password)
    fonts = {
        "F1": latin_font(pdf, "Helvetica"),
        "F2": latin_font(pdf, "Helvetica-Bold"),
        "F3": chinese_font(pdf),
    }
    pages_ref = pdf.add()
    page_refs = [pdf.add() for _ in pages]
    kids = []
    for page, ref in zip(pages, page_refs):
        annotation_refs = []
        for rect, kind, target in page.annotations:
            annotation = {"Type": Name("Annot"), "Subtype": Name("Link"), "Rect": rect, "Border": [0, 0, 0]}
            if kind == "uri":
                annotation["A"] = {"S": Name("URI"), "URI": Text(target)}
            elif kind == "goto":
                annotation["A"] = {"S": Name("GoTo"), "D": [page_refs[target[0]], Name("XYZ"), None, target[1], None]}
            else:
                annotation["Dest"] = [page_refs[target[0]], Name("XYZ"), 0, target[1], None]
            annotation_refs.append(pdf.add(annotation))
        content = pdf.add(Stream("\n".join(page.ops).encode("ascii")))
        dictionary = {
            "Type": Name("Page"), "Parent": pages_ref, "MediaBox": [0, 0, page.width, page.height], "Contents": content,
            "Resources": {"Font": fonts},
        }
        if annotation_refs:
            dictionary["Annots"] = annotation_refs
        pdf.set(ref, dictionary)
        kids.append(ref)
    pdf.set(pages_ref, {"Type": Name("Pages"), "Kids": kids, "Count": len(kids)})

    catalog = {"Type": Name("Catalog"), "Pages": pages_ref}
    if labels:
        catalog["PageLabels"] = {"Nums": labels}
    if outline:
        catalog["Outlines"] = write_outline(pdf, outline, page_refs)
        catalog["PageMode"] = Name("UseOutlines")
    info_ref = pdf.add({key: Text(value) for key, value in info.items()})
    pdf.write(path, pdf.add(catalog), info_ref)


def write_outline(pdf, items, page_refs):
    """items: (title, destination, children), where a destination is (page, "XYZ", y), (page, "FitH", y) or (page, "Fit")."""

    def destination(target):
        ref = page_refs[target[0]]
        if target[1] == "XYZ":
            return [ref, Name("XYZ"), 0, target[2], None]
        if target[1] == "FitH":
            return [ref, Name("FitH"), target[2]]
        return [ref, Name("Fit")]

    def level(children, parent):
        refs = [pdf.add() for _ in children]
        total = 0
        for index, (title, target, grandchildren) in enumerate(children):
            entry = {"Title": Text(title), "Parent": parent}
            if target:
                entry["Dest"] = destination(target)
            if index > 0:
                entry["Prev"] = refs[index - 1]
            if index + 1 < len(refs):
                entry["Next"] = refs[index + 1]
            if grandchildren:
                first, last, count = level(grandchildren, refs[index])
                entry["First"], entry["Last"], entry["Count"] = first, last, count
                total += count
            pdf.set(refs[index], entry)
            total += 1
        return refs[0], refs[-1], total

    root = pdf.add()
    first, last, count = level(items, root)
    pdf.set(root, {"Type": Name("Outlines"), "First": first, "Last": last, "Count": count})
    return root


# ---------------------------------------------------------------------------------------------------------------------------
# The sample document
# ---------------------------------------------------------------------------------------------------------------------------

ACCENT = (0.16, 0.33, 0.85)
TINT = (0.90, 0.93, 0.99)
GREY = (0.93, 0.93, 0.95)
MARGIN = 64

CHAPTERS = [
    ("Pages and coordinates", "页面与坐标",
     "A page viewer measures everything in page units. One unit at one hundred percent zoom is one device independent pixel, so "
     "a page of A4 paper is about 793 by 1123 units whatever the screen does. The origin sits in the top left corner of the page "
     "and y grows downward. A PDF file counts from the bottom left in points, and a page may also carry a rotation; the backend "
     "converts all of that, so layers and links never have to know.",
     "查看器用页面单位度量一切。百分之百缩放时，一个单位就是一个设备无关像素。原点在页面左上角，纵坐标向下增长。"
     "文件里的坐标从左下角开始，并且页面可能带有旋转，这些换算都由后端完成。"),
    ("Zoom and fit", "缩放与适应",
     "Zoom scales the page around an anchor. The wheel and a pinch keep the point under the pointer still, while buttons and keys "
     "keep the middle of the viewport still. Fit modes recompute the zoom whenever the viewport changes: fit width makes a line of "
     "text as long as the window allows, fit height shows the whole height, and fit page shows the whole sheet. Changing the zoom "
     "by hand leaves the fit mode.",
     "缩放以一个锚点为中心。滚轮和捏合让指针下的点保持不动，按钮和键盘让视口中心保持不动。适应模式会在视口变化时重新计算缩放比例。"),
    ("Continuous and single page", "连续与单页",
     "In continuous layout the pages form one long column and scrolling moves through the whole document. In single page layout one "
     "page fills the view and the next one replaces it. Pages of different sizes are centered in their column; this document has a "
     "landscape page at the end to show it. Because the presenter draws pages itself, a document of ten thousand pages costs no more "
     "visual tree than a document of one.",
     "连续布局把所有页面排成一列，滚动即可浏览全文；单页布局一次只显示一页。不同尺寸的页面会在列内居中，本文档最后一页就是横向页面。"),
    ("Rendering on demand", "按需渲染",
     "A page turns into pixels only when it is near the viewport. Requests nearest to the middle of the view go first, and requests "
     "for pages that scrolled away are cancelled before they start. While the zoom moves, the existing bitmap is stretched; a sharp one "
     "is requested once it settles. Very large zooms draw a capped page plus a detail tile for what is actually visible.",
     "页面只有接近视口时才会变成像素。离视口中心最近的请求优先，滚出视口的请求在开始之前就被取消。缩放进行时直接拉伸已有位图，停止后再请求清晰版本。"),
    ("Thumbnails and the outline", "缩略图与目录",
     "The sidebar has two pages. Thumbnails are small renders that go through the same scheduler and cache as the main view, so a "
     "hidden list costs nothing. The outline is the table of contents that the document itself carries; this file has one with two "
     "levels and titles in two languages. A document without an outline simply does not show that page of the sidebar.",
     "侧栏有两页。缩略图和主视图共用同一个调度器和缓存，隐藏时不产生任何开销。目录来自文档本身，本文件的目录有两层，标题使用两种语言。"),
    ("Searching text", "搜索文字",
     "Search works on plain text with a box for every character, which the backend provides. Matching ignores case unless asked not "
     "to, folds compatibility forms such as the ligature in the word office, and joins a word that a hyphen split across two lines. "
     "The word lighthouse appears exactly once in this document, on this page, so a test or a person can check that a search finds it.",
     "搜索基于纯文本和每个字符的位置框。匹配默认忽略大小写，并能合并被连字符拆到两行的单词。灯塔这个词在本文档中只出现一次，就在这一页。"),
    ("Selecting and copying", "选择与复制",
     "Dragging over text selects it, a double click takes a word and a triple click takes a line. A selection may run across pages and "
     "copies in reading order with a line break between pages. The text layer of a page is read only when the pointer first enters that "
     "page, so documents with thousands of pages open as quickly as short ones.",
     "在文字上拖动即可选择，双击选词，三击选行。选区可以跨页，复制时按阅读顺序拼接。文字层只在指针第一次进入页面时才读取。"),
    ("Links", "链接",
     "A link is either a place in the document or an address outside it. Internal links scroll to their target. External links open "
     "only for the http, https and mailto schemes, because a PDF file is untrusted input and a link could lead anywhere else. Addresses "
     "that are merely text on the page are detected too, like the one in the box below.",
     "链接要么指向文档内的位置，要么指向外部地址。内部链接滚动到目标；外部链接只接受安全的协议，因为文档是不可信的输入。"),
    ("Passwords and untrusted files", "密码与不可信文件",
     "A locked document asks for its password in a small form, and asks again when the password is wrong. The viewer keeps the password "
     "only for as long as opening takes, and it never reaches a log or an error message. Files can be hostile in other ways: an outline "
     "that runs in a circle is cut off, and scripts inside a document never run.",
     "受保护的文档会在表单中询问密码，密码错误时再次询问。密码只在打开期间保留，不会写入日志或错误消息。查看器也会防御循环目录等恶意内容。"),
]

CHAPTER_FIRST_PAGE = 2
TITLE_Y = 842 - MARGIN - 22


def footer(page, label):
    page.line(MARGIN, 48, page.width - MARGIN, 48, (0.82, 0.82, 0.85))
    page.text(MARGIN, 34, "The Arc page viewer", 8.5, "F1", (0.45, 0.45, 0.5))
    page.text(page.width - MARGIN - text_width(label, 8.5), 34, label, 8.5, "F1", (0.45, 0.45, 0.5))


def cover_page():
    page = Page()
    page.rect(0, 842 - 250, 595, 250, ACCENT)
    page.text(MARGIN, 842 - 130, "The Arc page viewer", 34, "F2", (1, 1, 1))
    page.text(MARGIN, 842 - 165, "A short field guide, in a PDF about itself", 14, "F1", (0.9, 0.93, 1))
    page.text(MARGIN, 842 - 195, "页面查看器简明指南", 16, "F3", (1, 1, 1))
    y = page.wrap(MARGIN, 842 - 310, 595 - MARGIN * 2,
                  "This document was written to exercise a PDF viewer: it has an outline, page labels, internal and external links, "
                  "a landscape page, English and Chinese text and a few drawings. Its text is original and may be reused freely.")
    y = page.wrap(MARGIN, y - 10, 595 - MARGIN * 2, "本文档用于测试 PDF 查看器：它包含目录、页码标签、内部与外部链接、横向页面、中英文文字和一些图形。")
    for i in range(6):
        page.rect(MARGIN + i * 78, 170, 66, 92, TINT if i % 2 else GREY, (0.75, 0.78, 0.85))
    footer(page, "i")
    return page


def contents_page(pages_total):
    page = Page()
    page.text(MARGIN, TITLE_Y, "Contents", 24, "F2")
    page.text(MARGIN + 110, TITLE_Y, "目录", 18, "F3")
    y = TITLE_Y - 44
    for index, (title, chinese, _, _) in enumerate(CHAPTERS):
        label = "%d. %s" % (index + 1, title)
        target = (CHAPTER_FIRST_PAGE + index, TITLE_Y + 30)
        page.link(MARGIN, y, label, "page", target, 12)
        page.text(MARGIN + 250, y, chinese, 11, "F3", (0.35, 0.35, 0.4))
        page.text(595 - MARGIN - 14, y, str(index + 1), 12, "F1", (0.35, 0.35, 0.4))
        y -= 26
    page.link(MARGIN, y, "Appendix: a wide page", "page", (pages_total - 1, 595 - 40), 12)
    y -= 50
    page.text(MARGIN, y, "Further reading", 14, "F2")
    y -= 24
    page.text(MARGIN, y, "The source of this library:", 11)
    page.link(MARGIN + text_width("The source of this library: ", 11), y, REPOSITORY_URL, "uri", REPOSITORY_URL, 11)
    footer(page, "ii")
    return page


def figure(page, index, y):
    """A drawing per chapter, so that the pages differ at a glance. Returns the y below it."""
    x = MARGIN
    top = y
    if index == 0:  # Axes: origin at the top left.
        page.rect(x, top - 140, 200, 140, GREY, (0.7, 0.7, 0.75))
        page.line(x, top, x + 180, top, ACCENT, 1.5)
        page.line(x, top, x, top - 120, ACCENT, 1.5)
        page.text(x + 186, top - 4, "x", 10, "F2", ACCENT)
        page.text(x + 4, top - 130, "y", 10, "F2", ACCENT)
        return top - 160
    if index == 1:  # Nested fits.
        for i, (w, h, fill) in enumerate([(300, 150, GREY), (220, 110, TINT), (140, 70, ACCENT)]):
            page.rect(x + (300 - w) / 2, top - 150 + (150 - h) / 2, w, h, fill)
        return top - 170
    if index == 2:  # A column of pages.
        for i in range(4):
            page.rect(x + 40, top - 40 - i * 46, 120, 40, GREY if i % 2 else TINT, (0.7, 0.7, 0.75))
        page.rect(x + 220, top - 150, 90, 130, TINT, (0.7, 0.7, 0.75))
        return top - 190
    if index == 3:  # Tiles.
        for row in range(4):
            for column in range(6):
                near = abs(row - 1.5) + abs(column - 2.5) < 2.2
                page.rect(x + column * 46, top - 36 - row * 36, 42, 32, ACCENT if near else GREY)
        return top - 172
    if index == 4:  # A sidebar and a page.
        page.rect(x, top - 160, 90, 160, GREY)
        for i in range(3):
            page.rect(x + 22, top - 46 - i * 50, 46, 38, (1, 1, 1), (0.7, 0.7, 0.75))
        page.rect(x + 110, top - 160, 190, 160, TINT)
        return top - 180
    if index == 5:  # Highlights.
        for i in range(6):
            page.rect(x, top - 14 - i * 16, 330, 8, GREY)
        page.rect(x + 70, top - 46, 90, 8, (1.0, 0.84, 0.04))
        page.rect(x + 190, top - 78, 60, 8, (1.0, 0.58, 0.0))
        return top - 120
    if index == 6:  # A selection across lines.
        for i in range(6):
            page.rect(x, top - 14 - i * 16, 330, 8, GREY)
        page.rect(x + 120, top - 32, 210, 8, TINT)
        page.rect(x, top - 48, 330, 8, TINT)
        page.rect(x, top - 64, 150, 8, TINT)
        return top - 120
    if index == 7:  # The box with the plain address and the links.
        page.rect(x, top - 60, 595 - MARGIN * 2, 60, GREY)
        page.text(x + 12, top - 24, "This address is only text on the page:", 10.5)
        page.text(x + 12, top - 44, PLAIN_URL, 11)
        y = top - 86
        width = page.link(x, y, "Back to the contents", "goto", (1, TITLE_Y + 30))
        page.link(x + width + 24, y, "Visit the repository", "uri", REPOSITORY_URL)
        return y - 20
    page.rect(x, top - 90, 200, 90, GREY, (0.7, 0.7, 0.75))  # A lock.
    page.rect(x + 80, top - 70, 40, 30, ACCENT)
    page.line(x + 88, top - 40, x + 88, top - 26, ACCENT, 3)
    page.line(x + 112, top - 40, x + 112, top - 26, ACCENT, 3)
    page.line(x + 88, top - 26, x + 112, top - 26, ACCENT, 3)
    return top - 110


def chapter_page(index):
    title, chinese, english_text, chinese_text = CHAPTERS[index]
    page = Page()
    number = "%d. " % (index + 1)
    page.text(MARGIN, TITLE_Y, number + title, 22, "F2")
    page.text(MARGIN, TITLE_Y - 26, chinese, 14, "F3", (0.35, 0.35, 0.4))
    y = page.wrap(MARGIN, TITLE_Y - 60, 595 - MARGIN * 2, english_text)
    y = page.wrap(MARGIN, y - 10, 595 - MARGIN * 2, chinese_text)
    figure(page, index, y - 16)
    footer(page, str(index + 1))
    return page


def appendix_page():
    page = Page(842, 595)
    page.text(MARGIN, 595 - MARGIN - 18, "Appendix: a wide page", 22, "F2")
    page.text(MARGIN + 290, 595 - MARGIN - 18, "附录：横向页面", 14, "F3", (0.35, 0.35, 0.4))
    page.wrap(MARGIN, 595 - MARGIN - 46, 842 - MARGIN * 2,
              "This page is landscape, wider than the others, so continuous layout has to center pages of different widths. The table "
              "lists the keys a viewer answers to.")
    rows = [("Keys", "What they do"), ("Arrow keys", "Scroll by a small step"), ("Page Up, Page Down", "Scroll by a screen, or turn a page in single page layout"),
            ("Home, End", "First page, last page"), ("Ctrl or Cmd with + and -", "Zoom in, zoom out"), ("Ctrl or Cmd with 0", "Back to one hundred percent"),
            ("Ctrl or Cmd with F", "Open search")]
    y = 595 - MARGIN - 100
    for index, (keys, action) in enumerate(rows):
        page.rect(MARGIN, y - 8, 842 - MARGIN * 2, 26, ACCENT if index == 0 else GREY if index % 2 else TINT)
        color = (1, 1, 1) if index == 0 else (0.13, 0.13, 0.15)
        page.text(MARGIN + 10, y, keys, 11, "F2" if index == 0 else "F1", color)
        page.text(MARGIN + 240, y, action, 11, "F2" if index == 0 else "F1", color)
        y -= 26
    footer(page, "10")
    return page


def make_sample():
    pages = [cover_page(), None]
    pages.extend(chapter_page(i) for i in range(len(CHAPTERS)))
    pages.append(appendix_page())
    pages[1] = contents_page(len(pages))
    assert len(pages) == 12
    info = {
        "Title": "The Arc page viewer", "Author": "TKCode.Arc", "Subject": "A sample document for the PDF viewer",
        "Keywords": "pdf, viewer, sample", "Creator": "tools/pdf/make-sample.py", "Producer": "tools/pdf/make-sample.py",
        "CreationDate": "D:20261108120000+08'00'", "ModDate": "D:20261108120000+08'00'",
    }
    chapter_outline = []
    for i, (title, chinese, _, _) in enumerate(CHAPTERS):
        chapter_outline.append(("%d. %s  %s" % (i + 1, title, chinese), (CHAPTER_FIRST_PAGE + i, "XYZ", TITLE_Y + 30), []))
    outline = [
        ("Cover", (0, "Fit"), []),
        ("Contents  目录", (1, "XYZ", TITLE_Y + 30), []),
        ("Viewing  查看", None, chapter_outline[0:3]),
        ("Documents  文档", (CHAPTER_FIRST_PAGE + 3, "FitH", TITLE_Y + 30), chapter_outline[3:5]),
        ("Reading  阅读", (CHAPTER_FIRST_PAGE + 5, "XYZ", TITLE_Y + 30), chapter_outline[5:8]),
        (chapter_outline[8][0], chapter_outline[8][1], []),
        ("Appendix  附录", (len(pages) - 1, "XYZ", 595 - 40), []),
    ]
    labels = [0, {"S": Name("r")}, 2, {"S": Name("D")}]
    write_document(os.path.join(SAMPLES, "arc-sample.pdf"), "arc-sample", pages, info, outline, labels)


def make_locked_sample():
    first = Page()
    first.rect(0, 842 - 180, 595, 180, ACCENT)
    first.text(MARGIN, 842 - 110, "A locked document", 30, "F2", (1, 1, 1))
    y = first.wrap(MARGIN, 842 - 240, 595 - MARGIN * 2,
                   "You are reading this, so the password was right. It is written in the documentation of the viewer on purpose: this "
                   "file only exists to show how a viewer asks for a password. The password is arc-sample.")
    first.wrap(MARGIN, y - 10, 595 - MARGIN * 2, "您能看到这段文字，说明密码正确。这个文件只用于演示查看器如何询问密码。")
    footer(first, "1")
    second = chapter_page(8)
    third = Page()
    third.text(MARGIN, TITLE_Y, "The last page", 22, "F2")
    third.wrap(MARGIN, TITLE_Y - 36, 595 - MARGIN * 2,
               "Everything in this file except its security dictionary is encrypted with RC4 and a 128-bit key: the page content, the "
               "outline titles and the document information.")
    footer(third, "3")
    info = {"Title": "A locked document", "Author": "TKCode.Arc", "Creator": "tools/pdf/make-sample.py", "Producer": "tools/pdf/make-sample.py",
            "CreationDate": "D:20261108120000+08'00'"}
    outline = [("A locked document", (0, "Fit"), []), ("The last page", (2, "Fit"), [])]
    write_document(os.path.join(SAMPLES, "arc-sample-locked.pdf"), "arc-sample-locked", [first, second, third], info, outline,
                   password=LOCKED_PASSWORD, owner_password=LOCKED_OWNER_PASSWORD)


# ---------------------------------------------------------------------------------------------------------------------------
# Test fixtures
# ---------------------------------------------------------------------------------------------------------------------------


def make_rotated():
    """Five pages with the word MARK at (50, 500) and a black square at (300, 100)-(350, 150) of page space. The first four are
    400 x 600 with /Rotate 0, 90, 180 and 270; the fifth has the MediaBox [100 200 500 800], so the same drawing moves with the box.
    Page 2 has a link over the word that leads to (50, 500) of page 3."""
    pdf = PdfFile("rotated")
    font = latin_font(pdf, "Helvetica")
    content = b"BT /F1 24 Tf 50 500 Td (MARK) Tj ET\n0 0 0 rg 300 100 50 50 re f"
    shifted = b"BT /F1 24 Tf 150 700 Td (MARK) Tj ET\n0 0 0 rg 400 300 50 50 re f"
    pages_ref = pdf.add()
    refs = [pdf.add() for _ in range(5)]
    for index, ref in enumerate(refs):
        page = {"Type": Name("Page"), "Parent": pages_ref, "Resources": {"Font": {"F1": font}}}
        if index < 4:
            page["MediaBox"] = [0, 0, 400, 600]
            page["Rotate"] = index * 90
            page["Contents"] = pdf.add(Stream(content))
        else:
            page["MediaBox"] = [100, 200, 500, 800]
            page["Contents"] = pdf.add(Stream(shifted))
        if index == 1:
            page["Annots"] = [pdf.add({
                "Type": Name("Annot"), "Subtype": Name("Link"), "Rect": [50, 495, 120, 525], "Border": [0, 0, 0],
                "Dest": [refs[2], Name("XYZ"), 50, 500, None],
            })]
        pdf.set(ref, page)
    pdf.set(pages_ref, {"Type": Name("Pages"), "Kids": refs, "Count": len(refs)})
    pdf.write(os.path.join(FIXTURES, "rotated.pdf"), pdf.add({"Type": Name("Catalog"), "Pages": pages_ref}))


def small_document(pdf, count=3):
    """The pages of a document with little on them. Returns the Pages reference and the page references."""
    font = latin_font(pdf, "Helvetica")
    pages_ref = pdf.add()
    refs = []
    for index in range(count):
        content = pdf.add(Stream(b"BT /F1 18 Tf 72 700 Td (Page %d) Tj ET" % (index + 1)))
        refs.append(pdf.add({
            "Type": Name("Page"), "Parent": pages_ref, "MediaBox": [0, 0, 612, 792], "Contents": content,
            "Resources": {"Font": {"F1": font}},
        }))
    pdf.set(pages_ref, {"Type": Name("Pages"), "Kids": refs, "Count": count})
    return pages_ref, refs


def make_cyclic_outline():
    """A -> Next B -> Next A runs in a circle among siblings, and B's child C has B as its own child, a circle through the levels."""
    pdf = PdfFile("cyclic-outline")
    pages_ref, refs = small_document(pdf)
    root, a, b, c = pdf.add(), pdf.add(), pdf.add(), pdf.add()
    pdf.set(a, {"Title": Text("A"), "Parent": root, "Next": b, "Prev": b, "Dest": [refs[0], Name("Fit")]})
    pdf.set(b, {"Title": Text("B"), "Parent": root, "Next": a, "Prev": a, "First": c, "Last": c, "Count": 1, "Dest": [refs[1], Name("Fit")]})
    pdf.set(c, {"Title": Text("C"), "Parent": b, "First": b, "Last": b, "Count": 1, "Dest": [refs[2], Name("Fit")]})
    pdf.set(root, {"Type": Name("Outlines"), "First": a, "Last": b, "Count": 2})
    pdf.write(os.path.join(FIXTURES, "cyclic-outline.pdf"), pdf.add({"Type": Name("Catalog"), "Pages": pages_ref, "Outlines": root}))


def make_deep_outline():
    pdf = PdfFile("deep-outline")
    pages_ref, refs = small_document(pdf, 1)
    root = pdf.add()
    nodes = [pdf.add() for _ in range(100)]
    for depth, node in enumerate(nodes):
        entry = {"Title": Text("Level %d" % depth), "Parent": nodes[depth - 1] if depth else root, "Dest": [refs[0], Name("Fit")]}
        if depth + 1 < len(nodes):
            entry["First"] = nodes[depth + 1]
            entry["Last"] = nodes[depth + 1]
            entry["Count"] = 1
        pdf.set(node, entry)
    pdf.set(root, {"Type": Name("Outlines"), "First": nodes[0], "Last": nodes[0], "Count": 1})
    pdf.write(os.path.join(FIXTURES, "deep-outline.pdf"), pdf.add({"Type": Name("Catalog"), "Pages": pages_ref, "Outlines": root}))


def make_corrupt():
    noise = b"".join(md5(b"corrupt-%d" % i) for i in range(64))
    os.makedirs(FIXTURES, exist_ok=True)
    path = os.path.join(FIXTURES, "corrupt.pdf")
    with open(path, "wb") as f:
        f.write(b"%PDF-1.7\n" + noise)
    print("%-90s %7d bytes" % (os.path.relpath(path, ROOT), len(noise) + 9))


if __name__ == "__main__":
    make_sample()
    make_locked_sample()
    make_rotated()
    make_cyclic_outline()
    make_deep_outline()
    make_corrupt()
