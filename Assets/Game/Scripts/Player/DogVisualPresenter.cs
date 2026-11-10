using System;
using DogSimulator.CustomDogFace;
using UnityEngine;

namespace DogSimulator.Player
{
    // Visual only: no input, physics, network writes or autonomous demo playback.
    public sealed class DogVisualPresenter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private DogAnimationConfig config;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject defaultVisual;
        [SerializeField] private Transform mouthAnchor;
        private int renderedSequence = int.MinValue;
        private int renderedModelId = int.MinValue;
        private DogAction renderedAction;
        [SerializeField] private DogFaceSurface defaultFaceSurface;
        [SerializeField] private Transform defaultPeePelvis;
        private DogModelVisual instantiatedVisual;
        private Quaternion standingRotation;
        private bool hasStandingRotation;
        private bool wasKnockedDown;
        private float knockdownBlend;
        private float knockdownSide = 1f;
        private const float KnockdownTilt = 82f;
        private const float FallDuration = .35f;
        public Animator Animator => instantiatedVisual != null ? instantiatedVisual.Animator : animator;
        public Transform PeePelvis => instantiatedVisual != null ? instantiatedVisual.PeePelvis : defaultPeePelvis;
        public DogAction RenderedAction => renderedAction;
        public int RenderedModelId => renderedModelId;
        public DogFaceSurface FaceSurface { get; private set; }
        public event Func<bool> VisualChanged;

        public void PresentKnockdown(bool knockedDown, Vector3 direction)
        {
            if (visualRoot == null) return;
            if (!hasStandingRotation)
            {
                standingRotation = visualRoot.localRotation;
                hasStandingRotation = true;
            }
            if (knockedDown && !wasKnockedDown)
                knockdownSide = Vector3.Dot(direction, transform.right) >= 0 ? -1f : 1f;

            if (!knockedDown)
            {
                // Getting up is intentionally immediate once all knockback inertia is gone.
                knockdownBlend = 0;
                visualRoot.localRotation = standingRotation;
                wasKnockedDown = false;
                return;
            }

            knockdownBlend = Mathf.MoveTowards(knockdownBlend, 1f,
                Time.deltaTime / Mathf.Max(.01f, FallDuration));
            var easedBlend = Mathf.SmoothStep(0, 1, knockdownBlend);
            visualRoot.localRotation = standingRotation
                * Quaternion.Euler(0, 0, KnockdownTilt * knockdownSide * easedBlend);
            wasKnockedDown = true;
        }

        private void OnEnable() => renderedSequence = int.MinValue;

        public void ApplyModel(DogModelCatalog catalog, int modelId)
        {
            if (renderedModelId == modelId || catalog == null) return;
            var entry = catalog.Resolve(modelId);
            if (entry == null) return;
            renderedModelId = modelId;
            renderedSequence = int.MinValue;
            if (instantiatedVisual != null)
            {
                instantiatedVisual.gameObject.SetActive(false);
                Destroy(instantiatedVisual.gameObject);
            }
            instantiatedVisual = null;

            if (entry.VisualPrefab == null)
            {
                if (defaultVisual != null) defaultVisual.SetActive(true);
            }
            else
            {
                InstallModelVisual(entry);
            }

            if (mouthAnchor != null) mouthAnchor.localPosition = entry.MouthAnchor;
            FaceSurface = instantiatedVisual != null ? instantiatedVisual.FaceSurface : defaultFaceSurface;
            VisualChanged?.Invoke();
        }

        private void InstallModelVisual(DogModelCatalog.Entry entry)
        {
            if (defaultVisual != null) defaultVisual.SetActive(false);
            var parent = visualRoot != null ? visualRoot : transform;
            instantiatedVisual = InstantiateVisual(entry, parent);
            instantiatedVisual.name = $"DogModel_{entry.ModelId}_{entry.DisplayName}";
            SetLayerRecursively(instantiatedVisual.transform, parent.gameObject.layer);
            var animator = instantiatedVisual.Animator;
            if (entry.AnimationController != null) animator.runtimeAnimatorController = entry.AnimationController;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
        }

        internal static DogModelVisual InstantiateVisual(DogModelCatalog.Entry entry, Transform parent)
        {
            var visual = Instantiate(entry.Visual, parent, false);
            visual.transform.SetLocalPositionAndRotation(entry.LocalPosition, entry.LocalRotation);
            visual.transform.localScale = entry.LocalScale;
            visual.DisableDemos();
            return visual;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }

        public void Present(float speed, bool grounded, DogAction action, int sequence, float remainingSeconds)
        {
            var animator = Animator;
            if (animator == null || config == null || !animator.isActiveAndEnabled) return;
            speed = Mathf.Clamp01(speed);
            animator.SetFloat("Speed", speed);
            animator.SetFloat("LocomotionRate", Mathf.Lerp(config.MinimumRunRate, config.MaximumRunRate, speed));
            animator.SetBool("Grounded", grounded);
            if (renderedSequence == sequence && renderedAction == action) return;
            renderedSequence = sequence;
            renderedAction = action;
            if (action == DogAction.None)
            {
                animator.CrossFadeInFixedTime(speed > .03f ? "Run" : "Idle", config.CrossFadeSeconds);
                return;
            }
            var duration = config.Duration(action);
            var elapsed = Mathf.Max(0, duration - remainingSeconds);
            var rate = action == DogAction.Jump ? config.JumpRate : 1;
            // Start partway through an existing action when a remote/late-joining peer first sees it.
            var clipOffset = Mathf.Clamp(elapsed * rate, 0, duration * .98f);
            animator.CrossFadeInFixedTime(action.ToString(), config.CrossFadeSeconds, 0, clipOffset);
        }
    }
}
