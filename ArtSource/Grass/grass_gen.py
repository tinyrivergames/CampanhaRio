r"""
Family generator: Grass (Phase 3) - a tuft of grass in the style of the approved pine: thin folded blades that lean out
and curl, each with its own slight tone (darker at the root, lighter at the tip), the vertex ALPHA as the wind weight
(0 at the root, 1 at the tip). One material (SoftToon Foliage, vertex colours). Meant to be scattered by the thousand.

    art build Grass\grass_gen.py --asset TufoGramaA [--blades 14] [--height 0.45] [--spread 0.22]
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

FAMILY = "Grass"
BUDGET = {"LOD0": 120, "LOD1": 40, "LOD2": 8}


def srgb(h):
    return Vector(common.hex_srgb(h))


def build_tuft(name, lod, p, col):
    rnd = random.Random(p["seed"])
    bm = bmesh.new()
    colors = {}
    dark, mid, light = srgb(p["root"]), srgb(p["mid"]), srgb(p["tip"])
    warm, cool = Vector((1.08, 1.04, 0.82)), Vector((0.9, 0.98, 1.08))
    blades = {"LOD0": p["blades"], "LOD1": max(4, p["blades"] // 3), "LOD2": 2}[lod]
    for i in range(blades):
        a = 2 * math.pi * i / blades + rnd.uniform(-0.4, 0.4)
        r0 = rnd.uniform(0.0, p["spread"] * 0.35)
        base = Vector((math.cos(a) * r0, math.sin(a) * r0, 0.0))
        h = p["height"] * rnd.uniform(0.6, 1.15)
        lean = rnd.uniform(0.25, 0.75) * p["lean"]
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        w = p["width"] * rnd.uniform(0.8, 1.2)
        hue = rnd.uniform(-1, 1)
        tone = Vector((1, 1, 1)).lerp(warm if hue > 0 else cool, abs(hue) * 0.6)
        shade = rnd.uniform(0.88, 1.08)
        mid_pt = base + out * (h * lean * 0.35) + Vector((0, 0, h * 0.55))
        tip = base + out * (h * lean * 1.1) + Vector((0, 0, h * (1.0 - 0.25 * lean)))

        def v(pos, c, alpha):
            vert = bm.verts.new(pos)
            cc = Vector((c.x * tone.x, c.y * tone.y, c.z * tone.z)) * shade
            colors[vert] = (min(cc.x, 1), min(cc.y, 1), min(cc.z, 1), alpha)
            return vert

        L0, R0 = v(base - side * w, dark, 0.0), v(base + side * w, dark, 0.0)
        if lod == "LOD2":
            T = v(tip, light, 1.0)
            bm.faces.new((L0, R0, T))
            continue
        Lm, Rm = v(mid_pt - side * w * 0.7, mid, 0.5), v(mid_pt + side * w * 0.7, mid, 0.5)
        C = v(mid_pt + Vector((0, 0, w * 0.4)) - out * w * 0.3, mid, 0.5)  # the fold
        T = v(tip, light, 1.0)
        for f in ((L0, R0, C), (L0, C, Lm), (R0, Rm, C), (Lm, C, T), (C, Rm, T)):
            bm.faces.new(f)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    attr_layer = None
    bm.to_mesh(mesh)
    vert_list = list(bm.verts)
    bm.free()
    obj = common.mesh_object(name, mesh, col)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for poly in mesh.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            attr.data[li].color_srgb = colors[vert_list[mesh.loops[li].vertex_index]]
    # Soft "inflated" light: every normal tilted up (grass reads as a soft mass, not single blades)
    normals = []
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            n = poly.normal.copy()
            if n.z < 0: n = -n
            normals.append(n.lerp(Vector((0, 0, 1)), 0.6).normalized())
    mesh.normals_split_custom_set(normals)
    _ = attr_layer
    return obj


def build_carpet(name, lod, p, col):
    """The carpet: a square patch (p["patch"] m) of thin straight blades that fills the ground, dark at the root and light
    at the tip (the developer's reference: a soft, simple field), each blade its own height, lean and tone."""
    rnd = random.Random(p["seed"])
    bm = bmesh.new()
    colors = {}
    dark, mid, light = srgb(p["root"]), srgb(p["mid"]), srgb(p["tip"])
    count = {"LOD0": p["blades"], "LOD1": p["blades"] // 2, "LOD2": max(4, p["blades"] // 8)}[lod]
    half = p["patch"] / 2
    for i in range(count):
        base = Vector((rnd.uniform(-half, half), rnd.uniform(-half, half), 0.0))
        a = rnd.uniform(0, 2 * math.pi)
        side = Vector((math.cos(a), math.sin(a), 0.0))
        lean_dir = Vector((-side.y, side.x, 0.0)) * rnd.uniform(-1, 1)
        h = p["height"] * rnd.uniform(0.65, 1.2)
        w = p["width"] * rnd.uniform(0.8, 1.25)
        tip = base + lean_dir * h * 0.25 * p["lean"] + Vector((0, 0, h))
        k = rnd.uniform(0.9, 1.08)
        t = rnd.uniform(-1, 1)
        tint = Vector((1.0 + 0.06 * t, 1.0 + 0.03 * t, 1.0 - 0.08 * t))

        def v(pos, c, alpha):
            vert = bm.verts.new(pos)
            cc = Vector((c.x * tint.x, c.y * tint.y, c.z * tint.z)) * k
            colors[vert] = (min(cc.x, 1), min(cc.y, 1), min(cc.z, 1), alpha)
            return vert

        L0, R0 = v(base - side * w, dark, 0.0), v(base + side * w, dark, 0.0)
        T = v(tip, light, 1.0)
        if lod == "LOD0":
            midp = base.lerp(tip, 0.55)
            Lm, Rm = v(midp - side * w * 0.6, mid, 0.55), v(midp + side * w * 0.6, mid, 0.55)
            for f in ((L0, R0, Rm), (L0, Rm, Lm), (Lm, Rm, T)):
                bm.faces.new(f)
        else:
            bm.faces.new((L0, R0, T))
    mesh = bpy.data.meshes.new(name)
    vert_list = list(bm.verts)
    bm.to_mesh(mesh)
    bm.free()
    obj = common.mesh_object(name, mesh, col)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    normals = []
    for poly in mesh.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            attr.data[li].color_srgb = colors[vert_list[mesh.loops[li].vertex_index]]
            normals.append(Vector((0, 0, 1)))  # all up: the field reads as one soft mass, lit by the gradient
    mesh.normals_split_custom_set(normals)
    return obj


def build(asset, p):
    rig = common.load_rig()
    common.reset_scene()
    col = common.collection(asset)
    make = build_carpet if p["style"] == "carpet" else build_tuft
    parts = {k: make(f"{asset}_{k}", k, p, col) for k in ("LOD0", "LOD1", "LOD2")}
    mat = common.soft_toon_material("M_" + asset, rig, preset="Foliage", _BaseColor="#FFFFFF", _UseVertexColor=1.0)
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
                    "## Briefing (2026-10-06, Fase 3)\n"
                    "- **O que é:** o tufo de grama da floresta (espalhado aos milhares), no estilo das lâminas do pinheiro aprovado.\n"
                    "- **Pedido do desenvolvedor:** \"monta as texturas principais da floresta e popule ela como floresta\" (referência: floresta low-poly densa com grama e capim).\n"
                    f"- **Triângulos:** LOD0 <= {BUDGET['LOD0']}, LOD1 <= {BUDGET['LOD1']}, LOD2 <= {BUDGET['LOD2']}.\n"
                    "- **Gerador:** `ArtSource/Grass/grass_gen.py`.\n\n"
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
    asset = args.get("asset", "TufoGramaA")
    f = lambda k, d: float(args.get(k, d))  # noqa: E731
    p = {
        "seed": int(args.get("seed", 5)),
        "blades": int(args.get("blades", 14)),
        "style": args.get("style", "tuft"),
        "patch": f("patch", 0.8),
        "height": f("height", 0.45),
        "spread": f("spread", 0.22),
        "lean": f("lean", 1.0),
        "width": f("width", 0.03),
        "root": args.get("root", "#3E5E2A"),
        "mid": args.get("mid", "#5C8A3A"),
        "tip": args.get("tip", "#9BBF5A"),
    }
    parts = build(asset, p)
    print(f"[grass] tris { {k: common.triangle_count(o) for k, o in parts.items()} }")
    if "draft" in args:
        out = os.path.join(common.TMP_DIR, f"draft_{asset}_{args['draft']}.png")
        preview.render_sheet(out, "rascunho " + str(args["draft"]))
        print(f"[grass] draft {out}")
        return
    version, paths = common.save_new_version(FAMILY, asset, int(args["version"]) if "version" in args else None)
    preview.render_sheet(paths["preview"], f"v{version:03d}")
    write_notes(asset, version, parts, p, paths, args.get("changes", "_(descrever)_"))
    print(f"[grass] {paths['blend']}")


if __name__ == "__main__":
    main()
