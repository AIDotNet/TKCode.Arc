"""Prints the DT_NEEDED entries or the defined global dynamic symbols of an ELF64 little-endian shared library.
Used by tools/video/build-linux.sh when no readelf is installed (e.g. on macOS).
Usage: python3 elfinfo.py needed|exports <file.so>"""
import struct
import sys


def main():
    mode, path = sys.argv[1], sys.argv[2]
    data = open(path, "rb").read()
    if data[:4] != b"\x7fELF" or data[4] != 2 or data[5] != 1:
        sys.exit("not an ELF64 little-endian file")
    shoff, = struct.unpack_from("<Q", data, 0x28)
    shentsize, shnum = struct.unpack_from("<HH", data, 0x3A)
    sections = []
    for i in range(shnum):
        name, stype, flags, addr, off, size, link, info, align, entsize = struct.unpack_from(
            "<IIQQQQIIQQ", data, shoff + i * shentsize)
        sections.append((stype, off, size, link, entsize))

    def string(table, offset):
        start = sections[table][1] + offset
        return data[start:data.index(b"\0", start)].decode()

    for stype, off, size, link, entsize in sections:
        if mode == "needed" and stype == 6:  # SHT_DYNAMIC
            for j in range(size // 16):
                tag, val = struct.unpack_from("<qQ", data, off + j * 16)
                if tag == 1:  # DT_NEEDED
                    print("NEEDED " + string(link, val))
        if mode == "exports" and stype == 11:  # SHT_DYNSYM
            for j in range(size // 24):
                st_name, st_info, st_other, st_shndx, st_value, st_size = struct.unpack_from(
                    "<IBBHQQ", data, off + j * 24)
                bind = st_info >> 4
                visibility = st_other & 3
                if bind == 1 and st_shndx != 0 and visibility == 0:
                    print(string(link, st_name))


main()
