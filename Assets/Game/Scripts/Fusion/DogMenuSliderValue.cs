using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DogSimulator.Networking
{
    internal sealed class DogMenuSliderValue : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format;

#if UNITY_EDITOR
        internal void Bind(Slider source, TMP_Text target, string valueFormat)
        {
            slider = source;
            label = target;
            format = valueFormat;
        }
#endif

        private void OnEnable()
        {
            slider.onValueChanged.AddListener(ShowValue);
            ShowValue(slider.value);
        }

        private void OnDisable() => slider.onValueChanged.RemoveListener(ShowValue);
        private void ShowValue(float value) => label.text = value.ToString(format);
    }
}
