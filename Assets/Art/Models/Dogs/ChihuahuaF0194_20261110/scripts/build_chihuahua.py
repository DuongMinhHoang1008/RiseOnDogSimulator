"""Build the F0194 Chihuahua on the local animated quadruped rig."""
import importlib.util
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / "model"
PREVIEWS = ROOT / "previews"
NAME = "ChihuahuaF0194_Rigged"
COAT = (163, 94, 58)

spec = importlib.util.spec_from_file_location(
    "tan_dog", ROOT.parent / "TanMuzzleDog_20261110/scripts/build_tan_muzzle_dog.py"
)
tan = importlib.util.module_from_spec(spec)
sys.dont_write_bytecode = True
spec.loader.exec_module(tan)
baseline = tan.baseline


def fit_trunk(point):
    x, y, z = point
    belly = max(0, 1 - abs(z - .475) / .16)
    throat = max(0, 1 - abs(z - .740) / .13) * max(0, min(1, (-y - .29) / .12))
    return (x * .84, y - .025 * max(0, y) / .515 - .012 * throat,
            z + belly * .030)


def fit_head(point):
    x, y, z = point
    # Full upper skull and cheekbones, with a short muzzle below the stop.
    forehead = min(1, max(0, (z - .80) / .18))
    muzzle = max(0, min(1, (-y - .50) / .15))
    nose_tip = max(0, 1 - abs(z - .800) / .055)
    crown = max(0, (z - .974) / .048) * max(0, 1 - abs(x) / .13)
    return Vector((x * (.90 + .29 * forehead),
                   y + (.066 - .021 * nose_tip) * muzzle - .031 * forehead,
                   z + .024 * forehead + .025 * crown))


def add_ear(blocks, sign, side):
    bone = f"Ear_{side}"
    outline = [
        (.100, -.413, .979), (.115, -.398, 1.113),
        (.204, -.368, 1.255), (.218, -.364, 1.242),
        (.227, -.375, 1.137), (.202, -.391, 1.024),
        (.152, -.413, .975),
    ]
    if sign < 0:
        outline.reverse()
    rim = [Vector((sign * x, y, z)) for x, y, z in outline]
    center = Vector((sign * .170, -.383, 1.100))
    inset = [point.lerp(center, .21) + Vector((0, .009, 0)) for point in rim]
    back = [point.lerp(center, .23) + Vector((0, .049, -.005)) for point in rim]
    blocks.shell((inset, rim, back), (bone,) * 3, material=1, caps=(False, True),
                 soft_corners=True)
    bowl = tan.append_ring(blocks, [center + Vector((0, .028, -.021))], [bone])[0]
    first = bowl - 3 * len(rim)
    for index in range(len(rim)):
        blocks.face((first + index, first + (index + 1) % len(rim), bowl), 3)


def geometry():
    body = baseline.Blocks()
    tan.torso(body)
    body.vertices[:] = [fit_trunk(point) for point in body.vertices]
    # Restrict the lighter bib to the throat and underside.
    for index, polygon in enumerate(body.faces):
        if body.slots[index] == 2 and min(body.vertices[i][1] for i in polygon) >= -.365:
            body.slots[index] = 1

    start = len(body.vertices)
    face, lining = tan.head(body)
    body.vertices[start:] = [fit_head(point) for point in body.vertices[start:]]
    face.vertices[:] = [fit_head(point) for point in face.vertices]
    for polygon in lining:
        body.face(polygon, 4)

    tan.LEG_GIRTH = .65
    for sign, side in ((-1, "L"), (1, "R")):
        add_ear(body, sign, side)
        start = len(body.vertices)
        first_face = len(body.faces)
        tan.leg(body, side, rear=False)
        tan.leg(body, side, rear=True)
        body.slots[first_face:] = [1] * (len(body.faces) - first_face)
        body.vertices[start:] = [(x * .86, y, z - .017 * max(0, 1 - z / .29))
                                 for x, y, z in body.vertices[start:]]

    start = len(body.vertices)
    tan.tail(body)
    # The tail is obscured in the photo. Give it a slim, modest sickle shape.
    centers = ((0, .438, .671), (0, .495, .747), (.004, .518, .832),
               (.008, .496, .899), (.013, .448, .941), (.019, .389, .940))
    for ring, center in enumerate(centers):
        section = slice(start + ring * 6, start + (ring + 1) * 6)
        points = body.vertices[section]
        old_center = sum(points, Vector()) / len(points)
        body.vertices[section] = [Vector(center) + (point - old_center) * .63
                                  for point in points]

    # Reduce the inherited jaw travel for this shorter muzzle. Use the same
    # factor on the face and the lining so their shared edges stay together.
    for blocks in (body, face):
        for index, weights in enumerate(blocks.weights):
            if isinstance(weights, dict) and "Jaw" in weights:
                jaw = weights["Jaw"] * .78
                blocks.weights[index] = {"Head": 1 - jaw, "Jaw": jaw}
    return body, face


def replace_mesh(obj, blocks, name, materials):
    previous = obj.data
    obj.data = blocks.mesh(name, materials)
    obj.vertex_groups.clear()
    blocks.skin(obj)
    bpy.data.meshes.remove(previous)


def build_character(body, face):
    body_blocks, face_blocks = geometry()
    palette = (("Back", (148, 82, 50)), ("Coat", COAT), ("Chest", (181, 117, 90)),
               ("InnerEar", (164, 96, 90)), ("Mouth", (71, 36, 29)))
    materials = [baseline.matte(f"F0194_{label}", tan.rgba(color)) for label, color in palette]
    replace_mesh(body, body_blocks, "ChihuahuaF0194_BodyMesh", materials)
    body.name = "ChihuahuaF0194_Body"
    uv = body.data.uv_layers.new(name="BodyUV")
    for loop in body.data.loops:
        _, y, z = body.data.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((y + .65) / 1.25, z / 1.30)

    tan.MODEL, tan.SAND = MODEL, COAT
    material, image = tan.blank_material()
    material.name = "F0194_BlankFace"
    replace_mesh(face, face_blocks, "FaceSurface_Mesh", [material])
    uv = face.data.uv_layers.new(name="FaceUV")
    min_z = min(point.z for point in face_blocks.vertices)
    max_z = max(point.z for point in face_blocks.vertices)
    half_width = max(point.x for point in face_blocks.vertices)
    for loop in face.data.loops:
        x, _, z = face.data.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (.5 + .5 * x / half_width, (z - min_z) / (max_z - min_z))
    return image


def render_view(label, position, target, scale):
    scene = bpy.context.scene
    tan.set_camera(scene.camera, position, target, scale)
    scene.render.filepath = str(PREVIEWS / f"{label}.png")
    bpy.ops.render.render(write_still=True)


def main():
    MODEL.mkdir(exist_ok=True)
    PREVIEWS.mkdir(exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(tan.BASELINE / "model/BlankFaceDog_Rigged.blend"))
    rig = bpy.data.objects["Dog_Rig"]
    body = bpy.data.objects["BlankFaceDog_Body"]
    face = bpy.data.objects["FaceSurface"]
    image = build_character(body, face)
    bpy.data.orphans_purge(do_recursive=True)

    scene = bpy.context.scene
    rig.animation_data.action = bpy.data.actions["Idle"]
    scene.frame_set(1)
    baseline.MODEL = MODEL
    baseline.export_character(body, face, rig)
    for extension in ("fbx", "glb"):
        (MODEL / f"BlankFaceDog_Rigged.{extension}").replace(MODEL / f"{NAME}.{extension}")

    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    front = ((0, -4, .83), (0, -.05, .62), 1.46)
    side = ((4, -.01, .80), (0, -.05, .62), 1.46)
    hero = ((2.75, -4, 1.63), (0, -.035, .62), 1.46)
    close = ((.95, -4, 1.02), (0, -.43, .99), .64)
    for label, view in (("front", front), ("side", side), ("hero", hero), ("face", close)):
        render_view(label, *view)

    image.filepath = "//BlankFace_Default.png"
    tan.set_camera(scene.camera, *hero)
    scene.render.filepath = "//../previews/hero.png"
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(MODEL / f"{NAME}.blend"))

    for mesh in (body.data, face.data):
        mesh.calc_loop_triangles()
    report = {
        "rig": tan.BASELINE.name,
        "bones": len(rig.data.bones),
        "clips": {name: list(bpy.data.actions[name].frame_range) for name in baseline.CLIPS},
        "vertices": len(body.data.vertices) + len(face.data.vertices),
        "triangles": len(body.data.loop_triangles) + len(face.data.loop_triangles),
        "face_vertices": len(face.data.vertices),
        "face_layout_version": 1,
    }
    (ROOT / "asset_report.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
