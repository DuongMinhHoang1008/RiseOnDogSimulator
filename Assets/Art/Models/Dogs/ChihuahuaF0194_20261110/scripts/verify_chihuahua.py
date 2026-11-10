"""Round-trip all exports, check the animated face, and render motion previews."""
import argparse
import importlib.util
import json
import shutil
import sys
from itertools import combinations
from pathlib import Path
from tempfile import TemporaryDirectory
from types import SimpleNamespace

import bpy
import numpy as np
from mathutils.kdtree import KDTree

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_chihuahua import COAT, MODEL, NAME, PREVIEWS, ROOT, render_view


def validate_face(rig, body, face, validator):
    world = [face.matrix_world @ vertex.co for vertex in face.data.vertices]
    tree = KDTree(len(body.data.vertices))
    for vertex in body.data.vertices:
        tree.insert(body.matrix_world @ vertex.co, vertex.index)
    tree.balance()
    seam = []
    for index, point in enumerate(world):
        for _, body_index, _ in tree.find_range(point, 1e-5):
            seam.append((body_index, index))
    unique_positions = {tuple(round(c, 5) for c in world[index]) for _, index in seam}
    assert len(unique_positions) >= 27, len(unique_positions)

    assert len(face.data.materials) == 1
    image, = validator.texture_images(face.data.materials)
    assert tuple(image.size) == (512, 512)
    pixels = np.empty(512 * 512 * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    assert np.ptp(pixels.reshape(-1, 4), axis=0).max() < 1e-6
    uv = face.data.uv_layers[0].data
    coordinates = np.array([uv[loop.index].uv[:] for loop in face.data.loops])
    position = np.array([world[loop.vertex_index][:] for loop in face.data.loops])
    for axis, coordinate in enumerate((0, 2)):
        source = position[:, coordinate]
        expected = (source - source.min()) / np.ptp(source)
        assert np.max(np.abs(coordinates[:, axis] - expected)) < .0001

    center = {loop.vertex_index: uv[loop.index].uv.y for loop in face.data.loops
              if abs(uv[loop.index].uv.x - .5) < 1e-5}
    # GLB can split one position into multiple vertices for shading.
    candidates = list({tuple(round(c, 5) for c in world[v]): v
                       for v in center if .13 < center[v] < .27}.values())
    pair = min(combinations(candidates, 2), key=lambda pair: (world[pair[0]] - world[pair[1]]).length)
    lower, upper = sorted(pair, key=center.get)
    assert world[upper].z > world[lower].z
    scene = bpy.context.scene
    gaps, motion = {}, {}
    for action in bpy.data.actions:
        clip = action.name.rsplit("|", 1)[-1]
        rig.animation_data.action = action
        if not rig.animation_data.action_slot and len(action.slots) == 1:
            rig.animation_data.action_slot = action.slots[0]
        first, last = map(round, action.frame_range)
        lip, border, ground = [], [], []
        for frame in range(first, last + 1):
            scene.frame_set(frame)
            graph = bpy.context.evaluated_depsgraph_get()
            head = validator.points(face, graph)
            coat = validator.points(body, graph)
            lip.append((head[upper] - head[lower]).length)
            border.append(max((coat[a] - head[b]).length for a, b in seam))
            ground.append(min(point.z for point in coat))
        gaps[clip] = round(max(border), 8)
        motion[clip] = {
            "max_opening": max(lip),
            "peak_frame": first + int(np.argmax(lip)),
            "lowest_mesh_z": min(ground),
            "highest_ground_clearance": max(ground),
        }
    assert max(gaps.values()) < 1e-4, gaps
    idle = motion["Idle"]["max_opening"]
    assert idle < .010 and all(idle * 2 < motion[clip]["max_opening"] < .065
                              for clip in ("Bark", "Bite")), motion
    return {"matched_positions": len(unique_positions), "seam_gap_per_clip": gaps,
            "mouth_and_ground_per_clip": motion, "upper_lip_v": center[upper],
            "solid_512_png": True, "front_uv_0_to_1": True}


def save_sheet(name, paths, columns):
    tiles = []
    for path in paths:
        image = bpy.data.images.load(str(path), check_existing=False)
        width, height = image.size
        pixels = np.empty(width * height * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        tiles.append(pixels.reshape(height, width, 4)[::-1])
        bpy.data.images.remove(image)
        path.unlink()
    rows = len(tiles) // columns
    sheet = np.concatenate([
        np.concatenate(tiles[start:start + columns], axis=1)
        for start in range(0, len(tiles), columns)
    ])
    image = bpy.data.images.new(name, width=width * columns, height=height * rows)
    image.pixels.foreach_set(sheet[::-1].ravel())
    image.filepath_raw = str(PREVIEWS / f"{name}.png")
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def review(rig, face, report, source):
    scene = bpy.context.scene
    scene.render.resolution_x = scene.render.resolution_y = 300
    view = ((2.6, -3.8, 1.6), (0, .01, .78), 1.85)
    labels = []
    for clip in ("Run", "Jump"):
        action = bpy.data.actions[clip]
        rig.animation_data.action = action
        frames = np.linspace(*action.frame_range, 8).round().astype(int)
        for frame in frames:
            scene.frame_set(int(frame))
            label = f"_{clip}_{frame}"
            render_view(label, *view)
            labels.append(PREVIEWS / f"{label}.png")
    save_sheet("motion_review", labels, 4)

    if source is not None:
        # Sample the reference around its face and place its mouth on the UV slit.
        original = bpy.data.images.load(str(source), check_existing=False)
        width, height = original.size
        pixels = np.empty(width * height * 4, dtype=np.float32)
        original.pixels.foreach_get(pixels)
        pixels = pixels.reshape(height, width, 4)
        u = np.linspace(.386, .644, 512)
        v = (1 - .363) + (np.linspace(0, 1, 512) - report["upper_lip_v"]) * .274
        sample = pixels[np.round(v * (height - 1)).astype(int)[:, None],
                        np.round(u * (width - 1)).astype(int)[None, :]]
        # Fade the chair near the outline into the base coat for this preview.
        height_from_top = 1 - v
        rows = (.14, .19, .24, .28, .32, .36, .39, .42)
        left = np.interp(height_from_top, rows, (.425, .407, .398, .407, .418, .438, .429, .424))
        right = np.interp(height_from_top, rows, (.582, .611, .627, .614, .593, .575, .582, .591))
        mask = np.clip(np.minimum(u - left[:, None], right[:, None] - u) / .018, 0, 1)
        coat = ((np.array(COAT, dtype=np.float32) / 255 + .055) / 1.055) ** 2.4
        sample[:, :, :3] = sample[:, :, :3] * mask[:, :, None] + coat * (1 - mask[:, :, None])
        image = bpy.data.images.new("Temporary mouth alignment sample", width=512, height=512)
        image.pixels.foreach_set(sample.ravel())
        face.data.materials[0].node_tree.nodes.get("Image Texture").image = image

    scene.render.resolution_x = scene.render.resolution_y = 380
    labels = []
    for clip in ("Idle", "Bark", "Bite"):
        rig.animation_data.action = bpy.data.actions[clip]
        scene.frame_set(1 if clip == "Idle" else report["mouth_and_ground_per_clip"][clip]["peak_frame"])
        label = f"_mouth_{clip}"
        render_view(label, (.28, -4, .92), (0, -.46, .86), .44)
        labels.append(PREVIEWS / f"{label}.png")
    save_sheet("mouth_review", labels, 3)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--validator", type=Path, required=True)
    parser.add_argument("--reference", type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    spec = importlib.util.spec_from_file_location("dog_validator", args.validator)
    validator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validator)
    reports = ROOT / "validation"
    reports.mkdir(exist_ok=True)
    with TemporaryDirectory() as temporary:
        for extension in ("blend", "fbx", "glb"):
            asset = MODEL / f"{NAME}.{extension}"
            if extension != "blend":
                asset = Path(shutil.copy2(asset, temporary))
            checks = SimpleNamespace(input=asset, rig=None, max_vertices=None, max_influences=4,
                                     actions="Idle,Run,Jump,Bark,Bite,Poop,Pee", samples=5)
            report = {"errors": [], "warnings": []}
            validator.run(checks, report)
            assert not report["errors"], report["errors"]
            rig = bpy.data.objects[report["rig"]]
            face = bpy.data.objects["FaceSurface"]
            body = bpy.data.objects["ChihuahuaF0194_Body"]
            report["custom_face"] = validate_face(rig, body, face, validator)
            (reports / f"{extension}.json").write_text(json.dumps(report, indent=2) + "\n")
            if extension == "blend":
                review(rig, face, report["custom_face"], args.reference)


if __name__ == "__main__":
    main()
