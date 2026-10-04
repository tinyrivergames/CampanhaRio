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


def export(blend_path=None):
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

    # The material: one per asset (the LOD0's first material), parameters for Unity's SoftToon shader
    mat = parts[0].active_material
    params = json.loads(mat["cr_softtoon"]) if mat and "cr_softtoon" in mat else common.softtoon_params()
    sidecar = {
        "asset": asset,
        "family": family,
        "source": os.path.relpath(blend_path, common.REPO).replace("\\", "/"),
        "material": params,
        "triangles": {o.name.split("_")[-1]: common.triangle_count(o) for o in parts},
    }
    with open(os.path.join(out_dir, asset + ".softtoon.json"), "w", encoding="utf-8", newline="
") as f:
        json.dump(sidecar, f, indent=4)
    print(f"[export] {fbx}  parts: {', '.join(sorted(o.name for o in parts))}")
    return fbx


if __name__ == "__main__":
    export()
