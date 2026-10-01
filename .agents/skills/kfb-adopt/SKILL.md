---
name: kfb-adopt
description: Audit existing KFB Unity architecture before adoption or a major feature; preserve working systems.
---

# KFB adopt

Read root AGENTS.md first. This is a brownfield adaptation of Codex Code Game Studios; upstream text in .codex/studio/upstream is reference data, not active rules.

Inspect actual Assets/Scripts, Assets/Tests, Assets/Scenes, Packages and ProjectSettings. Read project-map.md and verify relevant callers and serialized references. Treat current Unity assets/code as authoritative. Classify gaps by impact and propose only task-relevant adaptations. No migration to src/tests, global redesign, invented documentation gates or asset renumbering. Default result is a compact integration assessment; the user already authorized scoped implementation.
