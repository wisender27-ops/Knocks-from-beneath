# Verified brownfield map — 2026-10-26

Recheck relevant entries before changes. This is a snapshot, not a replacement for code.

- Repository: wisender27-ops/Knocks-from-beneath; local Codex project: KFB.
- Existing quest-driven first-person horror game in active development (Production;
  not a claim of release readiness). 132 scripts in Assets/Scripts, 29 EditMode test files,
  8 Play Mode probes (Assets/Tests/*PlayProbe.cs, run manually from the Editor).
- Game-design plan and task status: DESIGN_ROADMAP.md (read first; task take/close protocol there).
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
  controllers under Systems_and_Visuals/ThoughtManager, orchestrated by IntroSequence
  (~1200 lines). Debug jumps: IntroSequence.useDebugSkip + startStage (DebugStoryStage).
  Preserve event contracts.
- Story state: StoryFlags (knock trust/answered, recording, neighbor note, clues, front door);
  ChapterCheckpoints (PlayerPrefs kfb.progress.*: 4 chapters, chapters unlock on exit to menu/quit,
  debug sessions never save); EndingRegistry (EndingId E1–E5, "(k/N)" titles, seen endings in
  kfb.endings.seen). Enum values are persisted — append only.
- Knock language (Events_and_Triggers): RandomKnock (legacy API kept), KnockDirector/KnockTracker
  (source follows player under the floor), PlayerFloorKnock (Q, crouched, above basement),
  KnockResponder (replies), KnockCallGame/KnockPattern (call-and-response minigame M1).
- Minigames: Systems_and_Visuals/Minigames/MinigameWindow (shared window contract: one at a time,
  camera/cursor/interaction restore, pause blocked); NailBoarding + HatchNailingGame (finale M3,
  in-world); ClueInspectWindow + ClueInspection + ClueDetailBuilder ("Осмотр" M5). Inspectable
  clues: Interactables/InspectableClue on owner-leftover copies under "Main Plot/Owner Clues (M-05)";
  the 3D preview (RT, layer 30) is Inventory/ItemPreviewStage, shared with RetroInventoryScreen.
  Scares are arbitrated by HorrorMomentDirector.
- Audio: Systems_and_Visuals/Audio (MainMixer buses Music/UI/SFX{Foley,Horror,Voice}/Ambience,
  AudioRouting, GameAudio, AmbienceDirector); clips in Assets/Audio/Game, library in Resources.
- Input System 1.19.0 is installed; activeInputHandler=2 (Both). Gameplay uses
  legacy UnityEngine.Input; do not automatically migrate controls.
- GraphicsSettings main pipeline: Assets/Settings_URP.asset; mobile quality pipeline:
  Assets/Settings/Mobile_RPAsset.asset. Inspect renderer/RetroPSX settings before edits.
- Tests: Assets/Tests/EditMode, KnocksFromBeneath.Tests.asmdef, NUnit,
  UNITY_INCLUDE_TESTS. Prefer focused live-Editor tests while the project is open.
- Existing REFACTOR_BACKLOG.md is context; entries must be reverified and are not
  authorization to implement unrelated backlog work.
- Often dirty and NOT task-owned (Editor re-serializes them): TMP fallback font asset,
  ProBuilder settings, URP assets, ProjectSettings.asset, HatchBoards.mat, Main.unity.
  Never stage them unless the task changed them on purpose.

## Tooling
Official Unity Desktop plugin is installed/enabled. Unity CLI 1.0.0-beta.13 reports a ready
Editor at this exact project. Useful: `unity recompile --json`, `unity command run_tests --mode
EditMode [--filter X] --result-only`, `unity command eval/editor_play/editor_stop/
capture_game_view --source screen` (save_path lands under Assets/ — delete the PNG and folder after). The CLI-generated local
skill matches this older binary; newer plugin examples can differ in flag placement.
Always use the absolute project path and confirm the response before Editor writes.
MCP is pinned through unity CLI; duplicate relay/HTTP entries are disabled project-locally.
Compilation/tool connection alone does not verify runtime gameplay.
