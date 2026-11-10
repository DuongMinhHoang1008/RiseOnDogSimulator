using DogSimulator.CustomDogFace;
using UnityEngine;

namespace DogSimulator.Player
{
    [DisallowMultipleComponent]
    public sealed class DogModelVisual : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private DogFaceSurface faceSurface;
        [SerializeField] private Transform peePelvis;
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Collider[] colliders;
        [SerializeField] private Rigidbody[] bodies;
        [SerializeField] private Behaviour[] demoBehaviours;

        internal Animator Animator => animator;
        internal DogFaceSurface FaceSurface => faceSurface;
        internal Transform PeePelvis => peePelvis;
        internal Renderer[] Renderers => renderers;

        internal void DisableDemos()
        {
            foreach (var behaviour in demoBehaviours) behaviour.enabled = false;
        }

        internal void DisablePhysics()
        {
            foreach (var collider in colliders) collider.enabled = false;
            foreach (var body in bodies)
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
        }
    }
}
