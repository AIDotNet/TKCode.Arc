"""Build the US states + counties map for the Choropleth explorer Gallery demo.

Source: US Census Bureau cartographic boundary files cb_2023_us_state_20m / cb_2023_us_county_20m (public domain).
Projection: Albers USA composite (lower 48 conic equal-area 29.5/45.5, Alaska and Hawaii insets), scale 1300,
translate (487.5, 305) -> a 975 x 610 view box. Rings are simplified with Visvalingam-Whyatt.
Usage: python3 -I build_us.py <raw_dir> <out_file>
Output lines (UTF-8, '|' separated):
  S|<postal>|<fips>|<name>|<x0>,<y0>,<x1>,<y1>|<path>
  C|<state postal>|<geoid>|<name>|<x0>,<y0>,<x1>,<y1>|<path>
"""
import math
import struct
import sys

RAW, OUT = sys.argv[1], sys.argv[2]
K, TX, TY = 1300.0, 487.5, 305.0
EXCLUDE = {"60", "66", "69", "72", "78"}  # territories


def read_dbf(path):
    with open(path, "rb") as f:
        data = f.read()
    n = struct.unpack("<I", data[4:8])[0]
    hlen, rlen = struct.unpack("<HH", data[8:12])
    fields = []
    pos = 32
    while data[pos] != 0x0D:
        name = data[pos:pos + 11].split(b"\0")[0].decode()
        flen = data[pos + 16]
        fields.append((name, flen))
        pos += 32
    rows = []
    for i in range(n):
        rec = data[hlen + i * rlen + 1:hlen + (i + 1) * rlen]
        row, p = {}, 0
        for name, flen in fields:
            row[name] = rec[p:p + flen].decode("utf-8", "replace").strip()
            p += flen
        rows.append(row)
    return rows


def read_shp(path):
    with open(path, "rb") as f:
        data = f.read()
    pos, shapes = 100, []
    while pos < len(data):
        _, clen = struct.unpack(">II", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + clen * 2]
        pos += 8 + clen * 2
        stype = struct.unpack("<i", body[0:4])[0]
        if stype == 0:
            shapes.append([])
            continue
        nparts, npts = struct.unpack("<ii", body[36:44])
        parts = list(struct.unpack("<%di" % nparts, body[44:44 + 4 * nparts]))
        off = 44 + 4 * nparts
        pts = [struct.unpack("<dd", body[off + 16 * i:off + 16 * i + 16]) for i in range(npts)]
        parts.append(npts)
        shapes.append([pts[parts[i]:parts[i + 1]] for i in range(nparts)])
    return shapes


class Conic:
    def __init__(self, rotate, center, parallels, scale, translate):
        y0, y1 = (math.radians(p) for p in parallels)
        sy0 = math.sin(y0)
        self.n = (sy0 + math.sin(y1)) / 2
        self.c = 1 + sy0 * (2 * self.n - sy0)
        self.r0 = math.sqrt(self.c) / self.n
        self.rot = rotate
        self.k = scale
        cx, cy = self.raw(math.radians(center[0]), math.radians(center[1]))
        self.dx = translate[0] - scale * cx
        self.dy = translate[1] + scale * cy

    def raw(self, x, y):
        r = math.sqrt(max(0.0, self.c - 2 * self.n * math.sin(y))) / self.n
        return r * math.sin(x * self.n), self.r0 - r * math.cos(x * self.n)

    def __call__(self, lon, lat):
        lam = lon + self.rot
        lam = (lam + 180) % 360 - 180
        x, y = self.raw(math.radians(lam), math.radians(lat))
        return self.dx + self.k * x, self.dy - self.k * y


LOWER48 = Conic(96, (-0.6, 38.7), (29.5, 45.5), K, (TX, TY))
ALASKA = Conic(154, (-2, 58.5), (55, 65), 0.35 * K, (TX - 0.307 * K, TY + 0.201 * K))
HAWAII = Conic(157, (-3, 19.9), (8, 18), K, (TX - 0.205 * K, TY + 0.212 * K))


def projector(statefp):
    return {"02": ALASKA, "15": HAWAII}.get(statefp, LOWER48)


def area2(a, b, c):
    return abs((b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])) / 2


def visvalingam(ring, min_area):
    pts = ring[:-1] if ring[0] == ring[-1] else ring[:]
    if len(pts) <= 4:
        return pts
    alive = list(range(len(pts)))
    changed = True
    while changed and len(alive) > 4:
        changed = False
        best, best_i = None, -1
        m = len(alive)
        for j in range(m):
            a, b, c = pts[alive[j - 1]], pts[alive[j]], pts[alive[(j + 1) % m]]
            ar = area2(a, b, c)
            if best is None or ar < best:
                best, best_i = ar, j
        if best is not None and best < min_area:
            del alive[best_i]
            changed = True
    return [pts[i] for i in alive]


def visvalingam_fast(ring, min_area):
    # Heap-free but batched: drop every vertex under the threshold whose neighbours stay, repeat.
    pts = ring[:-1] if ring[0] == ring[-1] else ring[:]
    while len(pts) > 4:
        m = len(pts)
        areas = [area2(pts[j - 1], pts[j], pts[(j + 1) % m]) for j in range(m)]
        keep, dropped, j = [True] * m, 0, 0
        for j in range(m):
            if areas[j] < min_area and keep[j - 1] and keep[(j + 1) % m] and m - dropped > 4:
                keep[j] = False
                dropped += 1
        if dropped == 0:
            break
        pts = [p for p, k in zip(pts, keep) if k]
    return pts


def ring_area(pts):
    s = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i - 1]
        x1, y1 = pts[i]
        s += x0 * y1 - x1 * y0
    return abs(s) / 2


def num(v):
    t = "%.1f" % v
    t = t.rstrip("0").rstrip(".") if "." in t else t
    return "0" if t in ("-0", "") else t


def relative_ring(ring):
    # Relative SVG commands rounded to 0.1 view-box units: "M x,y l dx,dy ... z".
    cx, cy = round(ring[0][0], 1), round(ring[0][1], 1)
    out = ["M%s,%sl" % (num(cx), num(cy))]
    first = True
    for x, y in ring[1:]:
        x, y = round(x, 1), round(y, 1)
        dx, dy = round(x - cx, 1), round(y - cy, 1)
        if dx == 0 and dy == 0:
            continue
        sx, sy = num(dx), num(dy)
        out.append(("" if first or sx.startswith("-") else " ") + sx + ("," if not sy.startswith("-") else "") + sy)
        first = False
        cx, cy = x, y
    out.append("z")
    return "".join(out)


def build(shapes, rows, kind, min_area, min_ring, digits):
    out = []
    fmt = "%%.%df" % digits
    for shape, row in zip(shapes, rows):
        sfp = row["STATEFP"]
        if sfp in EXCLUDE:
            continue
        proj = projector(sfp)
        rings = []
        for ring in shape:
            pr = [proj(lon, lat) for lon, lat in ring]
            if ring_area(pr) < min_ring:
                continue
            s = visvalingam_fast(pr, min_area)
            if len(s) >= 3:
                rings.append(s)
        if not rings:
            continue
        xs = [p[0] for r in rings for p in r]
        ys = [p[1] for r in rings for p in r]
        bbox = ",".join(fmt % v for v in (min(xs), min(ys), max(xs), max(ys)))
        path = "".join(relative_ring(r) for r in rings)
        if kind == "S":
            out.append("S|%s|%s|%s|%s|%s" % (row["STUSPS"], sfp, row["NAME"], bbox, path))
        else:
            out.append("C|%s|%s|%s|%s|%s" % (row["STUSPS_"] if "STUSPS_" in row else row.get("STUSPS", sfp), row["GEOID"], row["NAME"], bbox, path))
    return out


states_rows = read_dbf(RAW + "/state/cb_2023_us_state_20m.dbf")
states = read_shp(RAW + "/state/cb_2023_us_state_20m.shp")
postal = {r["STATEFP"]: r["STUSPS"] for r in states_rows}
county_rows = read_dbf(RAW + "/county/cb_2023_us_county_20m.dbf")
for r in county_rows:
    r["STUSPS"] = postal.get(r["STATEFP"], r["STATEFP"])
counties = read_shp(RAW + "/county/cb_2023_us_county_20m.shp")
lines = build(states, states_rows, "S", 0.35, 0.6, 1)
lines.sort(key=lambda l: l.split("|")[3])
clines = build(counties, county_rows, "C", 0.02, 0.004, 1)
# Counties only for the states the demo lets you open (our own choice of ten).
COUNTY_STATES = {"CA", "TX", "NY", "FL", "WA", "IL", "PA", "GA", "CO", "MA"}
clines = [l for l in clines if l.split("|")[1] in COUNTY_STATES]
clines.sort(key=lambda l: (l.split("|")[1], l.split("|")[3]))
with open(OUT, "w", encoding="utf-8") as f:
    f.write("# US states and counties, Albers USA, view box 975 x 610. Source: US Census Bureau cartographic boundary\n")
    f.write("# files cb_2023_us_state_20m and cb_2023_us_county_20m (public domain), simplified.\n")
    f.write("\n".join(lines + clines) + "\n")
print(len(lines), "states", len(clines), "counties")
