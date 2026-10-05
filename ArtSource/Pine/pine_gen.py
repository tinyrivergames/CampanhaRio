"""
Family generator: Pine (Phase 1 style anchor). Option A of the brief: SOLID "skirts" (no alpha cards).

A sturdy stylized pine: a low-poly trunk and N stacked skirts, each a closed soft cone with a scalloped, slightly
drooping rim (the branch tufts). Normals are blended toward a smooth crown ellipsoid so the light wraps the whole tree
softly ("inflated"), while each skirt still reads. ONE material for the whole tree (colour in the vertex colours:
darker inside and under each skirt, lighter on top; the trunk is bark), the vertex ALPHA is the wind weight.
LOD0 and LOD1 are generated (not decimated) from the same parameters.

    art build Pine\pine_gen.py --asset PinheiroA [--seed 4] [--height 9] [--radius 2] [--layers 5] [--lean 0.04]
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "pipeline"))
import common  # noqa: E402
import lookdev  # noqa: E402
import preview  # noqa: E402

FAMILY = "Pine"
BUDGET = {"LOD0": 1500, "LOD1": 500}  # raised from 700/250 at v002 (the developer asked for more detailed foliage)

# Skirt profile: (radius fraction, height fraction of the skirt) from the tip down to the hidden inner ring
PROFILE = {
    "LOD0": [(0.14, 1.0), (0.62, 0.55), (1.0, 0.0), (0.7, -0.12), (0.15, 0.05)],
    "LOD1": [(0.14, 1.0), (0.7, 0.45), (1.0, 0.0), (0.2, -0.05)],
}
SEGMENTS = {"LOD0": 10, "LOD1": 6}
TRUNK_SIDES = {"LOD0": 6, "LOD1": 4}
RIM_ROW = 2  # the profile index of the scalloped rim


def srgb(hexstr):
    return Vector(common.hex_srgb(hexstr))


def build_tree(name, lod, p, col):
    rnd = random.Random(p["seed"])
    bm = bmesh.new()
    colors = {}  # vert -> (r, g, b, a) sRGB + wind
    dark, mid, light, bark = srgb(common.palette("pine_dark")), srgb(common.palette("pine_mid")), srgb(common.palette("pine_light")), srgb(common.palette("bark"))
    n = p["layers"]
    segs, prof = SEGMENTS[lod], PROFILE[lod]

    def lean(y):
        return Vector((p["lean"] * y, 0.0, 0.0))

    # Trunk: a tapered prism from the ground into the crown
    sides = TRUNK_SIDES[lod]
    rings = [(0.0, 0.18), (p["height"] * 0.7, 0.07)] if lod == "LOD1" else [(0.0, 0.18), (1.8, 0.14), (p["height"] * 0.75, 0.06)]
    prev = None
    for y, r in rings:
        ring = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            v = bm.verts.new(Vector((math.cos(a) * r, math.sin(a) * r, y)) + lean(y))
            colors[v] = (*bark, 0.0)
            ring.append(v)
        if prev:
            for j in range(sides):
                bm.faces.new((prev[j], prev[(j + 1) % sides], ring[(j + 1) % sides], ring[j]))
        prev = ring

    # Skirts, bottom to top
    for i in range(n):
        t = i / max(1, n - 1)
        rim_y = 1.7 + (p["height"] - 2.4 - 1.7) * t ** 0.92
        R = p["radius"] * (1.0 - 0.62 * t) * rnd.uniform(0.94, 1.06)
        H = 2.2 + (0.35 if i == n - 1 else 0.0)
        phase = rnd.uniform(0, 2 * math.pi)
        lobes = rnd.choice((5, 6, 7))
        rows = []
        for k, (rf, hf) in enumerate(prof):
            ring = []
            for j in range(segs):
                a = 2 * math.pi * j / segs + phase * 0.1
                lobe = 0.5 * (1 + math.cos(lobes * a + phase))  # 0..1, the tufts
                weight = {RIM_ROW: 1.0, RIM_ROW - 1: 0.5, RIM_ROW + 1: 0.7}.get(k, 0.15)
                r = R * rf * (1.0 + p["scallop"] * (lobe - 0.5) * weight)
                y = rim_y + H * hf - (p["droop"] * R * (1 - lobe) if k == RIM_ROW else 0.0)
                v = bm.verts.new(Vector((math.cos(a) * r, math.sin(a) * r, y)) + lean(y))
                # Colour: dark inside / underneath, mid on the slope, light on the upper outer part, lighter toward the top
                upper = k <= RIM_ROW
                radial = min(1.0, r / max(R, 1e-3))
                c = dark.lerp(mid, radial) if not upper else mid.lerp(light, 0.25 + 0.5 * (1 - abs(hf - 0.5) * 2) * radial)
                c = c.lerp(light, 0.25 * t if upper else 0.0)
                c = c * rnd.uniform(0.97, 1.03)
                colors[v] = (min(c.x, 1), min(c.y, 1), min(c.z, 1), radial * (0.35 + 0.65 * t))
                ring.append(v)
            rows.append(ring)
        for k in range(len(rows) - 1):
            a_ring, b_ring = rows[k], rows[k + 1]
            for j in range(segs):
                bm.faces.new((a_ring[j], a_ring[(j + 1) % segs], b_ring[(j + 1) % segs], b_ring[j]))
        tip_y = rim_y + H * 1.08
        apex = bm.verts.new(Vector((0, 0, tip_y)) + lean(tip_y))
        colors[apex] = (*light.lerp(mid, 0.3), 1.0)
        for j in range(segs):
            bm.faces.new((apex, rows[0][(j + 1) % segs], rows[0][j]))
        inner_y = rim_y + H * prof[-1][1]
        core = bm.verts.new(Vector((0, 0, inner_y)) + lean(inner_y))
        colors[core] = (*dark, 0.0)
        for j in range(segs):
            bm.faces.new((core, rows[-1][j], rows[-1][(j + 1) % segs]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    vert_colors = [colors[v] for v in bm.verts]
    bm.to_mesh(mesh)
    bm.free()
    obj = common.mesh_object(name, mesh, col)
    for poly in mesh.polygons:
        poly.use_smooth = True

    # Soft normals: blend each vertex normal toward the crown ellipsoid (the "inflated" light), trunk keeps its own
    center = Vector((0, 0, p["height"] * 0.55))
    axes = Vector((p["radius"] * 1.1, p["radius"] * 1.1, p["height"] * 0.55))
    normals = []
    for v, c in zip(mesh.vertices, vert_colors):
        own = v.normal.copy()
        if c[3] == 0.0 and c[:3] == tuple(bark):
            normals.append(own)
            continue
        d = v.co - center - Vector((p["lean"] * v.co.z, 0, 0))
        ell = Vector((d.x / axes.x ** 2, d.y / axes.y ** 2, d.z / axes.z ** 2)).normalized()
        normals.append(own.lerp(ell, p["inflate"]).normalized())
    mesh.normals_split_custom_set_from_vertices(normals)

    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for loop in mesh.loops:
        attr.data[loop.index].color_srgb = vert_colors[loop.vertex_index]
    return obj


def branch_plan(p):
    """Where every branch goes (the same for every LOD, so LODs swap without popping)."""
    rnd = random.Random(p["seed"])
    plan = []
    top = p["height"] - 1.3
    for w in range(p["whorls"]):
        t = w / max(1, p["whorls"] - 1)
        h = 1.6 + (top - 1.6) * t ** 0.95
        k = max(3, round(7 - 3.5 * t))
        base_yaw = w * 2.4
        for j in range(k):
            L = (p["radius"] * (1.0 - 0.72 * t ** 0.9) + 0.35) * rnd.uniform(0.85, 1.12)
            plan.append(dict(
                h=h + rnd.uniform(-0.12, 0.12), yaw=base_yaw + j * 2 * math.pi / k + rnd.uniform(-0.25, 0.25), L=L,
                W=L * 0.3 + 0.1, droop=(0.34 - 0.2 * t) * rnd.uniform(0.8, 1.2), lift=0.22, t=t, shade=rnd.uniform(0.94, 1.06)))
    return plan


def build_tree_tufts(name, lod, p, col):
    """
    The pine as individual branch TUFTS: each branch is a long, slightly drooping leaf clump with a raised crest and a
    serrated (notched) outline, the needle clumps. Branches sit in whorls around a visible trunk, shorter toward the
    top; a dark inner core hides the gaps and a small leader caps the tip.
    """
    bm = bmesh.new()
    colors = {}
    dark, mid, light, bark = srgb(common.palette("pine_dark")), srgb(common.palette("pine_mid")), srgb(common.palette("pine_light")), srgb(common.palette("bark"))
    samples = 5 if lod == "LOD0" else 3
    serrate = lod == "LOD0"
    up = Vector((0, 0, 1))

    def lean(y):
        return Vector((p["lean"] * y, 0.0, 0.0))

    def ring(y, r, sides, color, alpha):
        out = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            v = bm.verts.new(Vector((math.cos(a) * r, math.sin(a) * r, y)) + lean(y))
            colors[v] = (*color, alpha)
            out.append(v)
        return out

    def bridge(a, b):
        n = len(a)
        for j in range(n):
            bm.faces.new((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j]))

    # Trunk, a bit sturdier and more visible than v001
    sides = 6 if lod == "LOD0" else 4
    t0, t1, t2 = ring(0.0, 0.22, sides, bark, 0.0), ring(2.0, 0.16, sides, bark, 0.0), ring(p["height"] - 1.0, 0.05, sides, bark, 0.0)
    bridge(t0, t1)
    bridge(t1, t2)

    # The dark core: a slim cone inside the branches, so no sky shows through the middle
    core_sides = 7 if lod == "LOD0" else 5
    c0 = ring(1.5, p["radius"] * 0.45, core_sides, dark * 0.8, 0.0)
    c1 = ring(p["height"] * 0.6, p["radius"] * 0.3, core_sides, dark * 0.85, 0.2)
    bridge(c0, c1)
    cap = bm.verts.new(Vector((0, 0, p["height"] - 0.9)) + lean(p["height"] - 0.9))
    colors[cap] = (*dark, 0.4)
    for j in range(core_sides):
        bm.faces.new((cap, c1[j], c1[(j + 1) % core_sides]))

    # The branches
    for b in branch_plan(p):
        d = Vector((math.cos(b["yaw"]), math.sin(b["yaw"]), 0.0))
        side = Vector((-d.y, d.x, 0.0))
        origin = Vector((0, 0, b["h"])) + lean(b["h"]) + d * 0.08
        rows = []
        for i in range(samples - 1):
            s = i / (samples - 1)
            c = origin + d * b["L"] * s + up * b["L"] * (b["lift"] * s - b["droop"] * s * s)
            w = b["W"] * math.sin(math.pi * (0.18 + 0.82 * s)) ** 0.7
            if serrate and 0 < i:
                w *= 1.28 if i % 2 == 1 else 0.82  # the notches between needle clumps
            h = b["W"] * 0.32 * (1.0 - 0.6 * s)
            pts = {"l": c - side * w - up * h * 0.25, "t": c + up * h, "r": c + side * w - up * h * 0.25, "b": c - up * h * 0.45}
            row = {}
            for key, pt in pts.items():
                v = bm.verts.new(pt)
                crest = {"t": 1.0, "l": 0.45, "r": 0.45, "b": 0.0}[key]
                col_ = dark.lerp(mid, 0.35 + 0.65 * s) if key != "b" else dark * 0.9
                col_ = col_.lerp(light, crest * (0.25 + 0.55 * s) * (0.7 + 0.3 * b["t"]))
                col_ = col_ * b["shade"]
                colors[v] = (min(col_.x, 1), min(col_.y, 1), min(col_.z, 1), s * (0.4 + 0.6 * b["t"]))
                row[key] = v
            rows.append(row)
        tip_pos = origin + d * b["L"] + up * b["L"] * (b["lift"] - b["droop"])
        tip = bm.verts.new(tip_pos)
        c_tip = mid.lerp(light, 0.55) * b["shade"]
        colors[tip] = (min(c_tip.x, 1), min(c_tip.y, 1), min(c_tip.z, 1), 0.4 + 0.6 * b["t"])
        order = ("l", "t", "r", "b")
        for i in range(len(rows) - 1):
            for q in range(4):
                a_k, b_k = order[q], order[(q + 1) % 4]
                bm.faces.new((rows[i][a_k], rows[i][b_k], rows[i + 1][b_k], rows[i + 1][a_k]))
        for q in range(4):
            bm.faces.new((rows[-1][order[q]], rows[-1][order[(q + 1) % 4]], tip))

    # The leader: a small upward tuft at the top
    lead_sides = 6 if lod == "LOD0" else 4
    l0 = ring(p["height"] - 1.5, 0.32, lead_sides, mid, 0.6)
    lt = bm.verts.new(Vector((0, 0, p["height"])) + lean(p["height"]))
    colors[lt] = (*light, 1.0)
    for j in range(lead_sides):
        bm.faces.new((lt, l0[j], l0[(j + 1) % lead_sides]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    vert_colors = [colors[v] for v in bm.verts]
    bm.to_mesh(mesh)
    bm.free()
    obj = common.mesh_object(name, mesh, col)
    for poly in mesh.polygons:
        poly.use_smooth = True
    center = Vector((0, 0, p["height"] * 0.5))
    axes = Vector((p["radius"] * 1.2, p["radius"] * 1.2, p["height"] * 0.55))
    normals = []
    for v, c in zip(mesh.vertices, vert_colors):
        own = v.normal.copy()
        if c[:3] == tuple(bark):
            normals.append(own)
            continue
        dv = v.co - center - Vector((p["lean"] * v.co.z, 0, 0))
        ell = Vector((dv.x / axes.x ** 2, dv.y / axes.y ** 2, dv.z / axes.z ** 2)).normalized()
        normals.append(own.lerp(ell, p["inflate"]).normalized())
    mesh.normals_split_custom_set_from_vertices(normals)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for loop in mesh.loops:
        attr.data[loop.index].color_srgb = vert_colors[loop.vertex_index]
    return obj


def build_tree_blades(name, lod, p, col):
    """
    The pine as dense TIERS OF BLADES (the developer's spruce/fir reference): each tier is a fan of thin, pointed,
    folded blades radiating from the trunk and drooping toward the tips, plus a darker under-layer offset by half a
    step. Blades vary in length, angle and shade, so the light breaks into facets and the outline is jagged. A visible
    trunk with a flared base, a dark core and a small crown of upward blades at the tip.
    """
    rnd = random.Random(p["seed"])
    bm = bmesh.new()
    colors = {}
    dark, mid, light, bark = srgb(common.palette("pine_dark")), srgb(common.palette("pine_mid")), srgb(common.palette("pine_light")), srgb(common.palette("bark"))
    up = Vector((0, 0, 1))
    detail = lod == "LOD0"

    def lean(y):
        return Vector((p["lean"] * y, 0.0, 0.0))

    def vert(pos, color, alpha):
        v = bm.verts.new(pos)
        colors[v] = (min(color.x, 1), min(color.y, 1), min(color.z, 1), alpha)
        return v

    # Trunk with a flared base (alternating root spurs)
    sides = 6 if detail else 4
    rings = []
    for y, r, flare in ((0.0, 0.34, 0.35), (0.35, 0.2, 0.0), (2.0, 0.16, 0.0), (p["height"] - 1.2, 0.05, 0.0)):
        ring = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            rr = r * (1.0 + (flare if j % 2 == 0 else -flare * 0.4))
            ring.append(vert(Vector((math.cos(a) * rr, math.sin(a) * rr, y)) + lean(y), bark * (0.85 if y == 0 else 1.0), 0.0))
        rings.append(ring)
    for a_r, b_r in zip(rings, rings[1:]):
        for j in range(sides):
            bm.faces.new((a_r[j], a_r[(j + 1) % sides], b_r[(j + 1) % sides], b_r[j]))

    # The core: hides the gaps in the middle so no sky shows through
    if p["core_soft"]:
        core_rnd = random.Random(p["seed"] + 99)  # its own draws: the approved blades keep their layout
        # v007: a slim STAR-shaped core in a few rings (it reads as inner needles, not as a solid cone), in the blades'
        # greens (darker low, lighter high) instead of near-black, so it blends with the foliage around it
        core_sides = (12 if detail else 6)
        rings = max(2, p["core_rings"]) if detail else 2
        top_y = p["height"] - 1.0
        heights = [1.9 + (top_y - 1.9) * k / rings for k in range(rings)] + [top_y]
        prev = None
        for k, y in enumerate(heights[:-1]):
            ring_ = []
            t = k / (len(heights) - 1)
            for j in range(core_sides):
                a = 2 * math.pi * (j + 0.5 * k) / core_sides
                point = j % 2 == 0
                step = 1.0 if (k % 2 == 0 or rings < 3) else p["core_step"]  # v008: stepped like the tiers around it
                r = p["radius"] * p["core_r"] * (1.0 - 0.7 * t) * step * (1.0 if point else 0.5)
                # points lighter, valleys darker (like the blades' folds), each with its own slight tone (like each blade)
                shade = dark.lerp(mid, p["core_light"] + 0.25 * t + (0.12 if point else -0.2)) * core_rnd.uniform(0.9, 1.06)
                hue = core_rnd.uniform(-1, 1) * p["hue_var"]
                tone = Vector((1, 1, 1)).lerp(Vector((1.07, 1.05, 0.82)) if hue > 0 else Vector((0.88, 0.98, 1.1)), abs(hue) * 0.7)
                shade = Vector((shade.x * tone.x, shade.y * tone.y, shade.z * tone.z))
                ring_.append(vert(Vector((math.cos(a) * r, math.sin(a) * r, y)) + lean(y), shade, 0.1 * k))
            if prev:
                for j in range(core_sides):
                    bm.faces.new((prev[j], prev[(j + 1) % core_sides], ring_[(j + 1) % core_sides], ring_[j]))
            prev = ring_
        apex = vert(Vector((0, 0, heights[-1])) + lean(heights[-1]), dark.lerp(mid, p["core_light"] + 0.35), 0.3)
        for j in range(core_sides):
            bm.faces.new((apex, prev[j], prev[(j + 1) % core_sides]))
    else:
        core_sides = 6 if detail else 4
        core = [vert(Vector((math.cos(2 * math.pi * j / core_sides) * p["radius"] * 0.4, math.sin(2 * math.pi * j / core_sides) * p["radius"] * 0.4, 1.9)) + lean(1.9), dark * 0.7, 0.0)
                for j in range(core_sides)]
        apex = vert(Vector((0, 0, p["height"] - 1.0)) + lean(p["height"] - 1.0), dark * 0.8, 0.3)
        for j in range(core_sides):
            bm.faces.new((apex, core[j], core[(j + 1) % core_sides]))

    warm, cool = Vector((1.07, 1.05, 0.82)), Vector((0.88, 0.98, 1.1))

    def mul(a, b):
        return Vector((a.x * b.x, a.y * b.y, a.z * b.z))

    def blade(base, yaw, L, w, droop, shade, t, under, hue=0.0):
        d = Vector((math.cos(yaw), math.sin(yaw), 0.0))
        side = Vector((-d.y, d.x, 0.0))
        a0, a1 = droop, droop + p["curl"]
        mid_pt = base + (d * math.cos(a0) - up * math.sin(a0)) * (L * 0.48)
        tip = mid_pt + (d * math.cos(a1) - up * math.sin(a1)) * (L * 0.52)
        top = light if not under else mid
        c_base, c_mid, c_tip = dark.lerp(mid, 0.25) * shade, mid.lerp(top, p["top_light"] + 0.2 * t) * shade, top.lerp(light, 0.3) * shade
        if under:
            c_base, c_mid, c_tip = dark * 0.85 * shade, dark.lerp(mid, 0.55) * shade, mid * shade
        # Each blade its own slight tone (yellower or bluer), a cooler inside and warmer tips: colour beyond the light
        tone = Vector((1, 1, 1)).lerp(warm if hue > 0 else cool, abs(hue) * p["hue_var"])
        c_base = mul(mul(c_base, tone), Vector((0.94, 0.98, 1.06)))
        c_mid = mul(c_mid, tone)
        c_tip = mul(mul(c_tip, tone), Vector((1.05, 1.03, 0.92)))
        wind = 0.4 + 0.6 * t
        B = vert(base, c_base, 0.0)
        T = vert(tip, c_tip, wind)
        if detail:
            C = vert(mid_pt + up * w * 0.35, c_mid, wind * 0.5)  # the fold along the middle
            Lm = vert(mid_pt - side * w, c_mid * 0.92, wind * 0.5)
            Rm = vert(mid_pt + side * w, c_mid * 0.92, wind * 0.5)
            if p["tip_round"] > 0.0:
                # A short blunt tip (two points close together, pulled back a little): softer, still a blade
                back = (tip - mid_pt) * 0.06
                half = side * w * 0.5 * p["tip_round"]
                bm.verts.remove(T)
                TL, TR = vert(tip - back - half, c_tip, wind), vert(tip - back + half, c_tip, wind)
                for f in ((B, Lm, C), (B, C, Rm), (Lm, TL, C), (C, TL, TR), (C, TR, Rm)):
                    bm.faces.new(f)
            else:
                for f in ((B, Lm, C), (B, C, Rm), (Lm, T, C), (C, T, Rm)):
                    bm.faces.new(f)
        else:
            Lm = vert(mid_pt - side * w, c_mid, wind * 0.5)
            Rm = vert(mid_pt + side * w, c_mid, wind * 0.5)
            bm.faces.new((B, Lm, T))
            bm.faces.new((B, T, Rm))

    # The tiers, bottom to top
    tiers = p["tiers"]
    first, last = 1.9, p["height"] - 1.1
    for i in range(tiers):
        t = i / max(1, tiers - 1)
        h = first + (last - first) * t
        R = p["radius"] * (1.0 - 0.88 * t) + 0.25
        n = max(6, round((18 - 10 * t) * p["density"] * (1.0 if detail else 0.45)))
        droop = math.radians(p["droop_deg"] * (1.0 - 0.45 * t))
        offset = rnd.uniform(0, 2 * math.pi)
        for layer in ((False, 1.0, 0.0), (True, 0.8, 0.5)) if detail or t < 0.5 else ((False, 1.0, 0.0),):
            under, scale, half = layer
            for j in range(n):
                yaw = offset + (j + half) * 2 * math.pi / n + rnd.uniform(-0.12, 0.12)
                L = R * scale * rnd.uniform(0.82, 1.12)
                base = Vector((0, 0, h - (0.14 if under else 0.0) + rnd.uniform(-p["h_jitter"], p["h_jitter"]))) + lean(h) + Vector((math.cos(yaw), math.sin(yaw), 0)) * 0.08
                w = L * p["blade_width"] * rnd.uniform(0.85, 1.15)
                blade(base, yaw, L, w, droop * rnd.uniform(0.8, 1.2), rnd.uniform(0.9, 1.08), t, under, rnd.uniform(-1.0, 1.0))

    # The crown: a spike and a few small blades pointing up
    tip_y = p["height"]
    spike_base = Vector((0, 0, last - 0.1)) + lean(last)
    for j in range(5 if detail else 3):
        yaw = j * 2 * math.pi / (5 if detail else 3)
        blade(spike_base, yaw, 0.6, 0.07, math.radians(-55), 1.0, 1.0, False)
    s0 = vert(spike_base + Vector((0.06, 0, 0)), mid, 0.7)
    s1 = vert(spike_base + Vector((-0.03, 0.05, 0)), mid, 0.7)
    s2 = vert(spike_base + Vector((-0.03, -0.05, 0)), mid, 0.7)
    st = vert(Vector((0, 0, tip_y)) + lean(tip_y), light, 1.0)
    for f in ((s0, s1, st), (s1, s2, st), (s2, s0, st)):
        bm.faces.new(f)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    vert_colors = [colors[v] for v in bm.verts]
    bm.to_mesh(mesh)
    bm.free()
    obj = common.mesh_object(name, mesh, col)
    for poly in mesh.polygons:
        poly.use_smooth = True
    center = Vector((0, 0, p["height"] * 0.48))
    axes = Vector((p["radius"] * 1.2, p["radius"] * 1.2, p["height"] * 0.58))
    normals = []
    for v, c in zip(mesh.vertices, vert_colors):
        own = v.normal.copy()
        if own.z < 0:  # one-sided blades seen from below: light them like their top
            own = -own
        dv = v.co - center - Vector((p["lean"] * v.co.z, 0, 0))
        ell = Vector((dv.x / axes.x ** 2, dv.y / axes.y ** 2, dv.z / axes.z ** 2)).normalized()
        normals.append(own.lerp(ell, p["inflate"]).normalized())
    mesh.normals_split_custom_set_from_vertices(normals)
    attr = mesh.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    mesh.color_attributes.active_color = attr
    for loop in mesh.loops:
        attr.data[loop.index].color_srgb = vert_colors[loop.vertex_index]
    return obj


def build(asset, p):
    rig = common.load_rig()
    common.reset_scene()
    col = common.collection(asset)
    make = {"tufts": build_tree_tufts, "blades": build_tree_blades}.get(p["style"], build_tree)
    lod0 = make(asset + "_LOD0", "LOD0", p, col)
    lod1 = make(asset + "_LOD1", "LOD1", p, col)
    mat = common.soft_toon_material("M_" + asset, rig, preset="Foliage", _BaseColor="#FFFFFF", _UseVertexColor=1.0,
                                    _Translucency=0.3, _RimStrength=0.14)
    for o in (lod0, lod1):
        o.data.materials.append(mat)
    lod1.hide_set(True)
    lookdev.build(rig)
    return {"LOD0": lod0, "LOD1": lod1}


def write_notes(asset, version, parts, args, paths, changes):
    notes = os.path.join(paths["dir"], "NOTES.md")
    if not os.path.exists(notes):
        with open(notes, "w", encoding="utf-8", newline="\n") as f:
            f.write(f"# {FAMILY} / {asset}\n\n"
                    "## Briefing (aprovado em 2026-10-05, opção A)\n"
                    "- **O que é:** o pinheiro-mestre da família (âncora de estilo da Fase 1).\n"
                    "- **Tamanho:** ~9 m de altura, copa ~4 m, tronco 0,35 m na base, 1,5 m visível, levemente inclinado.\n"
                    "- **Forma:** 5 \"saias\" arredondadas e infladas, bordas onduladas e macias, ponta arredondada, copa cheia.\n"
                    f"- **Triângulos:** LOD0 <= {BUDGET['LOD0']}, LOD1 <= {BUDGET['LOD1']}, LOD2 impostor (2 tris), fundo: cartão-borrão (2 tris).\n"
                    "- **Cores:** pine_dark (base, interior) -> pine_light (topo), tronco bark. Uma cor por vértice, um material só.\n"
                    "- **Técnica:** opção A, saias sólidas sem transparência; normais \"infladas\" (puxadas para um elipsoide da copa).\n"
                    "- **Referências:** Docs/Reference/estetica_01_floresta e estetica_03_lago.\n"
                    "- **Gerador:** `ArtSource/Pine/pine_gen.py`. O impostor e o cartão-borrão vêm depois que a forma for aprovada.\n\n"
                    "## Versões\n")
    tris = ", ".join(f"{k} {common.triangle_count(o)}" for k, o in parts.items())
    with open(notes, "a", encoding="utf-8", newline="\n") as f:
        f.write(f"\n### v{version:03d}\n"
                f"- Gerado com: `{' '.join(f'--{k} {v}' for k, v in args.items())}`\n"
                f"- Triângulos: {tris}\n"
                f"- Prévia: `{os.path.basename(paths['preview'])}`\n"
                "- **Feedback do desenvolvedor (literal):** _(aguardando)_\n"
                f"- **O que mudou:** {'primeira versão.' if version == 1 else changes}\n")


def main():
    args = common.script_args()
    asset = args.get("asset", "PinheiroA")
    p = {
        "style": args.get("style", "blades"),
        "tiers": int(args.get("tiers", 12)),
        "density": float(args.get("density", 1.0)),
        "top_light": float(args.get("top_light", 0.55)),
        "hue_var": float(args.get("hue_var", 0.0)),
        "tip_round": float(args.get("tip_round", 0.0)),
        "core_soft": int(args.get("core_soft", 0)),
        "core_r": float(args.get("core_r", 0.3)),
        "core_light": float(args.get("core_light", 0.35)),
        "core_rings": int(args.get("core_rings", 2)),
        "core_step": float(args.get("core_step", 1.0)),
        "h_jitter": float(args.get("h_jitter", 0.0)),
        "droop_deg": float(args.get("blade_droop", 32)),
        "curl": math.radians(float(args.get("curl", 22))),
        "blade_width": float(args.get("blade_width", 0.16)),
        "whorls": int(args.get("whorls", 9)),
        "seed": int(args.get("seed", 4)),
        "height": float(args.get("height", 9.0)),
        "radius": float(args.get("radius", 2.0)),
        "layers": int(args.get("layers", 5)),
        "lean": float(args.get("lean", 0.04)),
        "scallop": float(args.get("scallop", 0.28)),
        "droop": float(args.get("droop", 0.12)),
        "inflate": float(args.get("inflate", 0.4)),
    }
    parts = build(asset, p)
    if "draft" in args:  # a test render only: no version, no notes (ArtSource/_tmp/draft_<asset>.png)
        out = os.path.join(common.TMP_DIR, f"draft_{asset}_{args['draft']}.png")
        preview.render_sheet(out, "rascunho " + str(args["draft"]))
        print(f"[pine] draft {out}")
        return
    version, paths = common.save_new_version(FAMILY, asset, int(args["version"]) if "version" in args else None)
    preview.render_sheet(paths["preview"], f"v{version:03d}")
    write_notes(asset, version, parts, p, paths, args.get("changes", "_(descrever)_"))
    print(f"[pine] {paths['blend']}")


if __name__ == "__main__":
    main()
