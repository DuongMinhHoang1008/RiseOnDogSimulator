using System;
using UnityEngine;
using UnityEngine.AI;

namespace DogSimulator.Networking
{
    internal sealed class DogJail : MonoBehaviour, IDisposable
    {
        internal const float HalfWidth = 5f;
        internal const float HalfDepth = 4f;
        internal const float GateHalfWidth = 1.45f;
        internal const float Height = 3.2f;
        private static readonly Vector3 GateOffset = new(0, Height / 2, HalfDepth);
        private static readonly Vector3 GateHalfSize = new(GateHalfWidth, Height / 2, .25f);
        [SerializeField] private GameObject gate;
        [SerializeField] private BoxCollider lever;
        private readonly Collider[] blockers = new Collider[16];
        private Vector3 origin => transform.position;

        internal Vector3 BowlSpawn => origin + new Vector3(-3, .7f, -2);
        internal Vector3 Plate => origin + new Vector3(3, .12f, -1.5f);
        internal Vector3 Lever => lever.bounds.center;

        internal void Place(Vector3 position)
        {
            transform.position = position;
            Present(false);
            gameObject.SetActive(true);
        }

        internal Vector3 CellSpawn(int slot)
            => origin + new Vector3(-3.9f + slot % 7 * 1.25f, .12f, -.4f + slot / 7 % 3 * 1.25f);

        internal bool Contains(Vector3 point)
        {
            var local = point - origin;
            return Mathf.Abs(local.x) <= HalfWidth && Mathf.Abs(local.z) <= HalfDepth
                && local.y >= -.4f && local.y <= Height;
        }

        internal bool CrossedExit(Vector3 previous, Vector3 current)
        {
            var plane = origin.z + HalfDepth;
            if (!Contains(previous) || current.z <= plane) return false;
            var crossing = Vector3.Lerp(previous - origin, current - origin,
                (plane - previous.z) / (current.z - previous.z));
            return Mathf.Abs(crossing.x) < GateHalfWidth && crossing.y >= 0 && crossing.y < Height;
        }

        internal bool BowlOnPlate(Vector3 point)
        {
            var offset = point - Plate;
            return Mathf.Abs(offset.x) < .72f && Mathf.Abs(offset.z) < .72f && offset.y is > 0 and < 1f;
        }

        internal bool CanReachLever(NetworkDogPlayer dog)
        {
            if (dog.InJail || dog.KnockdownActive || dog.CarrierId.IsValid || dog.SeatedVehicleId.IsValid
                || Contains(dog.transform.position)) return false;
            var from = dog.MouthAnchor.position;
            var to = Lever - from;
            if (to.sqrMagnitude > 2.2f * 2.2f) return false;
            foreach (var hit in Physics.RaycastAll(from, to.normalized, to.magnitude,
                AnnoyingRules.SightLayers, QueryTriggerInteraction.Ignore))
                if (hit.collider != lever && !hit.transform.IsChildOf(dog.transform)) return false;
            return true;
        }

        internal bool GateBlocked()
        {
            var count = Physics.OverlapBoxNonAlloc(origin + GateOffset + Vector3.up * .05f,
                GateHalfSize + new Vector3(.15f, -.025f, .22f), blockers, Quaternion.identity,
                AnnoyingRules.SightLayers, QueryTriggerInteraction.Ignore);
            if (count == blockers.Length) return true;
            for (var i = 0; i < count; i++)
                // Props and NPCs have rigidbodies. The curb and cage shell are static.
                if (blockers[i].attachedRigidbody != null) return true;
            return false;
        }

        internal bool DogBlocksGate(Vector3 position)
        {
            // Sample the avatar as well as PhysX, since its controller may be disabled while carried.
            var offset = position - (origin + Vector3.forward * HalfDepth);
            return Mathf.Abs(offset.x) < GateHalfWidth + .5f && Mathf.Abs(offset.z) < .75f
                && offset.y >= -.4f && offset.y < Height;
        }

        internal void Present(bool open)
        {
            if (gate.activeSelf == open) gate.SetActive(!open);
        }

        public void Dispose() => gameObject.SetActive(false);

        internal static bool TryFindSite(SimplePolyCityWorld city, out Vector3 origin)
        {
            // Prefer the east plaza, then test nearby blocks before enabling arrests.
            var center = new Vector3(206, 0, 100);
            for (var radius = 16; radius <= 80; radius += 8)
            for (var z = -radius; z <= radius; z += 8)
            for (var x = -radius; x <= radius; x += 8)
            {
                if (Mathf.Abs(x) != radius && Mathf.Abs(z) != radius) continue;
                var candidate = center + new Vector3(x, 0, z);
                if (!NavMesh.SamplePosition(candidate, out var nav, .6f, NavMesh.AllAreas)) continue;
                candidate.y = nav.position.y + .1f;
                if (SiteIsClear(city, candidate))
                {
                    origin = candidate;
                    return true;
                }
            }
            origin = default;
            return false;
        }

        private static bool SiteIsClear(SimplePolyCityWorld city, Vector3 point)
        {
            var half = new Vector3(5.5f, 1.75f, 5.8f);
            if (Physics.CheckBox(point + Vector3.up * (half.y + .23f), half, Quaternion.identity,
                AnnoyingRules.SightLayers, QueryTriggerInteraction.Ignore)) return false;
            for (var x = -5; x <= 5; x += 5)
            for (var z = -4; z <= 4; z += 4)
            {
                if (!Physics.Raycast(point + new Vector3(x, .25f, z), Vector3.down, out var floor, .8f,
                    AnnoyingRules.SightLayers, QueryTriggerInteraction.Ignore) || floor.normal.y < .85f) return false;
            }
            if (!NavMesh.SamplePosition(point + Vector3.forward * 6f, out _, 1f, NavMesh.AllAreas)) return false;
            foreach (var spawn in city.SafeSpawns)
                if ((spawn - point).sqrMagnitude < 64f) return false;
            if ((GoldenBoneCourse.Delivery.center - point).sqrMagnitude < 225f
                || (GoldenBoneCourse.Pedestal - point).sqrMagnitude < 225f) return false;
            foreach (var vehicle in city.Vehicles)
            {
                if ((vehicle.placement.position - point).sqrMagnitude < 81f) return false;
                var route = vehicle.route;
                if (route == null) continue;
                for (var i = 0; i < route.Length; i++)
                {
                    var start = route[i].position;
                    var segment = route[(i + 1) % route.Length].position - start;
                    var t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / Mathf.Max(segment.sqrMagnitude, .01f));
                    if ((start + segment * t - point).sqrMagnitude < 81f) return false;
                }
            }
            foreach (var prop in city.Props)
                if ((prop.position - point).sqrMagnitude < 42f) return false;
            foreach (var npc in city.Pedestrians)
                if ((npc.start - point).sqrMagnitude < 56f || (npc.end - point).sqrMagnitude < 56f) return false;
            return true;
        }

    }
}
