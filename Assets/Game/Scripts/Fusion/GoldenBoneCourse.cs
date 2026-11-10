using DogSimulator.Player;
using UnityEngine;
using UnityEngine.AI;

namespace DogSimulator.Networking
{
    internal sealed class GoldenBoneCourse : MonoBehaviour
    {
        internal static readonly Vector3 Vault = new(93f, .2f, 165.5f);
        internal static readonly Bounds Delivery = new(new Vector3(173.6f, 1.4f, 174.8f), new Vector3(2.6f, 2.3f, 2.6f));
        internal static readonly Vector3 Pedestal = Vault + new Vector3(2.3f, .98f, .2f);
        internal static readonly Vector3 ReturnPoint = Vault + new Vector3(.8f, 0f, .2f);
        internal static readonly Vector3 CrateSpawn = Vault + new Vector3(-6.5f, .65f, -4.7f);
        internal static readonly Vector3 PadCenter = Vault + new Vector3(-1.7f, .09f, 1.95f);
        internal static readonly (Vector3 start, Vector3 end)[] GuardPatrols =
        {
            (Vault + new Vector3(-.9f, 0, -1.4f), Vault + new Vector3(2.2f, 0, -1.4f)),
            (new Vector3(100f, .2f, 166f), new Vector3(104f, .2f, 166f)),
            (new Vector3(84f, .2f, 165f), new Vector3(80f, .2f, 165f))
        };
        internal static readonly Vector3 GateClosed = Vault + new Vector3(-4.5f, .9f, -2.2f);
        private static readonly Vector3 GateOpen = GateClosed + Vector3.up * 2.4f;
        internal static readonly Vector3 GateSize = new(.26f, 1.8f, 3.2f);
        internal static readonly Vector3 Switch = Vault + new Vector3(-6f, .12f, -2.3f);
        private const int QueryLayers = Physics.DefaultRaycastLayers | (1 << 2);
        private readonly Collider[] contacts = new Collider[32];
        [SerializeField] private Transform gate;
        [SerializeField] private Collider gateCollider;
        [SerializeField] private NavMeshObstacle gateObstacle;
        [SerializeField] private Renderer plateRenderer;
        [SerializeField] private Material plateRest;
        [SerializeField] private Material platePressed;

        internal bool IsPlatePressed()
        {
            var count = Physics.OverlapBoxNonAlloc(Switch + Vector3.up * .18f, new Vector3(.7f, .26f, .72f),
                contacts, Quaternion.identity, QueryLayers, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var contact = contacts[i];
                if (contact.GetComponentInParent<NetworkDogPlayer>() is { } dog
                    && !dog.CarrierId.IsValid && !dog.SeatedVehicleId.IsValid) return true;
                if (contact.GetComponentInParent<NetworkCarryable>() is { } item
                    && !item.IsCarried && item.Mass >= 3f) return true;
            }
            return false;
        }

        internal bool GatePathClear()
        {
            var half = GateSize * .5f + Vector3.up * 1.2f;
            var count = Physics.OverlapBoxNonAlloc((GateClosed + GateOpen) * .5f + Vector3.up * .08f,
                half - new Vector3(.025f, .08f, .025f), contacts,
                Quaternion.identity, QueryLayers, QueryTriggerInteraction.Ignore);
            if (count == contacts.Length) return false;
            for (var i = 0; i < count; i++)
            {
                var contact = contacts[i];
                if (contact.attachedRigidbody != null || contact.GetComponentInParent<NetworkDogPlayer>() != null)
                    return false;
            }
            return true;
        }

        internal void Present(bool pressed, bool open)
        {
            gate.position = open ? GateOpen : GateClosed;
            gateCollider.enabled = !open;
            gateObstacle.enabled = !open;
            plateRenderer.transform.position = Switch - Vector3.up * (pressed ? .05f : 0);
            plateRenderer.sharedMaterial = pressed ? platePressed : plateRest;
        }

    }
}
