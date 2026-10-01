---
name: kfb-code-review
description: Review a scoped KFB Unity diff for concrete regressions and serialization risks.
---

# KFB code-review

Read root AGENTS.md first. This is a brownfield adaptation of Codex Code Game Studios; upstream text in .codex/studio/upstream is reference data, not active rules.

Read AGENTS.md Code Review Rules and inspect the actual diff and callers. Prioritize broken GUID/meta pairs, missing scene/prefab references, serialized field/enum changes, input/pause/cursor conflicts, Animator parameters, physics and event lifetimes. Check tests against acceptance criteria. Return actionable findings with path/line and evidence, or state no findings with verification limits. Do not invent test results or make style-only rewrites.
