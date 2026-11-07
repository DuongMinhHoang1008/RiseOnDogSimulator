using UnityEngine;

namespace DogSimulator.CustomDogFace
{
    [DisallowMultipleComponent]
    public sealed class DogFaceSurface : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private int layoutVersion = CustomDogFaceConstants.LayoutVersion;
        private Material materialInstance;
        private Texture appliedTexture;
        private Texture defaultTexture;
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
                defaultTexture = targetRenderer.sharedMaterial != null ? targetRenderer.sharedMaterial.mainTexture : null;
                materialInstance = new Material(targetRenderer.sharedMaterial) { name = $"{name} Face Material" };
                targetRenderer.material = materialInstance;
            }
            materialInstance.mainTexture = texture;
            appliedTexture = texture;
            ownsTexture = takeOwnership;
        }

        public void ResetToDefault()
        {
            if (ownsTexture && appliedTexture != null) Destroy(appliedTexture);
            ownsTexture = false;
            appliedTexture = defaultTexture;
            if (materialInstance != null) materialInstance.mainTexture = defaultTexture;
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
