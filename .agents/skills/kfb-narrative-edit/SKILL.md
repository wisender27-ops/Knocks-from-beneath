---
name: kfb-narrative-edit
description: Edit Russian dialogue, inner thoughts and story-facing quest text in Knocks-from-beneath against its actual scene and quest progression.
---

# KFB narrative editing

Deliver the revised text in the game. Read the current narrative controllers and scene text before writing; stale comments and debug stage names are not plot canon. Author answers take precedence over implementation when they differ; report discrepancies instead of silently redesigning gameplay.

## Find the context

- Trace `IntroSequence` callbacks through the controllers in `Assets/Scripts/Systems_and_Visuals/ThoughtManager/`.
- Include `TrashManager`, `PlayerDoorOpen`, `FinaleController` and serialized `NeighborNoteInteractable.noteLines`. Search for additional calls to `ShowThoughts`, localized assets and scene/prefab overrides.
- For each passage establish its trigger, speaker, current knowledge, intended next action and optional prerequisites. Optional phone content must not become assumed knowledge in mandatory scenes.
- Ask the author about missing backstory, motive, physical location or ending meaning when those facts affect the rewrite. Continue editing independent passages while the answer is pending; do not invent lore.

## Write for this game

Author-confirmed context (2026-10-02): the protagonist encountered the same threat before moving and bought this house from a family to get away from it. He is rough and sarcastic, with moderate swearing. A creature scattered kitchen items during the first night, but the hero need not know its identity yet. The basement hatch replaces the former kitchen-hole plot. The author removed the evening door-lock choice; do not reintroduce it as canon.

After the basement attack the creature disappears and the hero falls. His demand to find a hammer and barricade the hatch is intentionally irrational: the author intends an unreliable protagonist and wants a fuller narrative treatment later. Present the barricading idea as the hero's belief, not proof that the creature is imprisoned below; do not reveal a diagnosis or decide which events were hallucinations.

The author also removed escape/hiding endings. The final action is E on the basement hatch after finding the hammer: before the deadline it is boarded; a late attempt is interrupted by the creature pushing the hero downstairs and closing it. Expiry does not itself cause an attack. Both results conclude the same finale, without numbered ending collections.

- Use natural Russian inner speech for thoughts; dialogue technique applies to actual exchanges, not forced conversations with an absent listener.
- Give reactive lines a purpose: a useful observation, decision or personal response. Trim recap, empty confirmations, repeated instructions and theatrical fragments.
- Keep the voice consistent with the author's chosen character and profanity level. Let fear build from mundane unease to immediate danger; don't make the hero identify the creature before seeing it.
- Preserve necessary navigation and interaction information. Quest text can be explicit; thoughts should sound like a person rather than a quest system.
- Keep mysteries ambiguous when the game leaves them ambiguous. Distinguish inference ('кажется', 'похоже') from knowledge. Don't assert unseen actions or guaranteed safety.

## Integrate and check

- Prefer changing string literals and serialized text only. Preserve quest tags, callbacks, conditions, array contracts, GUIDs and scene object identities.
- `ThoughtManager` completes callbacks after all lines have typed and displayed. Shortening text changes pacing; check when control, timers and quests start. Never remove a sequence by dropping its completion callback.
- Snapshot existing WIP before edits. Inspect the connected Editor before serialized changes; avoid saving unrelated dirty scene changes.
- Compare non-text code against the snapshot, compile, run existing focused narrative tests when relevant, and inspect representative text in Play Mode. Report the exact verified scope; full-story playthrough is a separate claim.

## Craft references

This project workflow uses the reactive-line and coherence approach described in [game-narrative](https://github.com/myshenoy/skills-open-source/blob/main/game-narrative/references/dialogue-and-coherence.md). For actual character conversations, [dialogue-craft](https://github.com/JoelLewis/skill-library/blob/main/plugins/narrative/skills/dialogue-craft/SKILL.md) supplies intention, subtext and read-aloud checks. These references are optional context, not installed dependencies or authority for changing the author's story.
