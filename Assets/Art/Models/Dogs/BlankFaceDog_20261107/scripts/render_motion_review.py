import bpy
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
PREVIEWS = ROOT / "previews"
bpy.ops.wm.open_mainfile(filepath=str(ROOT / "model" / "BlankFaceDog_Rigged.blend"))
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 320
scene.render.resolution_y = 320
scene.render.image_settings.file_format = "PNG"
rig = bpy.data.objects["Dog_Rig"]
camera = scene.camera
camera.location = (2.6, -3.8, 1.7)
camera.rotation_euler = (Vector((0, .02, .66)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.ortho_scale = 1.75


def pixels(path):
    image = bpy.data.images.load(str(path), check_existing=False)
    result = np.array(image.pixels[:], dtype=np.float32).reshape(image.size[1], image.size[0], 4)[::-1]
    bpy.data.images.remove(image)
    return result


for action_name in ("Run", "Jump"):
    action = bpy.data.actions[action_name]
    rig.animation_data.action = action
    frames = np.linspace(action.frame_range[0], action.frame_range[1], 8).round().astype(int)
    rendered = []
    for index, frame in enumerate(frames):
        scene.frame_set(int(frame))
        path = PREVIEWS / f"_{action_name}_{index}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        rendered.append(pixels(path))
        path.unlink(missing_ok=True)
    canvas = np.ones((640, 1280, 4), dtype=np.float32)
    for index, frame_pixels in enumerate(rendered):
        row, column = divmod(index, 4)
        canvas[row * 320:(row + 1) * 320, column * 320:(column + 1) * 320] = frame_pixels
    sheet = bpy.data.images.new(f"{action_name}_motion_review", width=1280, height=640, alpha=True)
    sheet.pixels.foreach_set(canvas[::-1].ravel())
    sheet.filepath_raw = str(PREVIEWS / f"{action_name}_motion_review.png")
    sheet.file_format = "PNG"
    sheet.save()
    bpy.data.images.remove(sheet)
    print("MOTION_REVIEW", action_name, frames.tolist(), flush=True)

print("MOTION_REVIEW_COMPLETE", flush=True)
