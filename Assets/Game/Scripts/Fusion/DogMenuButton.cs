using UnityEngine;

namespace DogSimulator.Networking
{
    internal sealed class DogMenuButton : UnityEngine.UI.Button
    {
        private float targetScale = 1;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            targetScale = state == SelectionState.Pressed ? .95f : state == SelectionState.Highlighted ? 1.025f : 1;
        }

        private void Update()
        {
            var scale = Mathf.Lerp(transform.localScale.x, targetScale, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 22));
            transform.localScale = new Vector3(scale, scale, 1);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            transform.localScale = Vector3.one;
            targetScale = 1;
        }
    }
}
