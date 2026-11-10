using System;
using System.Linq;
using DogSimulator.CustomDogFace;
using DogSimulator.Player;
using UnityEditor;
using UnityEngine;

namespace DogSimulator.Editor
{
    public static partial class EditorFirstMigration
    {
        private static void WireCatalogs()
        {
            foreach (var path in Paths("t:DogModelCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>(path);
                var data = new SerializedObject(catalog);
                var entries = data.FindProperty("entries");
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    var prefab = entry.FindPropertyRelative("visualPrefab").objectReferenceValue as GameObject;
                    if (prefab == null) continue;
                    var prefabPath = AssetDatabase.GetAssetPath(prefab);
                    if (!prefabPath.EndsWith(".prefab", StringComparison.Ordinal))
                        throw new InvalidOperationException($"{path}, entry {i}: author a prefab wrapper for {prefabPath}.");
                    entry.FindPropertyRelative("visual").objectReferenceValue = WireModelPrefab(prefabPath);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static DogModelVisual WireModelPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = One<DogModelVisual>(root, optional: true);
                var changed = visual == null;
                if (visual == null) visual = root.AddComponent<DogModelVisual>();
                var animator = Unique(root.GetComponentsInChildren<Animator>(true), root, "model animator", true);
                if (animator == null)
                {
                    animator = root.AddComponent<Animator>();
                    changed = true;
                }
                changed |= Set(visual, "animator", animator);
                changed |= Set(visual, "faceSurface",
                    Unique(root.GetComponentsInChildren<DogFaceSurface>(true), root, "face surface", true));
                changed |= Set(visual, "peePelvis", animator.transform.Find("Dog_Rig/Root/Pelvis"));
                changed |= SetArray(visual, "renderers", root.GetComponentsInChildren<Renderer>(true));
                changed |= SetArray(visual, "colliders", root.GetComponentsInChildren<Collider>(true));
                changed |= SetArray(visual, "bodies", root.GetComponentsInChildren<Rigidbody>(true));
                changed |= SetArray(visual, "demoBehaviours", root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(component => component != null && component.GetType().FullName
                        == "ReferenceDog.Animation.ReferenceDogAnimationPreview").ToArray());
                if (changed)
                {
                    var asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                    return One<DogModelVisual>(asset);
                }
                return One<DogModelVisual>(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static bool WireDefaultVisual(DogVisualPresenter presenter)
        {
            var root = Read<GameObject>(presenter, "defaultVisual");
            if (root == null) return false;
            var animator = Read<Animator>(presenter, "animator")
                ?? Unique(root.GetComponentsInChildren<Animator>(true), root, "default animator", true);
            var changed = Set(presenter, "animator", animator);
            changed |= Set(presenter, "defaultFaceSurface",
                Unique(root.GetComponentsInChildren<DogFaceSurface>(true), root, "default face surface", true));
            return changed | Set(presenter, "defaultPeePelvis", animator?.transform.Find("Dog_Rig/Root/Pelvis"));
        }
    }
}
