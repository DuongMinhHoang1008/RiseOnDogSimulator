using UnityEngine;
using UnityEngine.InputSystem;

namespace ReferenceDog.Animation
{
    // A standalone asset demonstration; gameplay/networking remains owned by the game.
    public sealed class ReferenceDogAnimationPreview : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private bool autoShowcase = true;
        private static readonly string[] Clips = { "Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite" };
        private static readonly float[] HoldSeconds = { 2f, 2.4f, 1.6f, 1.4f, 3.8f, 3.4f, 1.1f };
        private int showcaseIndex;
        private float nextChange;

        public void Configure(Animator target)
        {
            animator = target;
        }

        private void OnEnable()
        {
            showcaseIndex = 0;
            nextChange = Time.time + HoldSeconds[0];
        }

        public void SetSpeed(float speed)
        {
            if (animator != null) animator.SetFloat("Speed", Mathf.Max(0, speed));
        }

        public void PlayAction(string clip)
        {
            if (animator == null) return;
            foreach (var name in Clips)
                if (name != "Idle" && name != "Run") animator.ResetTrigger(name);
            if (clip == "Idle" || clip == "Run")
            {
                SetSpeed(clip == "Run" ? 1 : 0);
                animator.CrossFadeInFixedTime(clip, .08f);
            }
            else
            {
                SetSpeed(0);
                animator.SetTrigger(clip);
            }
        }

        private void Update()
        {
            var keys = Keyboard.current;
            var manual = -1;
            if (keys != null)
            {
                if (keys.digit1Key.wasPressedThisFrame) manual = 0;
                if (keys.digit2Key.wasPressedThisFrame) manual = 1;
                if (keys.digit3Key.wasPressedThisFrame) manual = 2;
                if (keys.digit4Key.wasPressedThisFrame) manual = 3;
                if (keys.digit5Key.wasPressedThisFrame) manual = 4;
                if (keys.digit6Key.wasPressedThisFrame) manual = 5;
                if (keys.digit7Key.wasPressedThisFrame) manual = 6;
            }
            if (manual >= 0)
            {
                autoShowcase = false;
                PlayAction(Clips[manual]);
            }
            if (!autoShowcase || Time.time < nextChange) return;
            showcaseIndex = (showcaseIndex + 1) % Clips.Length;
            PlayAction(Clips[showcaseIndex]);
            nextChange = Time.time + HoldSeconds[showcaseIndex];
        }
    }
}
