# Configuration verification — 2026-10-01

- Codex v0.159.2 `debug prompt-input`, launched from the actual repository, includes
  root AGENTS.md and the 13 local skills. A model smoke session also acknowledged
  the entrypoint, four roles, existing-inventory reuse, one-agent small fixes and
  proposal-only global redesign. No implementation task was executed in that session.
- This host exposes generic collaboration agents rather than named role selectors;
  AGENTS.md explicitly supports passing role-file instructions to those agents.
  A read-only subagent performed architecture inspection and independent workflow
  review; its findings were addressed. Named-agent dispatch was not separately tested.
- All 13 local skill manifests passed Codex skill-creator quick_validate.py.
- Official Desktop plugin `unity@openai-curated-remote` 0.1.6-beta was confirmed
  installed/enabled; the local CLI fallback does not depend on remote plugin discovery.
- Real pinned Unity CLI MCP transport passed initialize and tools/list (151 tools).
  Read-only eval confirmed the exact project, active Main scene, idle Editor,
  not compiling. console_status reported compilationFailed=false.
- 1572 pre-existing tracked/WIP file hashes matched before/after installation.
  No Unity packages/assets/settings were modified by this setup.
- Existing Editor console contains one UnityConnect Token Exchange web-request
  exception, predating verification; it was left intact. No Play Mode or gameplay
  tests were run for these instruction/config-only changes.
- `--strict-config` is blocked by the pre-existing user-global obsolete
  `features.rmcp_client` key. Normal Codex loads with a warning and smoke test passes;
  global settings were intentionally left unchanged. Other global skill/icon warnings
  were unrelated to the local pack. New local manifests pass validation.
- Unity command flags differ by CLI version; the local unity-cli skill is generated
  from 1.0.0-beta.6. A read-only eval/console request succeeded with telemetry flags
  before the remote command name.

Run future tasks in the Codex project **KFB**, with the matching Unity project open.
Start a new chat after configuration changes. No special command is needed.
