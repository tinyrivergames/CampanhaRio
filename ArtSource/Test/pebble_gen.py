"""
Family generator: Test / Pebble (THROWAWAY: it only proves the pipeline; the real rock family comes in Phase 1).

A rounded, stylized river stone: a sphere squashed and pushed by a few large soft lumps (no small noise), a flattened
base, two LODs (+ a tiny collider), normals copied from the smooth proxy so the light wraps softly, a gentle vertex-colour
variation and the Rock preset of the SoftToon material.

    art build Test\pebble_gen.py --asset Pebble [--seed 3] [--length 0.62] [--width 0.46] [--height 0.32]

Saves the next version (never overwrites), renders its preview sheet and adds the version to NOTES.md.
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Vector, noise

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "pipeline"))
import common  # noqa: E402
import lookdev  # noqa: E402
import preview  # noqa: E402

FAMILY = "Test"
BUDGET = {"LOD0": 600, "LOD1": 150, "COL": 40}


def build(asset, seed, length, width, height):
    rig = common.load_rig()
    common.reset_scene()
    col = common.collection(asset)
    rnd = random.Random(seed)

    # The smooth proxy: a dense, squashed sphere with 3 big soft lumps and a flat base
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=5, radius=0.5)
    lumps = [Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.2, 1))).normalized() for _ in range(3)]
    for v in bm.verts:
        d = v.co.normalized()
        push = sum(0.08 * max(0.0, d.dot(l)) ** 3 for l in lumps)
        push += 0.025 * noise.noise(d * 1.4 + Vector((seed, 0, 0)))
        co = d * (0.5 + push)
        co.x *= length
        co.y *= width
        co.z *= height
        if co.z < -height * 0.18:  # a resting base, slightly rounded
            co.z = -height * 0.18 + (co.z + height * 0.18) * 0.25
        v.co = co
    lo_z = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= lo_z  # origin at the base centre (the export keeps it)
    proxy = common.mesh_object(asset + "_Proxy", bpy.data.meshes.new(asset + "_Proxy"), col)
    bm.to_mesh(proxy.data)
    bm.free()
    common.smooth(proxy)

    lod0 = common.make_lod(proxy, asset + "_LOD0", BUDGET["LOD0"] / common.triangle_count(proxy), col, normals_from=proxy)
    lod1 = common.make_lod(proxy, asset + "_LOD1", BUDGET["LOD1"] / common.triangle_count(proxy), col, normals_from=proxy)
    colmesh = common.make_lod(proxy, asset + "_COL", BUDGET["COL"] / common.triangle_count(proxy), col)

    # Slightly darker and warmer toward the base, a lighter sun-bleached top
    zmax = max(v.co.z for v in lod0.data.vertices)

    def tint(p, n):
        t = max(0.0, min(1.0, p.z / zmax))
        k = 0.82 + 0.18 * t + 0.05 * noise.noise(p * 3.0)
        return (k, k * (0.98 + 0.02 * t), k * (0.95 + 0.05 * t))

    mat = common.soft_toon_material("M_" + asset, rig, preset="Rock", _BaseColor=common.palette("rock_sand"),
                                    _UseVertexColor=1.0, _TopTint=common.palette("moss"), _TopTintAmount=0.2,
                                    _GradientBottom="#D9C6B4", _GradientTop="#FFFFFF", _GradientHeights=[0.0, height])
    for o in (lod0, lod1):
        common.paint_vertex_colors(o, tint)
        o.data.materials.append(mat)
    colmesh.data.materials.append(mat)
    colmesh.hide_render = True
    colmesh.display_type = "WIRE"
    bpy.data.objects.remove(proxy, do_unlink=True)
    lod1.hide_set(True)

    lookdev.build(rig)  # saved with the file: open it and the look is there
    return {"LOD0": lod0, "LOD1": lod1, "COL": colmesh}


def write_notes(asset, version, parts, args, paths):
    notes = os.path.join(paths["dir"], "NOTES.md")
    if not os.path.exists(notes):
        with open(notes, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"# {FAMILY} / {asset}\n\n"
                    "## Briefing\n"
                    "- **O que é:** um seixo de rio arredondado e estilizado. **Asset descartável**: só prova o pipeline (Fase 0).\n"
                    "- **Referências:** pedras lisas laranja/areia das imagens de referência (Docs/Reference).\n"
                    "- **Tamanho:** ~0,62 x 0,46 x 0,32 m.\n"
                    f"- **Orçamento de triângulos:** LOD0 <= {BUDGET['LOD0']}, LOD1 <= {BUDGET['LOD1']}, colisor <= {BUDGET['COL']}.\n"
                    "- **Cores:** rock_sand com topo levemente musgo (paleta do rig).\n"
                    "- **Gerador:** `ArtSource/Test/pebble_gen.py`.\n\n"
                    "## Versões\n")
    tris = ", ".join(f"{k} {common.triangle_count(o)}" for k, o in parts.items())
    with open(notes, "a", encoding="utf-8", newline="\n") as f:
        f.write(f"\n### v{version:03d}\n"
                f"- Gerado com: `{' '.join(f'--{k} {v}' for k, v in args.items())}`\n"
                f"- Triângulos: {tris}\n"
                f"- Prévia: `{os.path.basename(paths['preview'])}`\n"
                "- **Feedback do desenvolvedor (literal):** _(aguardando)_\n"
                "- **O que mudou:** primeira versão.\n" if version == 1 else
                f"\n### v{version:03d}\n"
                f"- Gerado com: `{' '.join(f'--{k} {v}' for k, v in args.items())}`\n"
                f"- Triângulos: {tris}\n"
                f"- Prévia: `{os.path.basename(paths['preview'])}`\n"
                "- **Feedback do desenvolvedor (literal):** _(aguardando)_\n"
                "- **O que mudou:** _(descrever)_\n")


def main():
    args = common.script_args()
    asset = args.get("asset", "Pebble")
    seed = int(args.get("seed", 3))
    length, width, height = float(args.get("length", 0.62)), float(args.get("width", 0.46)), float(args.get("height", 0.32))
    parts = build(asset, seed, length, width, height)
    version, paths = common.save_new_version(FAMILY, asset, int(args["version"]) if "version" in args else None)
    preview.render_sheet(paths["preview"], f"v{version:03d}")
    write_notes(asset, version, parts, {"asset": asset, "seed": seed, "length": length, "width": width, "height": height}, paths)
    print(f"[pebble] {paths['blend']}")


if __name__ == "__main__":
    main()
