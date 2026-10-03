# What came over from CayaCozy (and what didn't)

The old project (`C:/Users/henri/CayaCozy 1.0`, last stage: 11) is now the lab. Only systems that work came over, as
code, renamed to the `CampanhaRio` namespace, without the old art, watercolor look, HUD, menus or race/hub flow.
Every ported script **kept its `.meta` GUID**, so data (a prefab, a scene object) can later be copied between the
projects without breaking references.

## Ported

| Module | Files (new location) | From (old `Assets/_Game/`) | Changes |
|---|---|---|---|
| Kayak core | `Scripts/Kayak/KayakController`, `KayakPhysics` (+ `.Capsize`, `.Skills`), `KayakTypes`, `KayakRegistry`, `LocalPlayer`, `KayakInput`, `SyncJumps`, `KayakPaint` | `Scripts/Kayak/*`, `Scripts/Player/KayakInput` | The Stage 2–6 `Legacy` model and the F4 model swap are gone (KayakPhysics is the only simulation). The race's `TrackSettings` speed override is gone (the KayakPhysics caps apply). The old `GameFeelPreset` became `KayakFeel` with the "Natural" values (no boost button; earned meter = a surge). `GentleObstacle` is now a plain marker in `Kayak/`. |
| Paddler animation | `Scripts/Kayak/Paddler/*` | `Scripts/Kayak/Paddler/*` | **Character-agnostic:** `PaddlerRig.bones` maps bone names (Mixamo names by default); only hips, head and both arm chains are required, spine/neck/legs are optional, and `seatLegs` can turn the leg pose off for short animal legs. `LookTarget` moved to `Scripts/World/`. |
| Water and flow | `Scripts/River/RiverPath`, `RiverMeshBuilder`, `RiverFeature`, `RiverModifiers`, `FlowObstacle`, `CrossCurrent`, `CurrentZone`, `WaterArea`; `Art/Shaders/Water/RiverWater.shader` | `Scripts/River/*`, `Scripts/Water/WaterArea`, `Art/Shaders/RiverWater.shader` | Unchanged logic. The shader keeps the turquoise defaults and will be restyled in Phase 3. Its two procedural textures come from the ported `WaterTextureGenerator`. |
| Van | `Scripts/Van/VanController`, `VanAutoDriver`, `VanRoad`, `VanInput` | `Scripts/Van/*` | Unchanged. The auto-driver still knows "camp" and "take-out" stops; in Phase 2 that becomes "the end of this road". Seats, doors, rack and the van's network sync stay behind until the new van exists. |
| Networking base | `Scripts/Net/NetSession`, `NetConfig`, `NetOnline`, `KayakNetSync`, `KayakSnapshot`, `CountingTransport` | `Scripts/Net/*` | `NetSession` rewritten lean: host/join (direct or online code), **the protocol/version check** (`cr-net-1`), room size, friendly messages, the 6 s crash timeout, the shared clock. The race and hub flow is out; the game spawns what it needs on `NetSession.PlayerReady`, and clients sync scenes **additively** (Core stays, segments stream). `KayakNetSync` lost the pickups, spectating/parking and race hooks; the owner-authority snapshots, interpolation and timed events are unchanged. |
| Camera | `Scripts/Camera/KayakCamera` | `Scripts/Camera/KayakCamera` | The feel-preset values became fields (`surgeFov`). `Core/GameSettings` keeps only mouse sensitivity and invert Y for now. |
| Tools | `Scripts/Dev/FeelBenchmark`, `KayakAutopilot`, `KayakBotSpawner`, `TestSwitches` (new); `Scripts/Core/Loc`, `ScreenFader` | `Scripts/Debug/*`, `Scripts/UI/Loc`, `Scripts/Core/ScreenFader` | `NetAutoTest` (1,128 lines, hub-specific) was replaced by the small `TestSwitches` (`-cc-host`, `-cc-join`, `-cc-bot`, `-cc-scene`, `-cc-script`, `-cc-shots`, `-cc-quit`). `Loc` keeps Portuguese as the default, with only the network strings for now. The benchmark can trace the autopilot run too (`CR_BENCH_TRACE`). |
| Editor | `Scripts/Editor/River/RiverPathEditor`, `WaterTextureGenerator`, `WaterAssets`, `KayakTestBuilder`; `Scripts/Editor/Kayak/KayakPrefabBuilder`, `PaddlerPlaceholderRig`; `Scripts/Editor/Dev/FeelBenchmarkRunner` | `Editor/*` | `KayakPrefabBuilder` builds the graybox kayak and its network variant; `KayakTestBuilder` rebuilds River_01 as a graybox (below). |
| Settings and assets | `ProjectSettings/DynamicsManager.asset` (copied as is), layers Kayak/Obstacle/Environment/CameraBlocker, `Input/CampanhaRioInput.inputactions`, `Art/Materials/Physics/KayakPhysics.physicMaterial` | same | Identical physics settings, so the simulation matches. |

## The kayak test river and the FeelBenchmark comparison

`Scenes/Dev/KayakTest.unity` is a **graybox of River_01** (1,131 m, 9.8 m of descent): the same knots, widths, flow,
sculpted bed and banks, rocks, eddies, backwater, creek inflow, river features, fallen trees, logjam, shallow rocks and
pier. It uses the old builder's exact numbers and random seeds, and has no art. Using the same water makes the numbers
comparable one to one. Build it with *CampanhaRio > Setup > Build KayakTest Scene*. Run the benchmark with
*CampanhaRio > Feel Benchmark*, or in batch: `Unity.exe -batchmode -projectPath <path> -executeMethod CampanhaRio.Editor.FeelBenchmarkRunner.RunBatch`.

The kayak's tuning was checked field by field: the new prefab's `KayakPhysics` block equals the old `Kayak.prefab`'s.
The new prefab has 10 extra fields that the old prefab never serialized, so both projects use the same code defaults
for them.

Result (`Docs/Benchmarks/2026-10-03_2011_port_phase0` vs the old `2026-10-01_2231_stage11`):

- **95 of the 97 scripted values are identical**: spawn to current, lateral step, yaw in current and still water,
  settle, backwater glide and exit, eddies, eddy line, the 5 impact levels with capsize/roll times, the 10 airtime runs,
  and the gorge top speed. The other two differ in the third decimal (pour-over gorge, paddling: landing impact 2.221 vs
  2.218 m/s, takeoff 7.556 vs 7.555 m/s).
- **The free autopilot run** (a 6-minute ride, chaotic by nature) lands close: 356 s vs 348 s, top speed 9.26 vs
  9.33 m/s, 3 eddy catches vs 2, backwater exit in 9 strokes vs 10. The old scene also had art dressing in and around the
  water (bank rocks, bushes, drifting logs) that the graybox doesn't rebuild, so a long run drifts apart a little.

## Deliberately left behind

- **Look:** the watercolor post-process, the painterly shaders (Lit, Foliage, Terrain, Sky, Cloud), the look textures
  and `LookSettings`. They're replaced by the SoftToon look (see `TECH_DECISIONS.md`).
- **Old art:** every mesh, prefab, material and texture (trees, rocks, the turtle paddler, the van model, the camp).
- **HUD and menus:** `KayakHUD`, `RunHUD`, `SkillHUD`, the capsize HUD, brush strokes, `MenuController`/`MenuBackground`, `NetOverlay`, `NetDebugText`.
- **The race and the hub:** `RunManager`, `NetRun`, gates, pickups, ghosts, scores, medals per track, the base camp,
  avatars on foot, camp kayaks, the van's seats/rack/doors/net sync, `NetAutoTest`, `TrackSettings`, `GameFeelPreset`.
- **Water dressing:** drifting objects, the water FX particles, planar reflections, ambient life (they come back with the
  water restyle).
- **The Legacy kayak model** and the F4 swap.
