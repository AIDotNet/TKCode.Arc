"""Build the non-US maps for the Choropleth explorer Gallery demo.

Source: Natural Earth 1:10m admin-1 states and provinces (public domain, naturalearthdata.com).
Each country: conic equal-area projection, fitted to a 975 x 610 view box (y 18..592, centred), simplified with
Visvalingam-Whyatt. France, Italy and the United Kingdom are dissolved into their regions (top level) and keep their
departments / provinces / council areas as subregions.
Usage: python3 -I build_world.py <ne_dir> <out_dir>
Output: <out_dir>/map-<iso>.txt with lines
  S|<id>|<code>|<name>|<x0>,<y0>,<x1>,<y1>|<path>
  C|<parent id>|<id>|<name>|<x0>,<y0>,<x1>,<y1>|<path>
"""
import math
import struct
import sys

NE, OUT = sys.argv[1], sys.argv[2]
_src = open(__file__.replace("build_world.py", "build_us.py"), encoding="utf-8").read()
exec(_src.split("states_rows = read_dbf")[0].split("RAW, OUT = sys.argv[1], sys.argv[2]")[1])

W, H, PAD = 975.0, 610.0, 18.0

COUNTRIES = {
    # iso: (adm0_a3, rotate, parallels, dissolve, min_area, sub_min_area)
    "ca": ("CAN", 96, (50, 70), False, 0.5, 0),
    "br": ("BRA", 54, (-2, -22), False, 0.35, 0),
    "gb": ("GBR", 2, (50, 58), True, 0.35, 0.15),
    "fr": ("FRA", -2.5, (44, 49), True, 0.35, 0.15),
    "de": ("DEU", -10.5, (48, 54), False, 0.3, 0),
    "it": ("ITA", -12.5, (38, 46), True, 0.35, 0.15),
    "au": ("AUS", -134, (-18, -36), False, 0.35, 0),
}
DROP = {"Jervis Bay Territory", "Macquarie Island", "Lord Howe Island"}
FRANCE_OVERSEAS = {"Guyane française", "Martinique", "Guadeloupe", "Réunion", "Mayotte"}
ITALY_EN = {"Lombardia": "Lombardy", "Toscana": "Tuscany", "Piemonte": "Piedmont", "Sardegna": "Sardinia",
            "Apulia": "Apulia", "Sicily": "Sicily", "Valle d'Aosta": "Aosta Valley",
            "Trentino-Alto Adige": "Trentino-South Tyrol"}


def clean(v):
    return v.replace("\x00", "").strip()


def outlier(a3, ring):
    # Remote islands that would shrink the fitted map: Rockall (UK), the Atlantic islands of Brazil.
    lon = sum(p[0] for p in ring) / len(ring)
    return (a3 == "GBR" and lon < -10) or (a3 == "BRA" and lon > -33.5)


def gb_region(row):
    unit = clean(row["geonunit"])
    if unit in ("Scotland", "Wales", "Northern Ireland"):
        return unit
    region = clean(row["region"])
    return {"Eastern": "East of England", "East": "East of England", "Greater London": "London"}.get(region, region)


def dissolve(rings):
    """Union of edge-adjacent rings: drop edges shared in opposite directions, re-chain the rest."""
    key = lambda p: (round(p[0], 7), round(p[1], 7))
    count = {}
    for ring in rings:
        pts = ring if ring[0] != ring[-1] else ring[:-1]
        for i in range(len(pts)):
            a, b = key(pts[i]), key(pts[(i + 1) % len(pts)])
            if a == b:
                continue
            if (b, a) in count:
                count[(b, a)] -= 1
                if count[(b, a)] == 0:
                    del count[(b, a)]
            else:
                count[(a, b)] = count.get((a, b), 0) + 1
    nxt = {}
    for (a, b), n in count.items():
        for _ in range(n):
            nxt.setdefault(a, []).append(b)
    out = []
    while nxt:
        start = next(iter(nxt))
        ring, cur = [start], start
        while True:
            options = nxt.get(cur)
            if not options:
                break
            b = options.pop()
            if not options:
                del nxt[cur]
            if b == start:
                break
            ring.append(b)
            cur = b
        if len(ring) >= 3:
            out.append(ring)
    return out


def emit(kind, parent, ident, name, rings, fit, min_area, min_ring):
    proj = []
    for ring in rings:
        pr = [fit(p) for p in ring]
        if ring_area(pr) < min_ring:
            continue
        s = visvalingam_fast(pr + [pr[0]], min_area)
        if len(s) >= 3:
            proj.append(s)
    if not proj:
        return None
    xs = [p[0] for r in proj for p in r]
    ys = [p[1] for r in proj for p in r]
    bbox = ",".join("%.1f" % v for v in (min(xs), min(ys), max(xs), max(ys)))
    return "%s|%s|%s|%s|%s|%s" % (kind, parent, ident, name, bbox, "".join(relative_ring(r) for r in proj))


rows = read_dbf(NE + "/ne_10m_admin_1_states_provinces.dbf")
shapes = read_shp(NE + "/ne_10m_admin_1_states_provinces.shp")
for iso, (a3, rot, par, diss, min_area, sub_min_area) in COUNTRIES.items():
    conic = Conic(rot, (0, sum(par) / 2), par, 1.0, (0.0, 0.0))
    units = []
    for shape, row in zip(shapes, rows):
        if clean(row["adm0_a3"]) != a3:
            continue
        name = clean(row["name"])
        if name in DROP:
            continue
        region = clean(row["region"])
        if a3 == "FRA" and region in FRANCE_OVERSEAS:
            continue
        parent = gb_region(row) if a3 == "GBR" else ITALY_EN.get(region, region) if a3 == "ITA" else region
        units.append((clean(row["adm1_code"]), name, clean(row["postal"]) or clean(row["iso_3166_2"]), parent,
                       [[conic(lon, lat) for lon, lat in ring] for ring in shape if not outlier(a3, ring)]))
    xs = [p[0] for u in units for r in u[4] for p in r]
    ys = [p[1] for u in units for r in u[4] for p in r]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    k = min((H - 2 * PAD) / (y1 - y0), (W - 2 * PAD) / (x1 - x0))
    ox = (W - k * (x1 - x0)) / 2 - k * x0
    oy = (H - k * (y1 - y0)) / 2 - k * y0
    fit = lambda p, k=k, ox=ox, oy=oy: (ox + k * p[0], oy + k * p[1])
    lines = []
    if diss:
        groups = {}
        for u in units:
            groups.setdefault(u[3], []).append(u)
        for parent in sorted(groups):
            rings = dissolve([r for u in groups[parent] for r in u[4]])
            pid = "%s-%s" % (iso, parent.lower().replace(" ", "-").replace("'", ""))
            line = emit("S", pid, pid, parent, rings, fit, min_area, 0.6)
            if line:
                lines.append(line)
            for u in sorted(groups[parent], key=lambda u: u[1]):
                sub = emit("C", pid, u[0], u[1], u[4], fit, sub_min_area, 0.004)
                if sub:
                    lines.append(sub)
    else:
        for u in sorted(units, key=lambda u: u[1]):
            line = emit("S", u[0], u[2], u[1], u[4], fit, min_area, 0.6)
            if line:
                lines.append(line)
    with open("%s/map-%s.txt" % (OUT, iso), "w", encoding="utf-8") as f:
        f.write("# %s admin-1 regions, view box 975 x 610. Source: Natural Earth 1:10m admin-1 (public domain), "
                "projected (conic equal-area) and simplified.\n" % a3)
        f.write("\n".join(lines) + "\n")
    tops = sum(1 for l in lines if l.startswith("S"))
    print(iso, tops, "regions", len(lines) - tops, "subregions", sum(len(l) for l in lines), "bytes")
