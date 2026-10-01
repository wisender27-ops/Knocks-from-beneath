---
name: kfb-architecture-review
description: Review task-specific KFB integration architecture before local refactoring.
---

# KFB architecture-review

Read root AGENTS.md first. This is a brownfield adaptation of Codex Code Game Studios; upstream text in .codex/studio/upstream is reference data, not active rules.

Trace affected runtime entrypoints, consumers and state writers. Check Unity serialization, prefab/scene references, asmdefs, URP/RetroPSX, input and Animator contracts. Identify existing systems to extend. Return minimal boundaries, migration risks and test criteria. Do not require all-project traceability documents or restructure working assets. Global changes are proposals only.
