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
SOURCE = ROOT.parent / "PhotoDog_20261105" / "model" / "Dog_Rigged.fbx"
MODEL = ROOT / "model"
STEM = "PhotoDogCustomFace_Rigged"
CLIPS = ("Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite")
FPS = 30
FACE_CENTER_Z = .822
FACE_HALF_WIDTH = .172
FACE_HALF_HEIGHT = .157


def blank_texture():
    # Write exact sRGB bytes to the PNG and let Blender interpret them as sRGB.
    size = 512
    color = bytes((193, 127, 83))

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


def face_field(co):
    x, y, z = co
    radius = (x / FACE_HALF_WIDTH) ** 2 + ((z - FACE_CENTER_Z) / FACE_HALF_HEIGHT) ** 2
    # Bound the cut to the front-facing part of this model's head.
    return max(radius - 1, (y + .470) / .15)


def recess_lower_mouth_cavity(mesh):
    """Move the inherited cavity wall inside the chin while keeping the lips fixed."""
    slot = mesh.materials.find("Mouth_Interior")
    inner = {i for polygon in mesh.polygons if polygon.material_index == slot for i in polygon.vertices}
    outer = {i for polygon in mesh.polygons if polygon.material_index != slot for i in polygon.vertices}
    assert inner and not inner & outer
    for index in inner:
        co = mesh.vertices[index].co
        co.y += .026 * max(0, min(1, (.729 - co.z) / .034))
    mesh.update()


def cut_surface(source):
    """Split triangles crossed by the ellipse, keeping all points on the original surface."""
    positions = [vertex.co.copy() for vertex in source.vertices]
    weights = [{g.group: g.weight for g in vertex.groups if g.weight > 0} for vertex in source.vertices]
    field = [face_field(co) for co in positions]
    intersections = {}
    normals = [corner.vector.copy() for corner in source.corner_normals]
    uv = [loop.vector.copy() for loop in source.uv_layers[0].uv]
    corners = [(loop.vertex_index, normals[loop.index], uv[loop.index]) for loop in source.loops]
    fur = source.materials.find("Dog_Fur_and_ReferenceFace")

    def intersect(a, b):
        ai, an, au = a
        bi, bn, bu = b
        key = tuple(sorted((ai, bi)))
        t = field[ai] / (field[ai] - field[bi])
        if key not in intersections:
            combined = defaultdict(float)
            for vertex, fraction in ((ai, 1 - t), (bi, t)):
                for group, weight in weights[vertex].items():
                    combined[group] += fraction * weight
            strongest = sorted(combined.items(), key=lambda item: -item[1])[:4]
            total = sum(weight for _, weight in strongest)
            intersections[key] = len(positions)
            positions.append(positions[ai].lerp(positions[bi], t))
            weights.append({group: weight / total for group, weight in strongest})
        return intersections[key], an.lerp(bn, t).normalized(), au.lerp(bu, t)

    def clip(triangle, inside):
        result = []
        previous = triangle[-1]
        previous_kept = (field[previous[0]] < 0) == inside
        for current in triangle:
            kept = (field[current[0]] < 0) == inside
            if kept != previous_kept:
                result.append(intersect(previous, current))
            if kept:
                result.append(current)
            previous, previous_kept = current, kept
        return result

    body_polygons, face_polygons = [], []
    for triangle in source.loop_triangles:
        polygon = source.polygons[triangle.polygon_index]
        vertices = [corners[index] for index in triangle.loops]
        inside = [field[index] < 0 for index, _, _ in vertices]
        if polygon.material_index != fur or not any(inside):
            body_polygons.append((polygon, vertices))
        elif all(inside):
            face_polygons.append((polygon, vertices))
        else:
            body_polygons.append((polygon, clip(vertices, False)))
            face_polygons.append((polygon, clip(vertices, True)))
    return positions, weights, body_polygons, face_polygons, set(intersections.values())


def slice_mesh(source, positions, polygons, name, face_material=None):
    original_vertices = sorted({index for _, corners in polygons for index, _, _ in corners})
    remap = {old: new for new, old in enumerate(original_vertices)}
    local_positions = [positions[index] for index in original_vertices]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(local_positions, [], [[remap[i] for i, _, _ in corners] for _, corners in polygons])
    mesh.update()
    for material in ([face_material] if face_material else source.materials):
        mesh.materials.append(material)

    if face_material:
        uv = mesh.uv_layers.new(name="FaceUV")
        min_x, max_x = min(p.x for p in local_positions), max(p.x for p in local_positions)
        min_z, max_z = min(p.z for p in local_positions), max(p.z for p in local_positions)
    else:
        uv = mesh.uv_layers.new(name=source.uv_layers[0].name)

    normals = []
    for (old, corners), new in zip(polygons, mesh.polygons, strict=True):
        new.use_smooth = old.use_smooth
        new.material_index = 0 if face_material else old.material_index
        for (index, normal, original_uv), new_loop in zip(corners, new.loop_indices, strict=True):
            normals.append(normal[:])
            if face_material:
                x, _, z = positions[index]
                uv.uv[new_loop].vector = ((x - min_x) / (max_x - min_x), (z - min_z) / (max_z - min_z))
            else:
                uv.uv[new_loop].vector = original_uv
    mesh.normals_split_custom_set(normals)
    return mesh, original_vertices


def copy_skin(obj, group_names, vertex_weights, original_vertices):
    obj.vertex_groups.clear()
    for name in group_names:
        obj.vertex_groups.new(name=name)
    for index, original_index in enumerate(original_vertices):
        for group_index, weight in vertex_weights[original_index].items():
            obj.vertex_groups[group_index].add([index], weight, "REPLACE")


def split_face(body):
    source = body.data
    positions, vertex_weights, body_polygons, face_polygons, cut_vertices = cut_surface(source)
    material = bpy.data.materials.new("PhotoDogCustomFace_Blank")
    material.use_nodes = True
    material.use_backface_culling = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Roughness"].default_value = .84
    texture = material.node_tree.nodes.new("ShaderNodeTexImage")
    texture.image = blank_texture()
    texture.extension = "EXTEND"
    material.node_tree.links.new(texture.outputs["Color"], shader.inputs["Base Color"])

    face_mesh, face_indices = slice_mesh(source, positions, face_polygons, "FaceSurface_Mesh", material)
    body_mesh, body_indices = slice_mesh(source, positions, body_polygons, "PhotoDogCustomFace_BodyMesh")
    group_names = [group.name for group in body.vertex_groups]
    face = body.copy()
    face.name = "FaceSurface"
    face.data = face_mesh
    body.users_collection[0].objects.link(face)
    body.data = body_mesh
    body.name = "PhotoDogCustomFace_Body"
    copy_skin(body, group_names, vertex_weights, body_indices)
    copy_skin(face, group_names, vertex_weights, face_indices)
    face["face_layout_version"] = 1

    body_map = {original: i for i, original in enumerate(body_indices)}
    face_map = {original: i for i, original in enumerate(face_indices)}
    shared = sorted(body_map.keys() & face_map.keys())
    seam = [(body_map[i], face_map[i]) for i in shared]
    assert set(shared) == cut_vertices
    face_edges = defaultdict(int)
    for polygon in face_mesh.polygons:
        for edge in polygon.edge_keys:
            face_edges[edge] += 1
    boundary_edges = [edge for edge, count in face_edges.items()
                      if count == 1 and all(face_indices[i] in cut_vertices for i in edge)]
    body_edges = {tuple(sorted(body_indices[i] for i in edge.vertices)) for edge in body_mesh.edges}
    assert all(tuple(sorted(face_indices[i] for i in edge)) in body_edges for edge in boundary_edges)
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
    assert scene.render.fps / scene.render.fps_base == FPS
    rig, = [obj for obj in scene.objects if obj.type == "ARMATURE"]
    body, = [obj for obj in scene.objects if obj.type == "MESH"]
    for action in bpy.data.actions:
        action.name = action.name.rsplit("|", 1)[-1]
        action.use_fake_user = True
    assert {a.name for a in bpy.data.actions} == set(CLIPS)
    source_counts = mesh_counts(body)
    source_materials = [material.name for material in body.data.materials]
    relink_body_textures(body)
    recess_lower_mouth_cavity(body.data)
    face, seam, edge_count = split_face(body)
    body_counts, face_counts = mesh_counts(body), mesh_counts(face)
    assert source_counts["vertices"] + 2 * len(seam) == body_counts["vertices"] + face_counts["vertices"]
    assert source_counts["triangles"] + 2 * edge_count == body_counts["triangles"] + face_counts["triangles"]
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
        "lower_mouth_cavity_max_recess_m": .026,
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
