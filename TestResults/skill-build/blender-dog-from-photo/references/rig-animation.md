# Quadruped rig and basic clips

## Skeleton and skin

A practical small rig has a root, pelvis/spine/chest, neck/head/jaw, a few tail joints, and distinct front/rear limb chains. Optional ear bones are useful for expression. A compact rig around 25–35 bones is a starting point, not an exact requirement.

Fit shoulders, elbows, wrists, hips, stifles, hocks and paws to the mesh. Rear legs have a hock/ankle arrangement; treating them as straight front legs makes squats and leg lifts look wrong. Preserve planted feet with IK or an analytic limb solve when authoring. Pole/bend directions must be intentional and leg lengths should not stretch unintentionally.

Weights should follow anatomical regions, blend smoothly at joints, be normalized, and normally use at most four deform-bone influences per vertex for a small game character. Do not let broad torso or cheek vertices follow a nearby paw or jaw merely because it is the closest bone. Tail weights must not capture the entire rump.

Use an actual mouth opening for Bark/Bite. On a photo-textured face, deforming a closed surface only stretches the painted smile. A controlled lip seam and a small interior can be enough for basic clips; the closed pose must hide the interior, and jaw weights should not pull the upper face or surrounding fur apart. Keep accessory weights and materials attached to the intended jaw/head bones after joining meshes.

## Motion criteria

- Idle/Run: first and last poses should meet without a visible pop. Decide whether the Run is a trot or gallop and coordinate limb phases accordingly; do not label a single lifted-foot pose as a running animation.
- Jump: show anticipation, takeoff, an airborne pose, and landing. Contact frames and body height should agree; inspect the whole cycle.
- Bark/Bite: time the head/neck with jaw motion; make Bite's close/snap distinct from Bark's repeated opening.
- Poop: shift weight and crouch using the hind joints; avoid collapsing the pelvis mesh into the torso.
- Pee: transfer support to the other three legs while lifting a hind leg; keep the supporting paws planted.

Author complete actions that return to a sensible locomotion pose. Prefer 30 fps for the default deliverable unless the target specifies otherwise. Bake evaluated transforms needed for export; keep every named action associated with the intended armature. In Blender's layered-action API, validate slots as well as action names.

## Portable exports

Select the armature and character mesh only. Export skin, normals, UVs, materials, and every requested action. Do not add unnecessary leaf bones; do not lose an unweighted but necessary root/parent transform. Use consistent clip names across native, FBX and GLB exports. Exporters can enumerate inactive actions differently, so inspect the imported result.

Re-import into a clean temporary validation directory so FBX texture extraction cannot overwrite user assets. Check the armature, skin and actual deformed vertices. Some GLB importers create an `Icosphere` or other custom bone-display object: identify helpers through pose-bone `custom_shape` references, not by treating every imported mesh as part of the character or by blindly excluding a name.

Check texture pixels, not only image datablock names or file paths. Native packed textures, embedded GLB/FBX textures, and external companion PNGs are different guarantees. Report the actual delivery method.

## Only when game integration is requested

Inspect the existing engine project and use its current motor, input and networking. For Unity, use Generic rather than Humanoid and create a gameplay controller/prefab distinct from a showcase that cycles through clips automatically. Remove demo playback components from the playable visual. Preserve the player's colliders, authority components, references and prefab identity.

If the motor already moves the character vertically, use an adapted gameplay Jump clip or an explicit root-motion policy so the visual does not add a second jump. Keep the original exported demo clip intact. Match animation state and playback time to accepted movement/actions rather than raw key-down events; reject air-jump animation resets and clear buffered buttons when controls are disabled.

For multiplayer, let the existing authority own simulation/state changes; remote visual presentation should not run its own input or motor. Repeated actions and late join may need a sequence plus an authoritative start/end time. Do not replace the project's network architecture, add RPC effects, or claim multi-client verification from an offline test.
