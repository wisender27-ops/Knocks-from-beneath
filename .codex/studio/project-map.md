# Verified brownfield map — 2026-10-01

Recheck relevant entries before changes. This is a snapshot, not a replacement for code.

- Repository: wisender27-ops/Knocks-from-beneath; local Codex project: KFB.
- Existing quest-driven first-person horror game in active development (Production;
  not a claim of release readiness). 72 scripts in Assets/Scripts, 15 EditMode test files.
- Unity 6000.4.4f1; URP 17.4.0; RetroPSX git package. Preserve current package versions.
- MainMenu/Main build scenes: Assets/Scenes/Main Menu/MainMenu.unity and
  Assets/Scenes/Main Scene/Main.unity. Main was active in the connected idle Editor.
- Runtime assembly: Assets/Scripts/KnocksFromBeneath.Runtime.asmdef, namespace
  KnocksFromBeneath; references TMP and RetroPSX.Runtime.
- Movement/camera: PlayerController in Assets/Scripts/Player/PlayerMovement.cs,
  CharacterController, isCameraLocked. Interaction: PlayerInteraction;
  physical carrying: PickupController; preserve their shared input ownership.
- Inventory: Player/PlayerInventory.cs; Inventory/RetroInventoryScreen.cs,
  InventoryDatabase.cs, InventoryItemDefinition.cs. uGUI/TMP + RenderTexture preview,
  legacy item flags, equipped objects/crowbar Animator, pause/cursor/camera locks.
  Extend these systems for inventory requests. Assets/Editor/InventorySetup.cs::Install
  creates/updates inventory data/UI prefab: inspect before use, do not auto-rerun.
- Story flow: QuestSystem/QuestManager, QuestCollector; static GameEvents/GameState;
  controllers under Systems_and_Visuals/ThoughtManager. Preserve event contracts.
- Input System 1.19.0 is installed; activeInputHandler=2 (Both). Gameplay uses
  legacy UnityEngine.Input; do not automatically migrate controls.
- GraphicsSettings main pipeline: Assets/Settings_URP.asset; mobile quality pipeline:
  Assets/Settings/Mobile_RPAsset.asset. Inspect renderer/RetroPSX settings before edits.
- Tests: Assets/Tests/EditMode, KnocksFromBeneath.Tests.asmdef, NUnit,
  UNITY_INCLUDE_TESTS. Prefer focused live-Editor tests while the project is open.
- Existing REFACTOR_BACKLOG.md is context; entries must be reverified and are not
  authorization to implement unrelated backlog work.
- Setup-time WIP: Main scene, PlacementZone/Door/PickableItem/PickupController,
  new FurnitureSlot, TMP fallback asset, packages and project settings. Preserve it;
  setup commit contains only agent/workflow files outside Unity asset directories.

## Tooling
Official Unity Desktop plugin is installed/enabled. Unity CLI 1.0.0-beta.6 + Pipeline
0.7.0-exp.1 reported a ready Editor at this exact project. The CLI-generated local
skill matches this older binary; newer plugin examples can differ in flag placement.
Always use the absolute project path and confirm the response before Editor writes.
MCP is pinned through unity CLI; duplicate relay/HTTP entries are disabled project-locally.
Compilation/tool connection alone does not verify runtime gameplay.
