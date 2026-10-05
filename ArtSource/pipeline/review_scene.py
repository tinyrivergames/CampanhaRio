"""
A review scene to look around in the Blender GUI under a chosen time of day: the asset (its LOD0), the 0.9 m animal,
the match-test sphere and cube, the ground and the sky, with the SoftToon preview materials lit for that time.

    blender.exe --background --factory-startup --python ArtSource/pipeline/review_scene.py -- --asset Test/Pebble --time sunset
    then open ArtSource/_tmp/review_<time>.blend (art review-time does both).

The times copy the DayCycle keys (Assets/_Project/Scripts/Rendering/DayCycle.cs DefaultKeys): keep them in sync
until the keys move into lookdev_rig.json.
"""
import copy
import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402
import lookdev  # noqa: E402

TIMES, rig_at = common.TIMES, common.rig_at


def sky_world(rig):
    """The sky gradient for the camera only (so it never lights the scene: the material has its own ambient)."""
    world = bpy.context.scene.world
    nt = world.node_tree
    nt.nodes.clear()
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["Generated"], sep.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = common.lin(rig["background"]["horizon"])
    ramp.color_ramp.elements[1].position = 0.6
    ramp.color_ramp.elements[1].color = common.lin(rig["background"]["top"])
    nt.links.new(sep.outputs["Z"], ramp.inputs[0])
    path = nt.nodes.new("ShaderNodeLightPath")
    mix = nt.nodes.new("ShaderNodeMixShader")
    black = nt.nodes.new("ShaderNodeBackground")
    black.inputs["Color"].default_value = (0, 0, 0, 1)
    sky = nt.nodes.new("ShaderNodeBackground")
    nt.links.new(ramp.outputs[0], sky.inputs["Color"])
    nt.links.new(path.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(black.outputs[0], mix.inputs[1])
    nt.links.new(sky.outputs[0], mix.inputs[2])
    out = nt.nodes.new("ShaderNodeOutputWorld")
    nt.links.new(mix.outputs[0], out.inputs[0])


def main():
    args = common.script_args()
    family, asset = args.get("asset", "Test/Pebble").split("/")
    time = args.get("time", "sunset")
    rig = rig_at(time)
    common.reset_scene()
    lookdev.build(rig)
    scene = bpy.context.scene
    scene.render.film_transparent = False
    sky_world(rig)

    # The asset's LOD0 from its latest version, relit for this time
    version = common.latest_version(family, asset)
    src = common.version_paths(family, asset, version)["blend"]
    with bpy.data.libraries.load(src, link=False) as (data_from, data_to):
        data_to.objects = [n for n in data_from.objects if n == asset + "_LOD0"]
    col = common.collection(asset)
    for o in data_to.objects:
        col.objects.link(o)
        mat = o.active_material
        if mat and "cr_softtoon" in mat:
            p = json.loads(mat["cr_softtoon"])
            preset = p.pop("preset", "Default")
            common.soft_toon_material(mat.name, rig, preset=preset, **p)

    # The match-test sphere and cube (same places as the Unity LookDev), and the 0.9 m animal
    test = common.soft_toon_material("M_MatchTest", rig, _BaseColor="#D08A55")
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=0.5, location=(2.0, 1.5, 0.5))
    bpy.ops.object.shade_smooth()
    sphere = bpy.context.active_object
    bpy.ops.mesh.primitive_cube_add(size=0.8, location=(0.6, 1.5, 0.4), rotation=(0, 0, math.radians(-30)))
    cube = bpy.context.active_object
    for o in (sphere, cube):
        o.data.materials.append(test)
    lookdev.scale_reference(rig, -1.2)

    # A camera like the Unity LookDev view, and the viewport set to look through it
    cam = lookdev.camera()
    lookdev.frame(cam, [o for o in bpy.data.objects if o.type == "MESH" and o.name != "LookDev_Ground"], 200, 14, 40, 1.0)
    out = os.path.join(common.TMP_DIR, f"review_{time}.blend")
    os.makedirs(common.TMP_DIR, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=out)
    print(f"[review] {out}")


if __name__ == "__main__":
    main()
