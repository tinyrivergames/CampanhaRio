"""
Exports an APPROVED asset version to Unity:
    Assets/_Project/Art/Models/<Family>/<Asset>.fbx          LODs as <Asset>_LOD0/_LOD1/_LOD2 (+ optional <Asset>_COL)
    Assets/_Project/Art/Models/<Family>/<Asset>.softtoon.json  the SoftToon material parameters (Unity builds the material)

FBX settings for Unity: meters (scale 1), Y up and -Z forward with the transform applied (Blender's front, -Y, becomes
Unity's +Z), no leaf bones, normals and vertex colours exported. Each part's origin stays where it is (assets are built
with the origin at the base centre). The LookDev collection is never exported.

    blender.exe --background ArtSource/<Family>/<Asset>/<Asset>_v003.blend --python ArtSource/pipeline/export.py
"""
import json
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402


def export(blend_path=None, impostor=False):
    blend_path = blend_path or bpy.data.filepath
    family = os.path.basename(os.path.dirname(os.path.dirname(blend_path)))
    parts = [o for o in bpy.data.objects if o.type == "MESH" and common.PART_NAME.match(o.name)]
    problems = common.check_names(parts)
    if problems or not parts:
        raise RuntimeError("Naming problems: " + "; ".join(problems or ["no <Asset>_LOD0 mesh"]))
    asset = common.PART_NAME.match(parts[0].name).group("asset")

    out_dir = os.path.join(common.MODELS_DIR, family)
    os.makedirs(out_dir, exist_ok=True)
    fbx = os.path.join(out_dir, asset + ".fbx")

    # Optional far LOD: an impostor baked now from LOD0 (the approved .blend is not changed)
    if impostor and not any(o.name.endswith("_LOD2") for o in parts):
        parts.append(make_impostor(asset, family, next(o for o in parts if o.name.endswith("_LOD0"))))

    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.hide_set(False)
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.export_scene.fbx(
        filepath=fbx,
        use_selection=True,
        object_types={"MESH"},
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        use_mesh_modifiers=True,
        mesh_smooth_type="OFF",
        use_tspace=False,
        colors_type="SRGB",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
    )

    # The materials (by name) with their SoftToon parameters for Unity; textures as Unity asset paths
    materials = {}
    for o in parts:
        for slot in o.material_slots:
            m = slot.material
            if m and m.name not in materials:
                p = json.loads(m["cr_softtoon"]) if "cr_softtoon" in m else common.softtoon_params()
                if p.get("_BaseMap"):
                    p["_BaseMap"] = os.path.relpath(p["_BaseMap"], common.REPO).replace("\\", "/")
                materials[m.name] = p
    params = materials.get(parts[0].active_material.name) if parts[0].active_material else common.softtoon_params()
    sidecar = {
        "asset": asset,
        "family": family,
        "source": os.path.relpath(blend_path, common.REPO).replace("\\", "/"),
        "material": params,
        "materialList": [dict(name=k, **v) for k, v in materials.items()],
        "triangles": {o.name.split("_")[-1]: common.triangle_count(o) for o in parts},
    }
    with open(os.path.join(out_dir, asset + ".softtoon.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(sidecar, f, indent=4)
    print(f"[export] {fbx}  parts: {', '.join(sorted(o.name for o in parts))}")
    return fbx


def make_impostor(asset, family, lod0):
    """LOD2: the tree's colours baked from the front onto two crossed quads (Art/Textures/<Family>/<Asset>_impostor.png)."""
    import impostor
    tex_dir = os.path.join(common.REPO, "Assets", "_Project", "Art", "Textures", family)
    os.makedirs(tex_dir, exist_ok=True)
    png = os.path.join(tex_dir, asset + "_impostor.png")
    _, (lo, hi) = impostor.bake_albedo([lod0], png, 256, 512)
    obj = impostor.impostor_mesh(asset + "_LOD2", lo, hi, lod0.users_collection[0])
    src = json.loads(lod0.active_material["cr_softtoon"]) if lod0.active_material and "cr_softtoon" in lod0.active_material else {}
    preset = src.get("preset", "Foliage")
    mat = common.soft_toon_material(f"M_{asset}_Impostor", None, preset=preset, _BaseColor="#FFFFFF", _UseVertexColor=0.0,
                                    _BaseMap=png, _Translucency=src.get("_Translucency", 0.3), _RimStrength=0.08)
    obj.data.materials.append(mat)
    print(f"[export] impostor {png}: {common.triangle_count(obj)} tris")
    return obj


if __name__ == "__main__":
    export(impostor="impostor" in common.script_args())
