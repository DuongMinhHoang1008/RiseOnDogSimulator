using DogSimulator.Player;
using UnityEngine;

namespace DogSimulator.CustomDogFace
{
    [DisallowMultipleComponent]
    public sealed class DogFaceAppearance : MonoBehaviour
    {
        [SerializeField] private DogVisualPresenter presenter;
        private byte[] confirmedJpeg;
        private string confirmedHash;
        private int modelId;
        private int layoutVersion;

        public string ConfirmedHash => confirmedHash ?? string.Empty;
        public bool HasCustomFace => confirmedJpeg is { Length: > 0 };

        private void Awake()
        {
            if (presenter != null) presenter.VisualChanged += Rebind;
        }

        public bool ApplyCustom(int expectedModelId, int expectedLayout, byte[] jpeg)
        {
            if (!DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _)) return false;
            confirmedJpeg = (byte[])jpeg.Clone();
            confirmedHash = DogFaceImageValidator.Sha256Hex(confirmedJpeg);
            modelId = expectedModelId;
            layoutVersion = expectedLayout;
            return Rebind();
        }

        public void ApplyDefault(int expectedModelId, int expectedLayout)
        {
            confirmedJpeg = null;
            confirmedHash = string.Empty;
            modelId = expectedModelId;
            layoutVersion = expectedLayout;
            Rebind();
        }

        private bool Rebind()
        {
            if (presenter == null || presenter.RenderedModelId != modelId) return false;
            var surface = presenter.FaceSurface;
            if (surface == null || surface.LayoutVersion != layoutVersion) return false;
            if (confirmedJpeg == null) { surface.ResetToDefault(); return true; }
            return surface.TryApplyJpeg(confirmedJpeg);
        }

        private void OnDestroy()
        {
            if (presenter != null) presenter.VisualChanged -= Rebind;
        }
    }
}
