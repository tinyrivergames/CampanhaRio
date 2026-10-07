r"""
Family generator: Broadleaf (Phase 3) - the forest's leafy trees, after the developer's forest reference: a tapered trunk
with two or three branches, and a canopy of faceted round blobs (low-poly geodesic spheres), each facet with its own
tone (lighter on top, darker underneath), the vertex ALPHA as the wind weight (the canopy sways, the trunk doesn't).
LOD0 / LOD1 here; the impostor (LOD2) is baked at export (art export ... --impostor), as for the pine.

    art build Broadleaf\broadleaf_gen.py --asset ArvoreFolhosaA [--height 6] [--blobs 5] [--canopy 2.2]
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

FAMILY = "Broadleaf"
BUDGET = {"LOD0": 1400, "LOD1": 450}


def srgb(h):
    return Vector(common.hex_srgb(h))


def build_tree(name, lod, p, col):
    rnd = random.Random(p["seed"])
    bm = bmesh.new()
    face_col = {}
    bark, bark_dark = srgb(p["bark"]), srgb(p["bark"]) * 0.7
    leaf_dark, leaf, leaf_light = srgb(p["leaf_dark"]), srgb(p["leaf"]), srgb(p["leaf_light"])
    warm, cool = Vector((1.07, 1.05, 0.85)), Vector((0.9, 0.98, 1.08))
    H = p["height"]
    detail = lod == "LOD0"

    def prism(a, b, ra, rb, sides, c0, c1):
        """A tapered prism from a to b (the trunk, a branch)."""
        d = (b - a).normalized()
        side = d.orthogonal().normalized()
        up2 = d.cross(side)
        ring_a, ring_b = [], []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            off = side * math.cos(ang) + up2 * math.sin(ang)
            ring_a.append(bm.verts.new(a + off * ra))
            ring_b.append(bm.verts.new(b + off * rb))
        for k in range(sides):
            j = (k + 1) % sides
            f = bm.faces.new((ring_a[k], ring_a[j], ring_b[j], ring_b[k]))
            face_col[f] = (c0.lerp(c1, rnd.uniform(0.2, 0.8)), 0.0)
        return ring_b

    # Trunk with a slight bend, and branches up into the canopy
    sides = 6 if detail else 4
    base = Vector((0, 0, -0.1))
    mid = Vector((rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25), H * 0.45))
    top = Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), H * 0.62))
    tr = p["trunk"]
    prism(base, mid, tr * 1.25, tr * 0.85, sides, bark_dark, bark)
    prism(mid, top, tr * 0.85, tr * 0.55, sides, bark, bark)
    branch_ends = [top]
    for b in range(p["branches"]):
        ang = rnd.uniform(0, 2 * math.pi)
        start = mid.lerp(top, rnd.uniform(0.2, 0.7))
        end = start + Vector((math.cos(ang) * p["canopy"] * 0.55, math.sin(ang) * p["canopy"] * 0.55, H * rnd.uniform(0.12, 0.22)))
        prism(start, end, tr * 0.45, tr * 0.25, 4, bark, bark)
        branch_ends.append(end)

    # The canopy: faceted blobs around the branch ends and the top
    blobs = p["blobs"] if detail else max(3, p["blobs"] - 2)
    subdiv = 2 if detail else 1
    centre = Vector((0, 0, H * 0.75))
    for i in range(blobs):
        anchor = branch_ends[i % len(branch_ends)]
        c = anchor.lerp(centre, 0.35) + Vector((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), rnd.uniform(0.0, 0.9))) * p["canopy"] * p["spread"]
        if i == 0:
            c = centre + Vector((0, 0, H * 0.08))
        r = p["canopy"] * rnd.uniform(0.5, 0.75) * (1.1 if i == 0 else 1.0)
        res = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=r)
        verts = res["verts"]
        squash = p["squash"]
        for v in verts:
            v.co = Vector((v.co.x * rnd.uniform(0.95, 1.05), v.co.y * rnd.uniform(0.95, 1.05), v.co.z * squash)) + c
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            n_up = max(0.0, (f.calc_center_median() - c).normalized().z)
            down = max(0.0, -(f.calc_center_median() - c).normalized().z)
            base_c = leaf.lerp(leaf_light, n_up * p["top_light"]).lerp(leaf_dark, down * 0.8)
            hue = rnd.uniform(-1, 1)
            tone = Vector((1, 1, 1)).lerp(warm if hue > 0 else cool, abs(hue) * p["hue_var"])
            shade = rnd.uniform(0.9, 1.07)
            cc = Vector((base_c.x * tone.x, base_c.y * tone.y, base_c.z * tone.z)) * shade
            face_col[f] = (cc, 0.4 + 0.6 * min(1.0, (f.calc_center_median().z - H * 0.5) / (H * 0.5)))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    cols = [face_col.get(f, (leaf, 0.5)) for f in bm.faces]
    bm.to_mesh(mesh)
    bm.free()
    obj = common.mesh_object(name, mesh, col)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for poly, (c, a) in zip(mesh.polygons, cols):
        poly.use_smooth = True
        for li in poly.loop_indices:
            attr.data[li].color_srgb = (min(c.x, 1), min(c.y, 1), min(c.z, 1), a)
    # Facets that read (flat), softened a little toward the canopy's mass (the "inflated" light of the style)
    normals = []
    for poly in mesh.polygons:
        radial = (poly.center - centre).normalized() if poly.center.z > H * 0.5 else poly.normal
        n = poly.normal.lerp(radial, p["inflate"]).normalized()
        normals.extend([n] * len(poly.loop_indices))
    mesh.normals_split_custom_set(normals)
    return obj


def build(asset, p):
    rig = common.load_rig()
    common.reset_scene()
    col = common.collection(asset)
    lod0 = build_tree(asset + "_LOD0", "LOD0", p, col)
    lod1 = build_tree(asset + "_LOD1", "LOD1", p, col)
    mat = common.soft_toon_material("M_" + asset, rig, preset="Foliage", _BaseColor="#FFFFFF", _UseVertexColor=1.0,
                                    _Translucency=0.25, _RimStrength=0.14)
    for o in (lod0, lod1):
        o.data.materials.append(mat)
    lod1.hide_set(True)
    lookdev.build(rig)
    return {"LOD0": lod0, "LOD1": lod1}


def write_notes(asset, version, parts, args, paths, changes):
    notes = os.path.join(paths["dir"], "NOTES.md")
    if not os.path.exists(notes):
        with open(notes, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"# {FAMILY} / {asset}\n\n"
                    "## Briefing (2026-10-07, Fase 3)\n"
                    "- **O que é:** árvore de folha da floresta (copa de bolas facetadas, como a referência do desenvolvedor), para misturar com os pinheiros.\n"
                    "- **Pedido do desenvolvedor (literal):** \"Crie mais versoes de arvores variadas para popular a floresta.\"\n"
                    f"- **Triângulos:** LOD0 <= {BUDGET['LOD0']}, LOD1 <= {BUDGET['LOD1']}, LOD2 impostor.\n"
                    "- **Gerador:** `ArtSource/Broadleaf/broadleaf_gen.py`.\n\n"
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
    asset = args.get("asset", "ArvoreFolhosaA")
    f = lambda k, d: float(args.get(k, d))  # noqa: E731
    p = {
        "seed": int(args.get("seed", 3)),
        "height": f("height", 6.0),
        "canopy": f("canopy", 2.2),
        "blobs": int(args.get("blobs", 5)),
        "branches": int(args.get("branches", 3)),
        "trunk": f("trunk", 0.22),
        "squash": f("squash", 0.85),
        "spread": f("spread", 0.85),
        "top_light": f("top_light", 0.6),
        "hue_var": f("hue_var", 0.6),
        "inflate": f("inflate", 0.35),
        "bark": args.get("bark", "#7A4A30"),
        "leaf_dark": args.get("leaf_dark", "#3E6227"),
        "leaf": args.get("leaf", "#5E8E34"),
        "leaf_light": args.get("leaf_light", "#93BE4C"),
    }
    parts = build(asset, p)
    print(f"[broadleaf] tris { {k: common.triangle_count(o) for k, o in parts.items()} }")
    if "draft" in args:
        out = os.path.join(common.TMP_DIR, f"draft_{asset}_{args['draft']}.png")
        preview.render_sheet(out, "rascunho " + str(args["draft"]))
        print(f"[broadleaf] draft {out}")
        return
    version, paths = common.save_new_version(FAMILY, asset, int(args["version"]) if "version" in args else None)
    preview.render_sheet(paths["preview"], f"v{version:03d}")
    write_notes(asset, version, parts, p, paths, args.get("changes", "_(descrever)_"))
    print(f"[broadleaf] {paths['blend']}")


if __name__ == "__main__":
    main()
