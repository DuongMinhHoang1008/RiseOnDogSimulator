"""Build the boxy MrDog trial on the existing gameplay quadruped rig."""
import importlib.util
import json
import struct
import sys
import zlib
from math import cos, sin, tau
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT.parent / "BlankFaceDog_20261107"
MODEL = ROOT / "model"
PREVIEWS = ROOT / "previews"
NAME = "MrDogBoxy_Rigged"
GINGER = (216, 124, 48)
CREAM = (243, 218, 188)
HEAD_SCALE = .92

spec = importlib.util.spec_from_file_location(
    "blank_baseline", BASELINE / "scripts" / "build_blank_face_dog.py"
)
baseline = importlib.util.module_from_spec(spec)
write_bytecode = not sys.dont_write_bytecode
sys.dont_write_bytecode = True
try:
    spec.loader.exec_module(baseline)
finally:
    sys.dont_write_bytecode = not write_bytecode


def color(rgb):
    return (*(channel / 255 for channel in rgb), 1)


def blank_material():
    def chunk(kind, contents):
        return (struct.pack(">I", len(contents)) + kind + contents
                + struct.pack(">I", zlib.crc32(kind + contents)))

    path = MODEL / "BlankFace_Default.png"
    # Write exact sRGB bytes so the face and the solid coat shade identically.
    rows = (b"\x00" + bytes(GINGER) * 512) * 512
    path.write_bytes(b"\x89PNG\r\n\x1a\n"
                     + chunk(b"IHDR", struct.pack(">IIBBBBB", 512, 512, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(rows))
                     + chunk(b"IEND", b""))
    image = bpy.data.images.load(str(path))
    image.pack()

    material = baseline.matte("MrDog_BlankFace", color(GINGER))
    nodes = material.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    material.node_tree.links.new(texture.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"])
    return material


def torso(blocks):
    def ring(y, width, bottom, top, edge):
        # The lower third is cream. The color boundary follows the flank.
        return [(-width + edge, y, bottom), (width - edge, y, bottom),
                (width, y, bottom + edge), (width, y, bottom + .102),
                (width, y, top - edge), (width - edge, y, top),
                (-width + edge, y, top), (-width, y, top - edge),
                (-width, y, bottom + .102), (-width, y, bottom + edge)]

    blocks.shell(
        [ring(*profile) for profile in (
            (-.300, .205, .383, .726, .019), (-.277, .225, .361, .749, .018),
            (.085, .223, .357, .749, .018), (.465, .223, .373, .744, .018),
            (.490, .201, .394, .723, .016),
        )],
        ("Chest", "Chest", "Spine", "Pelvis", "Pelvis"),
        band=(1, 1, 1, 0, 0, 0, 0, 0, 1, 1), caps=(False, False),
    )
    for indices, material in (((8, 9, 0, 1, 2, 3), 1), ((3, 4, 5, 6, 7, 8), 0)):
        blocks.face(tuple(40 + index for index in reversed(indices)), material)

    # Weld the sloped shoulder ruff into the torso's open front. Its last
    # section follows Head and stays inside the head during the action clips.
    bib_peak = len(blocks.vertices)
    blocks.vertices.append((0, -.300, .553))
    blocks.weights.append("Chest")
    blocks.face((8, 9, 0, 1, 2, 3, bib_peak), 1)
    previous = [3, 4, 5, 6, 7, 8, bib_peak]
    for y, x_scale, heights, weights in (
        (-.342, .83, (.568, .770, .812, .812, .770, .568, .639),
         {"Chest": .35, "Neck": .65}),
        (-.399, .59, (.714, .808, .846, .846, .808, .714, .725), "Head"),
    ):
        first = len(blocks.vertices)
        current = list(range(first, first + len(previous)))
        blocks.vertices.extend((blocks.vertices[i][0] * x_scale, y, z)
                               for i, z in zip((3, 4, 5, 6, 7, 8, bib_peak), heights, strict=True))
        blocks.weights.extend([weights] * len(previous))
        for i, j in zip(range(len(previous)), (1, 2, 3, 4, 5, 6, 0), strict=True):
            blocks.face((previous[i], previous[j], current[j], current[i]),
                        1 if i in (5, 6) else 0)
        previous = current
    blocks.face(tuple(previous), 0)


def leg(blocks, side, rear):
    # One continuous shell across the joint, ankle and beveled paw.
    x = -.139 if side == "L" else .139
    if rear:
        name = "Rear"
        profiles = [
            (.017, .372, .060, .080, "Paw", 1),
            (.032, .372, .073, .091, "Paw", 1),
            (.090, .385, .071, .075, "Paw", .68),
            (.161, .397, .068, .074, "Ankle", .65),
            (.285, .364, .073, .083, "Lower", .53),
            (.422, .364, .079, .091, "Upper", .62),
            (.539, .358, .074, .084, "Upper", .18),
        ]
        upstream = {"Paw": "Ankle", "Ankle": "Lower", "Lower": "Upper", "Upper": "Pelvis"}
    else:
        name = "Front"
        profiles = [
            (.017, -.268, .059, .078, "Paw", 1),
            (.032, -.268, .071, .090, "Paw", 1),
            (.096, -.229, .066, .077, "Paw", .58),
            (.299, -.203, .071, .076, "Lower", .53),
            (.423, -.193, .079, .082, "Upper", 1),
            (.535, -.193, .078, .083, "Upper", 1),
        ]
        upstream = {"Paw": "Lower", "Lower": "Upper"}

    rings, joints = [], []
    for z, y, width, depth, joint, weight in profiles:
        # Clockwise seen from above so the bottom cap points downward.
        rings.append([(x + u, y - v, z) for u, v in baseline.profile(width, depth, 0)])
        bone = f"{name}{joint}_{side}"
        parent = upstream.get(joint)
        parent_bone = "Pelvis" if parent == "Pelvis" else f"{name}{parent}_{side}"
        joints.append(bone if weight == 1 else {bone: weight, parent_bone: 1 - weight})
    # Cream continues from the paws up to the lower edge of the torso.
    blocks.shell(rings, joints, material=1)
    blocks.slots[-5:] = [0] * 5


def ear(blocks, side, sign):
    # A truncated triangular prism with a flush cream inset.
    outline = [(.062, 1.018), (.220, 1.018), (.201, 1.216), (.163, 1.221)]
    inset = [(.098, 1.048), (.192, 1.048), (.182, 1.187), (.169, 1.190)]
    if sign < 0:
        outline.reverse()
        inset.reverse()
    first = len(blocks.vertices)
    for points, y in ((outline, -.468), (outline, -.352), (inset, -.468)):
        blocks.vertices.extend((sign * x, y + .19 * (z - 1.018), z) for x, z in points)
        blocks.weights.extend([f"Ear_{side}"] * 4)
    for i in range(4):
        j = (i + 1) % 4
        blocks.face((first + j, first + i, first + i + 4, first + j + 4), 0)
        blocks.face((first + i, first + j, first + j + 8, first + i + 8), 0)
    blocks.face(tuple(range(first + 7, first + 3, -1)), 0)
    blocks.face(tuple(range(first + 8, first + 12)), 1)


def tail(blocks):
    points = [Vector(co) for co in ((0, .441, .660), (0, .540, .786),
                                  (.006, .509, .938), (.014, .379, 1.009),
                                  (.017, .304, .967))]
    sizes = ((.084, .078), (.086, .075), (.080, .073), (.066, .066), (.056, .056))
    joints = ("Pelvis", "Tail_01", "Tail_02", "Tail_03", "Tail_04")
    rings = []
    for index, (point, size) in enumerate(zip(points, sizes, strict=True)):
        before = points[max(0, index - 1)]
        after = points[min(len(points) - 1, index + 1)]
        depth = Vector((1, 0, 0)).cross(after - before).normalized()
        rings.append([point + Vector((x, 0, 0)) + depth * z
                      for x, z in baseline.profile(*size, 0)])
    blocks.shell(rings, joints)
    blocks.slots[-5:] = [1] * 5


def make_model(body, face):
    blocks = baseline.Blocks()
    torso(blocks)
    head_start = len(blocks.vertices)
    z = .864
    front = baseline.section(-.680, z, .218, .195, 0)
    blocks.shell(
        [front, baseline.section(-.650, z, .244, .223, 0),
         baseline.section(-.253, z, .244, .223, 0),
         baseline.section(-.229, z, .218, .200, 0)],
        ("Head",) * 4, caps=(False, True),
    )
    for side, sign in (("L", -1), ("R", 1)):
        ear(blocks, side, sign)
    head_center = Vector((0, -.424, z))
    blocks.vertices[head_start:] = [
        head_center.lerp(Vector(point), HEAD_SCALE) for point in blocks.vertices[head_start:]
    ]
    front = blocks.vertices[head_start:head_start + len(front)]
    for side in ("L", "R"):
        leg(blocks, side, rear=False)
        leg(blocks, side, rear=True)
    tail(blocks)

    old_body = body.data
    body.data = blocks.mesh("MrDog_BodyMesh", [baseline.matte("MrDog_Ginger", color(GINGER)),
                                                baseline.matte("MrDog_Cream", color(CREAM))])
    body.vertex_groups.clear()
    blocks.skin(body)
    body.name = "MrDogBoxy_Body"
    bpy.data.meshes.remove(old_body)
    body_uv = body.data.uv_layers.new(name="BodyUV")
    for polygon in body.data.polygons:
        for i, loop in enumerate(polygon.loop_indices):
            angle = tau * i / polygon.loop_total
            body_uv.data[loop].uv = (.5 + .5 * cos(angle), .5 + .5 * sin(angle))

    face_blocks = baseline.Blocks()
    face_blocks.vertices = front
    face_blocks.weights = ["Head"] * 4
    face_blocks.face((0, 1, 2, 3), 0)
    old_face = face.data
    face.data = face_blocks.mesh("FaceSurface_Mesh", [blank_material()])
    face.vertex_groups.clear()
    face_blocks.skin(face)
    bpy.data.meshes.remove(old_face)
    uv = face.data.uv_layers.new(name="FaceUV")
    for loop in face.data.loops:
        uv.data[loop.index].uv = ((0, 0), (1, 0), (1, 1), (0, 1))[loop.vertex_index]


def main():
    MODEL.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(BASELINE / "model" / "BlankFaceDog_Rigged.blend"))
    rig = bpy.data.objects["Dog_Rig"]
    body = bpy.data.objects["BlankFaceDog_Body"]
    face = bpy.data.objects["FaceSurface"]
    make_model(body, face)
    bpy.data.orphans_purge(do_recursive=True)

    baseline.MODEL = MODEL
    baseline.PREVIEWS = PREVIEWS
    # Reuse the established axis, skin and clip export settings.
    baseline.export_character(body, face, rig)
    for extension in ("fbx", "glb"):
        (MODEL / f"BlankFaceDog_Rigged.{extension}").replace(MODEL / f"{NAME}.{extension}")

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 680
    scene.render.image_settings.file_format = "PNG"
    camera = scene.camera
    views = (
        ("front", (0, -4, .64), (0, 0, .64), 1.44),
        ("side", (4, 0, .64), (0, -.02, .64), 1.56),
        ("hero", (2.9, -3.7, 1.83), (0, -.02, .63), 1.56),
    )
    rig.animation_data.action = bpy.data.actions["Idle"]
    scene.frame_set(1)
    for name, position, target, scale in views:
        camera.location = position
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera.data.ortho_scale = scale
        scene.render.filepath = str(PREVIEWS / f"{name}.png")
        bpy.ops.render.render(write_still=True)

    bpy.data.images["BlankFace_Default.png"].filepath = "//BlankFace_Default.png"
    scene.render.filepath = "//../previews/hero.png"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(MODEL / f"{NAME}.blend"))
    meshes = [obj.data for obj in (body, face)]
    for mesh in meshes:
        mesh.calc_loop_triangles()
    stats = {
        "subject_reference": "MrDog.png",
        "style_reference": "SimplePoly City boxy Shiba turnaround",
        "rig_reference": BASELINE.name,
        "vertices": sum(len(mesh.vertices) for mesh in meshes),
        "triangles": sum(len(mesh.loop_triangles) for mesh in meshes),
        "bones": len(rig.data.bones),
        "clips": {name: list(bpy.data.actions[name].frame_range) for name in baseline.CLIPS},
        "face_layout_version": 1,
    }
    (ROOT / "asset_report.json").write_text(json.dumps(stats, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
