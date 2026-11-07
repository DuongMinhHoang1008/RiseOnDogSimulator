using System;
using UnityEngine;

namespace DogSimulator.CustomDogFace
{
    [Serializable]
    public sealed class DogFaceRecord
    {
        public int schemaVersion = 1;
        public int layoutVersion = CustomDogFaceConstants.LayoutVersion;
        public string sha256;
        public string jpegBase64;

        public static DogFaceRecord Create(byte[] jpeg)
            => new()
            {
                sha256 = DogFaceImageValidator.Sha256Hex(jpeg),
                jpegBase64 = Convert.ToBase64String(jpeg)
            };

        public bool TryGetJpeg(out byte[] jpeg)
        {
            jpeg = null;
            if (schemaVersion != 1 || layoutVersion != CustomDogFaceConstants.LayoutVersion
                || string.IsNullOrWhiteSpace(sha256) || string.IsNullOrWhiteSpace(jpegBase64)) return false;
            try { jpeg = Convert.FromBase64String(jpegBase64); }
            catch (FormatException) { return false; }
            return DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _)
                && string.Equals(sha256, DogFaceImageValidator.Sha256Hex(jpeg), StringComparison.OrdinalIgnoreCase);
        }

        public string ToJson() => JsonUtility.ToJson(this);
        public static DogFaceRecord FromJson(string json)
            => string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<DogFaceRecord>(json);
    }
}
