"""
Shared helpers for every CampanhaRio asset script (run inside Blender: blender.exe --background --python <script> -- <args>).

  - Paths, the lookdev rig (Assets/_Project/Art/LookDev/lookdev_rig.json, shared with Unity) and the palette.
  - Scene reset and units: 1 Blender unit = 1 m.
  - Unity <-> Blender axes: the FBX export maps Blender -Y (front) to Unity +Z (forward) and Blender +Z to Unity +Y.
  - The SoftToon preview material: the SAME formula as the Unity shader (Art/Shaders/SoftToon), output as emission,
    so the preview sheet and the game agree. Only the received sun shadow comes from EEVEE.
  - Vertex colours, gradients, naming rules, LOD helpers (decimate, merge, normals), versioning.
"""
import copy
import json
import math
import os
import re
import sys

import bpy
import bmesh
from mathutils import Vector

# ---------------------------------------------------------------- paths

PIPELINE_DIR = os.path.dirname(os.path.abspath(__file__))
ART_SOURCE = os.path.dirname(PIPELINE_DIR)
REPO = os.path.dirname(ART_SOURCE)
RIG_PATH = os.path.join(REPO, "Assets", "_Project", "Art", "LookDev", "lookdev_rig.json")
MODELS_DIR = os.path.join(REPO, "Assets", "_Project", "Art", "Models")
TMP_DIR = os.path.join(ART_SOURCE, "_tmp")
BLENDER_EXE = bpy.app.binary_path

ASSET_NAME = re.compile(r"^[A-Z][A-Za-z0-9]*$")
PART_NAME = re.compile(r"^(?P<asset>[A-Z][A-Za-z0-9]*)_(?P<part>LOD[0-2]|COL)$")


def script_args():
    """The arguments after '--' on the Blender command line, as a dict (--key value / --flag)."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out, i = {}, 0
    while i < len(argv):
        key = argv[i].lstrip("-")
        if i + 1 < len(argv) and not argv[i + 1].startswith("--"):
            out[key] = argv[i + 1]
            i += 2
        else:
            out[key] = True
            i += 1
    return out


def load_rig():
    with open(RIG_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


# ---------------------------------------------------------------- colour

def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_srgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def lin(h_or_rgb):
    """sRGB hex ('#RRGGBB') or an sRGB triple -> linear RGBA (what Blender nodes want)."""
    rgb = hex_srgb(h_or_rgb) if isinstance(h_or_rgb, str) else h_or_rgb
    return tuple(srgb_to_linear(c) for c in rgb[:3]) + (1.0,)


def palette(name):
    """A palette colour by name (sRGB hex), from the shared rig."""
    return load_rig()["palette"][name]


# ---------------------------------------------------------------- axes (Unity space <-> Blender space)

def unity_to_blender(v):
    """A direction or position in Unity space (x right, y up, z forward) -> Blender (x right, -y forward, z up)."""
    return Vector((-v[0], -v[2], v[1]))


def unity_euler_forward(pitch_deg, yaw_deg):
    """Unity's Quaternion.Euler(pitch, yaw, 0) * Vector3.forward."""
    p, y = math.radians(pitch_deg), math.radians(yaw_deg)
    return (math.sin(y) * math.cos(p), -math.sin(p), math.cos(y) * math.cos(p))


def sun_direction_blender(rig):
    """Direction TOWARD the sun in Blender space (the Unity light's -forward, converted)."""
    f = unity_euler_forward(rig["sun"]["elevationDeg"], rig["sun"]["azimuthDeg"])
    return -unity_to_blender(f).normalized()


# ---------------------------------------------------------------- scene

def reset_scene():
    """An empty scene in meters (no default cube, camera or light), with every orphan data block purged."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    return scene


def collection(name, parent=None):
    col = bpy.data.collections.get(name)
    if not col:
        col = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(col)
    return col


def link_only(obj, col):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    col.objects.link(obj)


def mesh_object(name, mesh, col):
    obj = bpy.data.objects.new(name, mesh)
    col.objects.link(obj)
    return obj


def triangle_count(obj):
    mesh = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    obj.to_mesh_clear()
    return tris


def world_bounds(objects):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in objects:
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
    return lo, hi


# ---------------------------------------------------------------- the SoftToon preview material

SOFTTOON_DEFAULTS = {
    "_BaseColor": "#FFFFFF",
    "_Wrap": 0.5,             # half-Lambert style wrap (0 = Lambert, 1 = full wrap)
    "_RampCenter": 0.45,      # where the light turns to shade, on the wrapped N.L
    "_RampSoftness": 0.28,    # half width of the soft ramp (big = soft)
    "_ReceiveShadows": 0.85,  # how dark received shadows get (0 = none, 1 = full)
    "_RimStrength": 0.16,
    "_RimPower": 3.5,
    "_UseVertexColor": 0.0,
    "_GradientBottom": "#FFFFFF",  # albedo tint along the object's height (object space, meters)
    "_GradientTop": "#FFFFFF",
    "_GradientHeights": [0.0, 1.0],
    "_TopTint": "#FFFFFF",     # Rock/Cliff: moss/dust on surfaces facing up
    "_TopTintAmount": 0.0,
    "_TopTintSharpness": 0.35,
    "_Translucency": 0.0,      # Foliage: light through the leaves when backlit
}

PRESETS = {
    "Default": {},
    "Rock": {"_Wrap": 0.4, "_RampSoftness": 0.32, "_TopTintAmount": 0.35, "_RimStrength": 0.1},
    "Foliage": {"_Wrap": 0.7, "_RampSoftness": 0.35, "_Translucency": 0.45, "_RimStrength": 0.12},
    "Character": {"_Wrap": 0.55, "_RampSoftness": 0.22, "_RimStrength": 0.22},
}


def softtoon_params(preset="Default", **overrides):
    p = dict(SOFTTOON_DEFAULTS)
    p.update(PRESETS[preset])
    p.update(overrides)
    p["preset"] = preset
    return p


def soft_toon_material(name, rig=None, preset="Default", **overrides):
    """
    A material that renders the SoftToon formula (see Art/Shaders/SoftToon/SoftToon.shader; keep them in sync):
        wrapped = (N.L + wrap) / (1 + wrap)
        light   = smoothstep(center - soft, center + soft, wrapped) * lerp(1, shadow, receive)
        direct  = sun * intensity * light + shadowTint * tintStrength * (1 - light)
        ambient = lerp(equator, sky, saturate(N.up)) + (ground - equator) * saturate(-N.up), times strength
        color   = albedo * (direct + ambient) + rim * sun,   albedo = base * vertexColor * gradient * topTint
    The parameters are stored on the material (["cr_softtoon"]) so the exporter can hand them to Unity.
    """
    rig = rig or load_rig()
    params = softtoon_params(preset, **overrides)
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat["cr_softtoon"] = json.dumps(params)
    try:
        mat.use_nodes = True
    except Exception:
        pass
    nt = mat.node_tree
    nt.nodes.clear()
    g = _Graph(nt)

    sun_dir = sun_direction_blender(rig)
    sun_col = Vector(lin(rig["sun"]["color"])[:3]) * rig["sun"]["intensity"]
    amb = rig["ambient"]

    geo = g.node("ShaderNodeNewGeometry")
    normal = geo.outputs["Normal"]
    ndl = g.vmath("DOT_PRODUCT", normal, g.vec(sun_dir), out="Value")

    # Received sun shadow (EEVEE): white diffuse lit only by the sun, divided by N.L, faded in near the terminator
    diffuse = g.node("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (1, 1, 1, 1)
    to_rgb = g.node("ShaderNodeShaderToRGB")
    g.link(diffuse.outputs[0], to_rgb.inputs[0])
    lum = g.node("ShaderNodeRGBToBW")
    g.link(to_rgb.outputs["Color"], lum.inputs[0])
    shadow_raw = g.math("DIVIDE", lum.outputs[0], g.math("MAXIMUM", ndl, 0.05), clamp=True)
    near_lit = g.map_range(ndl, 0.0, 0.05)  # only the terminator sliver (self-shadow acne); a low sun still casts on flat ground
    shadow = g.mix_float(near_lit, 1.0, shadow_raw)
    received = g.mix_float(params["_ReceiveShadows"], 1.0, shadow)

    wrap = params["_Wrap"]
    wrapped = g.math("DIVIDE", g.math("ADD", ndl, wrap), 1.0 + wrap)
    c, s = params["_RampCenter"], params["_RampSoftness"]
    ramp = g.map_range(wrapped, c - s, c + s)
    light = g.math("MULTIPLY", ramp, received)
    unlit = g.math("SUBTRACT", 1.0, light)

    tint = Vector(lin(rig["shadowTint"])[:3]) * rig["shadowTintStrength"]
    direct = g.vadd(g.vscale(g.vec(sun_col), light), g.vscale(g.vec(tint), unlit))

    # Ambient: sky / equator / ground by the normal's up component
    up = g.sep(normal, "Z")
    t_up = g.math("MAXIMUM", up, 0.0)
    t_dn = g.math("MAXIMUM", g.math("MULTIPLY", up, -1.0), 0.0)
    sky, eq, gr = (Vector(lin(amb[k])[:3]) for k in ("sky", "equator", "ground"))
    ambient = g.vadd(g.vec(eq), g.vadd(g.vscale(g.vec(sky - eq), t_up), g.vscale(g.vec(gr - eq), t_dn)))
    ambient = g.vscale(ambient, amb["strength"])

    # Contact darkening, like Unity's soft SSAO (lookdev_rig.json "contactAO"): all of the ambient, part of the sun
    cao = rig.get("contactAO")
    if cao:
        ao_node = g.node("ShaderNodeAmbientOcclusion")
        ao_node.inputs["Distance"].default_value = cao["radius"]
        occlusion = g.math("MULTIPLY", g.math("SUBTRACT", 1.0, ao_node.outputs["AO"]), cao["intensity"] * 0.6)
        ao = g.math("SUBTRACT", 1.0, occlusion, clamp=True)
        ambient = g.vscale(ambient, ao)
        direct = g.vscale(direct, g.mix_float(cao["directStrength"], 1.0, ao))

    # Albedo: base * vertex colour * height gradient * top tint
    albedo = g.vec(Vector(lin(params["_BaseColor"])[:3]))
    tex = None
    if params.get("_BaseMap"):  # a painted/baked texture (impostors, background cards); its alpha is clipped
        tex = g.node("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(params["_BaseMap"], check_existing=True)
        albedo = g.vmul(albedo, tex.outputs["Color"])
    if params["_UseVertexColor"] > 0.5:
        vc = g.node("ShaderNodeVertexColor")
        albedo = g.vmul(albedo, vc.outputs["Color"])
    if params["_GradientBottom"].upper() != "#FFFFFF" or params["_GradientTop"].upper() != "#FFFFFF":
        coords = g.node("ShaderNodeTexCoord")
        h0, h1 = params["_GradientHeights"]
        t = g.map_range(g.sep(coords.outputs["Object"], "Z"), h0, h1, smooth=False)
        grad = g.vlerp(Vector(lin(params["_GradientBottom"])[:3]), Vector(lin(params["_GradientTop"])[:3]), t)
        albedo = g.vmul(albedo, grad)
    if params["_TopTintAmount"] > 0.0:
        k = params["_TopTintSharpness"]
        t = g.math("MULTIPLY", g.map_range(up, 1.0 - k * 2.0, 1.0), params["_TopTintAmount"])
        albedo = g.vmul(albedo, g.vlerp(Vector((1, 1, 1)), Vector(lin(params["_TopTint"])[:3]), t))

    color = g.vmul(albedo, g.vadd(direct, ambient))

    # Rim: a soft light edge, stronger on the lit side
    ndv = g.vmath("DOT_PRODUCT", normal, geo.outputs["Incoming"], out="Value")
    fres = g.math("POWER", g.math("SUBTRACT", 1.0, g.math("MAXIMUM", ndv, 0.0)), params["_RimPower"])
    rim = g.math("MULTIPLY", g.math("MULTIPLY", fres, params["_RimStrength"]), g.math("ADD", 0.3, g.math("MULTIPLY", light, 0.7)))
    color = g.vadd(color, g.vscale(g.vec(sun_col), rim))

    # Foliage: light through the leaves when the sun is behind them
    if params["_Translucency"] > 0.0:
        back = g.vmath("DOT_PRODUCT", geo.outputs["Incoming"], g.vec(-sun_dir), out="Value")
        trans = g.math("MULTIPLY", g.math("POWER", g.math("MAXIMUM", back, 0.0), 4.0), params["_Translucency"])
        color = g.vadd(color, g.vmul(albedo, g.vscale(g.vec(sun_col), trans)))

    emission = g.node("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 1.0
    g.link(color, emission.inputs["Color"])
    out = g.node("ShaderNodeOutputMaterial")
    if tex is not None and params.get("_AlphaClip", 1) > 0.5:
        # Alpha clip, like the Unity shader: transparent where the texture's alpha is under the cutoff
        keep = g.math("GREATER_THAN", tex.outputs["Alpha"], params.get("_Cutoff", 0.5))
        clear = g.node("ShaderNodeBsdfTransparent")
        mix = g.node("ShaderNodeMixShader")
        g.link(keep, mix.inputs[0])
        g.link(clear.outputs[0], mix.inputs[1])
        g.link(emission.outputs[0], mix.inputs[2])
        g.link(mix.outputs[0], out.inputs["Surface"])
    else:
        g.link(emission.outputs[0], out.inputs["Surface"])
    return mat


class _Graph:
    """Tiny helper to write node math as expressions."""

    def __init__(self, nt):
        self.nt = nt
        self.x = 0

    def node(self, kind):
        n = self.nt.nodes.new(kind)
        n.location = (self.x, 0)
        self.x += 30
        return n

    def link(self, a, b):
        self.nt.links.new(a, b)

    def _in(self, socket, value):
        if hasattr(value, "is_output"):
            self.link(value, socket)
        else:
            socket.default_value = value

    def math(self, op, a, b=0.0, clamp=False):
        n = self.node("ShaderNodeMath")
        n.operation = op
        n.use_clamp = clamp
        self._in(n.inputs[0], a)
        self._in(n.inputs[1], b)
        return n.outputs[0]

    def map_range(self, value, lo, hi, smooth=True):
        n = self.node("ShaderNodeMapRange")
        n.interpolation_type = "SMOOTHSTEP" if smooth else "LINEAR"
        n.clamp = True
        self._in(n.inputs["Value"], value)
        n.inputs["From Min"].default_value = lo
        n.inputs["From Max"].default_value = hi
        return n.outputs["Result"]

    def mix_float(self, factor, a, b):
        n = self.node("ShaderNodeMix")
        n.data_type = "FLOAT"
        self._in(n.inputs["Factor"], factor)
        self._in(n.inputs["A"], a)
        self._in(n.inputs["B"], b)
        return n.outputs["Result"]

    def vec(self, v):
        n = self.node("ShaderNodeCombineXYZ")
        n.inputs[0].default_value, n.inputs[1].default_value, n.inputs[2].default_value = v[0], v[1], v[2]
        return n.outputs[0]

    def vmath(self, op, a, b, out="Vector"):
        n = self.node("ShaderNodeVectorMath")
        n.operation = op
        self._in(n.inputs[0], a)
        self._in(n.inputs[1], b)
        return n.outputs[out]

    def vadd(self, a, b):
        return self.vmath("ADD", a, b)

    def vmul(self, a, b):
        return self.vmath("MULTIPLY", a, b)

    def vscale(self, v, s):
        n = self.node("ShaderNodeVectorMath")
        n.operation = "SCALE"
        self._in(n.inputs[0], v)
        self._in(n.inputs["Scale"], s)
        return n.outputs["Vector"]

    def vlerp(self, a, b, t):
        n = self.node("ShaderNodeMix")
        n.data_type = "VECTOR"
        self._in(n.inputs["Factor"], t)
        n.inputs[4].default_value = a  # A (vector)
        n.inputs[5].default_value = b  # B (vector)
        return n.outputs[1]

    def sep(self, v, axis):
        n = self.node("ShaderNodeSeparateXYZ")
        self._in(n.inputs[0], v)
        return n.outputs[axis]


# ---------------------------------------------------------------- vertex colours and gradients

def paint_vertex_colors(obj, fn, name="Col"):
    """Per-vertex colour from fn(world_position, normal) -> sRGB (r, g, b). Stored as a byte colour attribute."""
    mesh = obj.data
    attr = mesh.color_attributes.get(name) or mesh.color_attributes.new(name, "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    mw = obj.matrix_world
    for loop in mesh.loops:
        v = mesh.vertices[loop.vertex_index]
        r, g, b = fn(mw @ v.co, (mw.to_3x3() @ v.normal).normalized())
        attr.data[loop.index].color_srgb = (r, g, b, 1.0)


# ---------------------------------------------------------------- LODs

def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def merge_by_distance(obj, distance=0.0005):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=distance)
    bm.to_mesh(obj.data)
    bm.free()


def triangulate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()


def make_lod(source, name, ratio, col, normals_from=None):
    """A decimated copy (collapse, ratio of the source triangles), merged, with normals from a smooth proxy if given."""
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = name
    obj.data.name = name
    col.objects.link(obj)
    if ratio < 0.999:
        dec = obj.modifiers.new("Decimate", "DECIMATE")
        dec.decimate_type = "COLLAPSE"
        dec.ratio = ratio
        dec.use_collapse_triangulate = True
        apply_modifiers(obj)
    merge_by_distance(obj)
    smooth(obj, normals_from)
    return obj


def smooth(obj, normals_from=None):
    """Smooth shading; with a proxy, the normals are copied from it (the soft, rounded light of the style)."""
    for p in obj.data.polygons:
        p.use_smooth = True
    if normals_from is not None:
        dt = obj.modifiers.new("NormalsFromProxy", "DATA_TRANSFER")
        dt.object = normals_from
        dt.use_loop_data = True
        dt.data_types_loops = {"CUSTOM_NORMAL"}
        dt.loop_mapping = "POLYINTERP_NEAREST"
        apply_modifiers(obj)


def check_names(objects):
    """Naming rule: <Asset>_LOD0..2 and <Asset>_COL. Returns the problems found (empty = fine)."""
    problems = []
    for obj in objects:
        if not PART_NAME.match(obj.name):
            problems.append(f"'{obj.name}' should be <Asset>_LOD0/1/2 or <Asset>_COL")
    return problems


# ---------------------------------------------------------------- versioning

def asset_dir(family, asset):
    return os.path.join(ART_SOURCE, family, asset)


def version_paths(family, asset, version):
    d = asset_dir(family, asset)
    stem = f"{asset}_v{version:03d}"
    return {"dir": d, "blend": os.path.join(d, stem + ".blend"), "preview": os.path.join(d, stem + "_preview.png"), "stem": stem}


def latest_version(family, asset):
    d = asset_dir(family, asset)
    if not os.path.isdir(d):
        return 0
    found = [int(m.group(1)) for f in os.listdir(d) for m in [re.match(rf"^{asset}_v(\d{{3}})\.blend$", f)] if m]
    return max(found) if found else 0


def save_new_version(family, asset, version=None):
    """Saves the open scene as <Asset>_vNNN.blend. Never overwrites an existing version."""
    version = version or latest_version(family, asset) + 1
    paths = version_paths(family, asset, version)
    os.makedirs(paths["dir"], exist_ok=True)
    if os.path.exists(paths["blend"]):
        raise RuntimeError(f"{paths['blend']} already exists: versions are never overwritten (use the next number).")
    bpy.ops.wm.save_as_mainfile(filepath=paths["blend"], compress=True)
    return version, paths


# ---------------------------------------------------------------- times of day
# A copy of the DayCycle keys (Assets/_Project/Scripts/Rendering/DayCycle.cs DefaultKeys): keep them in sync
# until the keys move into lookdev_rig.json. "afternoon" is the rig itself.
TIMES = {
    "afternoon": None,  # the rig itself
    "golden": dict(elevation=17, azimuth=-52, color="#FFC985", intensity=1.1, sky="#A7B2D6", equator="#CBAE8E", ground="#6B5444",
                   strength=0.5, tint="#6A5E9E", tintStrength=0.42, top="#6E9BD0", horizon="#F3D3A4"),
    "sunset": dict(elevation=4, azimuth=-62, color="#FF965A", intensity=0.85, sky="#8E8DB8", equator="#C98F78", ground="#4F3E3E",
                   strength=0.48, tint="#6A4E8E", tintStrength=0.5, top="#4D6AA8", horizon="#F59A6B"),
    "dusk": dict(elevation=-3, azimuth=-68, color="#C77B8F", intensity=0.22, sky="#4F5788", equator="#6E5872", ground="#2E2A3A",
                 strength=0.5, tint="#3B3A6A", tintStrength=0.55, top="#26305E", horizon="#8E6684"),
}


def rig_at(time):
    rig = load_rig()
    k = TIMES[time]
    if not k:
        return rig
    rig = copy.deepcopy(rig)
    rig["sun"].update(elevationDeg=max(k["elevation"], 0.5), azimuthDeg=k["azimuth"], color=k["color"], intensity=k["intensity"])
    rig["ambient"].update(sky=k["sky"], equator=k["equator"], ground=k["ground"], strength=k["strength"])
    rig["shadowTint"], rig["shadowTintStrength"] = k["tint"], k["tintStrength"]
    rig["background"].update(top=k["top"], horizon=k["horizon"])
    return rig

