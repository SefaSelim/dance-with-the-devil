# Project Instructions

**Dance with the Devil** is a Unity game made by our team for a game jam. Time is short: keep changes small,
working and playable at every commit.

## Before every task

1. Read `MAIN_GDD.md` (the one living Game Design Document, in Turkish).
2. Read the latest entries in `WORKLOG.md` (recent changes, including uncommitted and Unity Editor changes).
3. Do not propose an implementation before inspecting the relevant existing code, scenes and prefabs.
4. **Pick the toolkit helpers first.** Before starting the work, scan `.claude/agents/` and `.claude/skills/`
   (their names and `description` lines) and decide which agent(s) and skill(s) fit the request. Use them
   for the task, and say in one line which ones you chose (or that none fits). Examples:
   - level, room, arena, layout, dressing, encounter space → `level-designer` agent (and the
     `team-level` skill for a whole new area); you then place the result in Unity yourself;
   - mechanics and rules → `game-designer` / `systems-designer`; balance → `balance-check`;
   - fast throwaway experiments → `prototyper` / `prototype`;
   - gameplay code → `gameplay-programmer`; Unity APIs → `unity-specialist`;
   - VFX and game feel → `unity-vfx-game-feel` skill; shaders → `unity-shader-specialist`;
   - UI → `ui-programmer` / `unity-ui-specialist`, `ux-design`; audio → `audio-director` / `sound-designer`;
   - bugs → `bug-report`; code review → `code-review`; tests → `qa-tester`, `qa-plan`, `smoke-check`;
   - scope and time → `producer`, `scope-check`;
   - visual direction → `art-director`; story and text → `narrative-director`, `writer`.
   Agents that only read and write files (such as `level-designer`) produce the plan; the main session does
   the Unity Editor work. The rules in this file still win over anything an agent or skill suggests.

   This step is binding: actually call the chosen agents and skills; do not just name them and plan inline.
   "Work straight through", "no review needed" or "time does not matter" do not waive it. Skip a helper only
   when none fits, and say why in that same line.

   **Level design tasks** (map area, room, arena, layout, set dressing, scene decoration, placing
   environment assets in a scene) always go like this:
   1. Read `.claude/agents/level-designer.md` and `.claude/skills/team-level/SKILL.md`.
   2. Inspect the target scene and any existing area first (what is there, the scale, the player, the world shape).
   3. Run the `level-designer` agent for the layout plan: zones, paths, landmarks, pacing and sightlines.
      For a whole new area, run the `team-level` skill instead, which also brings in `art-director` and
      `world-builder`.
   4. Do the Unity Editor work in the main session from that plan, then check numerically that nothing floats,
      sinks or clips (downward raycasts from each object's base), viewing from more than one angle.
   5. Add `asset-audit` when the task brings in new asset packs or many new assets.

## Working rules

- The GDD (`MAIN_GDD.md`) defines the target design.
- Do not add mechanics that are not in the GDD on your own initiative.
- Do not settle open design decisions by guessing.
- Do not implement shelved or cancelled ideas.
- Do not rewrite working systems outside the task scope.
- Follow the established architecture of the closest existing mechanic. An Editor/project lock is not a reason to substitute a different runtime architecture; preserve the intended prefab/scene pattern and resolve or report the tooling blocker.
- Propose large refactors as separate tasks.
- Implement only the approved scope of each task.
- Explicitly report every Scene, Prefab, and ProjectSettings change.
- List any manual steps the user must perform in the Unity Editor.
- Several people work in parallel: avoid editing a scene or prefab someone else is working on; prefer
  prefabs and separate scenes so merges stay simple.
- Update the relevant documentation when an implementation is finished.

## Main GDD (mandatory)

- `MAIN_GDD.md` is the project's **only** GDD and it is a living document. It is never versioned as GDD2,
  GDD3 and so on, and no new GDD file is made: the same file is edited.
- **Every task that adds a new mechanic, changes how an existing mechanic works, or records a user design
  decision updates `MAIN_GDD.md` in the same task, before committing:**
  - edit the relevant section (rules, numbers, keys) and its status label (✅ / 🟡 / ⬜ / ❓ / ⛔ / 🧪);
  - set the **Son güncelleme** line at the top to the current date and time (`YYYY-MM-DD HH:MM (UTC+3)`,
    taken from the system clock, not guessed);
  - add one line at the bottom of *Değişiklik geçmişi* (date-time — who — what changed).
- Write it in Turkish, as a design document: what the player does and the rules, not script names.
- An open question (❓ AÇIK) is closed only by a user decision, never by an implementation choice.
- Bug fixes and refactors that do not change what the player experiences do not need a GDD change.

## Work log (mandatory)

- Log **every** change you make in `WORKLOG.md` (code, scenes, prefabs, components, ProjectSettings, packages, docs), in the same task, before committing. Use the template in that file.
- Log local and uncommitted changes too, and mark them `uncommitted`.
- For Unity Editor changes, write `scene or prefab › GameObject › component › fields (old → new)`. Always log Rigidbody, collider, and physics component changes.
- Append only: add your entry at the bottom; never edit or delete other entries.
- When investigating a regression, check `WORKLOG.md` and `git log` before changing code.

## Old branches become version tags (GitHub branch list)

GitHub keeps only `main` (the default branch, which cannot be deleted) and the branches being worked on. When a
branch is finished and merged, it becomes a **version tag** and is deleted, so the branch list stays short.
Nothing is lost: the tag holds every commit.

- **Version:** `vMAJOR.MINOR.PATCH`, in the order the branches were finished. A branch that adds or changes a
  mechanic or content raises MINOR (`v0.1.0` → `v0.2.0`); one with only fixes, docs or tooling raises PATCH
  (`v0.2.0` → `v0.2.1`). Look at the latest with `git tag -l "v*" --sort=-v:refname | head -1`.
- **The tag is annotated** with the branch name and a one-line summary of what it changed, so `git tag -n` reads as
  a changelog.
- It deletes branches on GitHub for the whole team, so ask the user first; leave out any branch someone is still
  working on.
- Run from Git Bash, once per old branch (`BRANCH`, `VERSION` and the summary filled in):

```bash
git fetch --prune origin && git tag -a VERSION origin/BRANCH -m "VERSION (BRANCH): summary of its changes" && git push origin refs/tags/VERSION && git push origin --delete BRANCH
```

- See them: `git tag -n --sort=v:refname`. Bring one back as a branch: `git checkout -b BRANCH VERSION`
  (and `git push -u origin BRANCH` if it should be on GitHub again).
- Local branches are not touched; delete one you no longer need with `git branch -D BRANCH`.

## Claude Code Game Studios toolkit (.claude/)

`.claude/agents/`, `.claude/skills/`, `.claude/rules/`, `.claude/docs/` and `.claude/scripts/` hold the
Claude Code Game Studios toolkit (MIT, `.claude/CCGS-LICENSE`, release 1.1.3), with its Unity reference in
`Docs/engine-reference/unity/` and its config in `project.yaml`. The folder is committed so the whole team
shares the same agents and skills; personal files (`.claude/settings.local.json`,
`.claude/agent-memory-local/`, `CLAUDE.local.md`) stay out of git. Use its agents and skills freely, but this
file wins wherever they disagree:

- `MAIN_GDD.md` stays the one GDD. Do not create the toolkit's per-system GDD files (`design/gdd/...`)
  as a second design source; put design decisions in `MAIN_GDD.md`.
- `WORKLOG.md` stays the record of changes; the toolkit's session-state and production files are optional
  working notes, never a replacement.
- Never settle open design questions or add mechanics because a toolkit skill suggests it.
- This is a jam game: skip the toolkit's heavyweight process (epics, sprints, director gates, release and
  live-ops checklists) unless the user asks for it.
- The engine is Unity 6 (URP, Input System); the exact editor version is in
  `ProjectSettings/ProjectVersion.txt`. Only the Unity specialists are installed.
