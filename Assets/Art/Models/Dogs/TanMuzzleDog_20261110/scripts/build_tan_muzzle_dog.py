"""Build the tan reference dog on the project's existing quadruped rig."""
import importlib.util
import json
import struct
import sys
import zlib
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT.parent / "BlankFaceDog_20261107"
MODEL = ROOT / "model"
PREVIEWS = ROOT / "previews"
NAME = "TanMuzzleDog_Rigged"
TAN = (184, 138, 88)
SAND = (207, 167, 115)
CREAM = (226, 195, 151)
EAR = (151, 108, 69)
MOUTH = (83, 54, 40)
HEAD_SCALE = .90
HEAD_PIVOT = Vector((0, -.440, .865))
LEG_GIRTH = .88

spec = importlib.util.spec_from_file_location(
    "blank_baseline", BASELINE / "scripts" / "build_blank_face_dog.py"
)
baseline = importlib.util.module_from_spec(spec)
write_bytecode = sys.dont_write_bytecode
sys.dont_write_bytecode = True
try:
    spec.loader.exec_module(baseline)
finally:
    sys.dont_write_bytecode = write_bytecode


def rgba(rgb):
    return (*(channel / 255 for channel in rgb), 1)


def blank_material():
    def chunk(kind, data):
        return (struct.pack(">I", len(data)) + kind + data
                + struct.pack(">I", zlib.crc32(kind + data)))

    path = MODEL / "BlankFace_Default.png"
    rows = (b"\0" + bytes(SAND) * 512) * 512
    path.write_bytes(b"\x89PNG\r\n\x1a\n"
                     + chunk(b"IHDR", struct.pack(">IIBBBBB", 512, 512, 8, 2, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))
    image = bpy.data.images.load(str(path))
    image.pack()
    material = baseline.matte("TanDog_BlankFace", rgba(SAND))
    nodes = material.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    material.node_tree.links.new(texture.outputs["Color"], nodes["Principled BSDF"].inputs["Base Color"])
    return material, image


def torso(blocks):
    def ring(y, width, bottom, top, tint_line):
        return [
            (-width * .83, y, bottom), (width * .83, y, bottom),
            (width, y, bottom + .028), (width, y, tint_line),
            (width, y, top - .039), (width * .79, y, top),
            (-width * .79, y, top), (-width, y, top - .039),
            (-width, y, tint_line), (-width, y, bottom + .028),
        ]

    rings = [ring(*section) for section in (
        (-.364, .129, .410, .686, .532), (-.230, .178, .379, .735, .515),
        (.125, .140, .475, .723, .566), (.414, .156, .450, .736, .568),
        (.515, .127, .478, .700, .572),
    )]
    # The front ring branches into the rising neck along the coat boundary.
    blocks.shell(rings, ("Chest", "Chest", "Spine", "Pelvis", "Pelvis"),
                 material=0, band=(2, 2, 2, 1, 1, 0, 1, 1, 2, 2), caps=(False, True))
    bib = len(blocks.vertices)
    blocks.vertices.append((0, -.388, .578))
    blocks.weights.append("Chest")
    blocks.face((8, 9, 0, 1, 2, 3, bib), 2)
    base = [3, 4, 5, 6, 7, 8, bib]
    previous = base
    for front_y, back_y, width_scale, heights, weight in (
        (-.400, -.306, 1.10, (.621, .730, .782, .782, .730, .621, .633),
         {"Chest": .32, "Neck": .68}),
        (-.421, -.325, .82, (.735, .815, .855, .855, .815, .735, .734), "Head"),
    ):
        first = len(blocks.vertices)
        current = list(range(first, first + len(base)))
        blocks.vertices.extend((blocks.vertices[i][0] * width_scale,
                                front_y if i in (3, 8, bib) else back_y, z)
                               for i, z in zip(base, heights, strict=True))
        blocks.weights.extend([weight] * len(base))
        for i, j in zip(range(len(base)), (1, 2, 3, 4, 5, 6, 0), strict=True):
            blocks.face((previous[i], previous[j], current[j], current[i]),
                        2 if i in (5, 6) else 1)
        previous = current
    blocks.face(tuple(previous), 1)


def leg(blocks, side, rear):
    sign = -1 if side == "L" else 1
    prefix = "Rear" if rear else "Front"
    if rear:
        sections = (
            (.018, .368, .043, .055, "Paw", None, 1),
            (.032, .366, .050, .065, "Paw", None, 1),
            (.096, .388, .042, .046, "Paw", "Ankle", .52),
            (.168, .409, .045, .049, "Ankle", "Lower", .65),
            (.295, .350, .054, .061, "Lower", "Upper", .56),
            (.428, .366, .065, .075, "Upper", "Pelvis", .70),
            (.547, .360, .056, .068, "Upper", "Pelvis", .20),
        )
    else:
        sections = (
            (.018, -.266, .043, .052, "Paw", None, 1),
            (.032, -.264, .051, .065, "Paw", None, 1),
            (.104, -.231, .047, .050, "Paw", "Lower", .47),
            (.291, -.206, .051, .054, "Lower", "Upper", .56),
            (.421, -.204, .059, .066, "Upper", "Chest", .76),
            (.548, -.202, .050, .062, "Upper", "Chest", .25),
        )
    rings, weights = [], []
    for z, y, w, depth, joint, parent, weight in sections:
        x = sign * (.124 if z < .30 else (.103 if rear else .113))
        rings.append([(x + u, y - v, z) for u, v in baseline.profile(w * LEG_GIRTH, depth * LEG_GIRTH, 0)])
        bone = f"{prefix}{joint}_{side}"
        if parent is None:
            weights.append(bone)
        else:
            parent_bone = parent if parent in ("Chest", "Pelvis") else f"{prefix}{parent}_{side}"
            weights.append({bone: weight, parent_bone: 1 - weight})
    blocks.shell(rings, weights, material=1)
    # Feet and lower legs form one light coat area without a separate paw block.
    first_face = len(blocks.slots) - (4 * (len(rings) - 1) + 2)
    blocks.slots[first_face:first_face + 9] = [2] * 9


def ear(blocks, sign, side):
    outer = ((.071, 1.011), (.146, .973), (.233, 1.112), (.222, 1.117))
    if sign < 0:
        outer = outer[::-1]
    rings = [[(sign * x, y + .28 * (z - .995), z) for x, z in outer] for y in (-.424, -.356)]
    # Shift the base with the smaller skull while keeping the ear's silhouette.
    root = Vector((sign * .113, -.395, 1.000))
    offset = HEAD_PIVOT.lerp(root, HEAD_SCALE) - root
    rings = [[Vector(point) + offset for point in ring] for ring in rings]
    front = len(blocks.faces)
    blocks.shell(rings, (f"Ear_{side}",) * 2, material=1)
    blocks.slots[front] = 3


def tail(blocks):
    points = [Vector(point) for point in (
        (0, .447, .674), (0, .507, .797), (.004, .492, .927),
        (.008, .428, 1.022), (.015, .315, 1.068), (.021, .199, 1.044),
    )]
    radii = (.057, .055, .049, .044, .036, .021)
    joints = ("Pelvis", "Tail_01", "Tail_02", "Tail_03", "Tail_04", "Tail_04")
    rings = []
    for i, (center, radius) in enumerate(zip(points, radii, strict=True)):
        before = points[max(0, i - 1)]
        after = points[min(len(points) - 1, i + 1)]
        depth = Vector((1, 0, 0)).cross(after - before).normalized()
        profile = [(-radius, -radius * .67), (0, -radius), (radius, -radius * .67),
                   (radius, radius * .76), (0, radius), (-radius, radius * .76)]
        rings.append([center + Vector((x, 0, 0)) + depth * z for x, z in profile])
    blocks.shell(rings, joints, material=0, band=(1, 1, 0, 0, 0, 0))


def append_ring(blocks, points, weights):
    start = len(blocks.vertices)
    blocks.vertices.extend(points)
    blocks.weights.extend(weights)
    return list(range(start, len(blocks.vertices)))


def face_band(face, bottom, top):
    # The two lip corners subdivide the outer panels of the mouth row.
    a = (0, 2, 3, 4, 6) if len(bottom) == 7 else range(5)
    b = (0, 2, 3, 4, 6) if len(top) == 7 else range(5)
    for i in range(4):
        face.face((*bottom[a[i]:a[i + 1] + 1], *reversed(top[b[i]:b[i + 1] + 1])), 0)


def mouth_lining(blocks, face, upper, lower):
    # Both textured lips meet a recessed lining in the body material.
    left, right = append_ring(blocks, (face.vertices[upper[0]], face.vertices[upper[-1]]),
                              ("Head", "Head"))
    top, middle, bottom = [left], [left], [left]
    for a, b in zip(upper[1:-1], lower[1:-1], strict=True):
        high, low = face.vertices[a], face.vertices[b]
        recess = high.lerp(low, .5) + Vector((0, .028, -.001))
        jaw = face.weights[b]["Jaw"] * .5
        indices = append_ring(blocks, (high, recess, low),
                              (face.weights[a], {"Head": 1 - jaw, "Jaw": jaw}, face.weights[b]))
        for ring, index in zip((top, middle, bottom), indices, strict=True):
            ring.append(index)
    for ring in (top, middle, bottom):
        ring.append(right)
    return [
        tuple(dict.fromkeys((high[i], low[i], low[i + 1], high[i + 1])))
        for high, low in ((top, middle), (middle, bottom))
        for i in range(len(high) - 1)
    ]


def head(blocks):
    # Chin, muzzle, cheekbones and forehead. Across
    # each section the midline projects beyond the outer edge of the cheek.
    rows = (
        (.716, .067, -.505, -.544, -.557),
        (.754, .084, -.536, -.600, -.614),
        (.793, .102, -.540, -.615, -.626),
        (.845, .128, -.511, -.559, -.578),
        (.919, .140, -.473, -.501, -.522),
        (.993, .123, -.429, -.462, -.478),
        (1.034, .101, -.399, -.422, -.430),
    )
    face = baseline.Blocks()
    rings = []
    lower_weights = tuple({"Head": 1 - jaw, "Jaw": jaw}
                          for jaw in (.15, .45, .56, .45, .15))
    for row, (z, width, edge, bevel, center) in enumerate(rows):
        reach = (.012, .030, .036, .017, .004, 0, 0)[row]
        points = []
        for factor, y in ((-1, edge), (-.53, bevel), (0, center), (.53, bevel), (1, edge)):
            point = HEAD_PIVOT.lerp(Vector((factor * width, y, z)), HEAD_SCALE)
            point.y -= reach * (1 - .4 * abs(factor))
            points.append(point)
        rings.append(append_ring(face, points, lower_weights if row < 2 else ("Head",) * 5))
    # Cut just below the projecting muzzle tip at about V=.22. End the
    # opening inside the cheeks and keep both corners on the head bone.
    lip = [face.vertices[a].lerp(face.vertices[b], .82)
           for a, b in zip(rings[1], rings[2], strict=True)]
    corners = (lip[1].lerp(lip[0], .31), lip[3].lerp(lip[4], .31))
    upper = append_ring(face, (lip[0], corners[0], *lip[1:-1], corners[1], lip[-1]),
                        ("Head",) * 7)
    moving_lip = append_ring(face, (p + Vector((0, .002, -.003)) for p in lip[1:-1]),
                             lower_weights[1:-1])
    lower = [*upper[:2], *moving_lip, *upper[-2:]]
    for section in ([*rings[:2], lower], [upper, *rings[2:]]):
        for bottom, top in zip(section, section[1:]):
            face_band(face, bottom, top)
    edge_rings = [*rings[:2], upper, *rings[2:]]
    boundary = [*edge_rings[0], *(ring[-1] for ring in edge_rings[1:]),
                *edge_rings[-1][-2::-1], *(ring[0] for ring in edge_rings[-2:0:-1])]
    seam = [face.vertices[i] for i in boundary]
    # Keep the front layout and use a gently tapered rear profile.
    back = [(x * .92, -.299 + .10 * (z - .865), .872 + (z - .865) * .90)
            for x, _, z in seam]
    front_indices = append_ring(blocks, seam, (face.weights[i] for i in boundary))
    back_indices = append_ring(blocks, back, ("Head",) * len(back))
    for i in range(len(seam)):
        j = (i + 1) % len(seam)
        blocks.face((front_indices[j], front_indices[i], back_indices[i], back_indices[j]), 1)
    blocks.face(tuple(reversed(back_indices)), 1)
    return face, mouth_lining(blocks, face, upper[1:-1], lower[1:-1])


def model(body, face):
    blocks = baseline.Blocks()
    torso(blocks)
    front, lining = head(blocks)
    for sign, side in ((-1, "L"), (1, "R")):
        ear(blocks, sign, side)
        leg(blocks, side, rear=False)
        leg(blocks, side, rear=True)
    tail(blocks)
    # Append the lining so the FBX keeps the four existing Unity material slots.
    for polygon in lining:
        blocks.face(polygon, 4)

    old_body = body.data
    body.data = blocks.mesh("TanMuzzleDog_BodyMesh", [
        baseline.matte(name, rgba(rgb)) for name, rgb in
        (("TanDog_Back", TAN), ("TanDog_Sand", SAND), ("TanDog_Cream", CREAM),
         ("TanDog_Ear", EAR), ("TanDog_Mouth", MOUTH))
    ])
    body.name = "TanMuzzleDog_Body"
    body.vertex_groups.clear()
    blocks.skin(body)
    bpy.data.meshes.remove(old_body)
    uv = body.data.uv_layers.new(name="BodyUV")
    for loop in body.data.loops:
        _, y, z = body.data.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((y + .65) / 1.25, z / 1.15)

    material, image = blank_material()
    old_face = face.data
    face.data = front.mesh("FaceSurface_Mesh", [material])
    face.vertex_groups.clear()
    front.skin(face)
    bpy.data.meshes.remove(old_face)
    uv = face.data.uv_layers.new(name="FaceUV")
    min_z = min(point[2] for point in front.vertices)
    max_z = max(point[2] for point in front.vertices)
    half_width = max(point[0] for point in front.vertices)
    for loop in face.data.loops:
        x, _, z = front.vertices[loop.vertex_index]
        uv.data[loop.index].uv = (.5 + .5 * x / half_width, (z - min_z) / (max_z - min_z))
    return image


def set_camera(camera, position, target, scale):
    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = scale


def main():
    global MODEL, PREVIEWS
    if "--" in sys.argv:
        output = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
        MODEL, PREVIEWS = output / "model", output / "previews"
    MODEL.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(BASELINE / "model" / "BlankFaceDog_Rigged.blend"))
    rig = bpy.data.objects["Dog_Rig"]
    body = bpy.data.objects["BlankFaceDog_Body"]
    face = bpy.data.objects["FaceSurface"]
    image = model(body, face)
    bpy.data.orphans_purge(do_recursive=True)

    baseline.MODEL = MODEL
    baseline.export_character(body, face, rig)
    for ext in ("fbx", "glb"):
        (MODEL / f"BlankFaceDog_Rigged.{ext}").replace(MODEL / f"{NAME}.{ext}")

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 680
    camera = scene.camera
    rig.animation_data.action = bpy.data.actions["Idle"]
    scene.frame_set(1)
    for label, position, target, scale in (
        ("front", (0, -4, .79), (0, -.01, .60), 1.35),
        ("side", (4, 0, .80), (0, -.01, .60), 1.48),
        ("face", (1.65, -4, 1.13), (0, -.44, .891), .57),
        ("hero", (3.4, -4, 1.91), (0, -.015, .58), 1.43),
    ):
        set_camera(camera, position, target, scale)
        scene.render.filepath = str(PREVIEWS / f"{label}.png")
        bpy.ops.render.render(write_still=True)

    image.filepath = "//BlankFace_Default.png"
    scene.render.filepath = "//../previews/hero.png"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(MODEL / f"{NAME}.blend"))

    for mesh in (body.data, face.data):
        mesh.calc_loop_triangles()
    report = {
        "rig": BASELINE.name,
        "bones": len(rig.data.bones),
        "clips": {name: list(bpy.data.actions[name].frame_range) for name in baseline.CLIPS},
        "vertices": len(body.data.vertices) + len(face.data.vertices),
        "triangles": len(body.data.loop_triangles) + len(face.data.loop_triangles),
        "face_vertices": len(face.data.vertices),
        "face_layout_version": 1,
    }
    (MODEL.parent / "asset_report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
