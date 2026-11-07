using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DogSimulator.CustomDogFace
{
    public static class DogFaceStorage
    {
        public static bool TryLoad(out byte[] jpeg)
        {
            jpeg = null;
            var json = PlayerPrefs.GetString(CustomDogFaceConstants.StorageKey, string.Empty);
            try { return DogFaceRecord.FromJson(json)?.TryGetJpeg(out jpeg) == true; }
            catch (Exception) { return false; }
        }

        public static bool Save(byte[] jpeg)
        {
            if (!DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _)) return false;
            try
            {
                var json = DogFaceRecord.Create(jpeg).ToJson();
                PlayerPrefs.SetString(CustomDogFaceConstants.StorageKey, json);
                PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
                DogFaceStorageSave(json);
#endif
                return true;
            }
            catch (Exception) { return false; }
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(CustomDogFaceConstants.StorageKey);
            PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
            DogFaceStorageDelete();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void DogFaceStorageSave(string json);
        [DllImport("__Internal")] private static extern void DogFaceStorageDelete();
#endif
    }
}
