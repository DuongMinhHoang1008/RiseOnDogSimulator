using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DogSimulator.CustomDogFace
{
    public sealed class DogFaceWebBridge : MonoBehaviour
    {
        public static DogFaceWebBridge Instance { get; private set; }
        public event Action<byte[]> ImagePicked;
        public event Action<string> PickerFailed;

        private void Awake() => Instance = this;

        public void SetImageDropEnabled(bool enabled)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DogFaceSetImageDrop(enabled ? 1 : 0, gameObject.name, nameof(OnWebImagePicked), nameof(OnWebPickerFailed),
                CustomDogFaceConstants.MaximumSourceBytes, CustomDogFaceConstants.MaximumSourceDimension,
                CustomDogFaceConstants.MaximumSourcePixels, CustomDogFaceConstants.MaximumEditorDimension);
#endif
        }

        public void OpenPicker()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DogFaceOpenImagePicker(gameObject.name, nameof(OnWebImagePicked), nameof(OnWebPickerFailed),
                CustomDogFaceConstants.MaximumSourceBytes, CustomDogFaceConstants.MaximumSourceDimension,
                CustomDogFaceConstants.MaximumSourcePixels, CustomDogFaceConstants.MaximumEditorDimension);
#elif UNITY_EDITOR
            ReadLocalImage(UnityEditor.EditorUtility.OpenFilePanel("Choose dog face", string.Empty, "png,jpg,jpeg"));
#elif UNITY_STANDALONE_WIN
            var path = DogFaceWindowsPicker.Open(out var error);
            if (error != 0)
                PickerFailed?.Invoke($"Could not open the image picker (0x{error:X}).");
            else
                ReadLocalImage(path);
#else
            PickerFailed?.Invoke("Image picking is supported on Windows and desktop browsers.");
#endif
        }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        private void ReadLocalImage(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            byte[] bytes;
            try
            {
                using var stream = File.OpenRead(path);
                var length = stream.Length;
                if (length > CustomDogFaceConstants.MaximumSourceBytes)
                {
                    PickerFailed?.Invoke("The image is larger than 16 MiB.");
                    return;
                }

                using var reader = new BinaryReader(stream);
                bytes = reader.ReadBytes((int)length);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                PickerFailed?.Invoke($"Could not read the image. {exception.Message}");
                return;
            }

            ImagePicked?.Invoke(bytes);
        }
#endif

        public void OnWebImagePicked(string base64)
        {
            try { ImagePicked?.Invoke(Convert.FromBase64String(base64)); }
            catch (FormatException) { PickerFailed?.Invoke("The browser returned invalid image data."); }
        }

        public void OnWebPickerFailed(string message) => PickerFailed?.Invoke(message);

        private void OnDestroy() { if (Instance == this) Instance = null; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DogFaceSetImageDrop(int enabled, string objectName, string successMethod,
            string errorMethod, int maxBytes, int maxDimension, int maxPixels, int editorDimension);

        [DllImport("__Internal")]
        private static extern void DogFaceOpenImagePicker(string objectName, string successMethod,
            string errorMethod, int maxBytes, int maxDimension, int maxPixels, int editorDimension);
#endif
    }
}
