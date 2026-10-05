r"""
Family generator: Kayak (Phase 1 style anchor) - the River Agency's sit-in touring kayak (reference: the developer's
orange/black kayak photo), in the faceted style of the approved pine and rock:
  - HULL lofted from a few cross-sections (keel, bilge, chine, sheer, a black gunwale lip, deck shoulder, crown): the
    big planes show, like the pine blades and the rock faces; normals mostly flat with a little smoothing;
  - ORANGE deck, near-black hull and lip; a cockpit with a black coaming, a dark tub and a padded seat (the paddler's hips
    sit where the game's procedural animation puts them);
  - the agency's CARGO WELL behind the cockpit (a parcel or a second passenger), under a bungee net;
  - bungee cords in an X on the front deck, a round hatch, deck fittings and carry handles at both ends.
The game's physics keep their own capsule collider: no _COL here. Bow toward Blender -Y (Unity +Z).

    art build Kayak\kayak_gen.py --asset CaiaqueA [--length 3.3] [--width 0.68] [--stations 18]
    --draft x renders a test sheet into ArtSource/_tmp without making a version.
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "pipeline"))
import common  # noqa: E402
import lookdev  # noqa: E402
import preview  # noqa: E402

FAMILY = "Kayak"
BUDGET = {"LOD0": 2500, "LOD1": 800, "LOD2": 200}

# The game's kayak (KayakPrefabBuilder): the paddler's hips at Unity (0, 0.13, -0.16) -> Blender (0, 0.16, 0.13)
HIPS = Vector((0.0, 0.16, 0.13))


def srgb(h):
    return Vector(common.hex_srgb(h))


# ---------------------------------------------------------------- hull shape
def station(p, s):
    """The hull at s in [-1 bow, +1 stern]: half width, keel z, sheer z, deck crown z (Blender meters)."""
    a = abs(s)
    sw = s - p["widest"]  # the widest point a little aft of the middle
    sw = sw / (1 + p["widest"]) if sw > 0 else sw / (1 - p["widest"])
    bow = s < 0
    w = p["width"] / 2 * max(0.0, 1 - abs(sw) ** 2.3) ** (0.62 if bow else 0.55)
    keel = -0.15 + p["rocker"] * a ** 2.6 + p["end_rise"] * a ** 14
    sheer = 0.05 + p["sheer_rise"] * a ** 2.2
    # The deck: a peak on the front deck (knee room), low at the cockpit, a flatter rear deck, closing at the ends
    front = math.exp(-((s + 0.38) / 0.3) ** 2) * 0.09
    rear = math.exp(-((s - 0.55) / 0.35) ** 2) * 0.045
    crown = (0.035 + front + rear) * (1 - a ** 6)
    lift = p["lift"]  # the waterline: the deck must stay above the game's water
    return w, keel + lift, sheer + lift, sheer + crown + lift


SEGMENT_BAND = ("bottom", "bottom", "side", "lip", "deck", "deck", "deck")  # the face from profile point i-1 to i

PROFILE = (  # (x as a fraction of the half width, z as a blend keel..sheer..crown, band)
    (0.00, ("k", 0.0), "bottom"),
    (0.48, ("ks", 0.12), "bottom"),
    (0.84, ("ks", 0.42), "bottom"),
    (1.00, ("ks", 0.96), "lip"),
    (0.985, ("sc", 0.0), "lip"),     # the black gunwale lip, from the sheer up a little
    (0.93, ("sc", 0.18), "deck"),
    (0.66, ("sc", 0.72), "deck"),
    (0.00, ("sc", 1.0), "deck"),
)


def profile_z(spec, keel, sheer, deck):
    kind, t = spec
    if kind == "k":
        return keel
    if kind == "ks":
        return keel + (sheer - keel) * t
    return sheer + 0.022 + (deck - sheer - 0.022) * t if t > 0 else sheer + 0.022


def build_hull(bm, p, stations, profile_idx):
    """Lofted hull: rings of the profile (mirrored) at each station, a single tip vertex at each end. Returns the
    band of each face (bottom / lip / deck) in a dict keyed by the face."""
    prof = [PROFILE[i] for i in profile_idx]
    L = p["length"]
    # Stations closer together at the ends, where the shape turns fastest
    ss = [math.sin((i / (stations - 1) - 0.5) * math.pi) * 0.985 for i in range(stations)]
    rings = []
    for s in ss:
        w, keel, sheer, deck = station(p, s)
        y = s * L / 2
        right = [Vector((fx * w, y, profile_z(spec, keel, sheer, deck))) for fx, spec, _ in prof]
        ring = [right[0]] + right[1:-1] + [right[-1]] + [Vector((-v.x, v.y, v.z)) for v in reversed(right[1:-1])]
        rings.append([bm.verts.new(v) for v in ring])
    bands = {}
    band_of_edge = [SEGMENT_BAND[profile_idx[i + 1] - 1] for i in range(len(prof) - 1)]
    n = len(rings[0])
    seg_band = band_of_edge + list(reversed(band_of_edge))
    for r in range(len(rings) - 1):
        a, b = rings[r], rings[r + 1]
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((a[i], a[j], b[j], b[i]))
            bands[f] = seg_band[i]
    for end, ring in ((-1, rings[0]), (1, rings[-1])):
        w, keel, sheer, deck = station(p, end)
        tip = bm.verts.new(Vector((0, end * L / 2 * 1.0, sheer + 0.03)))
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((ring[i], tip, ring[j]) if end < 0 else (ring[j], tip, ring[i]))
            bands[f] = seg_band[i]
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    return bands


# ---------------------------------------------------------------- small parts (each a list of (bmesh, colour))
def ellipse(cx, cy, rx, ry, n, z=0.0, phase=0.0):
    return [Vector((cx + rx * math.cos(phase + 2 * math.pi * i / n), cy + ry * math.sin(phase + 2 * math.pi * i / n), z)) for i in range(n)]


class Parts:
    """Collects extra geometry into the hull bmesh with a colour per face."""

    def __init__(self, bm, colours):
        self.bm, self.colours = bm, colours

    def face(self, verts, colour):
        f = self.bm.faces.new([self.bm.verts.new(v) for v in verts])
        self.colours[f] = colour
        return f

    def loft(self, loops, colour, cap_start=False, cap_end=False, closed=True):
        """Quads between consecutive loops of points (same count)."""
        vs = [[self.bm.verts.new(v) for v in loop] for loop in loops]
        n = len(loops[0])
        for a, b in zip(vs, vs[1:]):
            for i in range(n if closed else n - 1):
                j = (i + 1) % n
                f = self.bm.faces.new((a[i], a[j], b[j], b[i]))
                self.colours[f] = colour
        if cap_start:
            f = self.bm.faces.new(list(reversed(vs[0])))
            self.colours[f] = colour
        if cap_end:
            f = self.bm.faces.new(vs[-1])
            self.colours[f] = colour
        return vs

    def box(self, center, size, colour, rot=Matrix.Identity(3), taper=1.0):
        hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
        bot = [Vector((sx * hx, sy * hy, -hz)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        top = [Vector((v.x * taper, v.y * taper, hz)) for v in bot]
        loops = [[center + rot @ v for v in bot], [center + rot @ v for v in top]]
        self.loft(loops, colour, cap_start=True, cap_end=True)

    def tube(self, pts, radius, colour, sides=3, up=Vector((0, 0, 1))):
        """A thin cord through pts (a triangle cross-section, flat side down)."""
        loops = []
        for i, pt in enumerate(pts):
            d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
            side = d.cross(up).normalized()
            nup = side.cross(d).normalized()
            loops.append([pt + (side * math.cos(a) + nup * math.sin(a)) * radius
                          for a in (2 * math.pi * k / sides - math.pi / 2 for k in range(sides))])
        self.loft(loops, colour, cap_start=True, cap_end=True)


def build_kayak(name, lod, p, col):
    detail = {"LOD0": 2, "LOD1": 1, "LOD2": 0}[lod]
    stations = {2: p["stations"], 1: max(9, p["stations"] * 3 // 5), 0: 7}[detail]
    profile_idx = {2: range(8), 1: range(8), 0: (0, 2, 3, 6, 7)}[detail]
    profile_idx = list(profile_idx)
    bm = bmesh.new()
    bands = build_hull(bm, p, stations, profile_idx)

    # The deck surface for placing parts (before the cuts)
    deck_tree = BVHTree.FromBMesh(bm)

    def deck_z(x, y):
        hit = deck_tree.ray_cast(Vector((x, y, 2.0)), Vector((0, 0, -1)))
        return hit[0].z if hit[0] else 0.1

    def on_deck(x, y, lift):
        hit = deck_tree.ray_cast(Vector((x, y, 2.0)), Vector((0, 0, -1)))
        if not hit[0]:
            return Vector((x, y, 0.1 + lift))
        n = hit[1] if hit[1].z > 0 else -hit[1]
        return hit[0] + n * lift

    C = {k: srgb(v) for k, v in p["colours"].items()}
    colours = {f: C[{"bottom": "hull", "side": "side", "lip": "lip", "deck": "deck"}[b]] for f, b in bands.items()}

    # Openings: the cockpit and the cargo well (an elliptic cutter through the deck only)
    ck = (0.0, p["cockpit_y"], p["cockpit_w"] / 2, p["cockpit_l"] / 2)
    well = (0.0, p["well_y"], p["well_w"] / 2, p["well_l"] / 2)
    openings = [ck, well] if detail else []
    n_ell = {2: 20, 1: 14, 0: 10}[detail]
    for cx, cy, rx, ry in openings:
        rim = ellipse(cx, cy, rx, ry, n_ell)
        tops = [deck_z(v.x, v.y) for v in rim]
        # cut: delete the hull faces whose centre is inside the ellipse and above the sheer (the deck faces)
        kill = [f for f in bm.faces if f in bands and bands[f] == "deck"
                and ((f.calc_center_median().x - cx) / (rx * 1.02)) ** 2 + ((f.calc_center_median().y - cy) / (ry * 1.02)) ** 2 < 1.0]
        for f in kill:
            colours.pop(f, None)
        bmesh.ops.delete(bm, geom=kill, context="FACES_ONLY")

    parts = Parts(bm, colours)
    for idx, (cx, cy, rx, ry) in enumerate(openings):
        is_cockpit = idx == 0
        # The hole left by the deleted faces is a jagged quad outline: a wide coaming flange covers it
        out = ellipse(cx, cy, rx * 1.28 + 0.03, ry * 1.12 + 0.03, n_ell)
        inner = ellipse(cx, cy, rx, ry, n_ell)
        z_out = [deck_z(v.x, v.y) + 0.006 for v in out]
        lip_h = 0.035 if is_cockpit else 0.018
        flange = [Vector((v.x, v.y, z)) for v, z in zip(out, z_out)]
        top_in = [Vector((v.x, v.y, max(z_out[i], deck_z(v.x, v.y)) + lip_h)) for i, v in enumerate(inner)]
        top_mid = [a.lerp(b, 0.25) + Vector((0, 0, lip_h * 0.9)) for a, b in zip(flange, top_in)]
        coaming = C["lip"] if is_cockpit else C["deck_dark"]
        parts.loft([flange, top_mid, top_in], coaming)
        # Inside: the rim's inner wall and the tub, down to a floor
        floor_z = -0.1 + p["lift"] * 0.5 if is_cockpit else max(z_out) - 0.09
        under = [Vector((v.x, v.y, z - 0.03)) for v, z in zip(inner, (t.z for t in top_in))]
        tub_mid = [Vector((cx + (v.x - cx) * 0.92, cy + (v.y - cy) * 0.94, (floor_z + u.z) / 2)) for v, u in zip(inner, under)]
        tub_floor = [Vector((cx + (v.x - cx) * 0.78, cy + (v.y - cy) * 0.86, floor_z)) for v in inner]
        parts.loft([top_in, under, tub_mid, tub_floor], C["tub"], cap_end=True)

    if detail:
        # The seat: a padded pan and a backrest of three padded ribs (the reference's black seat)
        pan_c = Vector((0.0, HIPS.y - 0.02, -0.035))
        parts.box(pan_c, (0.36, 0.40, 0.07), C["seat"], taper=0.93)
        if detail == 2:
            parts.box(pan_c + Vector((0, -0.02, 0.045)), (0.31, 0.31, 0.025), C["seat_pad"], taper=0.92)
        back = Matrix.Rotation(math.radians(-16), 3, "X")
        base = Vector((0.0, HIPS.y + 0.2, 0.0))
        ribs = 3 if detail == 2 else 1
        for k in range(ribs):
            h = 0.3 / ribs
            c = base + back @ Vector((0, 0, h * (k + 0.5)))
            parts.box(c, (0.34 - 0.03 * k, 0.06, h * 0.86), C["seat_pad"] if k % 2 == 0 else C["seat"], rot=back, taper=0.94)
        parts.box(base + back @ Vector((0, 0.04, 0.15)), (0.3, 0.025, 0.3), C["seat"], rot=back)

    if detail == 2:
        # Front hatch: a round lid with a rim and a centre boss, sitting on the front deck
        hy = p["hatch_y"]
        hz = deck_z(0, hy)
        rim = ellipse(0, hy, 0.105, 0.105, 14, hz + 0.004)
        rim_top = ellipse(0, hy, 0.098, 0.098, 14, hz + 0.022)
        lid = ellipse(0, hy, 0.083, 0.083, 14, hz + 0.03)
        parts.loft([[Vector((v.x, v.y, deck_z(v.x, v.y) - 0.005)) for v in rim], rim_top, lid], C["lip"], cap_end=False)
        parts.loft([lid, ellipse(0, hy, 0.03, 0.03, 14, hz + 0.038)], C["hatch"], cap_end=True)

        # Bungee cords (the reference's X over the front deck) and the cargo net over the well, on small fittings
        def cord(a, b, step=0.09):
            n = max(2, int((b - a).length / step) + 1)
            pts = [on_deck(*(a.lerp(b, i / (n - 1))).xy, 0.012) for i in range(n)]
            parts.tube(pts, 0.009, C["cord"])

        def fitting(x, y):
            c = on_deck(x, y, 0.008)
            parts.box(c, (0.04, 0.03, 0.018), C["lip"], taper=0.7)

        def deck_pt(fx, s):
            w = station(p, s)[0]
            return Vector((fx * w, s * p["length"] / 2, 0))

        fore = [deck_pt(f, s) for f, s in ((0.64, -0.28), (-0.64, -0.28), (0.55, -0.58), (-0.55, -0.58))]
        for a, b in ((fore[0], fore[3]), (fore[1], fore[2]), (fore[0], fore[1]), (fore[2], fore[3])):
            cord(a, b)
        for v in fore:
            fitting(v.x, v.y)
        # The cargo net: a perimeter around the well and a cross over it, to hold a parcel (or a second rider's bag)
        wy, wl = p["well_y"], p["well_l"] / 2
        net = [deck_pt(f, (wy + d) / (p["length"] / 2)) for f, d in ((0.7, -wl - 0.06), (-0.7, -wl - 0.06), (0.62, wl + 0.08), (-0.62, wl + 0.08))]
        for a, b in ((net[0], net[3]), (net[1], net[2]), (net[0], net[2]), (net[1], net[3]), (net[2], net[3])):
            cord(a, b)
        for v in net:
            fitting(v.x, v.y)
        # Behind the cockpit, a short strap for the paddle
        strap = [deck_pt(f, (p["cockpit_y"] + p["cockpit_l"] / 2 + 0.12) / (p["length"] / 2)) for f in (0.55, -0.55)]
        cord(strap[0], strap[1])

        # Carry handles at both ends (a small black toggle on a cord)
        for end in (-1, 1):
            y = end * (p["length"] / 2 - 0.12)
            c = on_deck(0, y, 0.03)
            parts.box(c, (0.12, 0.028, 0.022), C["lip"], taper=0.85)
            parts.tube([on_deck(-0.04, y, 0.004), c + Vector((-0.04, 0, 0)), c + Vector((0.04, 0, 0)), on_deck(0.04, y, 0.004)], 0.006, C["cord"])

    bmesh.ops.remove_doubles(bm, verts=[v for v in bm.verts if not v.link_faces], dist=0.0)
    for v in [v for v in bm.verts if not v.link_faces]:
        bm.verts.remove(v)
    mesh = bpy.data.meshes.new(name)
    face_colour = [colours.get(f, C["deck"]) for f in bm.faces]
    face_band = [bands.get(f, "part") for f in bm.faces]
    bm.to_mesh(mesh)
    bm.free()
    obj = common.mesh_object(name, mesh, col)

    # Normals: the hull mostly flat (its planes show, as on the pine and the rock) with a little smoothing; parts flat
    for poly in mesh.polygons:
        poly.use_smooth = True
    vert_normals = [Vector() for _ in mesh.vertices]
    for poly, band in zip(mesh.polygons, face_band):
        if band != "part":
            for vi in poly.vertices:
                vert_normals[vi] += poly.normal * poly.area
    normals = []
    for poly, band in zip(mesh.polygons, face_band):
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            n = poly.normal if band == "part" or vert_normals[vi].length == 0 else vert_normals[vi].normalized().lerp(poly.normal, p["flat"])
            normals.append(n.normalized())
    mesh.normals_split_custom_set(normals)

    # Colour per face, with a small tone variation per deck plane (like each pine blade) and lighter top planes
    rnd = random.Random(11)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for poly, c, band in zip(mesh.polygons, face_colour, face_band):
        k = 1.0 + rnd.uniform(-1, 1) * p["tone"] * (1.0 if band == "deck" else 0.4)
        if band in ("deck", "side"):
            k *= 1.0 + 0.06 * max(0.0, poly.normal.z)
        cc = c * k
        for li in poly.loop_indices:
            attr.data[li].color_srgb = (min(cc.x, 1), min(cc.y, 1), min(cc.z, 1), 1.0)
    return obj


def build(asset, p):
    rig = common.load_rig()
    common.reset_scene()
    col = common.collection(asset)
    parts = {k: build_kayak(f"{asset}_{k}", k, p, col) for k in ("LOD0", "LOD1", "LOD2")}
    mat = common.soft_toon_material("M_" + asset, rig, preset="Default", _BaseColor="#FFFFFF", _UseVertexColor=1.0,
                                    _RampSoftness=p["ramp"], _Wrap=p["wrap"])
    for o in parts.values():
        o.data.materials.append(mat)
    parts["LOD1"].hide_set(True)
    parts["LOD2"].hide_set(True)
    lookdev.build(rig)
    return parts


def write_notes(asset, version, parts, args, paths, changes):
    notes = os.path.join(paths["dir"], "NOTES.md")
    if not os.path.exists(notes):
        with open(notes, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"# {FAMILY} / {asset}\n\n"
                    "## Briefing (confirmado em 2026-10-05)\n"
                    "- **O que é:** o caiaque do jogador (âncora de estilo da Fase 1), o barco da Agência do Rio: leva o remador e, "
                    "atrás do cockpit, uma encomenda ou um segundo passageiro.\n"
                    "- **Tamanho:** 3,3 x 0,68 m, o mesmo do colisor do jogo (a física não muda; sem `_COL`, o jogo usa a cápsula dele).\n"
                    "- **Referência:** a foto do desenvolvedor (caiaque sit-in laranja e preto, cabos em X, tampa redonda, assento preto).\n"
                    "- **Estilo:** facetado como o pinheiro e a pedra (planos que aparecem, luz firme), \"bem detalhado\".\n"
                    f"- **Triângulos:** LOD0 <= {BUDGET['LOD0']}, LOD1 <= {BUDGET['LOD1']}, LOD2 <= {BUDGET['LOD2']}.\n"
                    "- **Pedido do desenvolvedor (literal):** \"voce pegou o contexto de que o jogo sera a ideia de uma agencia do rio e das entregas e turistas certo? "
                    "faca o caiaque basico com base nisso [...] teremos animacoes do personagem dentro do caiaque e que esse primeiro caiaque unico vai levar "
                    "possivelmente um segundo passageiro ou uma encomenda com ele. Mas faca bem detalhado, seguindo a estica das arvores.\"\n"
                    "- **Gerador:** `ArtSource/Kayak/kayak_gen.py`.\n\n"
                    "## Versões\n")
    tris = ", ".join(f"{k} {common.triangle_count(o)}" for k, o in parts.items())
    with open(notes, "a", encoding="utf-8", newline="\n") as f:
        f.write(f"\n### v{version:03d}\n"
                f"- Gerado com: `{' '.join(f'--{k} {v}' for k, v in args.items() if k != 'colours')}`\n"
                f"- Triângulos: {tris}\n"
                f"- Prévia: `{os.path.basename(paths['preview'])}`\n"
                "- **Feedback do desenvolvedor (literal):** _(aguardando)_\n"
                f"- **O que mudou:** {'primeira versão.' if version == 1 else changes}\n")


def main():
    args = common.script_args()
    asset = args.get("asset", "CaiaqueA")
    f = lambda k, d: float(args.get(k, d))  # noqa: E731
    p = {
        "length": f("length", 3.3),
        "width": f("width", 0.68),
        "widest": f("widest", 0.06),
        "rocker": f("rocker", 0.11),
        "sheer_rise": f("sheer_rise", 0.07),
        "end_rise": f("end_rise", 0.13),
        "lift": f("lift", 0.0),
        "stations": int(args.get("stations", 18)),
        "cockpit_y": f("cockpit_y", 0.1),
        "cockpit_l": f("cockpit_l", 0.86),
        "cockpit_w": f("cockpit_w", 0.44),
        "well_y": f("well_y", 0.98),
        "well_l": f("well_l", 0.52),
        "well_w": f("well_w", 0.36),
        "hatch_y": f("hatch_y", -1.17),
        "flat": f("flat", 0.75),
        "tone": f("tone", 0.04),
        "ramp": f("ramp", 0.16),
        "wrap": f("wrap", 0.35),
        "colours": {
            "deck": args.get("deck", "#EE7424"), "deck_dark": "#C95E1C", "side": "#DD6620", "hull": "#2B2B2E", "lip": "#1F1F22",
            "tub": "#38383B", "seat": "#26272A", "seat_pad": "#3A3C40", "cord": "#18181A", "hatch": "#2E2E31",
        },
    }
    parts = build(asset, p)
    tris = {k: common.triangle_count(o) for k, o in parts.items()}
    print(f"[kayak] tris {tris}")
    if "draft" in args:
        out = os.path.join(common.TMP_DIR, f"draft_{asset}_{args['draft']}.png")
        preview.render_sheet(out, "rascunho " + str(args["draft"]))
        print(f"[kayak] draft {out}")
        return
    version, paths = common.save_new_version(FAMILY, asset, int(args["version"]) if "version" in args else None)
    preview.render_sheet(paths["preview"], f"v{version:03d}")
    write_notes(asset, version, parts, p, paths, args.get("changes", "_(descrever)_"))
    print(f"[kayak] {paths['blend']}")


if __name__ == "__main__":
    main()
