# DOG SIMULATOR — SOFT LOW-POLY STYLE GUIDE

> Version: 1.1  
> Status: Visual design baseline  
> Scope: Art direction, color palette, 3D asset modeling/materials, revisions to existing dog models, lighting, VFX, and PC UI/UX.  
> Intended users: Unity developers, Codex, 3D artists, and UI designers.

## 1. Visual identity

**Style:** Soft Low-Poly Cartoon — a cozy, playful suburban sandbox where the dogs and their silly interactions are the stars.

**Visual pillars (in priority order):**
1. **Readable:** Players instantly recognize dogs, interactable objects, and action feedback.
2. **Soft:** Rounded silhouettes, gentle light, low visual noise.
3. **Playful:** Slightly exaggerated proportions, expressive motion, whimsical details.
4. **Consistent:** Characters, environment, props, and UI share the same visual language.
5. **Efficient:** Practical for multiplayer scenes and many moving Rigidbody objects.

**Avoid:** neon-saturated greens, photorealism/PBR-heavy textures, hard cel-shading outlines, excessively sharp geometry, tiny busy decorations, glowing UI panels, realistic fur shaders, and generic asset-pack mismatches.

This guide is a **visual specification**, not a request to redesign gameplay, networking, collision logic, or game architecture.

---

## 2. Color system

### 2.1. World palette

| Role | Hex | Usage |
|---|---|---|
| Grass — main | `#92C96D` | Main grass fields |
| Grass — highlight | `#B7D98B` | Light patches and visual variety |
| Grass — shadow | `#6EA255` | Darker grass zones; subtle depth |
| Road | `#69767D` | Asphalt, low-saturation base |
| Pavement | `#C9CDD0` | Sidewalk surfaces |
| Pavement highlight | `#E7E3D9` | Curbs, bright stone paths |
| Sky | `#8FC9DF` | Clear, soft daylight |
| Cream | `#F3DFB8` | Building facades, neutral warm surfaces |
| Warm orange | `#F0AD67` | Shop fronts, playful accents |
| Brick / terracotta | `#CF826D` | Roofs, accents |
| Muted blue | `#75A9C5` | Vehicles, facades |
| Leaf green | `#78B775` | Foliage variations |
| Tree trunk brown | `#927358` | Bark, wood props |
| Dark outline / metal | `#4E5A5A` | Hardware, deep-detail color |

**Palette rules:**
- Use mostly mid-value, slightly warm colors; avoid extreme saturation across large surfaces.
- Environmental areas should use **1 dominant color + 1 support color + 1 accent**.
- Use warm accents sparingly so interactable items and important feedback stand out.
- Create variation by shifting hue/value slightly, not adding noisy surface textures.
- Maintain recognizable colors for quest objects; avoid making all interactables identical.
- These hex codes are **target display colors**, not guaranteed final Unity material values. Check under actual URP lighting and project color space.

### 2.2. UI palette

| Token | Hex | Usage |
|---|---|---|
| `ui.surface` | `#F8F2E6` | Popup and card surfaces |
| `ui.surfaceAlt` | `#E9DDC9` | Secondary UI surfaces |
| `ui.text` | `#303A36` | Primary text |
| `ui.textMuted` | `#69736E` | Helper text and secondary labels |
| `ui.accent` | `#E9A463` | Primary CTA / action highlights |
| `ui.accentHover` | `#F3B87D` | Hover state |
| `ui.secondary` | `#91B890` | Secondary buttons and supportive accents |
| `ui.border` | `#C8BDA9` | Subtle borders |
| `ui.danger` | `#CC7168` | Dangerous actions / warnings |
| `ui.success` | `#6FAD85` | Positive status |
| `ui.overlay` | `#26312C` at 45–60% alpha | Modal backdrop |

**Accessibility rule:** Do not assume the listed token combinations are accessible by default. Verify actual text contrast in final UI; aim for WCAG AA (at least 4.5:1 for normal text, 3:1 for large text) where practicable. Prefer dark text on pale surfaces, and check labels inside accent-colored buttons before shipping.

### 2.3. Gameplay highlight colors

- **Interactable candidate:** muted golden rim/highlight, e.g. `#F4C77C`.
- **Valid interaction:** brief warm highlight + small icon prompt, not full-screen bloom.
- **Unavailable action:** disabled neutral (`ui.textMuted`) plus succinct reason, when needed.
- **Quest-critical target:** distinctive silhouette and a restrained visual indicator; distinguish it from decorative props.
- **Player nameplates:** neutral light text with subtle dark shadow/outline. Do **not** color-code ownership or teams unless there is an actual gameplay need.

---

## 3. Shape language & modeling rules

### 3.1. Shared asset rules

- **Soft, chunky shapes:** clear primary forms; gentle bevels/rounding at high-visibility edges.
- **Low-poly is a style, not a polygon limit:** spend polygons on silhouette and deformation, not hidden details.
- **Controlled exaggeration:** add charm without reducing collision readability.
- **No photoreal surface noise:** primarily flat colors, subtle gradients/vertex-color variation when needed.
- **Consistent detail density:** hero/interactive objects > near scenery > distant decoration.
- **Readable at actual game-camera distance:** test at the expected PC gameplay resolution, not only in close-up model previews.
- **Scale and pivots:** use real Unity world scale consistently (approximately 1 unit = 1 meter if already established); preserve existing gameplay-required collider dimensions and pivots unless explicitly approved.
- **Performance:** reuse material instances/atlases and simple meshes where possible; avoid unnecessary unique materials or per-object update scripts.

### 3.2. Dog characters

- Preserve each dog's breed identity and distinctive silhouette.
- Prefer slightly enlarged head, readable ears/muzzle, soft torso and paws, expressive tail. **Do not force a single body proportion onto every breed.**
- Eyes and face need to remain recognizable from the normal third-person camera.
- Use simple fur material/color blocks; no strand-based fur rendering.
- Rigs should support playful anticipation, impact, stumble, ragdoll/knockdown, bite, bark, and carry states **where already part of gameplay**.
- Animation may exaggerate secondary motion, but actual hitboxes and gameplay timing must remain reliable.
- Character palettes should contrast with grass and asphalt in typical gameplay areas.
- A small, soft contact shadow helps anchor each dog to the ground.

#### Existing dog models in RiseOnDogSimulator

Audit the three visuals in `DogModelCatalog.asset`: Reference Dog (fallback, ID 1), Photo Dog (ID 1001), and Blank Face Dog (ID 1101). Review each in the selection preview and at normal gameplay distance against grass and road.

- Use Blank Face Dog as the P0 pilot. Edit its existing visual mesh where needed to soften the outline of the ears, torso, and paws. Simplify small details that disappear at gameplay distance and adjust fur color blocks to separate the dog from the background. Preserve the silhouette and proportions that identify the dog.
- Keep the existing model ID, catalog entry, prefab and asset references, root pivot, scale, rig hierarchy, animation controller, clips and events. Preserve gameplay colliders, hitboxes, attachment points (including `MouthAnchor` and `BackSupport`), and gameplay timing. Check skin weights after reshaping any deforming area.
- Preserve each model’s Custom Face v1 contract. Keep its `FaceSurface` binding, renderer and material assignment, UV orientation and framing, and `faceLayoutVersion`. If the surrounding head mesh changes, check the seam at the front and in profile. Verify that an existing saved face still aligns in customization, gameplay, and on a second client.
- Use the pilot to establish the target for Photo Dog and Reference Dog. In P1, adjust each existing mesh and material where the side-by-side review shows a need, then repeat the rig, Custom Face, and gameplay checks for that model.

### 3.3. Environment

**Trees:**
- Replace repeated tall triangular trees as the only tree type.
- Use 3–4 repeatable silhouettes: rounded canopy, layered soft-cone, compact bushy tree, and shrub.
- Slight height/rotation/color variance; avoid trees blocking player view or gameplay targets.

**Roads and paths:**
- Large smooth color fields with a little tonal variation.
- Curbs and crosswalks should have simplified, high-contrast but not glaring forms.
- Sparse decals: road markings, utility covers, soft patch marks. Avoid visual clutter.

**Buildings:**
- Simplified blocky volumes with softly beveled key edges.
- Oversized but readable doors/windows and warm accent colors.
- Consistent scale for doors, steps, fences, and places players can reach.

**Props:**
- Each interactive prop should be identifiable by shape and affordance, not color alone.
- Distinguish **decorative**, **interactive**, and **quest-critical** props visually.
- For throwable/movable props, do not change collider behavior or physics feel for aesthetics without explicit approval.
- Keep quest-critical props uncluttered and visible, especially on grass/roads.

---

## 4. Lighting, materials & VFX

### 4.1. Lighting

- Mood: clear, pleasant late-morning to soft-afternoon daylight.
- One primary directional sun; soft-ish shadows; moderate ambient fill.
- Subtle ambient occlusion/grounding only if it remains performant on target hardware.
- Static environment lighting may be baked where feasible; dynamic dog and physics-prop lighting stays responsive.
- Avoid hard black shadows, heavy bloom, extreme fog, high chromatic aberration, and aggressive color grading.

### 4.2. Materials

- Use **URP-compatible Lit/Simple Lit** or an existing lightweight stylized shader.
- Prefer color blocks and subtle smooth shading; no harsh toon bands by default.
- Materials should be matte to mildly satin, not wet/plastic-shiny.
- Avoid introducing a custom shader unless the existing material system cannot meet the approved look.
- Prefer material variants/shared properties rather than generating many distinct material assets.

### 4.3. VFX

- Small cartoon dust puffs for running/landing.
- Compact impact burst/stars for comedic collisions.
- Brief expressive icons for bark or interactions, when they aid readability.
- Keep VFX short, local, and low-cost; avoid hiding players behind particles.
- Multiplayer cosmetic VFX should follow current network-event architecture; **do not introduce network traffic for purely local presentation without review**.

---

## 5. PC UI visual language

### 5.1. Core UI principles

1. **Minimal HUD:** the world and dogs should dominate the screen.
2. **Context over permanence:** show prompts when usable; do not keep an always-visible control manual on screen.
3. **Rounded and tactile:** pill/chip buttons, softly rounded panels, warm surfaces, restrained shadows.
4. **Consistent hierarchy:** text and icons must remain readable at common desktop resolutions.
5. **Safe edges:** keep important information away from extreme corners and prevent overlap with scene objectives.

### 5.2. HUD layout — PC gameplay

- **Top-left:** small Settings/Pause entry, unobtrusive; show room/session info only if needed.
- **Above dogs:** short player nameplates at controlled distance; fade or hide when irrelevant/occluded to avoid clutter.
- **Bottom-center:** one context interaction prompt when relevant (`LMB · Pick Up`, `E · Drop`, etc.).
- **Bottom-right:** compact action hints grouped into no more than a few lines. Prioritize gameplay actions; collapse secondary/unavailable actions if they add clutter.
- **Quest objective:** a small, collapsible or short single-line panel only when a quest is active, placed so it does not collide with settings/nameplates/action hints.
- **No permanent left-side controls panel** in the gameplay HUD.

**Control hints must read from the actual input-binding source** rather than hardcoding labels that may become outdated. Never silently change mappings to match mockups.

### 5.3. UI sizes & spacing (starting values, not fixed across all resolutions)

| Token | Baseline |
|---|---|
| Base spacing | 8 px (multiples: 4 / 8 / 12 / 16 / 24) |
| Small pill radius | 10–14 px |
| Main panel radius | 16–20 px |
| HUD keyboard label | 13–15 px equivalent |
| Main action text | 15–17 px equivalent |
| Screen title | 26–34 px equivalent |
| Button height | 40–48 px |
| Edge safe margin | 20–32 px |
| HUD stack gap | 6–8 px |

- Use TextMeshPro or the project's established text system.
- Prefer one friendly, legible typeface family with regular/semibold/bold hierarchy; avoid multiple decorative fonts.
- For Unity UI, test actual 16:9 and 16:10 layouts and common scale factors. Treat numbers above as design references, not unverified pixel-perfect guarantees.

### 5.4. UI states and motion

- **Normal:** neutral cream/dark text.
- **Hover:** slightly brighter surface, subtle scale or shadow lift.
- **Pressed:** tiny inward compression, no heavy flash.
- **Disabled:** reduced contrast with retained legibility; do not imply availability.
- **Focused (keyboard/gamepad where supported):** visible distinct focus outline.
- **Motion:** quick and restrained (roughly 100–200 ms for small transitions); avoid bouncing loops on every HUD control.
- **Accessibility:** support sufficient contrast, icon+text where possible, and reduced motion if already supported by game settings.

### 5.5. Screens and overlays

- **Home:** left-aligned vertical menu, large breathing room, playful character presentation to the right.
- **Character preparation/customization:** clear large dog preview; controls grouped by task; fewer simultaneous panels.
- **Online hub/lobby:** room code, occupancy, join/create/ready clearly prioritized; avoid clutter.
- **Settings:** simple categories and readable controls; modal or dedicated screen, not a dense desktop-style form.
- All screens should use the same tokens, icon weights, rounding, text hierarchy, and focus/hover feedback.

---

## 6. Implementation boundaries for Codex

### MUST

1. Audit existing UI prefabs, materials, shaders, fonts, render settings, and reusable components **before** editing.
2. Keep changes scoped to **visual presentation only**, unless gameplay changes are explicitly requested.
3. Reuse existing design tokens/components where possible; centralize new color, spacing, and typography values.
4. Preserve input actions, gameplay behaviors, network authority, RPCs, physics, scene references, and serialization.
5. Use the project's current Unity version, URP version, UI stack, and asset import pipeline; do not upgrade dependencies just to obtain an aesthetic effect.
6. Make changes in small, reviewable increments and list impacted assets/prefabs/scenes.
7. Check the in-game third-person camera, not just Inspector previews.
8. Verify UI at different desktop aspect ratios and under common gameplay conditions.

### MUST NOT

- Replace working gameplay code with placeholder implementations.
- Change network architecture or physics ownership to support cosmetic visual effects.
- Mass-replace models/shaders in unrelated scenes.
- Introduce expensive full-screen effects, dynamic shadow lights on every prop, or custom shaders without profiling and approval.
- Hardcode keybind display strings into unrelated UI components.
- Make quest-critical objects visually harder to distinguish from scenery.
- Alter asset pivots, collider boundaries, or rig topology without checking downstream consequences.

### SHOULD

- Define color tokens as a shared asset or documented theme configuration.
- Establish a small reusable UI kit: `PrimaryButton`, `SecondaryButton`, `KeyHint`, `InteractionPrompt`, `Nameplate`, `QuestChip`, `PopupPanel` (naming is illustrative; follow existing conventions).
- Use prefab variants and shared materials to reduce duplicated styling.
- Profile representative scenes before/after visual changes.

---

## 7. Rollout order

### P0 — Quick visual improvement

- [ ] Apply world palette to grass, road, pavement, and common facades.
- [ ] Tune sunlight, ambient fill, and shadows to a softer daytime look.
- [ ] Add 3–4 consistent tree/foliage silhouettes and remove obvious repetitive placement.
- [ ] Review all three catalog dog models and complete a mesh and material pilot on Blank Face Dog (§3.2).
- [ ] Standardize existing HUD colors, rounded shapes, typography, and spacing.
- [ ] Reduce permanent HUD clutter; retain essential action clarity.

### P1 — Consistency pass

- [ ] Apply the approved dog style to Photo Dog and Reference Dog where needed, following the per-model checks in §3.2.
- [ ] Align key prop shapes to the new style.
- [ ] Add restrained visual variation to streets and building fronts.
- [ ] Clarify interactive and quest-critical object cues.
- [ ] Style home, character customization, lobby, and settings with the same tokens.

### P2 — Polish

- [ ] Add small comedic VFX and micro-animations.
- [ ] Improve contact shadows, subtle gradients, and character readability.
- [ ] Profile rendering and UI overdraw in typical multiplayer conditions.

---

## 8. Acceptance checklist

- [ ] Screenshot looks cohesive: dogs, trees, buildings, props, and UI feel part of one game.
- [ ] No dominant neon green / harsh asphalt-vs-grass contrast.
- [ ] Each revised dog stays distinguishable on grass and road at gameplay distance. Compare before/after from the same camera and inspect front, side, and rear views.
- [ ] Each revised dog deforms cleanly during locomotion, bark, bite, and knockdown where supported. Check carry alignment and grounding with the existing gameplay prefab.
- [ ] For each revised Custom Face model, verify the default face and an existing saved face in customization and gameplay, including a second client. Check head seams, clipping, and texture alignment in profile and during head motion.
- [ ] Interactive and quest-critical props can be recognized without guesswork.
- [ ] HUD feels compact on PC; no unnecessary left-side control legend.
- [ ] Input hints are accurate to current bindings.
- [ ] UI remains readable at 1920×1080 and at least one smaller 16:9 and one 16:10 test resolution.
- [ ] No broken prefab/scene references; no unintended physics or network changes.
- [ ] Visual improvements do not cause unacceptable frame-time/overdraw regressions.

## 9. Copy-ready Codex task prompt

> Read `DOG_SIMULATOR_SOFT_LOW_POLY_STYLE_GUIDE.md` and inspect the project's existing scenes, prefabs, materials, UI components, and rendering setup. Implement **P0 only** as a presentation-only change, including the existing Blank Face Dog mesh and material pilot defined in §3.2. Audit Photo Dog and Reference Dog during P0 and schedule their visual edits for P1. First summarize the current visual system, identify the minimum files to edit, and list potential risks. Reuse existing systems instead of introducing parallel frameworks. Preserve gameplay, networking, physics, input bindings, colliders, and serialization. Centralize reusable UI color and spacing tokens. Make small, testable changes, and report modified files plus verification results. Do not alter unrelated assets or start P1/P2 without an explicit request.
