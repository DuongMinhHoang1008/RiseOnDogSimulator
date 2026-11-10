using UnityEngine;

namespace DogSimulator.CustomDogFace
{
    [DisallowMultipleComponent]
    public sealed class DogFaceSurface : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private int layoutVersion = CustomDogFaceConstants.LayoutVersion;
        [SerializeField] private Material portraitMaterial;
        private Material materialInstance;
        private Material defaultMaterial;
        private Texture appliedTexture;
        private bool ownsTexture;

        public int LayoutVersion => layoutVersion;
        public Renderer TargetRenderer => targetRenderer;

        public void Apply(Texture texture, bool takeOwnership = false)
        {
            if (targetRenderer == null || texture == null) return;
            targetRenderer.enabled = true;
            if (ownsTexture && appliedTexture != null && appliedTexture != texture) Destroy(appliedTexture);
            if (materialInstance == null)
            {
                defaultMaterial = targetRenderer.sharedMaterial;
                // The stock face follows the coat lighting, while a player photo needs its original colors.
                var template = portraitMaterial != null ? portraitMaterial : defaultMaterial;
                materialInstance = new Material(template) { name = $"{name} Face Material" };
            }
            materialInstance.mainTexture = texture;
            targetRenderer.sharedMaterial = materialInstance;
            appliedTexture = texture;
            ownsTexture = takeOwnership;
        }

        public void ResetToDefault()
        {
            if (ownsTexture && appliedTexture != null) Destroy(appliedTexture);
            ownsTexture = false;
            appliedTexture = null;
            if (materialInstance == null) return;
            materialInstance.mainTexture = null;
            if (targetRenderer != null) targetRenderer.sharedMaterial = defaultMaterial;
        }

        public bool TryApplyJpeg(byte[] jpeg)
        {
            if (!DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _)) return false;
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false, false)
            {
                name = $"Custom Dog Face {DogFaceImageValidator.Sha256Hex(jpeg)[..8]}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            if (!ImageConversion.LoadImage(texture, jpeg, true)
                || texture.width != CustomDogFaceConstants.TextureSize || texture.height != CustomDogFaceConstants.TextureSize)
            { Destroy(texture); return false; }
            Apply(texture, true);
            return true;
        }

        private void OnDestroy()
        {
            if (ownsTexture && appliedTexture != null) Destroy(appliedTexture);
            if (materialInstance != null) Destroy(materialInstance);
        }
    }
}
