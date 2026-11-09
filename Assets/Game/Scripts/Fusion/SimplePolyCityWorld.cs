using System;
using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    public sealed class SimplePolyCityWorld : MonoBehaviour
    {
        public const string MapVersion = "simplepoly-city-v2";
        public const string SaveNamespace = "simplepoly-city-v1";

        [Serializable]
        public struct Placement
        {
            public string id;
            public NetworkObject prefab;
            public Vector3 position;
            public Quaternion rotation;
        }

        [Serializable]
        public struct RoutePoint
        {
            public Vector3 position;
            [Min(1)] public float speed;
        }

        [Serializable]
        public struct VehiclePlacement
        {
            public Placement placement;
            public RoutePoint[] route;
        }

        [Serializable]
        public struct PedestrianPlacement
        {
            public NetworkObject prefab;
            public Vector3 start;
            public Vector3 end;
        }

        [SerializeField] private Placement[] props;
        [SerializeField] private VehiclePlacement[] vehicles;
        [SerializeField] private PedestrianPlacement[] pedestrians;
        [SerializeField] private Vector3[] safeSpawns;

        public Placement[] Props => props;
        public VehiclePlacement[] Vehicles => vehicles;
        public PedestrianPlacement[] Pedestrians => pedestrians;
        public Vector3[] SafeSpawns => safeSpawns;
    }
}
