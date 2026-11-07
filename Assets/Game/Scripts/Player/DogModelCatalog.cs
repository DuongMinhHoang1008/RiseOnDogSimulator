using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogSimulator.Player
{
    [CreateAssetMenu(menuName = "Dog Simulator/Dog Model Catalog")]
    public sealed class DogModelCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Min(1)] private int modelId;
            [SerializeField] private string displayName;
            [SerializeField] private GameObject visualPrefab;
            [SerializeField] private RuntimeAnimatorController animationController;
            [SerializeField] private Vector3 localPosition;
            [SerializeField] private Vector3 localEulerAngles;
            [SerializeField] private Vector3 localScale = Vector3.one;
            [SerializeField] private Vector3 mouthAnchor = new Vector3(0, .8f, .6f);
            [SerializeField] private bool supportsCustomFace;
            [SerializeField, Min(0)] private int faceLayoutVersion;

            public int ModelId => modelId;
            public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? $"Dog {modelId}" : displayName;
            public GameObject VisualPrefab => visualPrefab;
            public RuntimeAnimatorController AnimationController => animationController;
            public Vector3 LocalPosition => localPosition;
            public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
            public Vector3 LocalScale => localScale == Vector3.zero ? Vector3.one : localScale;
            public Vector3 MouthAnchor => mouthAnchor;
            public bool SupportsCustomFace => supportsCustomFace && faceLayoutVersion > 0;
            public bool CustomFaceRequested => supportsCustomFace;
            public int FaceLayoutVersion => SupportsCustomFace ? faceLayoutVersion : 0;
        }

        [SerializeField, Min(1)] private int fallbackModelId = 1;
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public int FallbackModelId => fallbackModelId;
        public IReadOnlyList<Entry> Entries => entries;
        public int SelectableCount => entries?.Length ?? 0;

        public bool TryGet(int modelId, out Entry entry)
        {
            if (entries != null)
            {
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null && entries[i].ModelId == modelId)
                    {
                        entry = entries[i];
                        return true;
                    }
                }
            }
            entry = null;
            return false;
        }

        public Entry Resolve(int modelId)
        {
            if (TryGet(modelId, out var entry)) return entry;
            TryGet(fallbackModelId, out entry);
            return entry;
        }

        public bool IsValidModelId(int modelId) => TryGet(modelId, out _);

        public bool Validate(out string error)
        {
            var ids = new HashSet<int>();
            if (entries == null || entries.Length == 0)
            {
                error = "Catalog has no entries.";
                return false;
            }
            foreach (var entry in entries)
            {
                if (entry == null || entry.ModelId <= 0)
                {
                    error = "Every catalog entry needs a positive stable ModelId.";
                    return false;
                }
                if (!ids.Add(entry.ModelId))
                {
                    error = $"Duplicate Dog ModelId {entry.ModelId}.";
                    return false;
                }
                if (entry.CustomFaceRequested && entry.FaceLayoutVersion <= 0)
                {
                    error = $"Custom-face model {entry.ModelId} needs a positive layout version.";
                    return false;
                }
            }
            if (!ids.Contains(fallbackModelId))
            {
                error = $"Fallback ModelId {fallbackModelId} is not in the catalog.";
                return false;
            }
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Validate(out var error)) Debug.LogError($"Dog model catalog '{name}': {error}", this);
        }
#endif
    }
}
