"""
Family generator: Pine (Phase 1 style anchor). Option A of the brief: SOLID "skirts" (no alpha cards).

A sturdy stylized pine: a low-poly trunk and N stacked skirts, each a closed soft cone with a scalloped, slightly
drooping rim (the branch tufts). Normals are blended toward a smooth crown ellipsoid so the light wraps the whole tree
softly ("inflated"), while each skirt still reads. ONE material for the whole tree (colour in the vertex colours:
darker inside and under each skirt, lighter on top; the trunk is bark), the vertex ALPHA is the wind weight.
LOD0 and LOD1 are generated (not decimated) from the same parameters.

    art build Pine\pine_gen.py --asset PinheiroA [--seed 4] [--height 9] [--radius 2] [--layers 5] [--lean 0.04]
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "pipeline"))
import common  # noqa: E402
import lookdev  # noqa: E402
import preview  # noqa: E402

FAMILY = "Pine"
BUDGET = {"LOD0": 700, "LOD1": 250}

# Skirt profile: (radius fraction, height fraction of the skirt) from the tip down to the hidden inner ring
PROFILE = {
    "LOD0": [(0.14, 1.0), (0.62, 0.55), (1.0, 0.0), (0.7, -0.12), (0.15, 0.05)],
    "LOD1": [(0.14, 1.0), (0.7, 0.45), (1.0, 0.0), (0.2, -0.05)],
}
SEGMENTS = {"LOD0": 10, "LOD1": 6}
TRUNK_SIDES = {"LOD0": 6, "LOD1": 4}
RIM_ROW = 2  # the profile index of the scalloped rim


def srgb(hexstr):
    return Vector(common.hex_srgb(hexstr))


def build_tree(name, lod, p, col):
    rnd = random.Random(p["seed"])
    bm = bmesh.new()
    colors = {}  # vert -> (r, g, b, a) sRGB + wind
    dark, mid, light, bark = srgb(common.palette("pine_dark")), srgb(common.palette("pine_mid")), srgb(common.palette("pine_light")), srgb(common.palette("bark"))
    n = p["layers"]
    segs, prof = SEGMENTS[lod], PROFILE[lod]

    def lean(y):
        return Vector((p["lean"] * y, 0.0, 0.0))

    # Trunk: a tapered prism from the ground into the crown
    sides = TRUNK_SIDES[lod]
    rings = [(0.0, 0.18), (p["height"] * 0.7, 0.07)] if lod == "LOD1" else [(0.0, 0.18), (1.8, 0.14), (p["height"] * 0.75, 0.06)]
    prev = None
    for y, r in rings:
        ring = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            v = bm.verts.new(Vector((math.cos(a) * r, math.sin(a) * r, y)) + lean(y))
            colors[v] = (*bark, 0.0)
            ring.append(v)
        if prev:
            for j in range(sides):
                bm.faces.new((prev[j], prev[(j + 1) % sides], ring[(j + 1) % sides], ring[j]))
        prev = ring

    # Skirts, bottom to top
    for i in range(n):
        t = i / max(1, n - 1)
        rim_y = 1.7 + (p["height"] - 2.4 - 1.7) * t ** 0.92
        R = p["radius"] * (1.0 - 0.62 * t) * rnd.uniform(0.94, 1.06)
        H = 2.2 + (0.35 if i == n - 1 else 0.0)
        phase = rnd.uniform(0, 2 * math.pi)
        lobes = rnd.choice((5, 6, 7))
        rows = []
        for k, (rf, hf) in enumerate(prof):
            ring = []
            for j in range(segs):
                a = 2 * math.pi * j / segs + phase * 0.1
                lobe = 0.5 * (1 + math.cos(lobes * a + phase))  # 0..1, the tufts
                weight = {RIM_ROW: 1.0, RIM_ROW - 1: 0.5, RIM_ROW + 1: 0.7}.get(k, 0.15)
                r = R * rf * (1.0 + p["scallop"] * (lobe - 0.5) * weight)
                y = rim_y + H * hf - (p["droop"] * R * (1 - lobe) if k == RIM_ROW else 0.0)
                v = bm.verts.new(Vector((math.cos(a) * r, math.sin(a) * r, y)) + lean(y))
                # Colour: dark inside / underneath, mid on the slope, light on the upper outer part, lighter toward the top
                upper = k <= RIM_ROW
                radial = min(1.0, r / max(R, 1e-3))
                c = dark.lerp(mid, radial) if not upper else mid.lerp(light, 0.25 + 0.5 * (1 - abs(hf - 0.5) * 2) * radial)
                c = c.lerp(light, 0.25 * t if upper else 0.0)
                c = c * rnd.uniform(0.97, 1.03)
                colors[v] = (min(c.x, 1), min(c.y, 1), min(c.z, 1), radial * (0.35 + 0.65 * t))
                ring.append(v)
            rows.append(ring)
        for k in range(len(rows) - 1):
            a_ring, b_ring = rows[k], rows[k + 1]
            for j in range(segs):
                bm.faces.new((a_ring[j], a_ring[(j + 1) % segs], b_ring[(j + 1) % segs], b_ring[j]))
        tip_y = rim_y + H * 1.08
        apex = bm.verts.new(Vector((0, 0, tip_y)) + lean(tip_y))
        colors[apex] = (*light.lerp(mid, 0.3), 1.0)
        for j in range(segs):
            bm.faces.new((apex, rows[0][(j + 1) % segs], rows[0][j]))
        inner_y = rim_y + H * prof[-1][1]
        core = bm.verts.new(Vector((0, 0, inner_y)) + lean(inner_y))
        colors[core] = (*dark, 0.0)
        for j in range(segs):
            bm.faces.new((core, rows[-1][j], rows[-1][(j + 1) % segs]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    vert_colors = [colors[v] for v in bm.verts]
    bm.to_mesh(mesh)
    bm.free()
    obj = common.mesh_object(name, mesh, col)
    for poly in mesh.polygons:
        poly.use_smooth = True

    # Soft normals: blend each vertex normal toward the crown ellipsoid (the "inflated" light), trunk keeps its own
    center = Vector((0, 0, p["height"] * 0.55))
    axes = Vector((p["radius"] * 1.1, p["radius"] * 1.1, p["height"] * 0.55))
    normals = []
    for v, c in zip(mesh.vertices, vert_colors):
        own = v.normal.copy()
        if c[3] == 0.0 and c[:3] == tuple(bark):
            normals.append(own)
            continue
        d = v.co - center - Vector((p["lean"] * v.co.z, 0, 0))
        ell = Vector((d.x / axes.x ** 2, d.y / axes.y ** 2, d.z / axes.z ** 2)).normalized()
        normals.append(own.lerp(ell, p["inflate"]).normalized())
    mesh.normals_split_custom_set_from_vertices(normals)

    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for loop in mesh.loops:
        attr.data[loop.index].color_srgb = vert_colors[loop.vertex_index]
    return obj


def build(asset, p):
    rig = common.load_rig()
    common.reset_scene()
    col = common.collection(asset)
    lod0 = build_tree(asset + "_LOD0", "LOD0", p, col)
    lod1 = build_tree(asset + "_LOD1", "LOD1", p, col)
    mat = common.soft_toon_material("M_" + asset, rig, preset="Foliage", _BaseColor="#FFFFFF", _UseVertexColor=1.0,
                                    _Translucency=0.3, _RimStrength=0.14)
    for o in (lod0, lod1):
        o.data.materials.append(mat)
    lod1.hide_set(True)
    lookdev.build(rig)
    return {"LOD0": lod0, "LOD1": lod1}


def write_notes(asset, version, parts, args, paths):
    notes = os.path.join(paths["dir"], "NOTES.md")
    if not os.path.exists(notes):
        with open(notes, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"# {FAMILY} / {asset}\n\n"
                    "## Briefing (aprovado em 2026-10-05, opção A)\n"
                    "- **O que é:** o pinheiro-mestre da família (âncora de estilo da Fase 1).\n"
                    "- **Tamanho:** ~9 m de altura, copa ~4 m, tronco 0,35 m na base, 1,5 m visível, levemente inclinado.\n"
                    "- **Forma:** 5 \"saias\" arredondadas e infladas, bordas onduladas e macias, ponta arredondada, copa cheia.\n"
                    f"- **Triângulos:** LOD0 <= {BUDGET['LOD0']}, LOD1 <= {BUDGET['LOD1']}, LOD2 impostor (2 tris), fundo: cartão-borrão (2 tris).\n"
                    "- **Cores:** pine_dark (base, interior) -> pine_light (topo), tronco bark. Uma cor por vértice, um material só.\n"
                    "- **Técnica:** opção A, saias sólidas sem transparência; normais \"infladas\" (puxadas para um elipsoide da copa).\n"
                    "- **Referências:** Docs/Reference/estetica_01_floresta e estetica_03_lago.\n"
                    "- **Gerador:** `ArtSource/Pine/pine_gen.py`. O impostor e o cartão-borrão vêm depois que a forma for aprovada.\n\n"
                    "## Versões\n")
    tris = ", ".join(f"{k} {common.triangle_count(o)}" for k, o in parts.items())
    with open(notes, "a", encoding="utf-8", newline="\n") as f:
        f.write(f"\n### v{version:03d}\n"
                f"- Gerado com: `{' '.join(f'--{k} {v}' for k, v in args.items())}`\n"
                f"- Triângulos: {tris}\n"
                f"- Prévia: `{os.path.basename(paths['preview'])}`\n"
                "- **Feedback do desenvolvedor (literal):** _(aguardando)_\n"
                f"- **O que mudou:** {'primeira versão.' if version == 1 else '_(descrever)_'}\n")


def main():
    args = common.script_args()
    asset = args.get("asset", "PinheiroA")
    p = {
        "seed": int(args.get("seed", 4)),
        "height": float(args.get("height", 9.0)),
        "radius": float(args.get("radius", 2.0)),
        "layers": int(args.get("layers", 5)),
        "lean": float(args.get("lean", 0.04)),
        "scallop": float(args.get("scallop", 0.28)),
        "droop": float(args.get("droop", 0.12)),
        "inflate": float(args.get("inflate", 0.45)),
    }
    parts = build(asset, p)
    version, paths = common.save_new_version(FAMILY, asset, int(args["version"]) if "version" in args else None)
    preview.render_sheet(paths["preview"], f"v{version:03d}")
    write_notes(asset, version, parts, p, paths)
    print(f"[pine] {paths['blend']}")


if __name__ == "__main__":
    main()
