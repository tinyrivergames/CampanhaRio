"""
The standard preview rig, matching the Unity LookDev scene (both read Assets/_Project/Art/LookDev/lookdev_rig.json):
  - a sun with the same angle and colour (soft shadows from its angular size),
  - no world light: the sky/ambient gradient is computed by the SoftToon material, the sky backdrop is composited,
  - a neutral ground plane that receives the shadow,
  - the scale reference: a simple rounded animal, 0.9 m tall (the future characters' size),
  - cameras placed from the shared views (yaw/pitch in Unity space), framing a bounding sphere like Unity does.
Everything lives in the "LookDev" collection, which the exporter ignores.
"""
import math

import bpy
from mathutils import Vector

import common

COLLECTION = "LookDev"


def build(rig=None):
    rig = rig or common.load_rig()
    scene = bpy.context.scene
    old = bpy.data.collections.get(COLLECTION)
    if old:
        for obj in list(old.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(old)
    col = common.collection(COLLECTION)

    # Sun: the light points down its local -Z; aim it so -Z travels from the sun toward the scene
    sun_data = bpy.data.lights.new("LookDev_Sun", "SUN")
    sun_data.energy = math.pi  # irradiance pi -> a white diffuse reads exactly N.L: the material's shadow probe (it divides by N.L)
    sun_data.color = (1.0, 1.0, 1.0)
    sun_data.angle = math.radians(rig["sun"]["angularDiameterDeg"])
    for attr, value in (("shadow_filter_radius", 3.0), ("use_shadow_jitter", False)):
        if hasattr(sun_data, attr):
            setattr(sun_data, attr, value)
    sun = bpy.data.objects.new("LookDev_Sun", sun_data)
    col.objects.link(sun)
    to_sun = common.sun_direction_blender(rig)
    sun.rotation_euler = to_sun.to_track_quat("Z", "Y").to_euler()

    # No world light (the material adds its own ambient); black, the backdrop is composited later
    world = bpy.data.worlds.get("LookDev_World") or bpy.data.worlds.new("LookDev_World")
    try:
        world.use_nodes = True
    except Exception:
        pass
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0, 0, 0, 1)
        bg.inputs["Strength"].default_value = 0.0
    scene.world = world

    # Ground: neutral, receives the shadow, same SoftToon formula
    size = rig["ground"]["size"]
    bpy.ops.mesh.primitive_plane_add(size=size)
    ground = bpy.context.active_object
    ground.name = "LookDev_Ground"
    common.link_only(ground, col)
    ground.data.materials.append(common.soft_toon_material("LookDev_GroundMat", rig, _BaseColor=rig["ground"]["color"], _RimStrength=0.0))

    configure_render(scene, rig)
    return col


def scale_reference(rig, at_x):
    """The 0.9 m rounded animal placeholder (body, head, ears, snout), standing at x = at_x, facing -Y (Blender front)."""
    col = common.collection(COLLECTION)
    h = rig["scaleRef"]["height"]
    mat = common.soft_toon_material("LookDev_ScaleRefMat", rig, preset="Character", _BaseColor=rig["scaleRef"]["color"])
    parts = []

    def blob(name, loc, scale):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, location=loc)
        o = bpy.context.active_object
        o.name = name
        o.scale = scale
        bpy.ops.object.shade_smooth()
        o.data.materials.append(mat)
        common.link_only(o, col)
        parts.append(o)

    s = h / 0.9
    blob("ScaleRef_Body", (at_x, 0, 0.30 * s), (0.22 * s, 0.2 * s, 0.30 * s))
    blob("ScaleRef_Head", (at_x, -0.03 * s, 0.70 * s), (0.17 * s, 0.16 * s, 0.16 * s))
    blob("ScaleRef_Snout", (at_x, -0.17 * s, 0.66 * s), (0.07 * s, 0.06 * s, 0.05 * s))
    blob("ScaleRef_EarL", (at_x - 0.09 * s, -0.01 * s, 0.85 * s), (0.04 * s, 0.03 * s, 0.07 * s))
    blob("ScaleRef_EarR", (at_x + 0.09 * s, -0.01 * s, 0.85 * s), (0.04 * s, 0.03 * s, 0.07 * s))
    return parts


def configure_render(scene, rig):
    scene.render.engine = "BLENDER_EEVEE"
    tile = rig["tileSize"]
    scene.render.resolution_x = tile
    scene.render.resolution_y = tile
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    # Linear EXR tiles: the sheet applies Unity's own post chain (preview.unity_post), so it shows what the game shows
    scene.render.image_settings.file_format = "OPEN_EXR"
    scene.render.image_settings.color_depth = "16"
    scene.render.image_settings.color_mode = "RGBA"
    ee = scene.eevee
    for attr, value in (("taa_render_samples", 32), ("use_shadows", True), ("shadow_ray_count", 2), ("shadow_step_count", 6)):
        if hasattr(ee, attr):
            setattr(ee, attr, value)
    # Neutral tonemapping like Unity's (closest available curve), no extra look
    for view in ("Khronos PBR Neutral", "Standard"):
        try:
            scene.view_settings.view_transform = view
            break
        except TypeError:
            continue
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0


def camera(name="LookDev_Camera"):
    cam = bpy.data.objects.get(name)
    if not cam:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        common.collection(COLLECTION).objects.link(cam)
    bpy.context.scene.camera = cam
    return cam


def frame(cam, objects, yaw_deg, pitch_deg, fov_deg, margin, zoom=1.0, center=None):
    """
    Places the camera like the Unity capture: orbit (pitch, yaw) around the bounding sphere's centre, far enough to fit
    the sphere in the vertical field of view (times margin, divided by zoom). yaw/pitch are Unity Euler angles.
    """
    lo, hi = common.world_bounds(objects)
    c = center if center is not None else (lo + hi) * 0.5
    radius = max((hi - lo).length * 0.5, 0.01)
    dist = radius * margin / math.sin(math.radians(fov_deg) * 0.5) / zoom
    forward_u = common.unity_euler_forward(pitch_deg, yaw_deg)
    offset = common.unity_to_blender([-forward_u[0] * dist, -forward_u[1] * dist, -forward_u[2] * dist])
    cam.location = c + offset
    cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(fov_deg)
    cam.data.clip_start = max(0.01, dist - radius * 4)
    cam.data.clip_end = dist + radius * 8 + 100
    return c, radius, dist
