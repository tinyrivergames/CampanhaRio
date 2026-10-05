"""
Impostors and background cards: what distant trees become (ART_PIPELINE.md, section 5).

  - bake_albedo(): renders objects' COLOUR only (their vertex colours / textures, no light) from the front, orthographic,
    with alpha, into a PNG. The game lights it with the SoftToon shader, so it follows the time of day.
  - impostor_mesh(): two crossed vertical quads (4 triangles) carrying that texture: the far LOD of a tree. Normals lean
    outward and up, so the flat planes light like a rounded volume.
  - card_mesh(): one quad (2 triangles) for a background "blur" card.
"""
import math
import os

import bpy
import numpy as np
from mathutils import Vector

import common


def bake_albedo(objects, out_png, width=256, height=512, blur=0, tint=None):
    """The objects' colour (no lighting) seen from the front (Blender -Y), cropped to their bounds, with alpha."""
    scene = bpy.context.scene
    saved = {k: getattr(scene.render, k) for k in ("resolution_x", "resolution_y", "film_transparent", "filepath")}
    saved_view = scene.view_settings.view_transform
    saved_cam = scene.camera
    hidden = {o: o.hide_render for o in bpy.data.objects}

    albedo = bpy.data.materials.new("Albedo_bake")
    try:
        albedo.use_nodes = True
    except Exception:
        pass
    nt = albedo.node_tree
    nt.nodes.clear()
    vc = nt.nodes.new("ShaderNodeVertexColor")
    em = nt.nodes.new("ShaderNodeEmission")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(vc.outputs["Color"], em.inputs["Color"])
    nt.links.new(em.outputs[0], out.inputs["Surface"])

    swaps = []
    for o in bpy.data.objects:
        o.hide_render = o not in objects
    for o in objects:
        for slot in o.material_slots:
            swaps.append((slot, slot.material))
            slot.material = albedo

    lo, hi = common.world_bounds(objects)
    size = hi - lo
    # The texture takes the bounds' exact aspect, so UV 0..1 spans the bounds (the long side gets the given size)
    if size.z >= size.x:
        width = max(4, int(round(height * size.x / size.z / 4)) * 4)
    else:
        height = max(4, int(round(width * size.z / size.x / 4)) * 4)
    cam = bpy.data.objects.new("BakeCam", bpy.data.cameras.new("BakeCam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = max(size.x, size.z)
    cam.location = Vector(((lo.x + hi.x) / 2, lo.y - 50.0, (lo.z + hi.z) / 2))
    cam.rotation_euler = (math.radians(90), 0, 0)
    cam.data.clip_end = 200
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)

    # Restore the scene
    for slot, mat in swaps:
        slot.material = mat
    for o, h in hidden.items():
        o.hide_render = h
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.materials.remove(albedo)
    for k, v in saved.items():
        setattr(scene.render, k, v)
    scene.view_settings.view_transform = saved_view
    scene.camera = saved_cam

    if blur or tint:
        soften(out_png, blur, tint)
    return out_png, (lo, hi)


def soften(png, blur, tint):
    """A box blur (the 'borrão') and an optional colour tint, on a straight-alpha PNG."""
    import preview
    img = preview.load_rgba(png)
    if blur:
        k = 2 * blur + 1
        pad = np.pad(img, ((blur, blur), (blur, blur), (0, 0)), mode="edge")
        acc = np.zeros_like(img)
        for dy in range(k):
            for dx in range(k):
                acc += pad[dy:dy + img.shape[0], dx:dx + img.shape[1]]
        img = acc / (k * k)
    if tint:
        img[..., :3] = img[..., :3] * (1 - tint[3]) + np.array(tint[:3], dtype=np.float32) * tint[3]
    h, w, _ = img.shape
    out = bpy.data.images.new("Softened", w, h, alpha=True)
    out.pixels.foreach_set(img[::-1].astype(np.float32).ravel())
    out.filepath_raw = png
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)


def _quad_mesh(name, quads, col):
    """quads: lists of 4 (position, uv, normal) from bottom-left, counter-clockwise."""
    verts, faces, uvs, normals = [], [], [], []
    for q in quads:
        base = len(verts)
        for pos, uv, n in q:
            verts.append(pos)
            uvs.append(uv)
            normals.append(Vector(n).normalized())
        faces.append((base, base + 1, base + 2, base + 3))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(v) for v in verts], [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        uv_layer.data[loop.index].uv = uvs[loop.vertex_index]
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.normals_split_custom_set_from_vertices(normals)
    return common.mesh_object(name, mesh, col)


def impostor_mesh(name, lo, hi, col):
    """
    Two crossed trapezoids (4 triangles) spanning the baked bounds, narrowing toward the top (the tree's cone, less
    overdraw). UVs follow the positions, so the texture lines up with the bounds it was baked from.
    """
    cx, w = (lo.x + hi.x) / 2, (hi.x - lo.x) / 2
    z0, z1 = lo.z, hi.z
    height = z1 - z0
    quads = []
    for axis in ("x", "y"):
        def at(offset, z):
            return Vector((cx + offset, 0, z)) if axis == "x" else Vector((cx, offset, z))
        side = Vector((1, 0, 0)) if axis == "x" else Vector((0, 1, 0))
        quads.append([
            (at(-w, z0), (0, 0), -side * 0.7 + Vector((0, 0, 0.4))),
            (at(w, z0), (1, 0), side * 0.7 + Vector((0, 0, 0.4))),
            (at(0.2 * w, z1), (0.6, 1), side * 0.3 + Vector((0, 0, 1.0))),
            (at(-0.2 * w, z1), (0.4, 1), -side * 0.3 + Vector((0, 0, 1.0))),
        ])
    return _quad_mesh(name, quads, col)


def card_mesh(name, width, height, col):
    """One upright quad facing -Y (the camera side of a backdrop), from z = 0."""
    w = width / 2
    quad = [
        (Vector((-w, 0, 0)), (0, 0), (0, -0.5, 0.8)),
        (Vector((w, 0, 0)), (1, 0), (0, -0.5, 0.8)),
        (Vector((w, 0, height)), (1, 1), (0, -0.3, 1.0)),
        (Vector((-w, 0, height)), (0, 1), (0, -0.3, 1.0)),
    ]
    return _quad_mesh(name, [quad], col)
