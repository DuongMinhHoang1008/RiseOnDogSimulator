using UnityEngine;

namespace DogSimulator.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DogBackSupport : MonoBehaviour
    {
        [SerializeField] private Transform ownerRoot;
        public Transform OwnerRoot => ownerRoot != null ? ownerRoot : transform.root;
        public DogMotor OwnerMotor => OwnerRoot != null ? OwnerRoot.GetComponent<DogMotor>() : null;

        private void Reset()
        {
            ownerRoot = transform.root;
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = new Vector3(0.9f, 0.12f, 1.15f);
        }
    }
}
