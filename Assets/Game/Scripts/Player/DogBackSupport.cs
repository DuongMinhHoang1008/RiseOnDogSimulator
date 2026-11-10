using UnityEngine;

namespace DogSimulator.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DogBackSupport : MonoBehaviour
    {
        [SerializeField] private Transform ownerRoot;
        [SerializeField] private DogMotor ownerMotor;
        public Transform OwnerRoot => ownerRoot;
        public DogMotor OwnerMotor => ownerMotor;

#if UNITY_EDITOR
        private void Reset()
        {
            ownerRoot = transform.root;
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = new Vector3(0.9f, 0.12f, 1.15f);
        }
#endif
    }
}
