"""Render the integrated face, a JPEG alignment chart and animation contact sheets."""

from pathlib import Path
import sys
from tempfile import TemporaryDirectory

import bpy
from mathutils import Vector
import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from convert_dog import MODEL, ROOT, STEM, clip_at


PREVIEWS = ROOT / "previews"
VIEWS = {
    "front": ((0, -4, .65), (0, 0, .65), 1.48),
    "side": ((4, 0, .65), (0, 0, .65), 1.55),
    "hero": ((2.5, -4, 1.65), (0, 0, .65), 1.60),
    "face": ((0, -4, .805), (0, -.56, .805), .48),
    "face_angle": ((1.62, -4, 1.12), (0, -.54, .805), .51),
}


def setup_studio(scene):
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.cycles.use_denoising = True
    scene.render.film_transparent = True
    scene.world = bpy.data.worlds.new("Studio")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes["Background"]
    background.inputs[0].default_value = (.32, .37, .45, 1)
    background.inputs[1].default_value = .55
    scene.render.image_settings.file_format = "PNG"

    camera = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    scene.camera = camera
    for name, location, power, size in [
        ("Key", (1.1, -2.0, 2.2), 150, 1.6),
        ("Fill", (-1.3, -1.1, 1.4), 55, 1.3),
        ("Rim", (.2, 1.8, 2.0), 175, 1.4),
    ]:
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.size = power, size
        light = bpy.data.objects.new(name, data)
        scene.collection.objects.link(light)
        light.location = location
        light.rotation_euler = (Vector((0, -.2, .65)) - light.location).to_track_quat("-Z", "Y").to_euler()


def render(scene, view, size, path):
    camera = scene.camera
    location, target, camera_scale = view
    camera.location = location
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = camera_scale
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def jpeg_probe(temp):
    """Asymmetric grid that makes flips, rotated UVs and stretching visible."""
    size = 512
    y, x = np.mgrid[0:size, 0:size]
    pixels = np.ones((size, size, 4), dtype=np.float32)
    pixels[:] = (.17, .54, .66, 1)
    pixels[(y > 255) & (x < 256)] = (.93, .51, .12, 1)
    pixels[(x > 255) & (y > 255)] = (.76, .22, .19, 1)
    pixels[(x < 256) & (y < 256)] = (.35, .56, .21, 1)
    pixels[(x % 64 < 2) | (y % 64 < 2)] = (.09, .11, .15, 1)
    arrow = ((abs(x - 256) < 11) & (120 < y) & (y < 346)) | (
        (330 <= y) & (y < 410) & (abs(x - 256) < 410 - y)
    )
    pixels[arrow] = (.96, .96, .92, 1)
    generated = bpy.data.images.new("UV alignment chart", width=size, height=size, alpha=False)
    generated.pixels.foreach_set(pixels.ravel())
    generated.file_format = "JPEG"
    path = temp / "face_uv_probe.jpg"
    generated.filepath_raw = str(path)
    generated.save()
    bpy.data.images.remove(generated)
    assert path.stat().st_size <= 128 * 1024
    return bpy.data.images.load(str(path))


def read_pixels(path):
    image = bpy.data.images.load(str(path))
    pixels = np.empty(len(image.pixels), dtype=np.float32)
    image.pixels.foreach_get(pixels)
    result = pixels.reshape(image.size[1], image.size[0], 4)[::-1]
    bpy.data.images.remove(image)
    return result


def motion_sheet(scene, rig, action_name, view, temp, frame_count):
    size = 280
    action = bpy.data.actions[action_name]
    frames = np.rint(np.linspace(*action.frame_range, frame_count)).astype(int)
    canvas = np.zeros((2 * size, frame_count // 2 * size, 4), dtype=np.float32)
    for index, frame in enumerate(frames):
        clip_at(rig, action, int(frame))
        path = temp / f"{action_name}_{frame}.png"
        render(scene, view, size, path)
        row, column = divmod(index, frame_count // 2)
        canvas[row * size:(row + 1) * size, column * size:(column + 1) * size] = read_pixels(path)
    sheet = bpy.data.images.new(action_name, width=canvas.shape[1], height=canvas.shape[0], alpha=True)
    sheet.pixels.foreach_set(canvas[::-1].ravel())
    sheet.file_format = "PNG"
    sheet.filepath_raw = str(PREVIEWS / f"{action_name}_motion_review.png")
    sheet.save()
    bpy.data.images.remove(sheet)
    print("Motion review", action_name, frames.tolist(), flush=True)


def main():
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(MODEL / f"{STEM}.blend"))
    scene = bpy.context.scene
    rig = bpy.data.objects["Dog_Rig"]
    setup_studio(scene)
    clip_at(rig, bpy.data.actions["Idle"], 1)
    for name, view in VIEWS.items():
        render(scene, view, 600, PREVIEWS / f"{name}.png")

    with TemporaryDirectory() as directory:
        temp = Path(directory)
        face_material = bpy.data.objects["FaceSurface"].data.materials[0]
        texture, = [n for n in face_material.node_tree.nodes if n.type == "TEX_IMAGE"]
        default = texture.image
        texture.image = jpeg_probe(temp)
        render(scene, VIEWS["face"], 600, PREVIEWS / "face_uv_probe.png")
        texture.image = default
        for action_name in ("Run", "Jump"):
            motion_sheet(scene, rig, action_name,
                         ((2.5, -4, 1.65), (0, -.025, .75), 1.83), temp, 8)
        for action_name in ("Bark", "Bite"):
            motion_sheet(scene, rig, action_name,
                         ((1.68, -4, 1.03), (0, -.56, .805), .53), temp, 6)


if __name__ == "__main__":
    main()
