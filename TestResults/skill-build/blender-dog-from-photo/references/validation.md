# Validator and observable review

`scripts/validate_dog_asset.py` runs inside Blender and writes JSON. It selects meshes skinned to one armature, ignoring studio objects and importer bone-display helpers. It checks topology data, UVs, texture pixel availability, normalized bone weights, influence limits, expected actions and finite sampled deformation. It also detects non-idle actions that do not move the skinned geometry and reports loop endpoint discrepancies.

It does **not** judge resemblance, skin quality, plausible gait/contact, face likeness, audio, effects, Unity controllers or network replication. Render review remains necessary. An image file being resolvable in the current folder does not prove the export is standalone on another machine.

## Invocation

Use the discovered Blender executable, quoted appropriately for the shell. For example in PowerShell:

```powershell
& 'D:\Blender 5.2\blender.exe' --background --factory-startup --python-exit-code 1 --python 'PATH_TO_SKILL\scripts\validate_dog_asset.py' -- --input 'OUTPUT\Dog_Rigged.blend' --report 'OUTPUT\validation.json'
```

- `--input`: `.blend`, `.fbx` or `.glb`/`.gltf`.
- `--report`: JSON destination; required, so output placement is explicit.
- `--rig`: armature object name when the file has more than one rig.
- `--actions`: comma-separated expected clip names, default `Idle,Run,Jump,Bark,Poop,Pee,Bite`.
- `--max-vertices`: optional budget for source mesh vertices; omit when no hard budget was requested.
- `--max-influences`: default 4.
- `--samples`: poses per action, default 5.

Apply a source-vertex budget to the native mesh. Imported FBX/GLB may have additional vertices split at normals, UVs or materials; do not reuse the native budget on those counts without accounting for that difference.

Run round-trip checks on export copies in a temporary validation directory, with any companion textures or external glTF buffers. Blender's FBX importer may extract embedded textures beside the imported file. The helper never saves over its input or changes the source model, but those importer-generated companion files are a possible side effect.

Blender must be launched with `--python-exit-code 1` so a failed script becomes a failed process. A failed validation still writes a report when possible. Fix demonstrated asset issues, then repeat only the affected checks. Stop retrying an unavailable executable, missing source image, missing dependency or external-service failure when no new evidence changes the cause; explain what is needed instead of claiming success.

## Review before delivery

Use front, side, three-quarter and back renders plus close-ups relevant to the reference. Review pose samples and an actual moving cycle. For a new face revision, compare the facial plane and cheek/chin silhouette in at least front and three-quarter views. For animation, inspect foot contact, knee/hock bends, jaw closure, and whether the same geometry remains valid through each action.

Report source vertex/triangle counts, bone count and clip names, along with which native/export checks ran. If an engine import is part of the task, measure its actual vertex count separately: flat normals, UV seams and material boundaries can split vertices. Distinguish Blender/render validation, engine playback, and real multiplayer tests in the final handoff.
