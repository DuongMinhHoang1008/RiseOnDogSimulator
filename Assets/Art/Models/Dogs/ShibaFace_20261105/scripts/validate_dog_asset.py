"""Run inside Blender. Inspect one rigged dog and emit a portable JSON QA report.

blender --background --factory-startup --python-exit-code 1 --python this.py --
    --input Dog.blend --report validation.json
FBX import may extract companion textures; validate copies in a scratch directory.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--rig")
    parser.add_argument("--actions", default="Idle,Run,Jump,Bark,Poop,Pee,Bite")
    parser.add_argument("--max-vertices", type=int)
    parser.add_argument("--max-influences", type=int, default=4)
    parser.add_argument("--samples", type=int, default=5)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    if args.samples < 2 or args.max_influences < 1:
        parser.error("--samples must be >=2; --max-influences must be >=1")
    if args.max_vertices is not None and args.max_vertices < 1:
        parser.error("--max-vertices must be positive")
    if args.report.suffix.lower() != ".json" or args.report.resolve() == args.input.resolve():
        parser.error("--report must be a separate .json file, never the input asset")
    return args


def load_asset(path):
    if not path.is_file():
        raise FileNotFoundError(path)
    ext = path.suffix.lower()
    if ext == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(path), use_scripts=False)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if ext == ".fbx":
            bpy.ops.import_scene.fbx(filepath=str(path))
        elif ext in (".glb", ".gltf"):
            bpy.ops.import_scene.gltf(filepath=str(path))
        else:
            raise ValueError("Input must be .blend, .fbx, .glb or .gltf")


def texture_images(materials):
    images = set()
    for material in materials:
        stack = [material.node_tree] if material and material.node_tree else []
        seen = set()
        while stack:
            tree = stack.pop()
            if tree.as_pointer() in seen:
                continue
            seen.add(tree.as_pointer())
            for node in tree.nodes:
                if node.type == "TEX_IMAGE" and node.image:
                    images.add(node.image)
                elif node.type == "GROUP" and node.node_tree:
                    stack.append(node.node_tree)
    return sorted(images, key=lambda image: image.name)


def points(mesh_object, depsgraph):
    evaluated = mesh_object.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        return [evaluated.matrix_world @ v.co for v in mesh.vertices]
    finally:
        evaluated.to_mesh_clear()


def run(args, report):
    errors, warnings = report["errors"], report["warnings"]
    load_asset(args.input.resolve())
    rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if args.rig:
        rigs = [o for o in rigs if o.name == args.rig]
    if len(rigs) != 1:
        raise ValueError("Expected one armature; use --rig to select one. Found: " + str([o.name for o in rigs]))
    rig = rigs[0]
    meshes = [o for o in bpy.data.objects if o.type == "MESH"
              and any(m.type == "ARMATURE" and m.object == rig for m in o.modifiers)]
    if not meshes:
        raise ValueError("No meshes skinned to the selected armature")
    report.update(rig=rig.name, bones=len(rig.data.bones), meshes=[], textures=[], actions={})
    materials = set()
    bones = {b.name for b in rig.data.bones if b.use_deform}
    total_vertices = total_triangles = 0
    for obj in meshes:
        data = obj.data
        data.calc_loop_triangles()
        total_vertices += len(data.vertices)
        total_triangles += len(data.loop_triangles)
        materials.update(m for m in data.materials if m)
        groups = {g.index for g in obj.vertex_groups if g.name in bones}
        unbound = unnormalized = excessive = 0
        for v in data.vertices:
            if not all(math.isfinite(c) for c in v.co):
                errors.append(f"{obj.name}: non-finite source vertex {v.index}")
                break
        for v in data.vertices:
            weights = [g.weight for g in v.groups if g.group in groups and g.weight > 1e-6]
            unbound += not weights
            unnormalized += bool(weights) and abs(sum(weights) - 1) > .001
            excessive += len(weights) > args.max_influences
        if unbound or unnormalized or excessive:
            errors.append(f"{obj.name}: invalid weights (unbound={unbound}, unnormalized={unnormalized}, too_many={excessive})")
        if data.uv_layers.active is None:
            errors.append(f"{obj.name}: missing UV layer")
        elif any(not math.isfinite(c) for uv in data.uv_layers.active.data for c in uv.uv):
            errors.append(f"{obj.name}: non-finite UV coordinates")
        report["meshes"].append({"name": obj.name, "vertices": len(data.vertices), "triangles": len(data.loop_triangles),
                                 "unbound_vertices": unbound, "unnormalized_vertices": unnormalized,
                                 "over_influence_limit": excessive})
    report.update(vertices=total_vertices, triangles=total_triangles, max_influences=args.max_influences)
    if args.max_vertices is not None and total_vertices > args.max_vertices:
        errors.append(f"Source vertex budget exceeded: {total_vertices} > {args.max_vertices}")
    images = texture_images(materials)
    if not images:
        errors.append("No image textures found; bake portable photo/fur maps before export")
    for image in images:
        size = list(image.size)
        if size and size[0] > 0:
            _ = image.pixels[0]  # Force lazy image loading before checking has_data.
        available = bool(size and min(size) > 0 and image.has_data)
        packed = bool(image.packed_file)
        if not available:
            errors.append(f"Texture pixels unavailable: {image.name}")
        if not packed and image.filepath:
            resolved = Path(bpy.path.abspath(image.filepath)).resolve()
            if not resolved.is_relative_to(args.input.resolve().parent):
                warnings.append(f"Unpacked external texture may need bundling: {image.name}: {resolved}")
        report["textures"].append({"name": image.name, "size": size, "available": available,
                                    "packed": packed, "color_space": image.colorspace_settings.name})
    expected = [name.strip() for name in args.actions.split(",") if name.strip()]
    if not expected:
        raise ValueError("Provide at least one expected action")
    actions = {}
    for action in bpy.data.actions:
        name = action.name.rsplit("|", 1)[-1]
        if name in expected:
            if name in actions:
                errors.append(f"Ambiguous duplicate action: {name}")
            actions[name] = action
    rig.animation_data_create()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
    for name in expected:
        if name not in actions:
            errors.append(f"Missing action: {name}")
            continue
        action = actions[name]
        rig.animation_data.action = action
        if hasattr(action, "slots") and not rig.animation_data.action_slot:
            if len(action.slots) == 1:
                rig.animation_data.action_slot = action.slots[0]
            else:
                errors.append(f"{name}: cannot select an unambiguous action slot")
                continue
        start, end = map(float, action.frame_range)
        if end <= start:
            errors.append(f"{name}: empty frame range")
            continue
        first = last = None
        max_motion = 0.
        for sample in range(args.samples):
            frame = start + (end - start) * sample / (args.samples - 1)
            integer = math.floor(frame)
            bpy.context.scene.frame_set(integer, subframe=frame - integer)
            bpy.context.view_layer.update()
            pose = [p for obj in meshes for p in points(obj, depsgraph)]
            if not all(math.isfinite(c) for p in pose for c in p):
                errors.append(f"{name}: non-finite deformation at frame {frame:g}")
                continue
            if first is None:
                first = pose
            elif len(first) != len(pose):
                errors.append(f"{name}: topology changes during animation")
                continue
            max_motion = max(max_motion, max(((p-q).length for p,q in zip(pose, first)), default=0.))
            last = pose
        if first is None:
            errors.append(f"{name}: no usable sampled poses")
            continue
        extent = max((max(p[i] for p in first)-min(p[i] for p in first) for i in range(3)), default=0.)
        if name.lower() != "idle" and max_motion <= max(1e-6, extent * 1e-5):
            errors.append(f"{name}: action does not move the skinned geometry")
        loop_delta = max(((p-q).length for p,q in zip(first, last)), default=0.) if last else None
        if name.lower() in ("idle", "run") and loop_delta is not None and loop_delta > max(.0001, extent * .005):
            warnings.append(f"{name}: loop endpoint differs by {loop_delta:g}; inspect the seam")
        report["actions"][name] = {"frames": [start, end], "duration_seconds": (end-start)/fps,
                                    "sampled_max_vertex_motion": max_motion, "loop_endpoint_delta": loop_delta}


def main():
    args = arguments()
    report = {"input": str(args.input.resolve()), "blender_version": bpy.app.version_string,
              "errors": [], "warnings": []}
    try:
        run(args, report)
    except Exception as exc:
        report["errors"].append(f"{type(exc).__name__}: {exc}")
    report["status"] = "FAIL" if report["errors"] else "PASS"
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"status": report["status"], "report": str(args.report.resolve()),
                      "vertices": report.get("vertices"), "bones": report.get("bones"),
                      "actions": list(report.get("actions", {})), "errors": report["errors"][:10],
                      "warnings": report["warnings"]}, ensure_ascii=False), flush=True)
    if report["errors"]:
        raise RuntimeError("Dog asset validation failed; inspect the JSON report")


if __name__ == "__main__":
    main()
