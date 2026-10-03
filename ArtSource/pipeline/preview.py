"""
The preview sheet for an asset version (one PNG the developer reviews):

    row 1:  FRENTE | LADO | TRAS | 3/4                  (the same 4 angles the Unity LookDev capture renders)
    row 2:  CLOSE  | SILHUETA | ESCALA (0,9 m animal) | LODs side by side
    header: asset, version, triangles per LOD (and collider), size in meters, date

Rendered with EEVEE under the standard lookdev rig, then composed with numpy (no extra installs). The text is rendered
by Blender itself (an overlay scene), so no font library is needed.

Run on a saved version:
    blender.exe --background ArtSource/<Family>/<Asset>/<Asset>_v001.blend --python ArtSource/pipeline/preview.py
Optional: -- --out <png>
The .blend is not modified.
"""
import datetime
import os
import sys

import bpy
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
        tiles.append((render_tile(os.path.join(tmp, f"{view['name']}.png")), view["label"], "sky"))

    cu = rig["closeup"]
    lookdev.frame(cam, [lod0], cu["yawDeg"], cu["pitchDeg"], camcfg["fovDeg"], camcfg["margin"], zoom=cu["zoom"])
    tiles.append((render_tile(os.path.join(tmp, "closeup.png")), cu["label"], "sky"))

    show_only([lod0], keep_ground=False)
    lookdev.frame(cam, [lod0], rig["views"][0]["yawDeg"], camcfg["pitchDeg"], camcfg["fovDeg"], camcfg["margin"])
    tiles.append((render_tile(os.path.join(tmp, "silhouette.png")), "SILHUETA", "silhouette"))

    # Scale: the animal stands to the asset's right (Blender +X), on the same ground
    sr = rig["scaleRef"]
    ref_x = hi.x + sr["gap"] + 0.22 * sr["height"] / 0.9
    ref = lookdev.scale_reference(rig, ref_x)
    for p in ref:
        p.location.z += lo.z
    bpy.context.view_layer.update()
    show_only([lod0] + ref)
    lookdev.frame(cam, [lod0] + ref, sr["yawDeg"], camcfg["pitchDeg"], camcfg["fovDeg"], camcfg["margin"])
    tiles.append((render_tile(os.path.join(tmp, "scale.png")), sr["label"], "sky"))
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
    tiles.append((render_tile(os.path.join(tmp, "lods.png")), f"LODs: {counts} tris", "sky"))
    for o, loc in zip(lods, saved):
        o.location = loc
    return tiles


# ---------------------------------------------------------------- composing

def load_rgba(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    buf = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(buf)
    bpy.data.images.remove(img)
    return buf.reshape(h, w, 4)[::-1]  # top row first


def sky_backdrop(t, rig):
    top = np.array(common.hex_srgb(rig["background"]["top"]), dtype=np.float32)
    hor = np.array(common.hex_srgb(rig["background"]["horizon"]), dtype=np.float32)
    k = np.linspace(0.0, 1.0, t, dtype=np.float32)[:, None, None] ** 0.8
    return np.broadcast_to(top * (1 - k) + hor * k, (t, t, 3)).copy()


def compose(tiles, header_lines, rig, out_path, tmp):
    t = rig["tileSize"]
    cols, rows = 4, 2
    W = cols * t + (cols + 1) * MARGIN
    H = HEADER + rows * t + (rows + 1) * MARGIN
    sheet = np.ones((H, W, 3), dtype=np.float32) * np.array(common.hex_srgb("#ECE7DD"), dtype=np.float32)
    sheet[:HEADER] = np.array(common.hex_srgb("#2B2F36"), dtype=np.float32)

    labels = []
    for i, (path, label, kind) in enumerate(tiles):
        r, c = divmod(i, cols)
        x = MARGIN + c * (t + MARGIN)
        y = HEADER + MARGIN + r * (t + MARGIN)
        rgba = load_rgba(path)
        alpha = rgba[..., 3:4]
        if kind == "silhouette":
            tile = np.ones((t, t, 3), dtype=np.float32) * (1.0 - alpha) + np.array([0.11, 0.12, 0.14], dtype=np.float32) * alpha
        else:
            tile = sky_backdrop(t, rig) * (1.0 - alpha) + rgba[..., :3] * alpha
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
