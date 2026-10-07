"""
The preview sheet for an asset version (one PNG the developer reviews):

    row 1:  FRENTE | LADO | TRAS | 3/4 | CLOSE       (the 4 angles are the ones the Unity LookDev capture renders)
    row 2:  SILHUETA | ESCALA (0,9 m animal) | LODs side by side | HORA DOURADA | POR DO SOL (3/4, relit)
    header: asset, version, triangles per LOD (and collider), size in meters, date

Rendered with EEVEE under the standard lookdev rig, then composed with numpy (no extra installs). The text is rendered
by Blender itself (an overlay scene), so no font library is needed.

Run on a saved version:
    blender.exe --background ArtSource/<Family>/<Asset>/<Asset>_v001.blend --python ArtSource/pipeline/preview.py
Optional: -- --out <png>
The .blend is not modified.
"""
import datetime
import math
import os
import sys

import bpy
import mathutils
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402
import lookdev  # noqa: E402

MARGIN = 16
HEADER = 150
LABEL_BAND = 44


def asset_parts():
    """{'LOD0': obj, 'LOD1': obj, ..., 'COL': obj} for the one asset in the file, and its name."""
    parts, name = {}, None
    for obj in bpy.data.objects:
        m = common.PART_NAME.match(obj.name)
        if m and obj.type == "MESH":
            name = name or m.group("asset")
            parts[m.group("part")] = obj
    if "LOD0" not in parts:
        raise RuntimeError("No <Asset>_LOD0 mesh in this file (naming rule: <Asset>_LOD0/1/2, <Asset>_COL).")
    return name, parts


def render_tile(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def show_only(objects, keep_ground=True):
    for obj in bpy.data.objects:
        if obj.type in {"MESH", "FONT"}:
            obj.hide_render = obj not in objects and not (keep_ground and obj.name == "LookDev_Ground")


def render_tiles(name, parts, rig, tmp):
    scene = bpy.context.scene
    cam = lookdev.camera()
    camcfg = rig["camera"]
    lod0 = parts["LOD0"]
    lo, hi = common.world_bounds([lod0])
    ground = bpy.data.objects["LookDev_Ground"]
    ground.location.z = lo.z
    tiles = []

    show_only([lod0])
    for view in rig["views"]:
        lookdev.frame(cam, [lod0], view["yawDeg"], camcfg["pitchDeg"], camcfg["fovDeg"], camcfg["margin"])
        tiles.append((render_tile(os.path.join(tmp, f"{view['name']}.exr")), view["label"], "sky", camcfg["pitchDeg"]))

    cu = rig["closeup"]
    lookdev.frame(cam, [lod0], cu["yawDeg"], cu["pitchDeg"], camcfg["fovDeg"], camcfg["margin"], zoom=cu["zoom"])
    tiles.append((render_tile(os.path.join(tmp, "closeup.exr")), cu["label"], "sky", cu["pitchDeg"]))

    show_only([lod0], keep_ground=False)
    lookdev.frame(cam, [lod0], rig["views"][0]["yawDeg"], camcfg["pitchDeg"], camcfg["fovDeg"], camcfg["margin"])
    tiles.append((render_tile(os.path.join(tmp, "silhouette.exr")), "SILHUETA", "silhouette", 0))

    # Scale: the animal stands to the asset's right (Blender +X), on the same ground
    sr = rig["scaleRef"]
    ref_x = hi.x + sr["gap"] + 0.22 * sr["height"] / 0.9
    ref = lookdev.scale_reference(rig, ref_x)
    for p in ref:
        p.location.z += lo.z
    bpy.context.view_layer.update()
    show_only([lod0] + ref)
    lookdev.frame(cam, [lod0] + ref, sr["yawDeg"], camcfg["pitchDeg"], camcfg["fovDeg"], camcfg["margin"])
    tiles.append((render_tile(os.path.join(tmp, "scale.exr")), sr["label"], "sky", camcfg["pitchDeg"]))
    for p in ref:
        p.hide_render = True

    # LODs side by side (moved temporarily), front 3/4
    lods = [parts[k] for k in ("LOD0", "LOD1", "LOD2") if k in parts]
    width = (hi.x - lo.x) * 1.25
    saved = [o.location.copy() for o in lods]
    for i, o in enumerate(lods):
        o.location.x = saved[i].x + (i - (len(lods) - 1) * 0.5) * width
    bpy.context.view_layer.update()
    show_only(lods)
    lookdev.frame(cam, lods, 180, camcfg["pitchDeg"], camcfg["fovDeg"], camcfg["margin"])
    counts = " / ".join(str(common.triangle_count(o)) for o in lods)
    tiles.append((render_tile(os.path.join(tmp, "lods.exr")), f"LODs: {counts} tris", "sky", camcfg["pitchDeg"]))
    for o, loc in zip(lods, saved):
        o.location = loc
    bpy.context.view_layer.update()

    # The same 3/4 view at golden hour and at sunset (the descents happen then): every SoftToon material is relit
    show_only([lod0])
    for time, label in (("golden", "HORA DOURADA"), ("sunset", "POR DO SOL")):
        relight(common.rig_at(time))
        lookdev.frame(cam, [lod0], rig["views"][3]["yawDeg"], camcfg["pitchDeg"], camcfg["fovDeg"], camcfg["margin"])
        tiles.append((render_tile(os.path.join(tmp, f"{time}.exr")), label, "sky", camcfg["pitchDeg"], time))
    relight(rig)
    return tiles


def relight(rig):
    """Rebuilds every SoftToon preview material and the sun for another light (a time of day)."""
    import json
    for mat in bpy.data.materials:
        if "cr_softtoon" in mat:
            p = json.loads(mat["cr_softtoon"])
            preset = p.pop("preset", "Default")
            common.soft_toon_material(mat.name, rig, preset=preset, **p)
    sun = bpy.data.objects.get("LookDev_Sun")
    if sun:
        sun.rotation_euler = common.sun_direction_blender(rig).to_track_quat("Z", "Y").to_euler()


# ---------------------------------------------------------------- composing

def load_rgba(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    buf = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(buf)
    bpy.data.images.remove(img)
    return buf.reshape(h, w, 4)[::-1]  # top row first


def sky_backdrop(t, rig, pitch_deg=None, fov_deg=None):
    """The Unity sky (SkyGradient.shader) by each row's view elevation: horizon -> top, and toward the ground below."""
    pitch = rig["camera"]["pitchDeg"] if pitch_deg is None else pitch_deg
    fov = math.radians(rig["camera"]["fovDeg"] if fov_deg is None else fov_deg)
    ndc = 1.0 - 2.0 * (np.arange(t, dtype=np.float32) + 0.5) / t
    up = np.sin(np.arctan(ndc * math.tan(fov * 0.5)) - math.radians(pitch))[:, None]
    top, hor = (np.array(common.lin(rig["background"][k])[:3], dtype=np.float32) for k in ("top", "horizon"))
    gnd = np.array(common.lin(rig["sky"]["ground"])[:3], dtype=np.float32)
    k = 1.0 - (1.0 - np.clip(up, 0.0, 1.0)) ** rig["sky"]["horizonSharpness"]
    sky = hor * (1.0 - k) + top * k
    g = np.clip(-up * 4.0, 0.0, 1.0)
    sky = sky * (1.0 - g) + gnd * g
    return np.broadcast_to(sky[:, None, :], (t, t, 3)).copy()


def unity_post(linear, rig):
    """
    Unity's post chain on scene-linear colour, so the sheet shows what the game shows (lookdev_rig.json "grading"):
    post exposure, white balance (approx.), contrast around mid grey in log space, saturation around luminance, then
    URP's Neutral tonemapper (the exact curve from URP's Color.hlsl NeutralTonemap) and the sRGB encoding.
    """
    gr = rig["grading"]
    x = np.maximum(linear, 0.0) * 2.0 ** gr["postExposure"]
    x = x * np.array([1.0 + gr["temperature"] * 0.004, 1.0, 1.0 - gr["temperature"] * 0.004], dtype=np.float32)
    x = 0.18 * np.power(np.maximum(x, 1e-6) / 0.18, 1.0 + gr["contrast"] / 100.0)
    lum = (x * np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)).sum(axis=-1, keepdims=True)
    x = np.maximum(lum + (x - lum) * (1.0 + gr["saturation"] / 100.0), 0.0)
    x = neutral_tonemap(x)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(np.clip(x, 0.0031308, 1.0), 1 / 2.4) - 0.055).clip(0.0, 1.0)


def neutral_tonemap(x):
    """URP's NeutralTonemap (Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl)."""
    a, b, c, d, e, f = 0.2, 0.29, 0.24, 0.272, 0.02, 0.3
    white_level, white_clip = 5.3, 1.0

    def curve(v):
        return ((v * (a * v + c * b) + d * e) / (v * (a * v + b) + d * f)) - e / f

    scale = 1.0 / curve(white_level)
    return curve(x * scale) * scale / white_clip


def over_sky(rgba, rig, pitch):
    """A linear, premultiplied render over the sky, through Unity's post: a display-ready tile."""
    a = rgba[..., 3:4]
    return unity_post(sky_backdrop(rgba.shape[0], rig, pitch) * (1.0 - a) + rgba[..., :3], rig)


def compose(tiles, header_lines, rig, out_path, tmp):
    t = rig["tileSize"]
    cols, rows = 5, 2
    W = cols * t + (cols + 1) * MARGIN
    H = HEADER + rows * t + (rows + 1) * MARGIN
    sheet = np.ones((H, W, 3), dtype=np.float32) * np.array(common.hex_srgb("#ECE7DD"), dtype=np.float32)
    sheet[:HEADER] = np.array(common.hex_srgb("#2B2F36"), dtype=np.float32)

    labels = []
    for i, (path, label, kind, pitch, *time) in enumerate(tiles):
        r, c = divmod(i, cols)
        x = MARGIN + c * (t + MARGIN)
        y = HEADER + MARGIN + r * (t + MARGIN)
        rgba = load_rgba(path)
        alpha = rgba[..., 3:4]
        if kind == "silhouette":
            tile = np.ones((t, t, 3), dtype=np.float32) * (1.0 - alpha) + np.array([0.11, 0.12, 0.14], dtype=np.float32) * alpha
        else:
            tile = over_sky(rgba, common.rig_at(time[0]) if time else rig, pitch)
        tile[:LABEL_BAND] = tile[:LABEL_BAND] * 0.55  # a darker band for the label
        sheet[y:y + t, x:x + t] = tile
        labels.append((x + 14, y + 31, label, 24))

    texts = [(MARGIN + 6, 62, header_lines[0], 44), (MARGIN + 8, 112, header_lines[1], 26)] + labels
    overlay = render_text_overlay(W, H, texts, tmp)
    a = overlay[..., 3:4]
    sheet = sheet * (1.0 - a) + overlay[..., :3] * a
    save_png(sheet, out_path)


def save_png(rgb, path):
    h, w, _ = rgb.shape
    img = bpy.data.images.new("PreviewSheet", w, h, alpha=True)
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((h, w, 1), dtype=np.float32)], axis=2)[::-1]
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def render_text_overlay(W, H, texts, tmp):
    """White text on transparent, rendered by Blender in a separate scene. texts: (x, baseline_y, string, px)."""
    main = bpy.context.window.scene if bpy.context.window else bpy.context.scene
    scene = bpy.data.scenes.new("SheetText")
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = W, H
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    if hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 8
    mat = bpy.data.materials.new("SheetTextMat")
    try:
        mat.use_nodes = True
    except Exception:
        pass
    nt = mat.node_tree
    nt.nodes.clear()
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (1, 1, 1, 1)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(em.outputs[0], out.inputs["Surface"])

    px = 0.01  # 1 unit = 100 px
    for x, y, s, size in texts:
        curve = bpy.data.curves.new("SheetText", "FONT")
        curve.body = s
        curve.size = size * px * 1.35
        obj = bpy.data.objects.new("SheetText", curve)
        obj.location = (x * px, (H - y) * px, 0)
        obj.data.materials.append(mat)
        scene.collection.objects.link(obj)
    cam = bpy.data.objects.new("SheetCam", bpy.data.cameras.new("SheetCam"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(W, H) * px
    cam.location = (W * px * 0.5, H * px * 0.5, 10)
    scene.collection.objects.link(cam)
    scene.camera = cam
    path = os.path.join(tmp, "overlay.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=scene.name)
    bpy.data.scenes.remove(scene)
    _ = main
    return load_rgba(path)


# ---------------------------------------------------------------- entry points

def render_sheet(out_path, version_label):
    rig = common.load_rig()
    name, parts = asset_parts()
    lookdev.build(rig)
    tmp = os.path.join(common.TMP_DIR, "preview_" + name)
    os.makedirs(tmp, exist_ok=True)
    tiles = render_tiles(name, parts, rig, tmp)

    lo, hi = common.world_bounds([parts["LOD0"]])
    size = hi - lo
    tris = "   ".join(f"{k} {common.triangle_count(parts[k])}" for k in ("LOD0", "LOD1", "LOD2", "COL") if k in parts)
    header = [f"{name}   {version_label}",
              f"tris: {tris}      tamanho: {size.x:.2f} x {size.y:.2f} x {size.z:.2f} m (L x P x A)      {datetime.date.today():%Y-%m-%d}"]
    compose(tiles, header, rig, out_path, tmp)
    print(f"[preview] wrote {out_path}")
    return out_path


def main():
    args = common.script_args()
    blend = bpy.data.filepath
    stem = os.path.splitext(os.path.basename(blend))[0]
    out = args.get("out") or os.path.join(os.path.dirname(blend), stem + "_preview.png")
    version = stem.split("_v")[-1] if "_v" in stem else "?"
    render_sheet(out, "v" + version)


if __name__ == "__main__":
    main()


def render_field(out_path, objects, rig, ground_hex="#3F7A2A", count=140, radius=3.2, eye=(0.0, 4.6, 1.15),
                 target=(0.0, 0.0, 0.35), fov_deg=50.0, size=(1200, 675), seed=1, scale=(0.8, 1.25)):
    """
    A field of copies of these objects (linked duplicates, random spin and size) on a plain ground in the game's light,
    seen from a low camera, through Unity's post: to compare a ground plant with a reference picture.
    """
    import random
    scene = bpy.context.scene
    rnd = random.Random(seed)
    col = bpy.data.collections.new("FieldView")
    scene.collection.children.link(col)
    gmesh = bpy.data.meshes.new("FieldGround")
    s = radius * 6
    gmesh.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    ground = bpy.data.objects.new("FieldGround", gmesh)
    col.objects.link(ground)
    ground.data.materials.append(common.soft_toon_material("M_FieldGround", rig, preset="Default", _BaseColor=ground_hex))
    copies = []
    for i in range(count):
        src = objects[i % len(objects)]
        o = src.copy()
        col.objects.link(o)
        a = rnd.uniform(0, 2 * math.pi)
        r = radius * math.sqrt(rnd.random())
        o.location = (math.cos(a) * r * 1.6, -math.sin(a) * r * 1.1 + radius * 0.4, 0.0)
        o.rotation_euler = (0.0, 0.0, rnd.uniform(0, 2 * math.pi))
        k = rnd.uniform(*scale)
        o.scale = (k, k, k)
        o.hide_render = False
        o.visible_shadow = False  # (in the game the grass is drawn without casting shadows)
        copies.append(o)
    for obj in bpy.data.objects:
        if obj.type in {"MESH", "FONT"} and obj not in copies and obj is not ground:
            obj.hide_render = True
    cam = lookdev.camera("FieldCam")
    cam.data.angle = math.radians(fov_deg)
    cam.location = eye
    d = mathutils.Vector(target) - mathutils.Vector(eye)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = size
    tmp = os.path.join(common.TMP_DIR, "field.exr")
    render_tile(tmp)
    rgba = load_rgba(tmp)
    pitch = math.degrees(math.atan2(-d.z, math.hypot(d.x, d.y)))
    h, w = rgba.shape[:2]
    sky = sky_backdrop(max(h, w), rig, pitch, fov_deg)[:h, :w]
    a = rgba[..., 3:4]
    save_png(unity_post(sky * (1.0 - a) + rgba[..., :3], rig), out_path)
    print(f"[preview] field {out_path}")
    return out_path
