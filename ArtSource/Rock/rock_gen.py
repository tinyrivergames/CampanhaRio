"""
Family generator: Rock (Phase 1 style anchor), made to sit with the approved pine:
  - FACETED like the pine: a convex hull of jittered points on a squashed ellipsoid gives a few big flat planes;
  - SOFT edges: every edge is bevelled (rounded), nothing sharp;
  - the same "inflated" light: normals blend the face, its smooth vertex normal and a whole-rock ellipsoid;
  - per-face TONE variation (warmer/cooler, like each pine blade), sand on top, a cooler darker base, a hint of moss.
LOD0 / LOD1 / LOD2 come from the same hull with less bevelling; a small convex hull is the collider (_COL).

    art build Rock\rock_gen.py --asset RochaA [--seed 7] [--size 2.4] [--points 26] [--bevel 0.14]
    --draft x renders a test sheet into ArtSource/_tmp without making a version.
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

FAMILY = "Rock"
BUDGET = {"LOD0": 800, "LOD1": 250, "LOD2": 80, "COL": 40}


def srgb(hexstr):
    return Vector(common.hex_srgb(hexstr))


def mul(a, b):
    return Vector((a.x * b.x, a.y * b.y, a.z * b.z))


def hull_points(p, count, seed_offset=0):
    """Jittered points on a squashed ellipsoid (the rock's volume), with a flattened base to sit on the ground."""
    rnd = random.Random(p["seed"] + seed_offset)
    sx, sy, sz = p["size"] / 2, p["size"] * p["depth"] / 2, p["size"] * p["height"] / 2
    pts = []
    golden = math.pi * (3 - math.sqrt(5))
    for i in range(count):
        y = 1 - 2 * (i + 0.5) / count
        r = math.sqrt(max(0.0, 1 - y * y))
        a = i * golden + rnd.uniform(-0.35, 0.35)
        d = Vector((math.cos(a) * r, math.sin(a) * r, y)) * rnd.uniform(1 - p["jitter"], 1.0)
        v = Vector((d.x * sx, d.y * sy, d.z * sz))
        v.z = max(v.z, -sz * 0.55)  # a flat-ish base
        pts.append(v)
    return pts


def build_rock(name, lod, p, col):
    bm = bmesh.new()
    count = {"LOD0": p["points"], "LOD1": p["points"], "LOD2": max(10, p["points"] // 2), "COL": 12}[lod]
    verts = [bm.verts.new(v) for v in hull_points(p, count, 0 if lod != "COL" else 1)]
    result = bmesh.ops.convex_hull(bm, input=verts)
    bmesh.ops.delete(bm, geom=result["geom_interior"] + result["geom_unused"], context="VERTS")
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(p["merge"]), verts=bm.verts, edges=bm.edges)  # merge near-coplanar faces into big planes
    segments = {"LOD0": 2, "LOD1": 1, "LOD2": 0, "COL": 0}[lod]
    if segments:
        bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=p["bevel"] * p["size"] * (1.0 if lod == "LOD0" else 0.8),
                        offset_type="OFFSET", segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True)
    # (no triangulation: each big plane stays one polygon, so it gets one tone; the FBX/Unity import triangulates)
    lo_z = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= lo_z  # origin at the base centre
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = common.mesh_object(name, mesh, col)
    if lod == "COL":
        return obj
    for poly in mesh.polygons:
        poly.use_smooth = True

    # Normals per corner: the face (big planes read flat), its smooth vertex normal (rounded bevels) and the ellipsoid
    height = max(v.co.z for v in mesh.vertices)
    center = Vector((0, 0, height * 0.45))
    axes = Vector((p["size"] / 2, p["size"] * p["depth"] / 2, height * 0.6))
    big = max(poly.area for poly in mesh.polygons)
    normals = []
    for poly in mesh.polygons:
        flatness = min(1.0, poly.area / (big * 0.25))  # big faces stay flat, small (bevel) faces go smooth
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            co = mesh.vertices[vi].co
            smooth = mesh.vertices[vi].normal
            n = smooth.lerp(poly.normal, 0.75 * flatness)
            d = co - center
            ell = Vector((d.x / axes.x ** 2, d.y / axes.y ** 2, d.z / axes.z ** 2)).normalized()
            normals.append(n.normalized().lerp(ell, p["inflate"]).normalized())
    mesh.normals_split_custom_set(normals)

    # Colour per face: a tone per big face (like each pine blade), sand toward the top, cooler and darker at the base
    rnd = random.Random(p["seed"] * 31 + 5)
    base, sand, shadow = srgb(common.palette("rock_orange")), srgb(common.palette("rock_sand")), srgb(common.palette("rock_shadow"))
    warm, cool = Vector((1.06, 1.02, 0.9)), Vector((0.92, 0.97, 1.06))
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for poly in mesh.polygons:
        hue = rnd.uniform(-1, 1) * p["tone"] * (1.0 if poly.area > big * 0.15 else 0.0)  # bevel strips stay neutral: no stripes
        tone = Vector((1, 1, 1)).lerp(warm if hue > 0 else cool, abs(hue))
        shade = rnd.uniform(0.95, 1.05)
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            t = co.z / max(height, 1e-3)
            c = shadow.lerp(base, min(1.0, t * 2.2)).lerp(sand, max(0.0, (t - 0.45) * 1.4) * 0.8)
            c = mul(c, tone) * shade
            attr.data[li].color_srgb = (min(c.x, 1), min(c.y, 1), min(c.z, 1), 1.0)
    return obj


def build(asset, p):
    rig = common.load_rig()
    common.reset_scene()
    col = common.collection(asset)
    parts = {k: build_rock(f"{asset}_{k}", k, p, col) for k in ("LOD0", "LOD1", "LOD2", "COL")}
    mat = common.soft_toon_material("M_" + asset, rig, preset="Rock", _BaseColor="#FFFFFF", _UseVertexColor=1.0,
                                    _TopTint=common.palette("moss"), _TopTintAmount=p["moss"], _TopTintSharpness=0.3)
    for k, o in parts.items():
        o.data.materials.append(mat)
    parts["COL"].hide_render = True
    parts["COL"].display_type = "WIRE"
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
                    "- **O que é:** o matacão-mestre da família (âncora de estilo da Fase 1): pedra de beira de rio e obstáculo na água.\n"
                    "- **Tamanho:** ~2,4 x 1,8 x 1,4 m, assentada no chão.\n"
                    "- **Forma:** grandes planos lisos com arestas arredondadas, volume \"inflado\" (referências `estetica_02`, `estetica_03`).\n"
                    f"- **Triângulos:** LOD0 <= {BUDGET['LOD0']}, LOD1 <= {BUDGET['LOD1']}, LOD2 <= {BUDGET['LOD2']}, colisor <= {BUDGET['COL']}.\n"
                    "- **Cores:** rock_orange, topo rock_sand, base rock_shadow; um tom por face (como as lâminas do pinheiro); musgo leve em cima.\n"
                    "- **Pedido do desenvolvedor:** \"se baseia em algo pra mesclar bem com os graficos do pinheiro\" → facetada como o pinheiro, normais infladas, variação de tom por face.\n"
                    "- **Gerador:** `ArtSource/Rock/rock_gen.py`.\n\n"
                    "## Versões\n")
    tris = ", ".join(f"{k} {common.triangle_count(o)}" for k, o in parts.items())
    with open(notes, "a", encoding="utf-8", newline="\n") as f:
        f.write(f"\n### v{version:03d}\n"
                f"- Gerado com: `{' '.join(f'--{k} {v}' for k, v in args.items())}`\n"
                f"- Triângulos: {tris}\n"
                f"- Prévia: `{os.path.basename(paths['preview'])}`\n"
                "- **Feedback do desenvolvedor (literal):** _(aguardando)_\n"
                f"- **O que mudou:** {'primeira versão.' if version == 1 else changes}\n")


def main():
    args = common.script_args()
    asset = args.get("asset", "RochaA")
    p = {
        "seed": int(args.get("seed", 7)),
        "size": float(args.get("size", 2.7)),
        "depth": float(args.get("depth", 0.8)),
        "height": float(args.get("height", 0.82)),
        "points": int(args.get("points", 18)),
        "merge": float(args.get("merge", 14)),
        "jitter": float(args.get("jitter", 0.18)),
        "bevel": float(args.get("bevel", 0.05)),
        "inflate": float(args.get("inflate", 0.3)),
        "tone": float(args.get("tone", 0.8)),
        "moss": float(args.get("moss", 0.25)),
    }
    parts = build(asset, p)
    if "draft" in args:
        out = os.path.join(common.TMP_DIR, f"draft_{asset}_{args['draft']}.png")
        preview.render_sheet(out, "rascunho " + str(args["draft"]))
        print(f"[rock] draft {out}")
        return
    version, paths = common.save_new_version(FAMILY, asset, int(args["version"]) if "version" in args else None)
    preview.render_sheet(paths["preview"], f"v{version:03d}")
    write_notes(asset, version, parts, p, paths, args.get("changes", "_(descrever)_"))
    print(f"[rock] {paths['blend']}")


if __name__ == "__main__":
    main()
