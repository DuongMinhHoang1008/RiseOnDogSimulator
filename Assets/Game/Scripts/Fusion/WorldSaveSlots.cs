using System;
using UnityEngine;

namespace DogSimulator.Networking
{
    internal static class WorldSaveSlots
    {
        public const int Count = 3;
        private const int FormatVersion = 1;

        [Serializable]
        internal struct PropPose
        {
            public Vector3 position;
            public Quaternion rotation;

            public bool IsValid => position.sqrMagnitude < 10000f * 10000f
                && Mathf.Abs(Quaternion.Dot(rotation, rotation) - 1f) < .02f;
        }

        [Serializable]
        private sealed class Snapshot
        {
            public int version;
            public PropPose[] props;
        }

        public static bool IsValidSlot(int slot) => slot >= 1 && slot <= Count;

        public static PropPose[] Load(int slot)
        {
            if (!IsValidSlot(slot)) return null;
            var json = PlayerPrefs.GetString(Key(slot));
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var snapshot = JsonUtility.FromJson<Snapshot>(json);
                return snapshot != null && snapshot.version == FormatVersion ? snapshot.props : null;
            }
            catch (ArgumentException) { return null; }
        }

        public static void Save(int slot, PropPose[] props)
        {
            if (!IsValidSlot(slot)) return;
            var key = Key(slot);
            var json = JsonUtility.ToJson(new Snapshot { version = FormatVersion, props = props });
            if (PlayerPrefs.GetString(key) == json) return;
            try
            {
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
            }
            catch (PlayerPrefsException) { Debug.LogWarning($"Could not save world slot {slot} on this device."); }
        }

        private static string Key(int slot) => $"DogSimulator.WorldSlot.{slot}";

        [Serializable]
        internal struct CityProp
        {
            public string id;
            public PropPose pose;
        }

        [Serializable]
        private sealed class CitySnapshot
        {
            public CityProp[] props;
        }

        internal static CityProp[] LoadCity(int slot)
        {
            if (!IsValidSlot(slot)) return null;
            var json = PlayerPrefs.GetString(CityKey(slot));
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<CitySnapshot>(json)?.props; }
            catch (ArgumentException) { return null; }
        }

        internal static void SaveCity(int slot, CityProp[] props)
        {
            if (!IsValidSlot(slot)) return;
            var key = CityKey(slot);
            var json = JsonUtility.ToJson(new CitySnapshot { props = props });
            if (PlayerPrefs.GetString(key) == json) return;
            try
            {
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
            }
            catch (PlayerPrefsException) { Debug.LogWarning($"Could not save city slot {slot} on this device."); }
        }

        private static string CityKey(int slot) => $"DogSimulator.{SimplePolyCityWorld.SaveNamespace}.WorldSlot.{slot}";
    }
}
