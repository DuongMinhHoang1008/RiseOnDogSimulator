using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogSimulator.UI
{
    public sealed class FpsHud : MonoBehaviour
    {
        public const string PreferenceKey = "DogSimulator.ShowFps";
        private const float SampleSeconds = .5f;
        [SerializeField] private Toggle toggle;
        [SerializeField] private GameObject display;
        [SerializeField] private TMP_Text label;
        private int frames;
        private float elapsed;
        private bool visible;

        public bool Visible => visible;

        private void Awake()
        {
            visible = PlayerPrefs.GetInt(PreferenceKey, 0) != 0;
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(visible);
                toggle.onValueChanged.AddListener(SetVisible);
            }
            ApplyVisibility();
        }

        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value;
            PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
            frames = 0;
            elapsed = 0;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (display != null) display.SetActive(visible);
        }

        private void Update()
        {
            if (!visible || label == null) return;
            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < SampleSeconds) return;
            var milliseconds = elapsed * 1000f / Mathf.Max(1, frames);
            var fps = frames / Mathf.Max(.0001f, elapsed);
            label.SetText("{0:0} FPS  |  {1:0.0} ms", fps, milliseconds);
            frames = 0;
            elapsed = 0;
        }

        private void OnDestroy()
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(SetVisible);
        }
    }
}
