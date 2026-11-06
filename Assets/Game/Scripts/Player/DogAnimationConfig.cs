using UnityEngine;

namespace DogSimulator.Player
{
    public enum DogAction { None, Jump, Bark, Poop, Pee, Bite }

    [CreateAssetMenu(menuName = "Dog Simulator/Animation Config")]
    public sealed class DogAnimationConfig : ScriptableObject
    {
        [SerializeField, Min(.1f)] private float jumpSeconds = 1.4f;
        [SerializeField, Min(.1f)] private float barkSeconds = 1.2f;
        [SerializeField, Min(.1f)] private float poopSeconds = 3.6f;
        [SerializeField, Min(.1f)] private float peeSeconds = 3.2f;
        [SerializeField, Min(.1f)] private float biteSeconds = .9f;
        [SerializeField, Range(.02f, .3f)] private float crossFadeSeconds = .08f;
        [SerializeField, Range(.2f, 2)] private float minimumRunRate = .65f;
        [SerializeField, Range(.5f, 3)] private float maximumRunRate = 1.5f;
        [SerializeField, Range(.5f, 4)] private float jumpRate = 2f;
        public float CrossFadeSeconds => crossFadeSeconds;
        public float MinimumRunRate => minimumRunRate;
        public float MaximumRunRate => maximumRunRate;
        public float JumpRate => jumpRate;
        public float Duration(DogAction action)
        {
            switch (action)
            {
                case DogAction.Jump: return jumpSeconds;
                case DogAction.Bark: return barkSeconds;
                case DogAction.Poop: return poopSeconds;
                case DogAction.Pee: return peeSeconds;
                case DogAction.Bite: return biteSeconds;
                default: return 0;
            }
        }
    }
}
