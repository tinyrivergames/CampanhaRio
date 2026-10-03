"""
The Blender half of the look match test: the same sphere and cube, with the same SoftToon material and positions, as the
"MatchTest" subject of the Unity LookDev scene, rendered from the same views (front, side, back, 3/4, close-up) into one
strip. Compare it with MatchTest_views.png from the Unity capture (see Docs/TECH_DECISIONS.md).

    blender.exe --background --factory-startup --python ArtSource/Test/match_test.py -- --out <png>
"""
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "pipeline"))
import common  # noqa: E402
import lookdev  # noqa: E402
import preview  # noqa: E402


def main():
    args = common.script_args()
    out = os.path.abspath(args["out"]) if "out" in args else os.path.join(common.TMP_DIR, "MatchTest_blender_views.png")
    rig = common.load_rig()
    common.reset_scene()
    lookdev.build(rig)
    col = common.collection("MatchTest")
    mat = common.soft_toon_material("M_MatchTest", rig, _BaseColor="#D08A55")

    # Unity (-0.7, 0.5, 0) sphere r 0.5 and (0.7, 0.4, 0) cube 0.8 turned 30 deg -> Blender (x, y, z) = (-ux, -uz, uy)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=0.5, location=(0.7, 0.0, 0.5))
    sphere = bpy.context.active_object
    bpy.ops.object.shade_smooth()
    bpy.ops.mesh.primitive_cube_add(size=0.8, location=(-0.7, 0.0, 0.4), rotation=(0, 0, math.radians(-30)))
    cube = bpy.context.active_object
    for o in (sphere, cube):
        o.data.materials.append(mat)
        common.link_only(o, col)
    bpy.data.objects["LookDev_Ground"].location.z = 0.0
    bpy.context.view_layer.update()

    tmp = os.path.join(common.TMP_DIR, "match_test")
    os.makedirs(tmp, exist_ok=True)
    cam = lookdev.camera()
    c = rig["camera"]
    paths, pitches = [], []
    for view in rig["views"]:
        lookdev.frame(cam, [sphere, cube], view["yawDeg"], c["pitchDeg"], c["fovDeg"], c["margin"])
        paths.append(preview.render_tile(os.path.join(tmp, view["name"] + ".png")))
        pitches.append(c["pitchDeg"])
    cu = rig["closeup"]
    lookdev.frame(cam, [sphere, cube], cu["yawDeg"], cu["pitchDeg"], c["fovDeg"], c["margin"], zoom=cu["zoom"])
    paths.append(preview.render_tile(os.path.join(tmp, "closeup.png")))
    pitches.append(cu["pitchDeg"])

    t, gap = rig["tileSize"], 8
    strip = np.ones((t, len(paths) * t + (len(paths) - 1) * gap, 3), dtype=np.float32) * np.array([236, 231, 221], dtype=np.float32) / 255.0
    for i, p in enumerate(paths):
        rgba = preview.load_rgba(p)
        a = rgba[..., 3:4]
        strip[:, i * (t + gap):i * (t + gap) + t] = preview.grade(preview.sky_backdrop(t, rig, pitches[i]) * (1 - a) + rgba[..., :3] * a, rig)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    preview.save_png(strip, out)
    print(f"[match_test] wrote {out}")


if __name__ == "__main__":
    main()
