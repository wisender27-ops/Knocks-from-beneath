---
name: kfb-qa-plan
description: Plan focused Unity verification for a substantial KFB feature.
---

# KFB qa-plan

Read root AGENTS.md first. This is a brownfield adaptation of Codex Code Game Studios; upstream text in .codex/studio/upstream is reference data, not active rules.

Derive tests from the request acceptance criteria and existing integration points. Select existing EditMode tests and necessary regression scenarios; add tests only for meaningful behavior. Include Play Mode checks for changed UI/gameplay, scenes, input, pause/cursor/camera, physics, quests and Animator as relevant. Coordinate exclusive Editor access and preserve dirty scene state. Record executed/pass/fail/unverified separately. No mandatory sprint or full-suite ceremony for small changes.
