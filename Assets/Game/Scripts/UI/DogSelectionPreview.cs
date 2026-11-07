using DogSimulator.CustomDogFace;
using DogSimulator.Player;
using UnityEngine;
using UnityEngine.UI;

namespace DogSimulator.UI
{
    public sealed class DogSelectionPreview : MonoBehaviour
    {
        private const int PreviewLayer = 31;
        [SerializeField] private DogModelCatalog catalog;
        [SerializeField] private RawImage viewport;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform previewAnchor;
        [SerializeField, Min(0f)] private float rotationDegreesPerSecond = 16f;
        private RenderTexture renderTexture;
        private GameObject previewModel;
        private bool floating;
        private bool menuPresentation;
        private Vector2 viewportRestPosition;
        private int displayedModelId = int.MinValue;
        public int DisplayedModelId => displayedModelId;
        public bool HasRenderedModel => previewModel != null;
        public bool RenderTextureReady => renderTexture != null && renderTexture.IsCreated();
        public DogFaceSurface FaceSurface => previewModel != null
            ? previewModel.GetComponentInChildren<DogFaceSurface>(true) : null;

        private void Awake()
        {
            renderTexture = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32)
            {
                name = "Dog Selection Preview",
                antiAliasing = 2,
                useMipMap = false
            };
            renderTexture.Create();
            previewCamera.targetTexture = renderTexture;
            viewport.texture = renderTexture;
        }

        public void ShowModel(int modelId)
        {
            if (catalog == null || previewAnchor == null || displayedModelId == modelId) return;
            var entry = catalog.Resolve(modelId);
            if (entry?.VisualPrefab == null) return;
            displayedModelId = modelId;
            if (previewModel != null)
            {
                previewModel.SetActive(false);
                Destroy(previewModel);
            }
            previewAnchor.localRotation = Quaternion.identity;
            previewModel = DogVisualPresenter.InstantiateVisual(entry, previewAnchor);
            previewModel.name = $"Preview_{entry.ModelId}_{entry.DisplayName}";
            PrepareHierarchy(previewModel.transform);
            var animator = previewModel.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = previewModel.AddComponent<Animator>();
            animator.runtimeAnimatorController = entry.AnimationController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
            if (menuPresentation) Rotate(145f);
            FrameModel();
        }

        public void PresentIn(RawImage destination, bool floatModel = false)
        {
            if (viewport != null && viewport != destination) viewport.gameObject.SetActive(false);
            viewport = destination;
            viewport.texture = renderTexture;
            viewportRestPosition = viewport.rectTransform.anchoredPosition;
            menuPresentation = true;
            floating = floatModel;
            previewCamera.backgroundColor = Color.clear;
            Rotate(145f);
            if (previewModel != null) FrameModel();
            SetVisible(true);
        }

        public void FocusFace()
        {
            if (FaceSurface?.TargetRenderer == null) return;
            Rotate(180);
            FrameBounds(FaceSurface.TargetRenderer.bounds, 1.28f);
        }

        public bool ApplyFaceJpeg(byte[] jpeg)
            => FaceSurface != null && FaceSurface.TryApplyJpeg(jpeg);

        public void ResetRotation() { if (previewAnchor != null) previewAnchor.localRotation = Quaternion.identity; }

        public void Rotate(float degrees)
        {
            if (previewAnchor != null) previewAnchor.localRotation = Quaternion.Euler(0, degrees, 0);
        }

        public void ResetFaceToDefault() => FaceSurface?.ResetToDefault();

        public void SetVisible(bool visible)
        {
            if (viewport != null) viewport.gameObject.SetActive(visible);
            if (previewCamera != null) previewCamera.enabled = visible;
        }

        private void Update()
        {
            if (previewModel == null || !previewCamera.enabled) return;
            if (floating)
            {
                var wave = Time.unscaledTime * 2.2f;
                viewport.rectTransform.anchoredPosition = viewportRestPosition + Vector2.up * Mathf.Sin(wave) * 14f;
                Rotate(145f + Mathf.Sin(wave * .65f) * 13f);
            }
            else if (!menuPresentation)
                previewAnchor.Rotate(0f, rotationDegreesPerSecond * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        private static void PrepareHierarchy(Transform root)
        {
            root.gameObject.layer = PreviewLayer;
            foreach (var collider in root.GetComponents<Collider>()) collider.enabled = false;
            foreach (var body in root.GetComponents<Rigidbody>())
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            for (var i = 0; i < root.childCount; i++) PrepareHierarchy(root.GetChild(i));
        }

        private void FrameModel()
        {
            var renderers = previewModel.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            FrameBounds(bounds, menuPresentation ? 1.02f : 1.25f);
        }

        private void FrameBounds(Bounds bounds, float padding)
        {
            var center = bounds.center;
            var radius = Mathf.Max(.25f, bounds.extents.magnitude);
            var halfFov = previewCamera.fieldOfView * .5f * Mathf.Deg2Rad;
            var distance = radius / Mathf.Tan(halfFov) * padding;
            var direction = new Vector3(0f, .18f, -1f).normalized;
            previewCamera.transform.position = center + direction * distance;
            previewCamera.transform.LookAt(center + Vector3.up * bounds.extents.y * .06f);
            previewCamera.nearClipPlane = Mathf.Max(.01f, distance - radius * 1.6f);
            previewCamera.farClipPlane = distance + radius * 2.5f;
        }

        private void OnDestroy()
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (renderTexture == null) return;
            renderTexture.Release();
            Destroy(renderTexture);
        }
    }
}
