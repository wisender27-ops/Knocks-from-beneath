# Knocks-from-beneath — Codex Game Studio

This is the automatic entrypoint for ordinary feature requests. Follow these rules without requiring slash commands, a story file, or a separate onboarding prompt.

## Scope and autonomy
- Implement the requested feature end to end: inspect → scoped plan → implementation/integration → validation → concise report. Do not stop at a plan or code snippets when repository changes are requested.
- This is an existing quest-driven first-person horror game. Its current implementation is authoritative; do not infer a new automation simulator from generic global guidance.
- Local refactoring within the task and its direct dependencies is authorized after inspecting callers. Propose global architecture changes separately; do not implement them without explicit authorization.
- Resolve routine decisions independently. Ask only for missing requirements that materially change the result, destructive actions, or a global redesign. Do not add approval gates for each file.
- Small fixes: one agent, no design document/sprint ceremony. Large features: delegate bounded work to the roles below, then integrate and verify. Do not independently add features from the backlog.

## Brownfield preflight
1. Confirm repository/branch and inspect `git status --short`; preserve unrelated modified, staged, and untracked work. Never reset/clean/stash or auto-save a dirty scene to simplify the task.
2. Read `.codex/studio/project-map.md`, then verify the relevant facts in current code/settings. Trace runtime entrypoints, callers, event writers, scene/prefab references, and existing tests before editing.
3. Reuse existing systems. In particular, inventory already exists in `PlayerInventory`, `RetroInventoryScreen`, `InventoryDatabase`, and `InventoryItemDefinition`; evolve these rather than adding a second inventory.
4. State acceptance criteria and a minimal file scope. Recheck the scope if a new dependency appears. Absence of template GDDs/stories is not a blocker or evidence of a fresh project.

## Unity invariants
- Preserve `.meta` files and GUIDs. Move/rename Unity assets through the Editor/AssetDatabase with their meta; never regenerate an existing meta or silently replace a GUID. Check for Missing Script and broken asset references.
- Preserve serialized field names/types, enum integer values, prefab overrides, scene object identity/fileIDs, and Animator parameter/state contracts. Use `FormerlySerializedAs` for an intentional field rename; append enum values, and explicitly migrate incompatible data.
- Inspect the connected Editor before scene/prefab/asset edits; prefer Unity APIs over raw YAML. Do not rebuild scenes or rerun setup utilities such as `InventorySetup.Install()` without tracing their destructive/update behavior.
- Keep URP/RetroPSX renderer settings and Render Graph compatibility. Do not upgrade Unity/packages or switch input/rendering/UI systems as incidental cleanup.
- Input System is installed with Both enabled; gameplay uses `UnityEngine.Input`. Integrate with the current input/cursor/pause/camera locks; preserve menu and inventory behavior.
- Use existing asmdefs/namespaces and uGUI/TMP integration. Preserve Animator triggers, Rigidbody/CharacterController behavior, layers/masks, and event subscription lifetimes.
- For hot paths, avoid per-frame allocations, scene-wide searches, repeated GetComponent calls and unbounded physics queries. Optimize only with relevant evidence; do not introduce DOTS/DI frameworks by default.

## Team workflow for substantial features
Use real subagents when available, with at most four concurrent agents including the coordinator. Reuse slots across phases; do not spawn all roles at once.
1. `game_designer_ux`: acceptance criteria, controls, edge cases, UX grounded in the current game. Read-only code inspection.
2. `technical_director_unity`: integration boundaries, serialization/URP/Input/Animator dependencies and local refactor scope. Read-only architectural assessment.
3. `gameplay_programmer`: bounded implementation and relevant tests after the integration plan. Assign disjoint files if there are several writers.
4. `qa_reviewer`: independent review, regression scenarios and verification evidence. Read-only source review; coordinate Editor test execution with the parent.
Only one agent may write Unity scenes/prefabs or operate the Editor at a time. The coordinator owns integration, final diff, and evidence. If named-agent spawning is unavailable, pass the corresponding `.codex/agents/*.toml` role instructions to available subagents; if subagents are unavailable, execute the same phases sequentially and say so.

## Skills and tool selection
- `.agents/skills/kfb-*` is a compact adaptation of Codex Code Game Studios for this Unity repository. Use only the relevant workflow; default user requests require no `$` command.
- Official Unity plugin skills are available in Desktop. CLI fallback skills are installed locally as `kfb-unity-*`; use the matching official guidance when needed, not both copies.
- `.agents/skills/unity-cli` is generated by the installed Unity CLI and matches its version. Check supported flags before using examples from a newer plugin.
- Follow user authorization and this repository's completion/scope contract when a skill offers canned responses, arbitrary tool budgets, or additional routine approval gates. These must not leave an authorized implementation incomplete; retain actual technical prerequisites and task boundaries.
- Run Editor commands from this repository, always passing its absolute path with `--project-path`. Inspect `unity status --json` and confirm the returned project before mutation. This repo config pins the canonical `unity-mcp` server; duplicate legacy server names are disabled only here.

## Verification and completion
- Compile relevant assemblies and run existing focused EditMode tests after code changes. Use the live Editor while it is open; do not launch a second batch Editor against the same project.
- For gameplay/UI/physics/animation changes, verify acceptance criteria in Play Mode and relevant scenes: pickup/equip/use, pause/menu/cursor/camera, quest transitions and missing references as applicable. Coordinate with the user if entering Play Mode would discard an unsaved scene or interrupt active play.
- Compilation is not runtime proof. Record tests actually executed, outcomes, and precise unverified checks. Do not claim success from tool availability or empty logs alone.
- Review the complete diff, including asset/meta pairs. Stage only task-owned files/hunks. Commit only when requested; never push/publish without explicit authorization.
- Exception — `DESIGN_ROADMAP.md` task sync between the two developers (standing authorization): `git pull` before taking a task; on take, mark `[~]` with the owner and immediately commit + push; on close, mark `[x]` + journal row and immediately commit (with the task's code) + push. Never take a task another developer marked `[~]`. No force-push.
- Answer in concise Russian: result, verification, limitations, commit. Apply changes directly when asked to implement; for a code-only answer, show only the changed function/block. These project rules specialize the global code-snippet preference for repository work.

## Code Review Rules
- Flag GUID/meta loss, serialized contract changes without migration, prefab/scene reference damage, conflicting input/pause handling, event subscription leaks, and unintended URP/Animator changes.
- Check feature acceptance and real regression paths. Do not equate builds or static configuration checks with Play Mode evidence.
