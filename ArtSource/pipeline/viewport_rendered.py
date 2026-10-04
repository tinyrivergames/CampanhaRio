"""Opened with a .blend in the GUI: every 3D view switches to Rendered shading and looks through the scene camera."""
import bpy


def setup():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            for space in area.spaces:
                if space.type == "VIEW_3D":
                    space.shading.type = "RENDERED"
                    space.overlay.show_floor = False
                    space.region_3d.view_perspective = "CAMERA"
    return None  # run once


bpy.app.timers.register(setup, first_interval=1.0)
