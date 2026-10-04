"""
Blender vs Unity, side by side (the last step of the review protocol: the asset in the game's light).

Top row: the Blender preview views of an asset (the tiles the last preview rendered: front, side, back, 3/4, close-up).
Bottom row: the Unity LookDev capture of the same views (<Asset>_views.png from CampanhaRio > LookDev > Capture).

    art compare Test Pebble <path to Pebble_views.png> [<out.png>]
"""
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import common  # noqa: E402
import preview  # noqa: E402

LABEL = 56


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    family, asset, unity_strip = args[0], args[1], os.path.abspath(args[2])
    out = os.path.abspath(args[3]) if len(args) > 3 else os.path.join(common.asset_dir(family, asset), f"{asset}_blender_vs_unity.png")
    rig = common.load_rig()
    tmp = os.path.join(common.TMP_DIR, "preview_" + asset)
    names = [v["name"] for v in rig["views"]] + ["closeup"]
    pitches = [rig["camera"]["pitchDeg"]] * len(rig["views"]) + [rig["closeup"]["pitchDeg"]]
    t, gap = rig["tileSize"], 8

    unity = preview.load_rgba(unity_strip)[..., :3]
    width = unity.shape[1]
    blender = np.ones((t, width, 3), dtype=np.float32) * np.array([236, 231, 221], dtype=np.float32) / 255.0
    for i, (name, pitch) in enumerate(zip(names, pitches)):
        rgba = preview.load_rgba(os.path.join(tmp, name + ".exr"))
        blender[:, i * (t + gap):i * (t + gap) + t] = preview.over_sky(rgba, rig, pitch)

    band = np.ones((LABEL, width, 3), dtype=np.float32) * np.array(common.hex_srgb("#2B2F36"), dtype=np.float32)
    sheet = np.concatenate([band, blender, band, unity[:, :width]], axis=0)
    H = sheet.shape[0]
    overlay = preview.render_text_overlay(width, H, [(16, 40, f"{asset}  -  BLENDER (previa)", 30), (16, LABEL + t + 40, f"{asset}  -  UNITY (LookDev, jogo)", 30)], tmp)
    a = overlay[..., 3:4]
    preview.save_png(sheet * (1 - a) + overlay[..., :3] * a, out)
    print(f"[compare] wrote {out}")


if __name__ == "__main__":
    main()
