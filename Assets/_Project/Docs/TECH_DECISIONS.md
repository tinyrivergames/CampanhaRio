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
  JSON; the Blender sheet applies an approximation of them. **Contact AO (soft SSAO):** URP SSAO on the renderer, small and smooth (radius 0.35 m, interleaved gradient, 12 samples,
  bilateral blur), applied by SoftToon to all of the ambient and 35% of the sunlight. It keeps objects on the ground at
  every time of day, dusk included (no sun shadow then). The Blender material does the same with its AO node
  (`contactAO` in the rig).
  No outlines, no watercolor.
- **Fog:** exponential, coloured by the DayCycle (atmospheric perspective toward the horizon colour).
- **URP:** Forward+, MSAA 4x, soft shadows (high), 4 cascades to 100 m, HDR, LOD cross-fade (dither).

## The Blender ↔ Unity match test

`Scenes/Dev/LookDev` (MatchTest subject) and `ArtSource/Test/match_test.py` render the same sphere and cube, with the
same material, from the same five views. Result: **the geometry, framing, sun direction, shadow shapes and the light/
shade split match**. Known differences:

1. **Same post chain:** Blender renders linear EXR tiles and the sheet applies Unity's chain in numpy: post exposure,
   white balance (approximated as a channel tint), contrast around mid grey in log space, saturation, then **URP's exact
   Neutral tonemap curve** and sRGB. (A first version used Blender's "Khronos PBR Neutral" view and came out clearly more
   saturated than the game; the shared curve fixed that.) Remaining: Unity's vignette and its white-balance math.
2. **The Pebble** (`ArtSource/Test/Pebble/Pebble_blender_vs_unity.png`) shows the same: shape, framing, light and
   colour match; Unity is a touch darker in the corners (vignette).
3. **Shadow filtering:** EEVEE's soft sun (3° wide) vs URP's soft shadow filter. The penumbrae are close; EEVEE's are a
   bit wider at distance.
4. **Received shadows** come from EEVEE's diffuse probe in Blender (Shader to RGB, sun energy π so it reads N·L) and from
   the shadow map in Unity. Both fade out only in the terminator sliver (N·L 0 to 0.05, the same on both sides). A first, wider fade (0.12 to
   0.35) removed every shadow on flat ground once the sun was low (sunset: N·L = sin 4° = 0.07), which made objects
   float; fixed 2026-10-03. The sun's shadow bias stays LOW (depth 0.4, normal 0.35): a high bias detached the shadows from the feet
   ("peter-panning", tried and reverted 2026-10-03). The dark curved lines seen on the sphere at a low sun are the cube's
   real shadow, not acne.
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

## The world by segments

- **Core + additive segments.** `Scenes/Core/Core.unity` is always loaded (bootstrap, network, players, light, camera,
  save; roots for the van, audio and UI). Each stretch of the journey is its own scene under `Scenes/Segments/`, with a
  `Segment` component (an area box and a "load next" box).
- **The host decides** (`SegmentStreamer`, on the host only): it loads the next segment through Netcode's scene
  management when any player reaches the end zone, and unloads a segment once every player is in a later one. Clients
  load and unload automatically (they sync additively, so Core stays). Netcode allows one scene event at a time, so
  decisions wait for the current one.
- **Solo is a private host** on this machine, so solo and group run the same code paths.
- **Checkpoint:** the latest segment that holds the whole group, saved by the host (see `SAVE_MODEL.md`).
- **Release test results** (graybox segments, one host + one client build, `-cc-script streamwalk`):
  loads of the next segment took 9–29 ms and unloads 3–16 ms. The worst frame while streaming during play was
  4.4–15 ms on both machines; **no frame over 50 ms after warm-up on either**; no exceptions. A friend joining
  mid-journey synced straight into the current segment and appeared beside the group. The only frames over 50 ms are at
  boot: the first segment loads while the game starts (~2.6 s first frame, then one ~70 ms frame for the first shader
  use). Shader warm-up (a variant collection) can hide that later, behind the start screen.

## The river rule

- `RiverChallenge` (host or solo): the time limit is the sunset. The DayCycle moves from the afternoon to dusk over the
  limit; at the limit night falls, the screen goes black, and the group is put back at the river start.
- Pass when **at least half** of the group (every kayak in the registry) crosses the finish line. The medal comes from
  the group's time against the base limit (the assist never buys a medal).
- **Hidden assist:** from the 3rd failure on, +8% per failure, capped at +25%. It's never shown.
- Release test (`-cc-scene KayakTest -cc-script riverrule`): 3 failures with a 12 s limit, then the limit was 12.96 s
  (+8%), then a pass with 1 of 2 kayaks finished in 91.3 s (gold) at day progress 0.48. The save recorded 4 attempts,
  3 failures, best 91.3 s, gold.
- Not yet: syncing the timer and result to clients (Phase 2, with the Rio 1 graybox).

## Boats over the water: stencil, and the D3D12 crash on quit (2026-10-06)
- **Stencil for boats:** the hull material writes stencil bit 1 (the river water drawn over it skips the shore foam, which
  the shallow depth would put on it); `BoatWaterMask.shader` caps the cockpit with bit 2 (no water drawn inside). Only the
  boats turn it on: SoftToon's `_StencilComp` is 0 (off) by default, and the importer sets it from the sidecar's
  `_StencilWriteMask`. `CampanhaRio > Art > Reimport Materials` rebuilds every pipeline material after such a change.
- **The crash:** from the first stencil build on, the player crashed on quit (0xC0000005 in `D3D12Core.dll`, after
  "CodeReloadManager destroyed"; DX11 was fine; it also happened when closing the window normally). Found by elimination
  (no stencil: fine; any SoftToon stencil write: crash) and then by deleting the D3D12 pipeline cache
  (`%TEMP%/TinyRiverGames/CampanhaRio/dx12_pso_cache_lib.bin`, 1.3 MB after dozens of builds): with a fresh cache every
  build quits cleanly, also the older ones. The crashing runs never got to rewrite the cache: Unity crashed while saving
  a corrupted pipeline library.
- **Fix:** `BuildTools.BuildRelease` deletes this machine's cache after every build. **For release:** players' caches
  could go bad the same way across updates; check Unity's D3D12 pipeline-cache options (or its issue tracker) before
  shipping.
