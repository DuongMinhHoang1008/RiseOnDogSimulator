using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ReferenceDog.Animation.Editor
{
    public static class ReferenceDogRigSetup
    {
        public const string Root = "Assets/Art/ReferenceDog";
        public const string PreviewScene = Root + "/Unity/ReferenceDog_AnimationPreview.unity";
        public const string ControllerPath = Root + "/Unity/ReferenceDog.controller";
        private static readonly string[] Variants = { "low_poly", "realistic_low_vertex" };
        private static readonly string[] ClipNames = { "Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite" };

        [MenuItem("Tools/Dog Simulator/Reference Dog/Build Rigged Prefabs and Preview")]
        public static void BuildFromMenu() { Build(); }

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode before rebuilding the rig preview assets.");
            EnsureFolder(Root + "/Unity/Materials");
            EnsureFolder(Root + "/Unity/Prefabs");
            foreach (var variant in Variants)
                AssetDatabase.ImportAsset(Root + "/" + variant + "/Dog_Rigged.fbx", ImportAssetOptions.ForceUpdate);
            var shared = new Dictionary<string, Material>
            {
                ["Mouth_Interior"] = MakeMaterial("Mouth_Interior", new Color(.035f, .006f, .009f)),
                ["Tongue"] = MakeMaterial("Tongue", new Color(.34f, .055f, .073f)),
                ["Teeth"] = MakeMaterial("Teeth", new Color(.78f, .73f, .60f))
            };
            var firstClips = LoadClips(Variants[0]);
            var controller = BuildController(firstClips);
            var prefabs = new List<GameObject>();
            foreach (var variant in Variants)
            {
                var folder = Root + "/" + variant;
                var fur = MakeMaterial("Fur_" + variant, Color.white);
                fur.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Dog_BaseColor.png"));
                if (variant == "realistic_low_vertex")
                {
                    var normalPath = folder + "/Dog_Normal.png";
                    var textureImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                    if (textureImporter.textureType != TextureImporterType.NormalMap)
                    {
                        textureImporter.textureType = TextureImporterType.NormalMap;
                        textureImporter.SaveAndReimport();
                    }
                    fur.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                    fur.SetFloat("_BumpScale", .35f);
                    fur.EnableKeyword("_NORMALMAP");
                }
                var map = new Dictionary<string, Material>(shared) { ["Dog_Fur_and_ReferenceFace"] = fur };
                var modelPath = folder + "/Dog_Rigged.fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                foreach (var pair in map)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var wrapper = new GameObject("ReferenceDog_" + variant);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(wrapper.transform, false);
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.updateWhenOffscreen = true;
                    var materials = renderer.sharedMaterials;
                    for (var i = 0; i < materials.Length; i++)
                        if (materials[i] != null && map.TryGetValue(materials[i].name, out var replacement)) materials[i] = replacement;
                    renderer.sharedMaterials = materials;
                }
                wrapper.AddComponent<ReferenceDogAnimationPreview>().Configure(animator);
                var prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, Root + "/Unity/Prefabs/ReferenceDog_" + variant + "_Rigged.prefab");
                prefabs.Add(prefab);
                UnityEngine.Object.DestroyImmediate(wrapper);
                EditorUtility.SetDirty(fur);
            }
            BuildPreviewScene(prefabs);
            AssetDatabase.SaveAssets();
            return Verify();
        }

        private static Dictionary<string, AnimationClip> LoadClips(string variant)
        {
            var path = Root + "/" + variant + "/Dog_Rigged.fbx";
            var result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToDictionary(c => c.name);
            foreach (var name in ClipNames)
                if (!result.ContainsKey(name)) throw new InvalidOperationException("Missing clip " + name + " in " + path + ": " + string.Join(", ", result.Keys));
            return result;
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var parameter in controller.parameters) controller.RemoveParameter(parameter);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (var name in ClipNames.Skip(2)) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);
            var states = new Dictionary<string, AnimatorState>();
            for (var i = 0; i < ClipNames.Length; i++)
            {
                var name = ClipNames[i];
                var state = machine.AddState(name, new Vector3(250 + i % 3 * 230, 80 + i / 3 * 110));
                state.motion = clips[name];state.writeDefaultValues = false;
                states.Add(name, state);
            }
            machine.defaultState = states["Idle"];
            var run = states["Idle"].AddTransition(states["Run"]);
            run.hasExitTime = false;run.duration = .10f;run.AddCondition(AnimatorConditionMode.Greater, .1f, "Speed");
            var idle = states["Run"].AddTransition(states["Idle"]);
            idle.hasExitTime = false;idle.duration = .10f;idle.AddCondition(AnimatorConditionMode.Less, .1f, "Speed");
            foreach (var name in ClipNames.Skip(2))
            {
                var enter = machine.AddAnyStateTransition(states[name]);
                enter.hasExitTime = false;enter.duration = .06f;enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0, name);
                var exit = states[name].AddTransition(states["Idle"]);
                exit.hasExitTime = true;exit.exitTime = .96f;exit.duration = .08f;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            var path = Root + "/Unity/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader unavailable.");
            if (material == null) { material = new Material(shader);AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0);material.SetFloat("_Smoothness", .17f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPreviewScene(List<GameObject> prefabs)
        {
            // Build additively so the user's unsaved gameplay scene is never replaced or saved.
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                for (var i = 0; i < prefabs.Count; i++)
                {
                    var dog = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
                    dog.transform.position = new Vector3(i == 0 ? -.65f : .65f, 0, 0);
                }
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Preview Floor";ground.transform.localScale = Vector3.one * 2;
                ground.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Preview_Floor", new Color(.075f, .11f, .15f));
                var cameraObject = new GameObject("Preview Camera");
                var camera = cameraObject.AddComponent<Camera>();camera.tag = "MainCamera";
                camera.transform.position = new Vector3(2.2f, 1.6f, 3.6f);
                camera.transform.LookAt(new Vector3(0, .58f, 0));
                camera.orthographic = true;camera.orthographicSize = 1.02f;
                camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = new Color(.035f, .055f, .08f);
                camera.nearClipPlane = .02f;camera.farClipPlane = 30;
                cameraObject.AddComponent<AudioListener>();
                var light = new GameObject("Preview Key").AddComponent<Light>();
                light.type = LightType.Directional;light.intensity = 2f;
                light.transform.rotation = Quaternion.Euler(38, 150, 0);light.shadows = LightShadows.Soft;
                var fill = new GameObject("Preview Fill").AddComponent<Light>();
                fill.type = LightType.Directional;fill.intensity = .55f;fill.transform.rotation = Quaternion.Euler(28, -25, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;RenderSettings.ambientLight = new Color(.40f, .43f, .48f);
                EditorSceneManager.SaveScene(scene, PreviewScene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Tools/Dog Simulator/Reference Dog/Verify Rigged Assets")]
        public static void VerifyFromMenu() { Verify(); }

        public static string Verify()
        {
            var report = new VerificationReport();
            foreach (var variant in Variants)
            {
                var clips = LoadClips(variant);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Unity/Prefabs/ReferenceDog_" + variant + "_Rigged.prefab");
                if (prefab == null) throw new InvalidOperationException("Missing prefab for " + variant);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    var animator = instance.GetComponentInChildren<Animator>();
                    var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (animator == null || animator.avatar == null || !animator.avatar.isValid) throw new InvalidOperationException("Invalid Generic avatar: " + variant);
                    if (skin == null || skin.bones.Length < 26 || skin.bones.Length > 27) throw new InvalidOperationException("Unexpected skin bone count: " + variant);
                    var validation = new VariantVerification { variant = variant, skinBones = skin.bones.Length, vertices = skin.sharedMesh.vertexCount };
                    foreach (var name in ClipNames)
                    {
                        var clip = clips[name];
                        if (clip.length < .5f || AnimationUtility.GetCurveBindings(clip).Length < 20) throw new InvalidOperationException("Empty animation: " + name);
                        if (clip.isLooping != (name == "Idle" || name == "Run")) throw new InvalidOperationException("Incorrect loop flag: " + name);
                        animator.Rebind();animator.Update(0);
                        animator.SetFloat("Speed", name == "Run" ? 1 : 0);
                        animator.Play(name, 0, .35f);animator.Update(0);
                        var before = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                        animator.Update(.04f);
                        var state = animator.GetCurrentAnimatorStateInfo(0);
                        if (!state.IsName(name) || state.normalizedTime <= before)
                            throw new InvalidOperationException("Animator controller failed to advance: " + variant + "/" + name);
                        var bounds = new Bounds();var first = true;
                        foreach (var time in new[] { 0f, clip.length * .25f, clip.length * .5f, clip.length * .75f })
                        {
                            var sharedClip = animator.runtimeAnimatorController.animationClips.First(c => c.name == name);
                            sharedClip.SampleAnimation(animator.gameObject, time);
                            var mesh = new Mesh();skin.BakeMesh(mesh);
                            var b = mesh.bounds;
                            if (float.IsNaN(b.size.x) || b.size.x > 3 || b.size.y > 3 || b.size.z > 3) throw new InvalidOperationException("Exploded imported skin: " + name);
                            if (first) { bounds = b;first = false; } else bounds.Encapsulate(b);
                            UnityEngine.Object.DestroyImmediate(mesh);
                        }
                        validation.clips.Add(new ClipVerification { name = name, seconds = clip.length, loop = clip.isLooping, controllerPlayback = "PASS", sampledBounds = bounds.size });
                    }
                    report.variants.Add(validation);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            report.status = "PASS";
            var json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Root + "/unity_rig_verification.json", json);
            Debug.Log("ReferenceDog rig/animation verification: PASS");
            return json;
        }

        public static string CapturePose(string clipName, float normalizedTime)
        {
            var scene = EditorSceneManager.OpenPreviewScene(PreviewScene);
            try
            {
                var roots = scene.GetRootGameObjects();
                var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>()).First();
                camera.scene = scene;
                camera.cameraType = CameraType.Preview;
                foreach (var animator in roots.SelectMany(r => r.GetComponentsInChildren<Animator>()))
                {
                    animator.Rebind();animator.Update(0);
                    var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == clipName);
                    clip.SampleAnimation(animator.gameObject, clip.length * normalizedTime);
                }
                foreach (var renderer in roots.SelectMany(r => r.GetComponentsInChildren<SkinnedMeshRenderer>()))
                    renderer.forceMatrixRecalculationPerRender = true;
                var target = new RenderTexture(1440, 900, 24);
                var previous = RenderTexture.active;
                var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = target;
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);image.Apply();
                    var path = Root + "/previews/unity_" + clipName + ".png";
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    return path;
                }
                finally
                {
                    camera.targetTexture = null;RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [Serializable] private sealed class VerificationReport { public string status;public List<VariantVerification> variants = new List<VariantVerification>(); }
        [Serializable] private sealed class VariantVerification { public string variant;public int skinBones;public int vertices;public List<ClipVerification> clips = new List<ClipVerification>(); }
        [Serializable] private sealed class ClipVerification { public string name;public float seconds;public bool loop;public string controllerPlayback;public Vector3 sampledBounds; }
    }
}
