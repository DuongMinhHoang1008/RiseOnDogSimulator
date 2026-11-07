"""Build a rigged dog whose replaceable face is part of the head surface."""
import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT.parents[2] / "ReferenceDog" / "realistic_low_vertex" / "Dog_Rigged.blend"
MODEL = ROOT / "model"
PREVIEWS = ROOT / "previews"
CLIPS = ("Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite")
FACE_CENTER_Z = .840
FACE_HALF_WIDTH = .150
FACE_HALF_HEIGHT = .180


def active(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def is_face_point(co):
    radius = (co.x / FACE_HALF_WIDTH) ** 2 + ((co.z - FACE_CENTER_Z) / FACE_HALF_HEIGHT) ** 2
    return co.y < -.500 and radius < 1.0


def relink_texture(name, filename, noncolor=False):
    image = next((item for item in bpy.data.images if item.name.startswith(name)), None)
    if image is None:
        raise RuntimeError(f"Missing packed source image: {name}")
    path = MODEL / filename
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    image.filepath = str(path)
    image.colorspace_settings.name = "Non-Color" if noncolor else "sRGB"
    image.pack()
    return image


def blank_texture():
    path = MODEL / "BlankFace_Default.png"
    size = 512
    image = bpy.data.images.new("BlankFace_Default", width=size, height=size, alpha=True)
    pixel = (.73, .39, .17, 1.0)
    image.pixels[:] = pixel * (size * size)
    image.filepath_raw = str(path)
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)
    image = bpy.data.images.load(str(path))
    image.name = "BlankFace_Default"
    image.colorspace_settings.name = "sRGB"
    image.pack()
    return image


def extract_face_surface(body, rig):
    """Move the existing curved facial polygons into a separate renderer."""
    source = body.data
    chosen = [poly for poly in source.polygons if is_face_point(poly.center)]
    if len(chosen) < 40:
        raise RuntimeError(f"Facial polygon selection is unexpectedly small: {len(chosen)}")

    old_indices = sorted({index for poly in chosen for index in poly.vertices})
    remap = {old: new for new, old in enumerate(old_indices)}
    verts = [tuple(source.vertices[index].co) for index in old_indices]
    faces = [tuple(remap[index] for index in poly.vertices) for poly in chosen]
    min_x, max_x = min(co[0] for co in verts), max(co[0] for co in verts)
    min_z, max_z = min(co[2] for co in verts), max(co[2] for co in verts)

    mesh = bpy.data.meshes.new("FaceSurface_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    surface = bpy.data.objects.new("FaceSurface", mesh)
    bpy.context.collection.objects.link(surface)

    uv_layer = mesh.uv_layers.new(name="FaceUV")
    for polygon in mesh.polygons:
        polygon.use_smooth = True
        for loop_index in polygon.loop_indices:
            co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv_layer.data[loop_index].uv = (
                (co.x - min_x) / (max_x - min_x),
                (co.z - min_z) / (max_z - min_z),
            )

    material = bpy.data.materials.new("FaceSurface_Blank")
    material.use_nodes = True
    material.diffuse_color = (.73, .39, .17, 1)
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .86
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = blank_texture()
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    mesh.materials.append(material)

    group = surface.vertex_groups.new(name="Head")
    group.add(list(range(len(mesh.vertices))), 1.0, "REPLACE")
    modifier = surface.modifiers.new("Dog_Skin", "ARMATURE")
    modifier.object = rig
    surface.parent = rig
    surface["custom_face_surface"] = True
    surface["face_layout_version"] = 1
    surface["uv_contract"] = "full texture rectangle, U left-to-right, V bottom-to-top"
    surface["design"] = "zero-thickness facial polygons extracted from the dog head"

    bm = bmesh.new()
    bm.from_mesh(source)
    removed = [face for face in bm.faces if is_face_point(face.calc_center_median())]
    bmesh.ops.delete(bm, geom=removed, context="FACES")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(source)
    bm.free()
    source.update()
    return surface, len(removed)


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
    body.data.name = "BlankFaceDog_BodyMesh"
    relink_texture("Dog_BaseColor", "Dog_BaseColor.png")
    relink_texture("Dog_Normal", "Dog_Normal.png", True)
    face, removed_faces = extract_face_surface(body, rig)
    body["design"] = "dog body with facial polygons extracted as a replaceable surface"
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
        "extracted_face_polygons": removed_faces,
        "bones": len(rig.data.bones),
        "face_layout_version": 1,
        "face_uv": "0..1 full rectangle",
        "clips": {name: list(bpy.data.actions[name].frame_range) for name in CLIPS},
        "limitations": "face is rigid on Head; Bark/Bite move the head and jaw but do not deform the 2D player image",
    }
    (ROOT / "asset_report.json").write_text(json.dumps(stats, indent=2), encoding="utf-8")

    export_character(body, face, rig)
    render_views(rig)
    rig.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(1)
    for image in bpy.data.images:
        if image.name.startswith(("Dog_BaseColor", "Dog_Normal", "BlankFace_Default")):
            image.pack()
    active(rig)
    bpy.ops.wm.save_as_mainfile(filepath=str(MODEL / "BlankFaceDog_Rigged.blend"))
    print("BLANK_FACE_DOG_COMPLETE", json.dumps(stats), flush=True)


if __name__ == "__main__":
    main()
