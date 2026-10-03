# Technical decisions

Short records: what was decided, why, and what it costs. Newest at the bottom of each section.

## Project and tools

- **Unity 6000.5.10f1, URP 17.5, the Universal 3D template.** Created with the CLI (`-createProject -cloneFromTemplate`).
  The template's sample content, the Mobile quality level and its renderer were removed; one "PC" quality level remains.
- **Settings:** Linear colour space, Force Text serialization, Visible Meta Files, new Input System only, 50 Hz fixed
  step (0.02 s, the kayak physics was tuned at it). The physics settings file is a copy of the old project's
  (same solver settings, so the simulation matches).
- **Packages (and why):** Netcode for GameObjects 2.13.3 + Transport 6.5.0 (the same versions as the old project: the
  kayak sync was proven on them); Services Multiplayer 2.3.3 (online rooms by join code: Relay + Lobby); Splines 2.9
  (RiverPath); Animation Rigging 1.4.1 (the paddler's IK); Timeline (the future van events); Test Framework; uGUI.
  Removed: Visual Scripting, Collab, Multiplayer Center, AI Navigation (unused).
- **Two assemblies:** `CampanhaRio` (runtime) and `CampanhaRio.Editor`. One root namespace, `CampanhaRio`, so renaming
  the game later is a find/replace.
- **Ported scripts keep their old `.meta` GUIDs**, so a prefab or scene object can be copied between the projects.

## Git and LFS (a lesson from the old project)

- `.gitattributes`: `* text=auto eol=lf` (identical bytes on every clone, whatever `core.autocrlf` says), YAML assets
  with Unity's smart merge, and **LFS for every binary**: `.blend`, `.fbx`, images, audio, fonts, and the Unity assets
  that stay binary even with Force Text (`*TerrainData*.asset`, `LightingData.asset`, baked `NavMesh*.asset`,
  `.cubemap`). Those lines come **after** the `*.asset` text rule (the last match wins). `ProjectSettings/NavMeshAreas.asset`
  is text and stays text (an early rule caught it; fixed).
- Checked: a fresh clone reproduces every file byte for byte, and a fake binary TerrainData file (CRLF + NUL bytes)
  survives a commit/clone round trip unchanged.

## The look

- **SoftToon is hand-written HLSL, not Shader Graph.** One readable formula (wrap + a wide smooth ramp + coloured
  shadows + a gradient ambient + rim), the same as the Blender preview material (`ArtSource/pipeline/common.py`), with
  explicit passes (ShadowCaster, DepthOnly, DepthNormals for SSAO later) and keywords (`_ALPHATEST_ON`, `_WIND`,
  `LOD_FADE_CROSSFADE`). It is SRP Batcher compatible and supports GPU instancing. Shader Graph would hide the formula and
  make the Blender match harder to keep. The presets (Default, Rock/Cliff, Foliage, Character) are values, not separate
  shaders.
- **Globals instead of Unity's ambient:** `_CR_ShadowTint`, `_CR_AmbientSky/Equator/Ground` (linear, strength applied),
  set by `DayCycle`. Unity's own ambient and fog are kept in step for other shaders (the water).
- **One light description:** `Art/LookDev/lookdev_rig.json` (sun, ambient, shadow tint, sky, grading, palette, preview
  views). Blender's preview rig and Unity's DayCycle afternoon key both come from it.
- **Post-processing:** Neutral tonemapping, gentle Color Adjustments (+0.1 exposure, +6 contrast, +10 saturation),
  White Balance +4 (warm), subtle Bloom (threshold 1.1, intensity 0.25), a light vignette. The numbers are in the rig
  JSON; the Blender sheet applies an approximation of them. **No SSAO yet:** it adds grey contact shading that fights
  the soft look on smooth graybox shapes; it gets re-evaluated with the first foliage (the shader already supports it).
  No outlines, no watercolor.
- **Fog:** exponential, coloured by the DayCycle (atmospheric perspective toward the horizon colour).
- **URP:** Forward+, MSAA 4x, soft shadows (high), 4 cascades to 100 m, HDR, LOD cross-fade (dither).

## The Blender ↔ Unity match test

`Scenes/Dev/LookDev` (MatchTest subject) and `ArtSource/Test/match_test.py` render the same sphere and cube, with the
same material, from the same five views. Result: **the geometry, framing, sun direction, shadow shapes and the light/
shade split match**. Known differences:

1. **Tonemapping curve:** Blender uses "Khronos PBR Neutral" (the closest available); Unity uses its own Neutral. The
   Blender sheet comes out a little more saturated and brighter in the lit oranges; Unity's shade side is slightly
   lighter.
2. **Grading** is applied to the Blender sheet in display space (an approximation of Unity's log-space contrast and
   white balance).
3. **Shadow filtering:** EEVEE's soft sun (3° wide) vs URP's soft shadow filter. The penumbrae are close; EEVEE's are a
   bit wider at distance.
4. **Received shadows** come from EEVEE's diffuse probe in Blender (Shader to RGB, sun energy π so it reads N·L) and from
   the shadow map in Unity. Both fade out near the terminator with the same N·L range (0.12 to 0.35), which removes
   self-shadow acne on both sides.
5. **Sky backdrop:** Blender composites the same sky gradient formula per pixel row. There is no sun glow in the
   backdrop.
6. **Anti-aliasing:** EEVEE 32 samples vs MSAA 4x.

## Networking

- **Owner authority for kayaks** (as proven in the old project): each player simulates their own kayak and sends 25 Hz
  snapshots; the others interpolate 100 ms in the past. **Host authority for the world**: the host decides which
  segments are loaded; clients follow through Netcode's scene management, synchronizing **additively** (everyone boots
  into Core).
- The connection carries a protocol string (`cr-net-1`): a different build is refused with a clear message.
- Multiplayer tests run as several **release builds** on one PC with command-line switches (`TestSwitches`).

## Testing rule

**Visuals and multiplayer are verified with release builds** (`CampanhaRio > Build > Windows (release)`,
`Builds/` is git-ignored), screenshots and the Player.log. Batch-mode editor runs are fine for logic and builders,
but never use `-nographics` for anything visual.
