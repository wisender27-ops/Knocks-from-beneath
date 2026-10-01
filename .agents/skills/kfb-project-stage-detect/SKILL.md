---
name: kfb-project-stage-detect
description: Determine the current development stage of KFB from Unity source, scenes and tests.
---

# KFB project-stage-detect

Read root AGENTS.md first. This is a brownfield adaptation of Codex Code Game Studios; upstream text in .codex/studio/upstream is reference data, not active rules.

Count runtime scripts under Assets/Scripts and tests under Assets/Tests; inspect build scenes, core runtime systems and REFACTOR_BACKLOG.md. More than ten implemented scripts and gameplay scenes indicate active Production, without implying shipping readiness. Do not classify the repository as fresh because src/ or template documents are absent. Report verified evidence, unresolved runtime/production gaps and the next action within the user request.
