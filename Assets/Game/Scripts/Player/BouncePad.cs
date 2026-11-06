using UnityEngine;

namespace DogSimulator.Player
{
    [DisallowMultipleComponent]
    public sealed class BouncePad : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float bounceSpeed = 11f;
        public float BounceSpeed => bounceSpeed;
    }
}
