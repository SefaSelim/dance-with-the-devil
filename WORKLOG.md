# Work Log

Every change to this project goes here — from people and from AI tools. Git shows *what* changed inside a file; this log records *who* changed it, *why*, and *where in the Unity Editor*. That matters most for changes git explains badly: edits to scenes, prefabs, and components; ProjectSettings; and local, uncommitted experiments.

## Rules

1. Add one entry per task or work session, **at the bottom** of the Entries section, before you commit. If nothing gets committed, add the entry anyway and mark it `uncommitted`.
2. Log local and uncommitted changes too. Update your entry when they get committed or thrown away.
3. For Unity Editor changes, give the full path: `scene or prefab › GameObject › component › fields (old → new)`. Example: `Assets/Scenes/SampleScene.unity › Player › Rigidbody › mass 1 → 2`.
4. Include component adds and removes, and every Rigidbody, collider, or physics change.
5. This log is append-only. Never edit or delete someone else's entry; to correct something, add a new entry.
6. Keep entries short and factual.

## Entry template

```
### YYYY-MM-DD — <person> (<tool: manual | Claude Code | Codex | Cursor | ...>) — branch <branch>
- Changed: <files, or scene › GameObject › component › fields>
- Why: <reason>
- Status: committed (<commit message>) | uncommitted | discarded
- Notes / risks: <optional>
```

## Entries

### 2026-10-23 — SefaSelim (Claude Code) — branch main
- Changed: added the Claude Code Game Studios toolkit to `.claude/` (agents, skills, rules, docs, scripts, hooks; Godot and Unreal specialists left out), `Docs/engine-reference/unity/`, `project.yaml`, `AGENTS.md`, `CLAUDE.md`, `MAIN_GDD.md` (empty skeleton) and this `WORKLOG.md`. `.gitignore` now keeps only personal Claude files out of git, so `.claude/` is shared.
- Why: the whole team uses the same AI agents, skills and project rules during the jam.
- Status: committed (Add Unity project at repo root and shared Claude Code toolkit)
- Notes / risks: the Unity project (Unity 6000.6.0f1, URP) moved from the `Dance with the Devil/` subfolder to the repo root, so the root is the project folder: re-add it in Unity Hub from the new path. The subfolder's own git repo (Unity Hub's "Initial check-in", remote `Dance-with-the-Devil-2026-10-23_22-13-27`) was dropped; this repo (`SefaSelim/dance-with-the-devil`) is the only one.
