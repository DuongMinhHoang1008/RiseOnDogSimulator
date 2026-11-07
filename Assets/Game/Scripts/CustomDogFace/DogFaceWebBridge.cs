using System;
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
        [DllImport("__Internal")]
        private static extern void DogFaceSetImageDrop(int enabled, string objectName, string successMethod,
            string errorMethod, int maxBytes, int maxDimension, int maxPixels, int editorDimension);

            DogFaceOpenImagePicker(gameObject.name, nameof(OnWebImagePicked), nameof(OnWebPickerFailed),
                CustomDogFaceConstants.MaximumSourceBytes, CustomDogFaceConstants.MaximumSourceDimension,
                CustomDogFaceConstants.MaximumSourcePixels, CustomDogFaceConstants.MaximumEditorDimension);
#elif UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("Choose dog face", string.Empty, "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path)) return;
            try { ImagePicked?.Invoke(System.IO.File.ReadAllBytes(path)); }
            catch (Exception exception) { PickerFailed?.Invoke(exception.Message); }
#else
            PickerFailed?.Invoke("Image picking is currently supported by the WebGL desktop build.");
#endif
        }

        public void OnWebImagePicked(string base64)
        {
            try { ImagePicked?.Invoke(Convert.FromBase64String(base64)); }
            catch (FormatException) { PickerFailed?.Invoke("The browser returned invalid image data."); }
        }

        public void OnWebPickerFailed(string message) => PickerFailed?.Invoke(message);

        private void OnDestroy() { if (Instance == this) Instance = null; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DogFaceOpenImagePicker(string objectName, string successMethod,
            string errorMethod, int maxBytes, int maxDimension, int maxPixels, int editorDimension);
#endif
    }
}
