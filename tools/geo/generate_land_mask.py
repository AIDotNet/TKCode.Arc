#!/usr/bin/env python3
"""Rasterize Natural Earth land polygons into the bit mask RevenueGlobe samples its land dots from.

    python3 -I tools/geo/generate_land_mask.py <ne_110m_land.geojson> <out.bin> [cells-per-degree=2]

Input: Natural Earth 1:110m Land (public domain), for example
https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_110m_land.geojson

Output layout (little endian):
    bytes 0-3   magic b"LNDM"
    byte  4     cells per degree (k); the grid is 360k columns by 180k rows
    bytes 5-7   reserved (0)
    then        rows from north (+90) to south (-90), columns from -180 to +180, one bit per cell, LSB first,
                each row padded to whole bytes; a set bit means the cell centre lies on land.
"""
import json
import sys


def rings_of(geometry):
    kind = geometry["type"]
    if kind == "Polygon":
        yield from geometry["coordinates"]
    elif kind == "MultiPolygon":
        for polygon in geometry["coordinates"]:
            yield from polygon


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        sys.exit(2)
    source, target = sys.argv[1], sys.argv[2]
    k = int(sys.argv[3]) if len(sys.argv) > 3 else 2
    with open(source, encoding="utf-8") as f:
        data = json.load(f)

    edges = []
    for feature in data["features"]:
        for ring in rings_of(feature["geometry"]):
            for (x0, y0), (x1, y1) in zip(ring, ring[1:] + ring[:1]):
                if y0 != y1:
                    edges.append((x0, y0, x1, y1))

    cols, rows = 360 * k, 180 * k
    stride = (cols + 7) // 8
    out = bytearray(b"LNDM" + bytes([k, 0, 0, 0]))
    land = 0
    for row in range(rows):
        lat = 90 - (row + 0.5) / k
        # Even-odd scanline fill: every edge crossing this latitude toggles inside/outside.
        xs = sorted(x0 + (lat - y0) * (x1 - x0) / (y1 - y0)
                    for x0, y0, x1, y1 in edges if (y0 <= lat < y1) or (y1 <= lat < y0))
        bits = bytearray(stride)
        for a, b in zip(xs[0::2], xs[1::2]):
            first = max(0, int((a + 180) * k - 0.5 + 0.999999))
            last = min(cols - 1, int((b + 180) * k - 0.5))
            for col in range(first, last + 1):
                bits[col >> 3] |= 1 << (col & 7)
                land += 1
        out += bits
    with open(target, "wb") as f:
        f.write(out)
    print(f"{cols}x{rows} cells, {land} land ({land / (cols * rows):.1%}), {len(out)} bytes -> {target}")


if __name__ == "__main__":
    main()
