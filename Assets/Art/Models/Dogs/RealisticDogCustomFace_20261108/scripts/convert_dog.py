"""Convert the reference FBX in Blender 5.2, preserving its original skinned surface.

Run with Blender --background --factory-startup --python-exit-code 1 --python this_file.
"""

from collections import defaultdict
import json
from pathlib import Path
from shutil import copyfile
import struct
import zlib

import bpy
import numpy as np


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT.parents[2] / "ReferenceDog" / "realistic_low_vertex" / "Dog_Rigged.fbx"
MODEL = ROOT / "model"
STEM = "RealisticDogCustomFace_Rigged"
CLIPS = ("Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite")
FPS = 30
FACE_CENTER_Z = .817
FACE_HALF_WIDTH = .151
FACE_HALF_HEIGHT = .157


def blank_texture():
    # Write exact sRGB bytes to the PNG and let Blender interpret them as sRGB.
    size = 512
    color = bytes((190, 128, 83))

    def chunk(kind, data):
        checksum = zlib.crc32(kind + data)
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", checksum)

    path = MODEL / "BlankFace_Default.png"
    header = struct.pack(">IIBBBBB", size, size, 8, 2, 0, 0, 0)
    rows = (b"\0" + color * size) * size
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
                     + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))
    image = bpy.data.images.load(str(path))
    image.pack()
    return image


def relink_body_textures(body):
    replacements = {}
    for filename, color_space in (("Dog_BaseColor.png", "sRGB"), ("Dog_Normal.png", "Non-Color")):
        copyfile(SOURCE.with_name(filename), MODEL / filename)
        image = bpy.data.images.load(str(MODEL / filename))
        image.colorspace_settings.name = color_space
        image.pack()
        replacements[filename] = image
    albedo = replacements["Dog_BaseColor.png"]
    pixels = np.empty(len(albedo.pixels), dtype=np.float32)
    albedo.pixels.foreach_get(pixels)
    assert (pixels[3::4] == 1).all(), "The source albedo must be fully opaque"
    for material in body.data.materials:
        tree = material.node_tree
        texture_nodes = [node for node in tree.nodes if node.type == "TEX_IMAGE" and node.image]
        for node in texture_nodes:
            filename = Path(node.image.filepath).name
            node.image = replacements[filename]
            if node.image == albedo:
                for link in list(node.outputs["Alpha"].links):
                    tree.links.remove(link)
            if not any(output.is_linked for output in node.outputs):
                tree.nodes.remove(node)


def recess_mouth_interior(mesh):
    """Clear the inherited cavity/tongue intersection with the outside of the chin."""
    offsets = {"Mouth_Interior": .028, "Tongue": .018}
    vertices = defaultdict(set)
    for polygon in mesh.polygons:
        material_name = mesh.materials[polygon.material_index].name
        vertices[material_name].update(polygon.vertices)
    for material_name, offset in offsets.items():
        others = set().union(*(indices for name, indices in vertices.items() if name != material_name))
        assert not vertices[material_name] & others
        for index in vertices[material_name]:
            mesh.vertices[index].co.y += offset
    mesh.update()


def face_selection(mesh):
    """Choose the front cap of the fur mesh. Keep the original lips and jaw weights."""
    fur = mesh.materials.find("Dog_Fur_and_ReferenceFace")
    selected = set()
    neighbors = defaultdict(set)
    edge_faces = defaultdict(list)
    fit_cost = {}
    for polygon in mesh.polygons:
        if polygon.material_index != fur:
            continue
        x, y, z = polygon.center
        radius = (x / FACE_HALF_WIDTH) ** 2 + ((z - FACE_CENTER_Z) / FACE_HALF_HEIGHT) ** 2
        if y < -.455 and radius < 1:
            selected.add(polygon.index)
        if y < -.455 and radius < 1.6:
            fit_cost[polygon.index] = polygon.area * (radius - 1)
        for edge in polygon.edge_keys:
            edge_faces[edge].append(polygon.index)
    for adjacent in edge_faces.values():
        for index in adjacent:
            neighbors[index].update(other for other in adjacent if other != index)

    # Keep the outline near the fitted ellipse while preferring a shorter seam.
    edge_lengths = {edge: (mesh.vertices[edge[0]].co - mesh.vertices[edge[1]].co).length
                    for edge in edge_faces}
    for _ in range(4):
        for index, cost in fit_cost.items():
            inside = index in selected
            adjacent = neighbors[index]
            if len(adjacent) != 3 or not adjacent & selected or adjacent <= selected:
                continue
            border_delta = 0.
            for edge in mesh.polygons[index].edge_keys:
                other, = (neighbor for neighbor in edge_faces[edge] if neighbor != index)
                border_delta += edge_lengths[edge] * (-1 if other in selected else 1)
            addition_cost = cost + .011 * border_delta
            if inside and addition_cost > 0:
                selected.remove(index)
            elif not inside and addition_cost < 0:
                selected.add(index)
    nose = min(selected, key=lambda i: mesh.polygons[i].center.y)
    connected = {nose}
    pending = [nose]
    while pending:
        index = pending.pop()
        new = (neighbors[index] & selected) - connected
        connected.update(new)
        pending.extend(new)
    return connected, edge_faces


def slice_mesh(source, indices, name, face_material=None):
    """Copy only the requested polygons, with their original corner normals and winding."""
    polygons = [source.polygons[index] for index in sorted(indices)]
    original_vertices = sorted({index for polygon in polygons for index in polygon.vertices})
    remap = {old: new for new, old in enumerate(original_vertices)}
    positions = [source.vertices[index].co[:] for index in original_vertices]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(positions, [], [[remap[i] for i in polygon.vertices] for polygon in polygons])
    mesh.update()
    for material in ([face_material] if face_material else source.materials):
        mesh.materials.append(material)

    if face_material:
        uv = mesh.uv_layers.new(name="FaceUV")
        min_x, max_x = min(p[0] for p in positions), max(p[0] for p in positions)
        min_z, max_z = min(p[2] for p in positions), max(p[2] for p in positions)
    else:
        uv = mesh.uv_layers.new(name=source.uv_layers[0].name)

    normals = []
    for old, new in zip(polygons, mesh.polygons, strict=True):
        new.use_smooth = old.use_smooth
        new.material_index = 0 if face_material else old.material_index
        for old_loop, new_loop in zip(old.loop_indices, new.loop_indices, strict=True):
            normals.append(source.corner_normals[old_loop].vector[:])
            if face_material:
                x, _, z = mesh.vertices[mesh.loops[new_loop].vertex_index].co
                uv.uv[new_loop].vector = ((x - min_x) / (max_x - min_x), (z - min_z) / (max_z - min_z))
            else:
                uv.uv[new_loop].vector = source.uv_layers[0].uv[old_loop].vector
    mesh.normals_split_custom_set(normals)
    return mesh, original_vertices


def copy_skin(obj, group_names, vertex_weights, original_vertices):
    obj.vertex_groups.clear()
    for name in group_names:
        obj.vertex_groups.new(name=name)
    for index, original_index in enumerate(original_vertices):
        for group_index, weight in vertex_weights[original_index]:
            obj.vertex_groups[group_index].add([index], weight, "REPLACE")


def split_face(body):
    source = body.data
    selected, edge_faces = face_selection(source)
    outside = set(range(len(source.polygons))) - selected
    material = bpy.data.materials.new("RealisticDogCustomFace_Blank")
    material.use_nodes = True
    material.use_backface_culling = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .84
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = blank_texture()
    texture.extension = "EXTEND"
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])

    face_mesh, face_indices = slice_mesh(source, selected, "FaceSurface_Mesh", material)
    body_mesh, body_indices = slice_mesh(source, outside, "RealisticDogCustomFace_BodyMesh")
    group_names = [group.name for group in body.vertex_groups]
    vertex_weights = [[(g.group, g.weight) for g in v.groups] for v in source.vertices]
    face = body.copy()
    face.name = "FaceSurface"
    face.data = face_mesh
    body.users_collection[0].objects.link(face)
    body.data = body_mesh
    body.name = "RealisticDogCustomFace_Body"
    copy_skin(body, group_names, vertex_weights, body_indices)
    copy_skin(face, group_names, vertex_weights, face_indices)
    face["face_layout_version"] = 1

    body_map = {original: i for i, original in enumerate(body_indices)}
    face_map = {original: i for i, original in enumerate(face_indices)}
    shared = sorted(body_map.keys() & face_map.keys())
    seam = [(body_map[i], face_map[i]) for i in shared]
    boundary_edges = [edge for edge, adjacent in edge_faces.items()
                      if any(index in selected for index in adjacent)
                      and any(index in outside for index in adjacent)]
    assert set(shared) == {i for edge in boundary_edges for i in edge}
    for left, right in seam:
        assert body.data.vertices[left].co == face.data.vertices[right].co
    return face, seam, len(boundary_edges)


def clip_at(rig, action, frame):
    rig.animation_data.action = action
    rig.animation_data.action_slot = action.slots[0]
    bpy.context.scene.frame_set(int(frame), subframe=frame - int(frame))


def verify_animated_seam(body, face, rig, seam):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    render = bpy.context.scene.render
    scene_fps = render.fps / render.fps_base
    distances = {}
    for clip in CLIPS:
        action = bpy.data.actions[clip]
        max_delta = 0.
        start, end = action.frame_range
        intervals = round((end - start) * FPS / scene_fps)
        for frame in np.linspace(start, end, intervals + 1):
            clip_at(rig, action, frame)
            posed_body = body.evaluated_get(depsgraph)
            posed_face = face.evaluated_get(depsgraph)
            for body_index, face_index in seam:
                a = posed_body.matrix_world @ posed_body.data.vertices[body_index].co
                b = posed_face.matrix_world @ posed_face.data.vertices[face_index].co
                max_delta = max(max_delta, (a - b).length)
        assert max_delta < 1e-6, (clip, max_delta)
        distances[clip] = max_delta
    return distances


def export(rig, body, face):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (rig, body, face):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(MODEL / f"{STEM}.fbx"), use_selection=True,
        object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, add_leaf_bones=False, mesh_smooth_type="OFF",
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0., path_mode="COPY", embed_textures=True,
    )
    bpy.ops.export_scene.gltf(
        filepath=str(MODEL / f"{STEM}.glb"), use_selection=True, export_format="GLB",
        export_animations=True, export_animation_mode="ACTIONS", export_bake_animation=True,
        export_skins=True, export_yup=True, export_materials="EXPORT", export_morph=False,
    )


def mesh_counts(obj):
    obj.data.calc_loop_triangles()
    return {"vertices": len(obj.data.vertices), "triangles": len(obj.data.loop_triangles)}


def main():
    MODEL.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(SOURCE))
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.fps = FPS
    rig, = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    body, = [obj for obj in scene.objects if obj.type == "MESH"]
    for action in bpy.data.actions:
        action.name = action.name.rsplit("|", 1)[-1]
        action.use_fake_user = True
    assert {a.name for a in bpy.data.actions} == set(CLIPS)
    source_counts = mesh_counts(body)
    source_materials = [material.name for material in body.data.materials]
    relink_body_textures(body)
    recess_mouth_interior(body.data)
    face, seam, edge_count = split_face(body)
    body_counts, face_counts = mesh_counts(body), mesh_counts(face)
    assert source_counts["triangles"] == body_counts["triangles"] + face_counts["triangles"]
    assert source_counts["vertices"] + len(seam) == body_counts["vertices"] + face_counts["vertices"]
    distances = verify_animated_seam(body, face, rig, seam)

    clip_at(rig, bpy.data.actions["Idle"], 1)
    bpy.context.preferences.filepaths.save_version = 0
    export(rig, body, face)
    for image in bpy.data.images:
        if image.filepath and Path(image.filepath).parent == MODEL and image.packed_file:
            image.filepath = f"//{Path(image.filepath).name}"
    bpy.ops.wm.save_as_mainfile(filepath=str(MODEL / f"{STEM}.blend"), relative_remap=False)
    report = {
        "source": SOURCE.relative_to(ROOT.parents[4]).as_posix(),
        "blender": bpy.app.version_string,
        "coordinates": {"forward": "-Y", "up": "+Z", "horizontal": "+X", "unit_scale": 1},
        "source_mesh": source_counts,
        "source_materials": source_materials,
        "mouth_interior_adjustment": {"Mouth_Interior_y": .028, "Tongue_y": .018},
        "source_armature": rig.name,
        "body": body_counts,
        "face": face_counts,
        "seam": {"shared_vertices": len(seam), "shared_edges": edge_count,
                 "max_gap_per_clip_scene_units": distances},
        "bones": len(rig.data.bones),
        "clips": {name: list(bpy.data.actions[name].frame_range) for name in CLIPS},
        "fps": scene.render.fps,
        "face_layout_version": 1,
    }
    (ROOT / "asset_report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
