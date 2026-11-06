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
        private int displayedModelId = int.MinValue;
        public int DisplayedModelId => displayedModelId;
        public bool HasRenderedModel => previewModel != null;
        public bool RenderTextureReady => renderTexture != null && renderTexture.IsCreated();

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
            if (previewModel != null) Destroy(previewModel);
            previewAnchor.localRotation = Quaternion.identity;
            previewModel = Instantiate(entry.VisualPrefab, previewAnchor, false);
            previewModel.name = $"Preview_{entry.ModelId}_{entry.DisplayName}";
            previewModel.transform.SetLocalPositionAndRotation(entry.LocalPosition, entry.LocalRotation);
            previewModel.transform.localScale = entry.LocalScale;
            PrepareHierarchy(previewModel.transform);
            var animator = previewModel.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = previewModel.AddComponent<Animator>();
            animator.runtimeAnimatorController = entry.AnimationController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
            FrameModel();
        }

        public void SetVisible(bool visible)
        {
            if (viewport != null) viewport.gameObject.SetActive(visible);
            if (previewCamera != null) previewCamera.enabled = visible;
        }

        private void Update()
        {
            if (previewModel != null && previewCamera.enabled)
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
            var center = bounds.center;
            var radius = Mathf.Max(.25f, bounds.extents.magnitude);
            var halfFov = previewCamera.fieldOfView * .5f * Mathf.Deg2Rad;
            var distance = radius / Mathf.Tan(halfFov) * 1.25f;
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
