---
name: kfb-ux-design
description: Design KFB inventory/menu/interaction UX while preserving current uGUI integration.
---

# KFB ux-design

Read root AGENTS.md first. This is a brownfield adaptation of Codex Code Game Studios; upstream text in .codex/studio/upstream is reference data, not active rules.

Inspect RetroInventoryScreen, PlayerInventory, PlayerInteraction and PlayerController before UI changes. Plan selection, keyboard/mouse navigation, empty states, feedback, item preview and equip/use only as requested. Preserve pause, cursor, camera lock, audio state, quest availability and input restoration on close/disable/destroy. Use existing uGUI/TMP patterns; do not switch to UI Toolkit or build a parallel inventory as incidental cleanup.
