"""Build a faceted dog around the existing animated quadruped rig."""
import bpy
import json
from math import cos, sin, tau
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT.parents[2] / "ReferenceDog" / "realistic_low_vertex" / "Dog_Rigged.blend"
MODEL = ROOT / "model"
PREVIEWS = ROOT / "previews"
CLIPS = ("Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite")
GINGER = (.71, .36, .15, 1)
CREAM = (.92, .74, .49, 1)
ACCENT = (.56, .24, .09, 1)


def matte(name, color):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    material.diffuse_color = color
    shader = material.node_tree.nodes.get("Principled BSDF")
    linear = tuple(((channel + .055) / 1.055) ** 2.4 for channel in color[:3])
    shader.inputs["Base Color"].default_value = (*linear, color[3])
    shader.inputs["Roughness"].default_value = .86
    return material


def face_material():
    path = MODEL / "BlankFace_Default.png"
    image = bpy.data.images.new("BlankFace_Bake", width=512, height=512, alpha=False)
    image.pixels[:] = GINGER * (512 * 512)
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)
    image = bpy.data.images.load(str(path))
    image.name = "BlankFace_Default"
    image.pack()

    material = matte("FaceSurface_Blank", GINGER)
    nodes = material.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    material.node_tree.links.new(texture.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"])
    return material


def profile(width, height, chamfer):
    if not chamfer:
        return [(-width, -height), (width, -height), (width, height), (-width, height)]
    return [
        (-width + chamfer, -height), (width - chamfer, -height),
        (width, -height + chamfer), (width, height - chamfer),
        (width - chamfer, height), (-width + chamfer, height),
        (-width, height - chamfer), (-width, -height + chamfer),
    ]


class Blocks:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.weights = []
        self.slots = []
        self.beveled = []

    def shell(self, rings, joints, material=0, band=None, caps=(True, True),
              soft_corners=False, soft_ends=False):
        sides = len(rings[0])
        first = len(self.vertices)
        for ring, joint in zip(rings, joints, strict=True):
            self.vertices.extend(ring)
            self.weights.extend([joint] * sides)
        if caps[0]:
            self.face(tuple(range(first, first + sides)), material)
        for ring_index in range(len(rings) - 1):
            near = first + ring_index * sides
            far = near + sides
            for i in range(sides):
                j = (i + 1) % sides
                slot = material if band is None else band[i]
                bevel = (soft_corners and i % 2 == 1) or (
                    soft_ends and ring_index in (0, len(rings) - 2)
                )
                self.face((near + j, near + i, far + i, far + j), slot, bevel)
        if caps[1]:
            last = first + (len(rings) - 1) * sides
            self.face(tuple(reversed(range(last, last + sides))), material)

    def face(self, indices, material, bevel=False):
        self.faces.append(indices)
        self.slots.append(material)
        self.beveled.append(bevel)

    def mesh(self, name, materials):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        for material in materials:
            mesh.materials.append(material)
        for polygon, slot, bevel in zip(mesh.polygons, self.slots, self.beveled, strict=True):
            polygon.material_index = slot
            polygon.use_smooth = bevel
        if any(self.beveled):
            # Blend narrow bevels toward adjacent broad planes without shading the planes themselves.
            weighted = [Vector() for _ in mesh.vertices]
            for polygon in mesh.polygons:
                for index in polygon.vertices:
                    weighted[index] += polygon.normal * polygon.area
            normals = [Vector() for _ in mesh.loops]
            for polygon in mesh.polygons:
                if polygon.use_smooth:
                    for index in polygon.loop_indices:
                        vertex = mesh.loops[index].vertex_index
                        normal = weighted[vertex].normalized()
                        joint = self.weights[vertex]
                        if isinstance(joint, str) and (joint == "Pelvis" or joint.startswith("RearUpper_")):
                            # Retain the sideward normal through the thin bevel instead
                            # of letting the upper plane dominate the highlight.
                            normal.z -= max(0, normal.z) * .52
                            normal.normalize()
                        normals[index] = normal
            mesh.normals_split_custom_set(normals)
        return mesh

    def skin(self, obj):
        weights = [{joint: 1.0} if isinstance(joint, str) else joint for joint in self.weights]
        names = dict.fromkeys(name for vertex_weights in weights for name in vertex_weights)
        groups = {name: obj.vertex_groups.new(name=name) for name in names}
        for index, vertex_weights in enumerate(weights):
            for name, weight in vertex_weights.items():
                groups[name].add([index], weight, "REPLACE")


def section(y, z, width, height, corner, x_center=0):
    return [(x_center + x, y, z + offset) for x, offset in profile(width, height, corner)]


def beam(blocks, bone, start, end, size_start, size_end, slot=0, soften=False):
    """A tapered prism aligned to one animated segment."""
    direction = (end - start).normalized()
    width_axis = Vector((1, 0, 0))
    depth_axis = width_axis.cross(direction).normalized()
    width_axis = direction.cross(depth_axis).normalized()
    chamfer = min(*size_start, *size_end) * .22 if soften else 0
    sections = [(start, size_start), (end, size_end)]
    if soften:
        inset = direction * chamfer
        sections = [
            (start, tuple(size - chamfer * .52 for size in size_start)),
            (start + inset, size_start),
            (end - inset, size_end),
            (end, tuple(size - chamfer * .52 for size in size_end)),
        ]
    rings = [
        [center + width_axis * x + depth_axis * z for x, z in profile(*size, chamfer)]
        for center, size in sections
    ]
    blocks.shell(rings, (bone,) * len(rings), slot, soft_corners=soften, soft_ends=soften)


def bone_beam(blocks, rig, name, start, end, size_start, size_end):
    bone = rig.data.bones[name]
    beam(blocks, name, bone.head_local.lerp(bone.tail_local, start),
         bone.head_local.lerp(bone.tail_local, end), size_start, size_end, soften=True)


def curved_tail(blocks):
    points = [
        (Vector((0, .422, .643)), .074, "Pelvis"),
        (Vector((0, .55, .784)), .071, "Tail_01"),
        (Vector((0, .546, .965)), .059, "Tail_02"),
        (Vector((.012, .365, 1.099)), .047, "Tail_03"),
        (Vector((.014, .204, 1.001)), .025, "Tail_04"),
        (Vector((.015, .166, .953)), .010, "Tail_04"),
    ]
    samples = [points[0]]
    for previous, current, following in zip(points, points[1:], points[2:]):
        center, radius, joint = current
        left = center.lerp(previous[0], .28)
        right = center.lerp(following[0], .28)
        left_radius = .72 * radius + .28 * previous[1]
        right_radius = .72 * radius + .28 * following[1]

        def weights(left_weight, right_weight):
            result = {joint: 1.0}
            for name, weight in ((previous[2], left_weight), (following[2], right_weight)):
                if weight and name != joint:
                    result[name] = result.get(name, 0) + weight
                    result[joint] -= weight
            return result

        samples.extend((
            (left, left_radius, weights(.28, 0)),
            (.25 * left + .50 * center + .25 * right,
             .25 * left_radius + .50 * radius + .25 * right_radius, weights(.07, .07)),
            (right, right_radius, weights(0, .28)),
        ))
    root, _, _ = samples[0]
    first_bend, _, _ = samples[1]
    samples[0] = (root + Vector((0, -.008, -.014)), .077, "Pelvis")
    samples.insert(1, (root.lerp(first_bend, .44) + Vector((0, -.008, .009)),
                       .072, {"Pelvis": .77, "Tail_01": .23}))

    tip, _, tip_joint = points[-1]
    before_tip, before_radius, _ = samples[-1]
    samples.extend((
        (before_tip.lerp(tip, .80), .20 * before_radius + .80 * .010, tip_joint),
        (tip, .006, tip_joint),
    ))

    rings = []
    for i, (center, radius, _) in enumerate(samples):
        tangent = (samples[min(i + 1, len(samples) - 1)][0] - samples[max(0, i - 1)][0]).normalized()
        cross = Vector((1, 0, 0)).cross(tangent).normalized()
        width = tangent.cross(cross).normalized()
        rings.append([center + width * x + cross * z for x, z in profile(radius, radius, radius * .34)])
    blocks.shell(rings, [joint for _, _, joint in samples], soft_corners=True, soft_ends=True)


def make_dog(body, rig):
    blocks = Blocks()
    blocks.shell(
        [section(*shape) for shape in (
            (-.31, .565, .137, .133, .043), (-.275, .560, .169, .160, .050),
            (-.16, .547, .208, .190, .060), (.19, .546, .202, .164, .052),
            (.41, .556, .199, .155, .054), (.475, .564, .174, .134, .049),
            (.51, .567, .147, .114, .045),
        )],
        ("Chest", "Chest", "Chest", "Spine", "Pelvis", "Pelvis", "Pelvis"),
        band=(1, 1, 0, 0, 0, 0, 0, 1), soft_corners=True,
    )
    bone_beam(blocks, rig, "Neck", -.36, 1.31, (.13, .126), (.146, .112))

    # Both renderers meet along the same octagon. The face bevel belongs to the
    # actual head shell, so a player image wraps over the front facets.
    center_z = .835
    seam = section(-.521, center_z, .164, .166, .035)
    face_front = [
        (x, -.601 + .24 * (z - center_z), z)
        for x, _, z in section(0, center_z - .010, .127, .122, .028)
    ]
    blocks.shell(
        [seam, section(-.442, .844, .209, .171, .048),
         section(-.317, .856, .182, .158, .047), section(-.251, .847, .113, .121, .04)],
        ("Head",) * 4, caps=(False, True),
    )
    for side, sign in (("L", -1), ("R", 1)):
        ear = f"Ear_{side}"
        root = Vector((sign * .137, -.395, .963))
        tip = Vector((sign * .216, -.368, 1.174))
        beam(blocks, ear, root, tip, (.064, .039), (.009, .008), slot=2)

        bone_beam(blocks, rig, f"FrontUpper_{side}", -.21, 1.15, (.072, .082), (.046, .046))
        bone_beam(blocks, rig, f"FrontLower_{side}", -.16, 1.13, (.048, .049), (.037, .033))
        bone_beam(blocks, rig, f"RearUpper_{side}", -.22, 1.19, (.082, .094), (.053, .052))
        bone_beam(blocks, rig, f"RearLower_{side}", -.17, 1.18, (.056, .052), (.041, .039))
        bone_beam(blocks, rig, f"RearAnkle_{side}", -.21, 1.18, (.044, .042), (.037, .033))
        for leg, y in (("Front", -.273), ("Rear", .373)):
            blocks.shell(
                [section(y + offset, .053, width, height, .015, sign * .139)
                 for offset, width, height in ((-.076, .045, .035), (-.040, .061, .049), (.052, .050, .049))],
                (f"{leg}Paw_{side}",) * 3, material=1, soft_corners=True,
            )

    curved_tail(blocks)

    materials = [matte("Dog_Ginger", GINGER), matte("Dog_Cream", CREAM), matte("Dog_Ear", ACCENT)]
    source_mesh = body.data
    body.data = blocks.mesh("BlankFaceDog_BodyMesh", materials)
    bpy.data.meshes.remove(source_mesh)
    body.vertex_groups.clear()
    blocks.skin(body)

    # Material-only body UVs let the exporters use a consistent mesh layout.
    body_uv = body.data.uv_layers.new(name="BodyUV")
    for polygon in body.data.polygons:
        for i, index in enumerate(polygon.loop_indices):
            angle = tau * i / polygon.loop_total
            body_uv.data[index].uv = (.5 + .5 * cos(angle), .5 + .5 * sin(angle))

    face_blocks = Blocks()
    face_blocks.shell([face_front, seam], ("Head", "Head"), caps=(True, False))
    mesh = face_blocks.mesh("FaceSurface_Mesh", [face_material()])
    face = bpy.data.objects.new("FaceSurface", mesh)
    bpy.context.collection.objects.link(face)
    uv = mesh.uv_layers.new(name="FaceUV")
    for loop in mesh.loops:
        co = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (max(0, min(1, .5 + co.x / .328)),
                                  max(0, min(1, .5 + (co.z - center_z) / .332)))
    face_blocks.skin(face)
    modifier = face.modifiers.new("Dog_Skin", "ARMATURE")
    modifier.object = rig
    face.parent = rig
    face["custom_face_surface"] = True
    face["face_layout_version"] = 1
    return face



def export_character(body, face, rig):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (body, face, rig):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(MODEL / "BlankFaceDog_Rigged.fbx"), use_selection=True,
        object_types={"MESH", "ARMATURE"}, apply_unit_scale=True,
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True,
        mesh_smooth_type="FACE", add_leaf_bones=False,
        use_armature_deform_only=True, armature_nodetype="NULL",
        bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0,
        bake_anim_step=1.0, path_mode="COPY", embed_textures=True,
    )
    bpy.ops.export_scene.gltf(
        filepath=str(MODEL / "BlankFaceDog_Rigged.glb"), use_selection=True,
        export_format="GLB", export_animations=True,
        export_animation_mode="ACTIONS", export_bake_animation=True,
        export_skins=True, export_yup=True, export_materials="EXPORT",
        export_morph=False,
    )


def render_views(rig):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 800
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    camera = bpy.data.objects.get("STUDIO_Hero_Camera")
    scene.camera = camera
    rig.animation_data.action = bpy.data.actions["Idle"]
    scene.frame_set(1)
    views = (
        ("front", (0, -4, 1.08), (0, -.03, .62), 1.47),
        ("side", (4, 0, 1.27), (0, .08, .62), 1.58),
        ("hero", (2.4, -4, 1.75), (0, .02, .62), 1.58),
        ("face", (0, -4, .92), (0, -.50, .84), .52),
        ("face_angle", (1.65, -4, 1.16), (0, -.49, .84), .57),
    )
    for label, position, target, scale in views:
        camera.location = position
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera.data.ortho_scale = scale
        scene.render.filepath = str(PREVIEWS / f"{label}.png")
        bpy.ops.render.render(write_still=True)


def main():
    MODEL.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    body = next(obj for obj in bpy.data.objects if obj.type == "MESH" and obj.find_armature())
    rig = body.find_armature()
    body.name = "BlankFaceDog_Body"
    face = make_dog(body, rig)
    body["face_layout_version"] = 1
    rig["animation_notes"] = "27-bone quadruped; seven baked clips; face surface follows Head"
    for action in bpy.data.actions:
        action.use_fake_user = True
    if set(CLIPS) != {action.name for action in bpy.data.actions}:
        raise RuntimeError("Source action set does not match the required seven clips")

    body.data.calc_loop_triangles()
    face.data.calc_loop_triangles()
    stats = {
        "source": str(SOURCE),
        "body_vertices": len(body.data.vertices),
        "face_vertices": len(face.data.vertices),
        "vertices_total": len(body.data.vertices) + len(face.data.vertices),
        "triangles_total": len(body.data.loop_triangles) + len(face.data.loop_triangles),
        "bones": len(rig.data.bones),
        "face_layout_version": 1,
        "face_uv": "0..1 full rectangle",
        "clips": {name: list(bpy.data.actions[name].frame_range) for name in CLIPS},
        "limitations": "face is rigid on Head; Bark/Bite move the head while the player image stays rigid",
    }
    (ROOT / "asset_report.json").write_text(json.dumps(stats, indent=2), encoding="utf-8")

    export_character(body, face, rig)
    render_views(rig)
    rig.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(1)
    bpy.data.images["BlankFace_Default"].filepath = "//BlankFace_Default.png"
    bpy.context.scene.render.filepath = "//../previews/hero.png"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(MODEL / "BlankFaceDog_Rigged.blend"))
    print("BLANK_FACE_DOG_COMPLETE", json.dumps(stats), flush=True)


if __name__ == "__main__":
    main()
