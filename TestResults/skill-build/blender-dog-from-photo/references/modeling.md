# Photo fitting and portable textures

## Fit the actual subject

Use the supplied image to establish the torso/neck/head proportions, shoulder and hip positions, paw contact, ear silhouette, and tail attachment/curl. A short-legged dog, a lean hound, and a shiba need different proportions. Infer unseen geometry conservatively; one image is not an exact 3D scan.

Construct editable surfaces with plausible joint transitions. Joining intersecting primitives followed by remeshing can create a useful starting surface, but inspect the union: inflated shoulders, disconnected paws, twin cheek bulbs, or a tail fused through its own curl need correction before skinning. Reduce geometry after the silhouette is stable; preserve enough loops around knees, hocks, shoulders, neck and jaw for animation.

Use meters and an intentional scale, with the character origin at the ground between its feet. Choose a documented forward/up convention and keep the modeling and rig scripts consistent with it. Evaluate the final exported mesh rather than the pre-decimation source when reporting counts.

## Faces and reference crops

For an ordinary dog, preserve the breed's muzzle, nose, lips and cheek structure. For a reference with a human face, preserve that deliberate hybrid instead of adding a canine snout over it. A projected photo needs real but restrained facial relief and an appropriate jaw silhouette; a flat sticker on a sphere is usually visibly distorted at an angle.

When a human face looks recessed, compare the eye/mouth/chin depth against the surrounding cheeks in a front and three-quarter render. Remove or narrow the underlying inflated cheek geometry and bring the facial plane forward as needed. Do not merely brighten the texture or add larger cheek spheres. A later face crop is evidence for the requested proportions, not permission to identify the person.

Keep texture projection inside valid source pixels and feather into compatible fur. Respect source alpha; transparent RGB, red cutout fringes or black background pixels must not be sampled onto the face. Keep dark eyes, nostrils and mouth detail: a global "remove dark pixels" rule destroys them. Avoid projecting photographic highlights/shadows as if they were accurate PBR detail, and disclose that limitation when relevant.

## Fur without excessive geometry

Use a consistent UV layout and a base-color atlas; bake procedural or photo-derived fur detail when portability matters. Add a subtle tangent-space normal map for micro-detail instead of geometrically modeling individual hairs. Normal maps use Non-Color data; base color uses the intended color space. Check a known source pixel and the saved PNG/render before applying a gamma conversion: Blender image-buffer and image-save behavior must not be guessed.

Keep UV island padding sufficient for mipmaps. Inspect seams, undersides, inner legs and the back of the head; a front photo alone does not supply those textures. Do not imply fur photographed on one side is reconstructed correctly on every hidden surface.

## Blender/Windows details worth checking

- Run the installed Blender executable headlessly for scripted modeling; inspect `bpy.app.version_string` and the actual operator properties rather than assuming an older API.
- `read_factory_settings(use_empty=True)` may leave `scene.world` unset. Create a world before configuring lighting.
- For Blender versions with `ImageFormatSettings.media_type`, set it to `VIDEO` before selecting `FFMPEG`; rendering PNGs uses `IMAGE`. Probe installed API support first.
- When a texture folder moves, stale paths can survive inside packed-image metadata. Relink to an existing current file; if necessary remove the old in-memory packed entry, reload the current file and pack again. Do not unpack over unrelated files.
- Keep render floor/cameras/lights out of selected character exports. Save the editable character and useful preview scene without silently importing that studio into the game asset.
