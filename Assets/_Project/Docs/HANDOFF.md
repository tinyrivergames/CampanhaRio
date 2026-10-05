# HANDOFF: the full context to continue the project (read this first)

> Written on 2026-10-05 by the developer's **planning session** (a Claude conversation that ran from 2026-09-28 to 2026-10-05),
> so that a new session (possibly on another account) can continue without the old conversation.
> **This file + `PLANO_CAMPANHA.md` are the source of truth.**

---

## 1. Who the developer is and how they like to work
- Henrique, a Brazilian solo dev (+ one friend who helps with design, art and testing, **without** using Claude). They build
  the game with Claude Code doing almost all of the code.
- **Speak to them in Portuguese (pt-BR).** Write the prompts for the executing sessions in **English**.
- They want **honest feedback, not agreement** ("não quero que você concorde com tudo, fale a verdade"). Point out risks and
  disagree when needed.
- They change direction often while exploring. That's fine for a prototype, but **help them lock decisions** once made.
- **Art matters a lot to them** ("sem problemas em demorar os gráficos, quero algo bem feito").
- They playtest with friends and give short, direct feedback.

## 2. How the work was organized (it worked well)
- **A planning session** (like the old conversation): talks with the developer, decides, writes stage prompts as `.md` files in
  `Docs/Prompts/` (old project) and sends them to the **executing session** (a Claude Code session opened in the project
  folder), via cross-session messages when available. It checks the results (git log, screenshots, Player.log) and reports
  back in Portuguese after each stage.
- **Each executing stage:** a short plan per part, **a commit per part**, a **verification with graphics** (a release build +
  screenshots + reading the Player.log; never trust `-nographics` for visuals), a short final report, and then stop.
- **From Phase 1 on, the art is interactive:** the developer talks directly to the executing session, which makes a version,
  **opens the preview sheet and the `.blend` for them**, and waits for feedback.

## 3. The project history (why things are the way they are)
1. **CayaCozy (old project, `C:/Users/henri/CayaCozy 1.0`, GitHub `tinyrivergames/CayaCozy---Game`):** started as a cozy
   single-player kayak prototype (river current carries you, you steer). It went through many stages: water/flow-field river,
   a painterly watercolor look, MeshAI turtle paddler, a physics kayak (Rigidbody + buoyancy), and then a **pivot to fast,
   skill-based descents** (Stage 8: "zero fun" → speed, boost-from-water verbs, a short dense track, medals, ghost). Then
   **multiplayer** (Stage 9: NGO + direct IP/Tailscale, owner authority, kinematic proxies), polish (9.5: removed the "Mario
   Kart" feel, a Portuguese menu), **on foot + base camp hub** (10), **the van + full loop** (11). Stage 12 (more tracks) was
   abandoned mid-way. **The old project is now a lab; don't develop it further.**
2. **Lessons learned:** multiplayer with friends works and was "very promising". **Faster = much more fun.** Avoid the arcade/kart feel.
   Silly bugs appear in real play, so test with graphics. `.gitattributes` once corrupted the binary terrain data (keep binaries in LFS).
3. **2026-10-03: the campaign pivot.** The developer wanted a reason to keep playing. First idea: a road trip where the van gets
   "lost" and you descend rivers to reach it. **Rejected by the developer as repetitive.** We considered It Takes Two-style
   mechanic changes per level; I advised **"one excellent core + variations"** instead (scope + replayability).
4. **2026-10-04: the final premise = "Agência do Rio"** (`PLANO_CAMPANHA.md` v3): after a storm, the river is the valley's only
   route, and 4 animal friends reopen grandma's (Vó Nina's) river agency doing **tours + deliveries**. **Descent = River × Job**
   (cargo/passenger modifiers on the same kayak physics), the sunset time limit, a jobs board + valley map, the agency and
   villages growing as visible rewards, and Seu Alce shuttling the van (explained once).

## 4. The current state (2026-10-05)
- **The new project:** `C:/Users/henri/CampanhaRio` (working name), Unity 6000.5.10f1, URP 17.5, Git + LFS (**no remote yet**;
  the developer will create a GitHub repo).
- **Phase 0 (foundation) ✅ done 2026-10-03:**
  - kayak/water/van/network/tools ported (the FeelBenchmark matches);
  - the Blender pipeline (`ArtSource/pipeline/`: `common`, `lookdev`, `preview` sheet, `export`, `open_for_review`, and per-time-of-day lighting);
  - a SoftToon shader + `DayCycle` + a LookDev scene;
  - a streaming Core + segments skeleton;
  - a save model;
  - the `RiverChallenge` (the sunset rule);
  - the docs (`ART_PIPELINE.md`, `STYLE_GUIDE.md`, `PORTED.md`, `SAVE_MODEL.md`, `TECH_DECISIONS.md`, `ROADMAP.md`).
- Shadows at a low sun were fixed (low bias + soft contact AO), and **approved by the planning session's review**.
- **`ROADMAP.md` still reflects the v2 plan.** Update it to match `PLANO_CAMPANHA.md` v3 (the phases in Section 8 there).
- The Blender 5.2 path is `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe`. Blender MCP is not installed (not needed;
  the script-based loop works).
- The aesthetic references are in `Assets/_Project/Docs/Reference/` (inspired by *RV There Yet?*: soft stylized look; **inspiration only, no HUD/van/characters copied**).

## 5. What comes next (the options offered to the developer)
1. **Phase 1, interactive:** the style anchors, in order: **pine → rock → kayak**. Start by proposing the **pine brief** (size,
   tris per LOD, colors, foliage technique, references) following `ART_PIPELINE.md`. **Trees must be extremely light; background
   trees are near-"blurs"** (silhouette cards/impostors). Each version: preview sheet + `.blend` opened for the developer, then wait.
   Also show it at sunset (the descents happen at golden hour → sunset).
2. **Phase 2 (can run in parallel in another session, but mind the usage limits):** a graybox of the vertical slice: the agency hub,
   a short road, the Rio do Moinho, the arrival; **the jobs system** (a board, cargo/passenger modifiers, a ⭐ rating); **2 jobs on
   the same river** (the urgent letter and the scared goat) to prove "same river, different experience"; a simple Seu Alce.

## 6. Open decisions (don't decide for them; ask when relevant)
The game's name; which animal each character is (the 4 friends, Vó Nina, Seu Alce); the campaign ending; the agency's name.

## 7. If you are the NEW PLANNING SESSION (the developer moved the planner to another account)
Your role is the "planner/reviewer" described in §2. The **executing session** (the "engineer") stays on the original account,
in a Claude Code session opened in `C:/Users/henri/CampanhaRio`.
- **Talk to the developer in Portuguese.** Brainstorm, decide with them, and push back honestly.
- **Write each stage prompt** in English as a file in `C:/Users/henri/CampanhaRio/Assets/_Project/Docs/Prompts/` (create the folder),
  following the format of the old prompts in `C:/Users/henri/CayaCozy 1.0/Assets/_Game/Docs/Prompts/` (context, goal, process rules,
  Parts with verification, out of scope, a short final report, then stop).
- **Deliver it:** if cross-session messaging works (list the local sessions and look for the one in the CampanhaRio folder), send it with a short
  message and subscribe for the idle notice. Otherwise, give the developer a one-line instruction to paste into the engineer session:
  `Leia e execute Assets/_Project/Docs/Prompts/<file>.md`.
- **Review each stage** yourself: `git log`, `git status`, the screenshots (read the PNGs), the Player.logs, and the docs. Then report to the
  developer in Portuguese: what was done, what you saw, any problems, and what's next. Be specific and honest (for example, the
  shadow review in Phase 0 caught peter-panning).
- **During interactive art (Phase 1+),** the developer talks to the engineer directly. You help with briefs, review the preview sheets when asked,
  and plan the next phases.
- **Don't edit the project's code yourself** unless the developer asks. The engineer does the work. Small doc edits (like this file or the
  plan) are fine.

## 8. Usage notes
The developer hit the weekly "all models" limit on one account. Long automatic stages (hours of builds and multi-instance tests)
are expensive. Prefer smaller steps, and avoid running two heavy sessions at once.
