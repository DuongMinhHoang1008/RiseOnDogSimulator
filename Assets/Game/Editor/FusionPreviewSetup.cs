using System;
using System.Linq;
using DogSimulator.Networking;
using DogSimulator.Player;
using DogSimulator.UI;
using Fusion;
using Fusion.Editor;
using Fusion.Photon.Realtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static class FusionPreviewSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/M1_FusionPreview.unity";
        public const string PrefabPath = "Assets/Game/Prefabs/DogPlayer.prefab";
        public const string ConfigPath = "Assets/Game/Config/FusionNetworkConfig.asset";
        private const string AssemblyName = "DogSimulator.Fusion";

        [InitializeOnLoadMethod]
        private static void RegisterWeaving() => EditorApplication.delayCall += EnsureConfiguration;

        private static void EnsureConfiguration()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<NetworkProjectConfigAsset>();
            FusionGlobalScriptableObjectUtils.EnsureAssetExists<PhotonAppSettings>();
            var config = NetworkProjectConfig.Global;
            if (config.AssembliesToWeave.Contains(AssemblyName)) return;
            config.AssembliesToWeave = config.AssembliesToWeave.Concat(new[] { AssemblyName }).ToArray();
            NetworkProjectConfigUtilities.SaveGlobalConfig(config);
            Debug.Log("Registered DogSimulator.Fusion for SDK IL weaving.");
        }

        [MenuItem("Tools/Dog Simulator/Create or Open M1 Fusion Preview")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before setup.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureConfiguration();
            if (NetworkProjectConfig.Global.PeerMode != NetworkProjectConfig.PeerModes.Single)
                throw new InvalidOperationException("This preview requires Single Peer mode: one client per process.");
            if (System.IO.File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            if (!System.IO.File.Exists(LocalPreviewSetup.ScenePath))
                throw new InvalidOperationException("Generate the existing local preview first.");
            if (!AssetDatabase.CopyAsset(LocalPreviewSetup.ScenePath, ScenePath))
                throw new InvalidOperationException("Could not copy the local preview scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var dog = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LocalMovementPreview>(true)).Single().gameObject;
            var camera = Find<DogCamera>(scene, "Main Camera");
            var oldUI = Find<LocalPreviewUI>(scene, "Preview UI");
            var canvas = oldUI.gameObject;
            Object.DestroyImmediate(oldUI);
            Object.DestroyImmediate(dog.GetComponent<LocalMovementPreview>());
            dog.name = "DogPlayer";
            var spawn = new GameObject("LocalSpawnPoint").transform;
            spawn.SetPositionAndRotation(dog.transform.position, dog.transform.rotation);

            var avatar = dog.AddComponent<NetworkObject>();
            avatar.Flags |= NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
            avatar.Flags &= ~(NetworkObjectFlags.AllowStateAuthorityOverride | NetworkObjectFlags.MasterClientObject);
            dog.AddComponent<NetworkTransform>();
            var driver = dog.AddComponent<NetworkDogPlayer>();
            var nameLabel = new GameObject("Nickname", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            nameLabel.transform.SetParent(dog.transform, false);
            nameLabel.transform.localPosition = new Vector3(0, 1.65f, 0);
            nameLabel.rectTransform.sizeDelta = new Vector2(4, 0.5f);
            nameLabel.font = Find<TMP_Text>(scene, "Player").font;
            nameLabel.fontSize = 2.2f;
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.richText = false;
            nameLabel.raycastTarget = false;
            nameLabel.text = string.Empty;
            Reference(driver, "nameLabel", nameLabel);
            var prefab = PrefabUtility.SaveAsPrefabAsset(dog, PrefabPath);
            Object.DestroyImmediate(dog);
            camera.Unbind();
            camera.transform.SetPositionAndRotation(new Vector3(0, 6, -8), Quaternion.Euler(25, 0, 0));

            var settings = AssetDatabase.LoadAssetAtPath<NetworkConfig>(ConfigPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<NetworkConfig>();
                AssetDatabase.CreateAsset(settings, ConfigPath);
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("gameVersion").stringValue = "office-m1-fusion214-v1";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var session = new GameObject("Fusion Session").AddComponent<FusionSessionService>();
            Reference(session, "config", settings);
            Reference(session, "playerPrefab", prefab.GetComponent<NetworkObject>());
            Reference(session, "followCamera", camera);
            Reference(session, "spawnPoint", spawn);

            // Wire the cloned menu in place; the original offline scene is preserved.
            var ui = canvas.AddComponent<FusionPreviewUI>();
            Reference(ui, "session", session);
            Reference(ui, "menu", Find<RectTransform>(scene, "Menu").gameObject);
            Reference(ui, "hud", Find<RectTransform>(scene, "HUD").gameObject);
            Reference(ui, "nickname", Find<TMP_InputField>(scene, "Nickname"));
            Reference(ui, "roomCode", Find<TMP_InputField>(scene, "RoomCode"));
            Reference(ui, "message", Find<TMP_Text>(scene, "Message"));
            Reference(ui, "playerLabel", Find<TMP_Text>(scene, "Player"));
            Reference(ui, "createButton", Button(scene, "CreateRoom", "Create private room"));
            Reference(ui, "joinButton", Button(scene, "JoinRoom", "Join room by code"));
            Reference(ui, "resumeButton", Button(scene, "PlayPreview", "Resume game"));
            Reference(ui, "leaveButton", Button(scene, "Reset", "Leave room / Cancel connection"));
            Reference(ui, "copyButton", Button(scene, "CheckCode", "Copy room code"));
            Find<TMP_Text>(scene, "Stage").text = "M1 NETWORK DEVELOPMENT PREVIEW / SHARED";
            Find<TMP_Text>(scene, "Status").text = "Photon Fusion 2.1.4 — configure your Fusion App ID before connecting.";
            Find<TMP_Text>(scene, "NetworkSettings").text = $"Region: {settings.Region} | Dev capacity: {settings.MaxPlayers} (unverified) | {settings.GameVersion}";
            Find<TMP_Text>(scene, "RoomLabel").text = "ROOM CODE / SAME REGION AND GAME BUILD REQUIRED";
            Find<TMP_Text>(scene, "Message").text = "Rigged dog ready. Model selection/reservations and interactions remain pending.";
            Find<TMP_Text>(scene, "Controls").text = DogAnimationDriver.Controls;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("M1 Fusion preview created using SDK APIs. Cloud/browser acceptance remains unverified.");
        }

        private static UnityEngine.UI.Button Button(Scene scene, string name, string label)
        {
            var button = Find<UnityEngine.UI.Button>(scene, name);
            button.GetComponentInChildren<TMP_Text>(true).text = label;
            button.interactable = true;
            return button;
        }

        private static T Find<T>(Scene scene, string name) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Single(component => component.name == name);

        private static void Reference(Object target, string field, Object value)
        {
            if (value == null) throw new ArgumentNullException(field);
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
