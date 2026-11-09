"""Run the dog validator and verify Custom Face v1 on all three saved formats.

Pass -- --validator <path to validate_dog_asset.py> after Blender's --python argument.
"""

import argparse
import json
from pathlib import Path
import runpy
from shutil import copyfile
import sys
from tempfile import TemporaryDirectory
from types import SimpleNamespace

import bpy
from mathutils.kdtree import KDTree
import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from convert_dog import CLIPS, MODEL, ROOT, STEM, verify_animated_seam


def weights(obj, index):
    return {obj.vertex_groups[group.group].name: group.weight
            for group in obj.data.vertices[index].groups if group.weight > 1e-6}


def matched_seam(body, face):
    points = KDTree(len(body.data.vertices))
    for vertex in body.data.vertices:
        points.insert(body.matrix_world @ vertex.co, vertex.index)
    points.balance()
    pairs = []
    for vertex in face.data.vertices:
        position = face.matrix_world @ vertex.co
        face_weights = weights(face, vertex.index)
        for _, index, _ in points.find_range(position, 1e-5):
            body_weights = weights(body, index)
            if body_weights.keys() == face_weights.keys() and all(
                abs(body_weights[name] - weight) < 1e-5 for name, weight in face_weights.items()
            ):
                pairs.append((index, vertex.index))
                break
    return pairs


def check_face(mesh, image_nodes):
    assert len(mesh.data.materials) == 1
    color_texture, = [link.from_node.image for link in mesh.data.materials[0].node_tree.links
                      if link.from_node.type == "TEX_IMAGE" and link.to_socket.name == "Base Color"]
    assert color_texture in image_nodes
    assert tuple(color_texture.size) == (512, 512)
    pixels = np.empty(len(color_texture.pixels), dtype=np.float32)
    color_texture.pixels.foreach_get(pixels)
    pixels = pixels.reshape(-1, 4)
    assert (pixels == pixels[0]).all(), "The default image must be a single solid color"

    uv = mesh.data.uv_layers[0].uv
    coordinates = np.array([tuple(mesh.matrix_world @ v.co) for v in mesh.data.vertices])[:, [0, 2]]
    projected = (coordinates - coordinates.min(axis=0)) / np.ptp(coordinates, axis=0)
    actual = np.array([tuple(loop.vector) for loop in uv])
    assert np.allclose(actual, projected[[loop.vertex_index for loop in mesh.data.loops]], atol=1e-5)
    assert np.allclose(actual.min(axis=0), (0, 0), atol=1e-5)
    assert np.allclose(actual.max(axis=0), (1, 1), atol=1e-5)
    return {"uv_min": actual.min(axis=0).tolist(), "uv_max": actual.max(axis=0).tolist(),
            "default_texture": list(color_texture.size), "default_texture_is_solid": True}


def verify_format(path, validator, asset):
    report = {"input": f"model/{path.name}", "blender_version": bpy.app.version_string,
              "errors": [], "warnings": []}
    try:
        args = SimpleNamespace(input=path, rig=None, actions=",".join(CLIPS), max_vertices=None,
                               max_influences=4, samples=7)
        validator["run"](args, report)
        assert report["bones"] == asset["bones"]
        assert report["triangles"] == asset["source_mesh"]["triangles"]
        for action in bpy.data.actions:
            action.name = action.name.rsplit("|", 1)[-1]
        body = bpy.data.objects["RealisticDogCustomFace_Body"]
        face = bpy.data.objects["FaceSurface"]
        rig = face.find_armature()
        assert rig == body.find_armature()
        images = validator["texture_images"](list(face.data.materials))
        report["custom_face"] = check_face(face, images)
        pairs = matched_seam(body, face)
        unique_positions = {tuple(round(value, 6) for value in face.data.vertices[index].co)
                            for _, index in pairs}
        assert len(unique_positions) == asset["seam"]["shared_vertices"], len(unique_positions)
        report["custom_face"]["matched_seam_vertices"] = len(unique_positions)
        report["custom_face"]["max_seam_gap_per_clip_scene_units"] = verify_animated_seam(
            body, face, rig, pairs
        )
        face.data.calc_loop_triangles()
        assert len(face.data.loop_triangles) == asset["face"]["triangles"]
        for name, limits in asset["clips"].items():
            expected_seconds = (limits[1] - limits[0]) / asset["fps"]
            assert abs(report["actions"][name]["duration_seconds"] - expected_seconds) < 1e-4
    except Exception as exc:
        report["errors"].append(f"{type(exc).__name__}: {exc}")
    report["status"] = "FAIL" if report["errors"] else "PASS"
    return report


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--validator", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    validator = runpy.run_path(str(args.validator))
    asset = json.loads((ROOT / "asset_report.json").read_text(encoding="utf-8"))
    reports = ROOT / "validation"
    reports.mkdir(exist_ok=True)
    failed = []
    with TemporaryDirectory() as directory:
        for extension in ("blend", "fbx", "glb"):
            path = MODEL / f"{STEM}.{extension}"
            if extension == "fbx":
                scratch = Path(directory) / path.name
                copyfile(path, scratch)
                path = scratch
            report = verify_format(path, validator, asset)
            (reports / f"{extension}.json").write_text(json.dumps(report, indent=2) + "\n",
                                                        encoding="utf-8")
            print(extension, report["status"], report["errors"], report["warnings"], flush=True)
            if report["errors"]:
                failed.append(extension)
    if failed:
        raise RuntimeError(f"Validation failed for {', '.join(failed)}")


if __name__ == "__main__":
    main()
