# Installed sources and adaptation

- Codex Code Game Studios: https://github.com/Euraika-Labs/Codex-Code-Game-Studios
  Revision: 9a182e29f8d9b46394851830ba27213fbe4da950; MIT license in LICENSE-game-studios.txt.
  Eight skills were installed with the Codex skill-installer and adapted as kfb-*.
  Exact installed SKILL.md files are preserved in upstream/ as reference data.
  Adaptation replaces src/tests paths, template-document blockers, repeated approvals,
  model pins and studio-wide ceremony with the root AGENTS.md contract. It does not
  install the full upstream bootstrap, hooks, release pack, or 50 template agents.
- Official Unity Technologies plugin: unity@openai-curated-remote, 0.1.6-beta,
  verified installed/enabled in Desktop. https://github.com/Unity-Technologies/unity-agent-plugin
  Four relevant skills mirrored from that installed package as kfb-unity-* for CLI.
  Skill frontmatter names are unique; YAML descriptions are quoted and package
  requirements are placed under metadata for Codex compatibility. Bodies/references
  remain official. Root instructions resolve routine approval/tool-budget conflicts.
  Unity Companion License preserved in LICENSE-unity.md.
- Unity CLI 1.0.0-beta.6: official `unity skill install codex --local` generated
  unity-cli and its references offline from the installed binary.
  Pipeline 0.7.0-exp.1 was already installed; no Unity package changes made.
- .codex/agents contains four project roles authored for the requested bounded workflow.
  They inherit the user's model/permissions. Desktop can use role instructions with its
  available subagent tools if named roles are not exposed by that host.

Global Codex config/AGENTS and other projects are unchanged. Three existing global
Unity server entries are reconciled only for this repository in .codex/config.toml.
