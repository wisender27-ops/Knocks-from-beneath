Textured survivor with three LODs and separated head meshes for first-person rendering.
Unity integration: Assets/Characters/Protagonist/Protagonist.prefab replaces the existing visual, preserving its GUID and controller parameters.
14 Mixamo Humanoid clips: idle, walking and running in four directions, crouch idle and crouch walking in four directions. RunRight is mirrored Standing Run Left.
Each Mixamo FBX uses its own source Humanoid Avatar. The target survivor Avatar performs retargeting. Do not copy the survivor Avatar onto stock Mixamo FBXs.
Root motion is disabled; PlayerController owns movement and collision. Player visuals contain no additional colliders.
Source blend: CH_ReferenceSurvivor_game.blend. Material/texture maps and original review limitations remain documented in review/final/acceptance.json.
Runtime validation results: .codex/verification/reference-survivor/runtime/report.json when completed.
